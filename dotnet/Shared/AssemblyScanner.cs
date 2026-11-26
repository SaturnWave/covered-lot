using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace CoveredLot.Shared
{
    /// <summary>One rule violation found in a compiled assembly: where it is, and which rule it breaks.</summary>
    internal sealed record Violation
    {
        /// <summary>A member, a member at an IL offset ("CoveredLot.Core.X::Run+IL_0004") or a reference ("type reference System.Random").</summary>
        public required string Location { get; init; }

        public required string Reason { get; init; }

        public override string ToString() => Location + ": " + Reason;
    }

    /// <summary>
    /// A type as it appears in a signature: a readable name, the name of its definition (generic arguments stripped),
    /// the external types it mentions and two flags. Every member is set by name, so two of the same type cannot change
    /// places unseen.
    /// </summary>
    internal sealed record TypeShape
    {
        /// <summary>The name with generic arguments and array ranks, for example System.Collections.Generic.List`1&lt;System.Int32&gt;.</summary>
        public required string Display { get; init; }

        /// <summary>The name of the definition: generic arguments stripped, arrays as System.Array, by-reference and pointer types as their element's.</summary>
        public required string DefinitionName { get; init; }

        /// <summary>The referenced types it mentions, generic arguments included, except those of the sibling assemblies.</summary>
        public required ImmutableArray<string> ExternalTypes { get; init; }

        /// <summary>Whether it is or holds a floating-point type, generic arguments included.</summary>
        public required bool UsesFloatingPoint { get; init; }

        /// <summary>Whether it is or holds a native-sized integer (nint, nuint, IntPtr or UIntPtr as signatures encode them).</summary>
        public required bool UsesNativeInteger { get; init; }

        /// <summary>The type arguments of a generic instantiation (String and Int32 of Dictionary`2&lt;String,Int32&gt;); empty otherwise.</summary>
        public ImmutableArray<TypeShape> TypeArguments { get; init; } = ImmutableArray<TypeShape>.Empty;

        /// <summary>The index of a placeholder for the method's own generic parameter: 1 for "!!1"; null for any other type.</summary>
        public int? MethodParameterIndex => IndexAfter("!!");

        /// <summary>The index of a placeholder for the declaring type's generic parameter: 1 for "!1"; null for any other type, "!!1" included.</summary>
        public int? TypeParameterIndex => Display.StartsWith("!!", StringComparison.Ordinal) ? null : IndexAfter("!");

        private int? IndexAfter(string prefix) =>
            Display.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(Display.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int index) ? index : null;
    }

    /// <summary>
    /// A call, field access or token use found in a method body. For a call the IL walk adds what it knows of the values
    /// passed (<see cref="Instance"/>, <see cref="Arguments"/>), so a policy can refuse, for example, a HashSet handed to a
    /// parameter that enumerates it.
    /// </summary>
    internal sealed record MemberUse(TypeShape DeclaringType, string MemberName, IReadOnlyList<TypeShape> Parameters, bool UsesFloatingPoint)
    {
        /// <summary>The method's own type arguments (Cell and String of OrderBy&lt;Cell,String&gt;); empty for a non-generic method.</summary>
        public IReadOnlyList<TypeShape> MethodTypeArguments { get; init; } = Array.Empty<TypeShape>();

        /// <summary>The static type of the receiver of an instance call or callvirt; null when the walk cannot tell or the member is static.</summary>
        public TypeShape? Instance { get; init; }

        /// <summary>The static types of the values passed, one per parameter; null where the walk cannot tell. Empty for a field or token.</summary>
        public IReadOnlyList<TypeShape?> Arguments { get; init; } = Array.Empty<TypeShape?>();

        public string Display => DeclaringType.Display + "::" + MemberName
            + "(" + string.Join(", ", Parameters.Select(parameter => parameter.Display)) + ")";

        /// <summary>The type with the declaring type's (!n) and the method's (!!n) generic parameters replaced by their arguments, where known.</summary>
        public TypeShape Resolve(TypeShape type)
        {
            if (type.MethodParameterIndex is int methodIndex)
            {
                return methodIndex < MethodTypeArguments.Count ? MethodTypeArguments[methodIndex] : type;
            }

            if (type.TypeParameterIndex is int typeIndex)
            {
                return typeIndex < DeclaringType.TypeArguments.Length ? DeclaringType.TypeArguments[typeIndex] : type;
            }

            return type;
        }
    }

    /// <summary>
    /// One member a method body names (a call, a field access or a member token): the calling type and method, and
    /// the member's declaring type (generic arguments stripped, for example System.Collections.Generic.List`1) and
    /// name. Nested and compiler-written types keep their full name, for example "CoveredLot.Core.X+&lt;&gt;c".
    /// </summary>
    internal sealed record MemberReferenceSite
    {
        public required string CallerType { get; init; }

        public required string CallerMethod { get; init; }

        public required string DeclaringType { get; init; }

        public required string MemberName { get; init; }

        public override string ToString() => CallerType + "::" + CallerMethod + " -> " + DeclaringType + "::" + MemberName;
    }

    /// <summary>
    /// One external type that a type definition names in its declarations (see
    /// <see cref="AssemblyScanner.DeclaredExternalTypes"/>), for example System.Collections.Generic.IReadOnlyList`1.
    /// </summary>
    internal sealed record DeclaredTypeUse
    {
        /// <summary>The type definition, nested types with their full name, for example "CoveredLot.Core.X+Y".</summary>
        public required string DeclaringType { get; init; }

        /// <summary>The external type's name, generic arguments stripped.</summary>
        public required string ExternalType { get; init; }
    }

    /// <summary>
    /// Reads a compiled assembly's metadata and IL without loading it, to prove rules the compiler cannot enforce.
    /// Everything is checked on the compiled output, so var hides nothing, and a helper method hides no call it makes:
    /// every method body is scanned, whoever calls it. What a rule sees of a value, though, is the static type the IL
    /// walk knows (see Flow):
    /// an integer formatted as a generic T or after boxing reaches System.Object.ToString, and a set handed on as an
    /// IEnumerable&lt;T&gt; through a helper's return value or an interface-typed local is no longer seen as a set. A
    /// port of the engine's AssemblyInspection (SaturnWave/puzzle-engine, tests/PuzzleCore.Tests/Support/AssemblyInspection.cs):
    /// its catch regions, type, member and assembly references, floating-point scan and platform-API policy walk, with the
    /// signature shapes and the stack-flow walk by which the policies judge each call. The engine's list of type mentions,
    /// which only its bot guard uses, is left out. Four scans of this repository's own are added: every catch block
    /// (Core has none at all), async state machines, static fields that can be reassigned, and the external types each
    /// type declares.
    /// </summary>
    internal sealed class AssemblyScanner : IDisposable
    {
        private const string CompilerGeneratedAttribute = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
        private const string AsyncStateMachineInterface = "System.Runtime.CompilerServices.IAsyncStateMachine";

        private static readonly IReadOnlyDictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetValue(null))
            .OfType<OpCode>()
            .ToDictionary(opCode => opCode.Value);

        private static readonly IReadOnlySet<short> FloatingPointOpCodes = new[]
        {
            OpCodes.Ldc_R4, OpCodes.Ldc_R8, OpCodes.Conv_R4, OpCodes.Conv_R8, OpCodes.Conv_R_Un,
            OpCodes.Ldind_R4, OpCodes.Ldind_R8, OpCodes.Stind_R4, OpCodes.Stind_R8,
            OpCodes.Ldelem_R4, OpCodes.Ldelem_R8, OpCodes.Stelem_R4, OpCodes.Stelem_R8, OpCodes.Ckfinite,
        }.Select(opCode => opCode.Value).ToHashSet();

        /// <summary>Types whose values or arithmetic are floating point even when no float appears in a signature.</summary>
        private static readonly IReadOnlySet<string> FloatingPointTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Single", "System.Double", "System.Decimal", "System.Half",
            "System.Numerics.Vector2", "System.Numerics.Vector3", "System.Numerics.Vector4",
            "System.Numerics.Matrix3x2", "System.Numerics.Matrix4x4", "System.Numerics.Quaternion",
            "System.Numerics.Plane", "System.Numerics.Complex",
        };

        /// <summary>
        /// Members the compiler writes for records. Only these count as synthesized: local
        /// functions are also marked [CompilerGenerated] but contain hand-written code.
        /// </summary>
        private static readonly IReadOnlySet<string> RecordSynthesizedMemberNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ToString", "PrintMembers", "GetHashCode", "Equals", "get_EqualityContract", "<Clone>$", "Deconstruct",
            "op_Equality", "op_Inequality",
        };

        /// <summary>
        /// The assemblies whose types are no platform APIs: the Unity-free Core and the two engine DLLs it is built against.
        /// Each is checked on its own: Core by the API policy tests, the engine's DLLs by the engine's own guard tests at the
        /// commit they were synced from. The engine reads its list from its project table; this one is written out.
        /// </summary>
        private static readonly IReadOnlySet<string> SiblingAssemblyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "CoveredLot.Core", "PuzzleCore", "PuzzleCore.Jam",
        };

        /// <summary>Instructions an empty catch compiles to: drop or store the exception, then leave.</summary>
        private static readonly IReadOnlySet<short> EmptyHandlerOpCodes = new[]
        {
            OpCodes.Nop, OpCodes.Pop, OpCodes.Leave, OpCodes.Leave_S,
            OpCodes.Stloc, OpCodes.Stloc_S, OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3,
        }.Select(opCode => opCode.Value).ToHashSet();

        private static readonly short[] ImpliedVariableOpCodes =
        {
            OpCodes.Ldarg_0.Value, OpCodes.Ldarg_1.Value, OpCodes.Ldarg_2.Value, OpCodes.Ldarg_3.Value,
            OpCodes.Ldloc_0.Value, OpCodes.Ldloc_1.Value, OpCodes.Ldloc_2.Value, OpCodes.Ldloc_3.Value,
        };

        private readonly PEReader _peReader;
        private readonly MetadataReader _reader;
        private readonly ShapeProvider _shapes;

        /// <summary>Reads the whole file into memory, so no file handle stays open while the scanner lives.</summary>
        public AssemblyScanner(string assemblyPath)
        {
            _peReader = new PEReader(File.ReadAllBytes(assemblyPath).ToImmutableArray());
            bool hasMetadata = false;
            try
            {
                _reader = _peReader.GetMetadataReader();
                hasMetadata = true;
            }
            finally
            {
                // A file that is not an assembly throws here; the reader must not outlive the failed constructor.
                if (!hasMetadata)
                {
                    _peReader.Dispose();
                }
            }

            _shapes = new ShapeProvider(this);
        }

        public void Dispose() => _peReader.Dispose();

        /// <summary>The names of the assemblies this one references (netstandard, PuzzleCore, ...), sorted ordinally.</summary>
        public IReadOnlyList<string> ReferencedAssemblyNames() => _reader.AssemblyReferences
            .Select(handle => _reader.GetString(_reader.GetAssemblyReference(handle).Name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        /// <summary>Whether the assembly's type reference table names the type, for example System.Random.</summary>
        public bool References(string typeFullName) =>
            _reader.TypeReferences.Select(FullName).Contains(typeFullName, StringComparer.Ordinal);

        /// <summary>
        /// Every member that every method body names (calls, field accesses and member tokens, in this assembly or
        /// another), with the method that names it; for guards that limit which code may touch which member.
        /// </summary>
        public IReadOnlyList<MemberReferenceSite> MemberReferences()
        {
            var found = new List<MemberReferenceSite>();
            foreach (MethodDefinitionHandle methodHandle in AllMethods())
            {
                MethodDefinition method = _reader.GetMethodDefinition(methodHandle);
                string callerType = FullName(method.GetDeclaringType());
                string callerMethod = _reader.GetString(method.Name);
                IEnumerable<MemberUse> uses = Instructions(methodHandle)
                    .Where(instruction => !instruction.Operand.IsNil)
                    .Select(instruction => MemberUseOf(instruction.Operand))
                    .OfType<MemberUse>();
                found.AddRange(uses.Select(use => new MemberReferenceSite
                {
                    CallerType = callerType,
                    CallerMethod = callerMethod,
                    DeclaringType = use.DeclaringType.DefinitionName,
                    MemberName = use.MemberName,
                }));
            }

            return found.Distinct().ToList();
        }

        /// <summary>Every floating-point type, signature, local, literal or instruction, with its location.</summary>
        public IReadOnlyList<Violation> FloatingPointUses()
        {
            var found = new List<Violation>();

            found.AddRange(_reader.TypeReferences
                .Select(FullName)
                .Where(FloatingPointTypeNames.Contains)
                .Select(name => new Violation { Location = "type reference " + name, Reason = "floating-point type" }));

            found.AddRange(_reader.MemberReferences
                .Select(DescribeMemberReference)
                .Where(use => use.UsesFloatingPoint)
                .Select(use => new Violation { Location = "member reference " + use.Display, Reason = "floating-point signature" }));

            foreach (TypeDefinitionHandle typeHandle in _reader.TypeDefinitions)
            {
                found.AddRange(FloatingPointUsesInDefinitions(typeHandle));
            }

            foreach (MethodDefinitionHandle method in AllMethods())
            {
                found.AddRange(FloatingPointUsesInBody(method));
            }

            return found.Distinct().ToList();
        }

        /// <summary>
        /// Checks every platform API the assembly touches against a policy: the Unity-bound allowlist and its member bans,
        /// or the member bans alone. Types of the sibling assemblies (Core using PuzzleCore and PuzzleCore.Jam) are not
        /// platform APIs: those assemblies are scanned on their own. Native-sized integers and native calls are refused
        /// under every policy.
        /// </summary>
        public IReadOnlyList<Violation> ApiPolicyViolations(IPlatformApiPolicy policy)
        {
            var found = new List<Violation>();

            found.AddRange(_reader.TypeReferences
                .Where(handle => !IsUnityBoundSiblingType(handle))
                .Select(FullName)
                .Where(name => !policy.IsTypeAllowed(name))
                .Select(name => new Violation { Location = "type reference " + name, Reason = "type " + policy.RefusedTypeReason }));

            foreach (TypeDefinitionHandle typeHandle in _reader.TypeDefinitions)
            {
                found.AddRange(NativeIntegerUsesInDefinitions(typeHandle));
            }

            foreach (MethodDefinitionHandle methodHandle in AllMethods())
            {
                MethodDefinition method = _reader.GetMethodDefinition(methodHandle);
                bool isNativeCall = (method.Attributes & MethodAttributes.PinvokeImpl) != 0 || !method.GetImport().Name.IsNil;
                if (isNativeCall)
                {
                    found.Add(new Violation { Location = Describe(methodHandle), Reason = "native call (P/Invoke) is not allowed" });
                }

                string methodName = _reader.GetString(method.Name);
                var caller = new CallerContext
                {
                    MethodName = methodName,
                    IsSynthesizedRecordMember = RecordSynthesizedMemberNames.Contains(methodName) && IsCompilerGenerated(method),
                    IsIteratorPlumbing = IsIteratorPlumbing(method, methodName),
                };
                foreach (FlowStep step in Flow(methodHandle))
                {
                    found.AddRange(PolicyViolationsAt(methodHandle, step, caller, policy));
                }
            }

            return found.Distinct().ToList();
        }

        /// <summary>
        /// Catch blocks that swallow everything (catch (Exception), a bare catch, or such a catch with a when-filter)
        /// and catch blocks with an empty body. CA1031 misses the filtered and the empty ones.
        /// </summary>
        public IReadOnlyList<Violation> GenericOrEmptyCatchBlocks() => Handlers().SelectMany(CatchViolations).Distinct().ToList();

        /// <summary>
        /// Every catch block, whatever it catches, with or without a when-filter: Core has none at all. The finally
        /// blocks that using, foreach and lock compile to are no catch blocks and are not reported.
        /// </summary>
        public IReadOnlyList<Violation> CatchBlocks() => Handlers()
            .Select(handler => new Violation { Location = At(handler.Location, handler.Region.HandlerOffset), Reason = DescribeCatch(handler.Region) })
            .ToList();

        /// <summary>
        /// The state machines the compiler writes for async methods: types that implement IAsyncStateMachine. Each
        /// holds a compiler-written generic catch that hands the exception to the returned task.
        /// </summary>
        public IReadOnlyList<Violation> AsyncStateMachines() => _reader.TypeDefinitions
            .Where(ImplementsAsyncStateMachine)
            .Select(type => new Violation { Location = FullName(type), Reason = "async state machine" })
            .ToList();

        /// <summary>
        /// Static fields that can be reassigned, being neither const nor readonly; a static auto-property with a
        /// setter has one as its backing field. Core keeps no mutable static state, and this is how such state gets
        /// in (a readonly field that holds a mutable object is not seen here). The lambda caches the compiler writes
        /// are fields of compiler-written types and are left out.
        /// </summary>
        public IReadOnlyList<Violation> ReassignableStaticFields()
        {
            var found = new List<Violation>();
            foreach (TypeDefinitionHandle typeHandle in _reader.TypeDefinitions.Where(type => !IsCompilerWritten(type)))
            {
                string typeName = FullName(typeHandle);
                found.AddRange(_reader.GetTypeDefinition(typeHandle).GetFields()
                    .Select(_reader.GetFieldDefinition)
                    .Where(IsReassignableStatic)
                    .Select(field => new Violation
                    {
                        Location = typeName + "::" + _reader.GetString(field.Name),
                        Reason = "static field that can be reassigned",
                    }));
            }

            return found;
        }

        /// <summary>
        /// The external types each type definition names in its declarations: its base type and interfaces, the types of
        /// its fields and properties, and the parameter, return and local types of its methods, generic arguments
        /// included. Attributes are not included, and neither are the sibling assemblies' types. The policy walk judges
        /// what method bodies name at each instruction; a refused type that appears only in such a declaration reaches
        /// <see cref="ApiPolicyViolations"/> once, unlocated, through the type reference table, and this shows which
        /// type named it.
        /// </summary>
        public IReadOnlyList<DeclaredTypeUse> DeclaredExternalTypes()
        {
            var found = new List<DeclaredTypeUse>();
            foreach (TypeDefinitionHandle typeHandle in _reader.TypeDefinitions)
            {
                TypeDefinition type = _reader.GetTypeDefinition(typeHandle);
                IEnumerable<TypeShape> supertypes = type.GetInterfaceImplementations()
                    .Select(implementation => ShapeOf(_reader.GetInterfaceImplementation(implementation).Interface))
                    .Concat(type.BaseType.IsNil ? Array.Empty<TypeShape>() : new[] { ShapeOf(type.BaseType) });
                IEnumerable<TypeShape> fields = type.GetFields()
                    .Select(field => _reader.GetFieldDefinition(field).DecodeSignature(_shapes, null));
                IEnumerable<TypeShape> properties = type.GetProperties()
                    .SelectMany(property => SignatureTypes(_reader.GetPropertyDefinition(property).DecodeSignature(_shapes, null)));
                IEnumerable<TypeShape> methods = type.GetMethods()
                    .SelectMany(method => SignatureTypes(_reader.GetMethodDefinition(method).DecodeSignature(_shapes, null)).Concat(LocalTypes(method)));
                string typeName = FullName(typeHandle);
                found.AddRange(supertypes.Concat(fields).Concat(properties).Concat(methods)
                    .SelectMany(shape => shape.ExternalTypes)
                    .Distinct(StringComparer.Ordinal)
                    .Select(external => new DeclaredTypeUse { DeclaringType = typeName, ExternalType = external }));
            }

            return found;
        }

        private IEnumerable<Violation> PolicyViolationsAt(MethodDefinitionHandle method, FlowStep step, CallerContext caller, IPlatformApiPolicy policy)
        {
            Instruction instruction = step.Instruction;
            if (instruction.Operand.IsNil)
            {
                yield break;
            }

            string location = At(Describe(method), instruction.Offset);

            foreach (string type in ExternalTypesOf(instruction.Operand).Where(type => !policy.IsTypeAllowed(type)))
            {
                yield return new Violation { Location = location, Reason = "uses " + type + ", which " + policy.RefusedTypeReason };
            }

            MemberUse? use = MemberUseOf(instruction.Operand) is MemberUse member ? member with { Instance = step.Instance, Arguments = step.Arguments } : null;
            if (use is null)
            {
                yield break;
            }

            foreach (string reason in policy.MemberViolations(use, caller))
            {
                yield return new Violation { Location = location, Reason = use.Display + ": " + reason };
            }
        }

        /// <summary>Whether a type reference resolves to a sibling assembly (nested types through their outermost type).</summary>
        private bool IsUnityBoundSiblingType(TypeReferenceHandle handle)
        {
            EntityHandle scope = _reader.GetTypeReference(handle).ResolutionScope;
            while (scope.Kind == HandleKind.TypeReference)
            {
                scope = _reader.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
            }

            return scope.Kind == HandleKind.AssemblyReference
                && SiblingAssemblyNames.Contains(_reader.GetString(_reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name));
        }

        private IEnumerable<Violation> FloatingPointUsesInDefinitions(TypeDefinitionHandle typeHandle)
        {
            TypeDefinition type = _reader.GetTypeDefinition(typeHandle);
            string typeName = FullName(typeHandle);

            foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
            {
                FieldDefinition field = _reader.GetFieldDefinition(fieldHandle);
                if (field.DecodeSignature(_shapes, null).UsesFloatingPoint)
                {
                    yield return new Violation { Location = typeName + "::" + _reader.GetString(field.Name), Reason = "floating-point field" };
                }
            }

            foreach (PropertyDefinitionHandle propertyHandle in type.GetProperties())
            {
                PropertyDefinition property = _reader.GetPropertyDefinition(propertyHandle);
                if (UsesFloatingPoint(property.DecodeSignature(_shapes, null)))
                {
                    yield return new Violation { Location = typeName + "::" + _reader.GetString(property.Name), Reason = "floating-point property" };
                }
            }

            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                if (UsesFloatingPoint(_reader.GetMethodDefinition(methodHandle).DecodeSignature(_shapes, null)))
                {
                    yield return new Violation { Location = Describe(methodHandle), Reason = "floating-point parameter or return type" };
                }
            }
        }

        private IEnumerable<Violation> FloatingPointUsesInBody(MethodDefinitionHandle methodHandle)
        {
            string location = Describe(methodHandle);
            if (LocalTypes(methodHandle).Any(local => local.UsesFloatingPoint))
            {
                yield return new Violation { Location = location, Reason = "floating-point local variable" };
            }

            foreach (Instruction instruction in Instructions(methodHandle))
            {
                if (FloatingPointOpCodes.Contains(instruction.OpCode.Value))
                {
                    yield return new Violation { Location = location, Reason = "floating-point instruction " + instruction.OpCode.Name };
                }
                else if (!instruction.Operand.IsNil && UsesFloatingPoint(instruction.Operand))
                {
                    yield return new Violation { Location = location, Reason = "uses floating point via " + DescribeOperand(instruction.Operand) };
                }
            }
        }

        private IEnumerable<Violation> NativeIntegerUsesInDefinitions(TypeDefinitionHandle typeHandle)
        {
            TypeDefinition type = _reader.GetTypeDefinition(typeHandle);
            string typeName = FullName(typeHandle);
            const string Reason = "native-sized integer (nint/nuint/IntPtr) has a different width on 32-bit devices";

            foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
            {
                FieldDefinition field = _reader.GetFieldDefinition(fieldHandle);
                if (field.DecodeSignature(_shapes, null).UsesNativeInteger)
                {
                    yield return new Violation { Location = typeName + "::" + _reader.GetString(field.Name), Reason = Reason };
                }
            }

            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodSignature<TypeShape> signature = _reader.GetMethodDefinition(methodHandle).DecodeSignature(_shapes, null);
                bool usesNativeInteger = signature.ReturnType.UsesNativeInteger
                    || signature.ParameterTypes.Any(parameter => parameter.UsesNativeInteger)
                    || LocalTypes(methodHandle).Any(local => local.UsesNativeInteger);
                if (usesNativeInteger)
                {
                    yield return new Violation { Location = Describe(methodHandle), Reason = Reason };
                }
            }
        }

        private IEnumerable<TypeShape> LocalTypes(MethodDefinitionHandle methodHandle)
        {
            MethodDefinition method = _reader.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0)
            {
                return Array.Empty<TypeShape>();
            }

            MethodBodyBlock body = _peReader.GetMethodBody(method.RelativeVirtualAddress);
            return body.LocalSignature.IsNil
                ? Array.Empty<TypeShape>()
                : _reader.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(_shapes, null);
        }

        private bool UsesFloatingPoint(EntityHandle handle) => handle.Kind switch
        {
            HandleKind.TypeReference or HandleKind.TypeSpecification => ShapeOf(handle).UsesFloatingPoint,
            HandleKind.TypeDefinition => false,
            HandleKind.MemberReference or HandleKind.MethodSpecification or HandleKind.MethodDefinition or HandleKind.FieldDefinition =>
                MemberUseOf(handle)?.UsesFloatingPoint ?? false,
            HandleKind.StandaloneSignature => UsesFloatingPoint((StandaloneSignatureHandle)handle),
            _ => throw new InvalidDataException("Unexpected IL operand kind " + handle.Kind.ToString()),
        };

        private bool UsesFloatingPoint(StandaloneSignatureHandle handle)
        {
            StandaloneSignature signature = _reader.GetStandaloneSignature(handle);
            return signature.GetKind() == StandaloneSignatureKind.LocalVariables
                ? signature.DecodeLocalSignature(_shapes, null).Any(local => local.UsesFloatingPoint)
                : UsesFloatingPoint(signature.DecodeMethodSignature(_shapes, null));
        }

        private static bool UsesFloatingPoint(MethodSignature<TypeShape> signature) =>
            signature.ReturnType.UsesFloatingPoint || signature.ParameterTypes.Any(parameter => parameter.UsesFloatingPoint);

        private static IEnumerable<TypeShape> SignatureTypes(MethodSignature<TypeShape> signature) =>
            signature.ParameterTypes.Prepend(signature.ReturnType);

        /// <summary>The external (referenced) types an IL operand mentions, including generic arguments.</summary>
        private IEnumerable<string> ExternalTypesOf(EntityHandle operand)
        {
            switch (operand.Kind)
            {
                case HandleKind.TypeReference:
                case HandleKind.TypeSpecification:
                    return ShapeOf(operand).ExternalTypes;
                case HandleKind.MemberReference:
                    return ShapeOfParent(_reader.GetMemberReference((MemberReferenceHandle)operand).Parent).ExternalTypes;
                case HandleKind.MethodSpecification:
                    MethodSpecification specification = _reader.GetMethodSpecification((MethodSpecificationHandle)operand);
                    return specification.DecodeSignature(_shapes, null).SelectMany(argument => argument.ExternalTypes)
                        .Concat(ExternalTypesOf(specification.Method));
                default:
                    return Array.Empty<string>();
            }
        }

        /// <summary>The member an IL operand names, or null when the operand is a type, a string or a signature.</summary>
        private MemberUse? MemberUseOf(EntityHandle operand)
        {
            switch (operand.Kind)
            {
                case HandleKind.MemberReference:
                    return DescribeMemberReference((MemberReferenceHandle)operand);
                case HandleKind.MethodSpecification:
                    MethodSpecification specification = _reader.GetMethodSpecification((MethodSpecificationHandle)operand);
                    MemberUse? method = MemberUseOf(specification.Method);
                    ImmutableArray<TypeShape> typeArguments = specification.DecodeSignature(_shapes, null);
                    bool argumentsUseFloat = typeArguments.Any(argument => argument.UsesFloatingPoint);
                    return method is null ? null : method with { UsesFloatingPoint = method.UsesFloatingPoint || argumentsUseFloat, MethodTypeArguments = typeArguments };
                case HandleKind.MethodDefinition:
                    MethodDefinition definition = _reader.GetMethodDefinition((MethodDefinitionHandle)operand);
                    MethodSignature<TypeShape> signature = definition.DecodeSignature(_shapes, null);
                    return new MemberUse(ShapeOfDefinition(definition.GetDeclaringType()), _reader.GetString(definition.Name),
                        signature.ParameterTypes, UsesFloatingPoint(signature));
                case HandleKind.FieldDefinition:
                    FieldDefinition field = _reader.GetFieldDefinition((FieldDefinitionHandle)operand);
                    return new MemberUse(ShapeOfDefinition(field.GetDeclaringType()), _reader.GetString(field.Name),
                        Array.Empty<TypeShape>(), field.DecodeSignature(_shapes, null).UsesFloatingPoint);
                default:
                    return null;
            }
        }

        private MemberUse DescribeMemberReference(MemberReferenceHandle handle)
        {
            MemberReference member = _reader.GetMemberReference(handle);
            TypeShape parent = ShapeOfParent(member.Parent);
            string name = _reader.GetString(member.Name);
            if (member.GetKind() == MemberReferenceKind.Field)
            {
                TypeShape fieldType = member.DecodeFieldSignature(_shapes, null);
                return new MemberUse(parent, name, Array.Empty<TypeShape>(), parent.UsesFloatingPoint || fieldType.UsesFloatingPoint);
            }

            MethodSignature<TypeShape> signature = member.DecodeMethodSignature(_shapes, null);
            return new MemberUse(parent, name, signature.ParameterTypes, parent.UsesFloatingPoint || UsesFloatingPoint(signature));
        }

        private TypeShape ShapeOfParent(EntityHandle parent) => parent.Kind switch
        {
            HandleKind.TypeReference or HandleKind.TypeSpecification => ShapeOf(parent),
            HandleKind.TypeDefinition => ShapeOfDefinition((TypeDefinitionHandle)parent),
            HandleKind.MethodDefinition => ShapeOfDefinition(_reader.GetMethodDefinition((MethodDefinitionHandle)parent).GetDeclaringType()),
            HandleKind.ModuleReference => ShapeProvider.Plain("<module>"),
            _ => throw new InvalidDataException("Unexpected member parent kind " + parent.Kind.ToString()),
        };

        private TypeShape ShapeOf(EntityHandle type) => type.Kind switch
        {
            HandleKind.TypeReference => _shapes.GetTypeFromReference(_reader, (TypeReferenceHandle)type, 0),
            HandleKind.TypeSpecification => _reader.GetTypeSpecification((TypeSpecificationHandle)type).DecodeSignature(_shapes, null),
            HandleKind.TypeDefinition => ShapeOfDefinition((TypeDefinitionHandle)type),
            _ => throw new InvalidDataException("Not a type handle: " + type.Kind.ToString()),
        };

        private TypeShape ShapeOfDefinition(TypeDefinitionHandle handle) =>
            _shapes.GetTypeFromDefinition(_reader, handle, 0);

        private string DescribeOperand(EntityHandle operand) =>
            MemberUseOf(operand)?.Display ?? ShapeOf(operand).Display;

        /// <summary>Every catch and filter handler of every method body, with the method's location and IL.</summary>
        private IEnumerable<Handler> Handlers()
        {
            foreach (MethodDefinitionHandle methodHandle in AllMethods())
            {
                MethodDefinition method = _reader.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0)
                {
                    continue;
                }

                MethodBodyBlock body = _peReader.GetMethodBody(method.RelativeVirtualAddress);
                byte[] il = body.GetILBytes() ?? Array.Empty<byte>();
                string location = Describe(methodHandle);
                foreach (ExceptionRegion region in body.ExceptionRegions.Where(IsCatchOrFilter))
                {
                    yield return new Handler(location, region, il);
                }
            }
        }

        private static bool IsCatchOrFilter(ExceptionRegion region) => region.Kind is ExceptionRegionKind.Catch or ExceptionRegionKind.Filter;

        private IEnumerable<Violation> CatchViolations(Handler handler)
        {
            ExceptionRegion region = handler.Region;
            string at = At(handler.Location, region.HandlerOffset);
            if (region.Kind == ExceptionRegionKind.Catch && IsCatchAllType(ShapeOf(region.CatchType).DefinitionName))
            {
                yield return new Violation { Location = at, Reason = "generic catch (" + ShapeOf(region.CatchType).Display + ")" };
            }

            if (region.Kind == ExceptionRegionKind.Filter)
            {
                IReadOnlyList<string> filteredTypes = DecodeInstructions(handler.Il, region.FilterOffset..region.HandlerOffset, handler.Location)
                    .Where(instruction => instruction.OpCode == OpCodes.Isinst && !instruction.Operand.IsNil)
                    .Select(instruction => ShapeOf(instruction.Operand).DefinitionName)
                    .ToList();
                if (filteredTypes.Count == 0 || filteredTypes.Any(IsCatchAllType))
                {
                    yield return new Violation { Location = at, Reason = "generic catch with a when-filter" };
                }
            }

            Range body = region.HandlerOffset..(region.HandlerOffset + region.HandlerLength);
            bool isEmpty = DecodeInstructions(handler.Il, body, handler.Location)
                .All(instruction => EmptyHandlerOpCodes.Contains(instruction.OpCode.Value));
            if (isEmpty)
            {
                yield return new Violation { Location = at, Reason = "empty catch block" };
            }
        }

        private string DescribeCatch(ExceptionRegion region) => region.Kind == ExceptionRegionKind.Catch
            ? "catch block (" + ShapeOf(region.CatchType).Display + ")"
            : "catch block with a when-filter";

        private static bool IsCatchAllType(string typeName) => typeName is "System.Exception" or "System.Object";

        private static bool IsReassignableStatic(FieldDefinition field) =>
            (field.Attributes & FieldAttributes.Static) != 0
            && (field.Attributes & (FieldAttributes.Literal | FieldAttributes.InitOnly)) == 0;

        private bool ImplementsAsyncStateMachine(TypeDefinitionHandle handle) => _reader.GetTypeDefinition(handle)
            .GetInterfaceImplementations()
            .Select(implementation => ShapeOf(_reader.GetInterfaceImplementation(implementation).Interface).DefinitionName)
            .Contains(AsyncStateMachineInterface, StringComparer.Ordinal);

        /// <summary>
        /// Whether the compiler wrote the type or a type it is nested in: a name C# cannot declare (one with '&lt;')
        /// that carries [CompilerGenerated]. Lambda caches, closures, iterators and &lt;PrivateImplementationDetails&gt;
        /// are such types; an attribute alone does not make a hand-written type one.
        /// </summary>
        private bool IsCompilerWritten(TypeDefinitionHandle handle)
        {
            for (TypeDefinitionHandle current = handle; !current.IsNil; current = _reader.GetTypeDefinition(current).GetDeclaringType())
            {
                TypeDefinition type = _reader.GetTypeDefinition(current);
                bool hasUnspeakableName = _reader.GetString(type.Name).Contains('<', StringComparison.Ordinal);
                if (hasUnspeakableName && HasCompilerGeneratedAttribute(type.GetCustomAttributes()))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCompilerGenerated(MethodDefinition method) => HasCompilerGeneratedAttribute(method.GetCustomAttributes());

        /// <summary>
        /// The constructor or GetEnumerator of a compiler-written iterator state machine
        /// (a type named &lt;Method&gt;d__N marked [CompilerGenerated]). Lambda closures
        /// (&lt;&gt;c, &lt;&gt;c__DisplayClass) and local functions do not match.
        /// </summary>
        private bool IsIteratorPlumbing(MethodDefinition method, string methodName)
        {
            TypeDefinition type = _reader.GetTypeDefinition(method.GetDeclaringType());
            string typeName = _reader.GetString(type.Name);
            bool isIteratorType = typeName.StartsWith('<') && typeName.Contains(">d__", StringComparison.Ordinal)
                && HasCompilerGeneratedAttribute(type.GetCustomAttributes());
            bool isPlumbingMethod = methodName == ".ctor" || methodName.EndsWith("GetEnumerator", StringComparison.Ordinal);
            return isIteratorType && isPlumbingMethod;
        }

        private bool HasCompilerGeneratedAttribute(CustomAttributeHandleCollection attributes) => attributes
            .Select(attribute => AttributeTypeName(_reader.GetCustomAttribute(attribute)))
            .Contains(CompilerGeneratedAttribute, StringComparer.Ordinal);

        private string AttributeTypeName(CustomAttribute attribute) => attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => ShapeOfParent(_reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent).DefinitionName,
            HandleKind.MethodDefinition => FullName(_reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()),
            _ => throw new InvalidDataException("Unexpected attribute constructor kind " + attribute.Constructor.Kind.ToString()),
        };

        private IEnumerable<MethodDefinitionHandle> AllMethods() =>
            _reader.TypeDefinitions.SelectMany(type => _reader.GetTypeDefinition(type).GetMethods());

        private IEnumerable<Instruction> Instructions(MethodDefinitionHandle methodHandle)
        {
            MethodDefinition method = _reader.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0)
            {
                return Array.Empty<Instruction>();
            }

            byte[] il = _peReader.GetMethodBody(method.RelativeVirtualAddress).GetILBytes() ?? Array.Empty<byte>();
            return DecodeInstructions(il, Range.All, Describe(methodHandle));
        }

        /// <summary>The instructions that start in <paramref name="span"/> of a method's IL, which must end on an instruction boundary.</summary>
        private static IEnumerable<Instruction> DecodeInstructions(byte[] il, Range span, string where)
        {
            (int offset, int length) = span.GetOffsetAndLength(il.Length);
            int end = offset + length;
            while (offset < end)
            {
                int start = offset;
                bool isTwoByte = il[offset] == 0xFE;
                short value = isTwoByte ? unchecked((short)(0xFE00 | il[offset + 1])) : il[offset];
                offset += isTwoByte ? 2 : 1;
                if (!OpCodesByValue.TryGetValue(value, out OpCode opCode))
                {
                    throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                        $"Unknown IL opcode 0x{value:X4} in {where} at IL_{start:X4}."));
                }

                EntityHandle operand = HasMetadataToken(opCode.OperandType)
                    ? MetadataTokens.EntityHandle(BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset)))
                    : default;
                int operandStart = offset;
                offset += OperandSize(opCode.OperandType, il, offset);
                yield return new Instruction(start, opCode, operand)
                {
                    Variable = VariableIndex(opCode, il, operandStart),
                    Targets = BranchTargets(opCode.OperandType, il, operandStart),
                };
            }
        }

        /// <summary>
        /// The argument or local that ldarg.0-ldarg.3 or ldloc.0-ldloc.3 imply, or that an ldarg, ldarga, starg, ldloc,
        /// ldloca or stloc names in its operand; null for any other instruction. stloc.0-stloc.3 are not decoded: the walk
        /// reads the types of loaded values only.
        /// </summary>
        private static int? VariableIndex(OpCode opCode, byte[] il, int operandStart)
        {
            int implied = Array.IndexOf(ImpliedVariableOpCodes, opCode.Value);
            if (implied >= 0)
            {
                return implied % 4;
            }

            return opCode.OperandType switch
            {
                OperandType.ShortInlineVar => il[operandStart],
                OperandType.InlineVar => BinaryPrimitives.ReadUInt16LittleEndian(il.AsSpan(operandStart)),
                _ => null,
            };
        }

        /// <summary>
        /// The IL offsets a branch or switch may jump to (its operands count from the next instruction, whose offset this
        /// works out with OperandSize); empty for any other instruction.
        /// </summary>
        private static ImmutableArray<int> BranchTargets(OperandType operandType, byte[] il, int operandStart)
        {
            int next = operandStart + OperandSize(operandType, il, operandStart);
            switch (operandType)
            {
                case OperandType.ShortInlineBrTarget:
                    return ImmutableArray.Create(next + unchecked((sbyte)il[operandStart]));
                case OperandType.InlineBrTarget:
                    return ImmutableArray.Create(next + BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(operandStart)));
                case OperandType.InlineSwitch:
                    int count = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(operandStart));
                    return Enumerable.Range(0, count)
                        .Select(index => next + BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(operandStart + 4 + (4 * index))))
                        .ToImmutableArray();
                default:
                    return ImmutableArray<int>.Empty;
            }
        }

        private static bool HasMetadataToken(OperandType operandType) => operandType is
            OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok
            or OperandType.InlineType or OperandType.InlineSig;

        private static int OperandSize(OperandType operandType, byte[] il, int offset) => operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod
                or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType
                or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset))),
            _ => throw new InvalidDataException("Unsupported IL operand type " + operandType.ToString()),
        };

        private string Describe(MethodDefinitionHandle handle)
        {
            MethodDefinition method = _reader.GetMethodDefinition(handle);
            return FullName(method.GetDeclaringType()) + "::" + _reader.GetString(method.Name);
        }

        private static string At(string location, int offset) =>
            location + "+IL_" + offset.ToString("X4", CultureInfo.InvariantCulture);

        private string FullName(TypeReferenceHandle handle)
        {
            TypeReference type = _reader.GetTypeReference(handle);
            string name = _reader.GetString(type.Name);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return FullName((TypeReferenceHandle)type.ResolutionScope) + "+" + name;
            }

            string typeNamespace = _reader.GetString(type.Namespace);
            return typeNamespace.Length == 0 ? name : typeNamespace + "." + name;
        }

        private string FullName(TypeDefinitionHandle handle)
        {
            TypeDefinition type = _reader.GetTypeDefinition(handle);
            string name = _reader.GetString(type.Name);
            TypeDefinitionHandle declaringType = type.GetDeclaringType();
            if (!declaringType.IsNil)
            {
                return FullName(declaringType) + "+" + name;
            }

            string typeNamespace = _reader.GetString(type.Namespace);
            return typeNamespace.Length == 0 ? name : typeNamespace + "." + name;
        }

        /// <summary>
        /// Every instruction of the method in order, each call, callvirt and newobj with the static types of the values it
        /// takes. One forward walk tracks the evaluation stack: a value loaded from an argument, a local or a field, returned
        /// by a call, made by newobj or cast keeps its declared type; every other value is unknown (null). Where control
        /// merges, a slot keeps its type only when every path that reached it so far agrees. After an unconditional jump the
        /// walk resumes with the stack a forward branch recorded for the target, else with an empty stack (C# leaves the stack
        /// empty at statement boundaries such as loop heads). A value stored into a local of another type loses its own
        /// type, so the checks built on this see a value only where it is passed straight on.
        /// </summary>
        private IEnumerable<FlowStep> Flow(MethodDefinitionHandle methodHandle)
        {
            MethodDefinition method = _reader.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0)
            {
                yield break;
            }

            MethodBodyBlock body = _peReader.GetMethodBody(method.RelativeVirtualAddress);
            MethodSignature<TypeShape> signature = method.DecodeSignature(_shapes, null);
            var variables = new VariableTypes
            {
                Arguments = signature.Header.IsInstance
                    ? signature.ParameterTypes.Prepend(ShapeOfDefinition(method.GetDeclaringType())).ToList()
                    : signature.ParameterTypes,
                Locals = LocalTypes(methodHandle).ToList(),
            };
            var recorded = new Dictionary<int, IReadOnlyList<TypeShape?>>();
            foreach (ExceptionRegion region in body.ExceptionRegions)
            {
                // A try starts with an empty stack, a catch or filter with the exception, a finally or fault with nothing.
                Record(recorded, region.TryOffset, Array.Empty<TypeShape?>());
                bool receivesTheException = region.Kind is ExceptionRegionKind.Catch or ExceptionRegionKind.Filter;
                Record(recorded, region.HandlerOffset, receivesTheException ? new TypeShape?[] { null } : Array.Empty<TypeShape?>());
                if (region.Kind == ExceptionRegionKind.Filter)
                {
                    Record(recorded, region.FilterOffset, new TypeShape?[] { null });
                }
            }

            var stack = new List<TypeShape?>();
            bool fallsThrough = true;
            byte[] il = body.GetILBytes() ?? Array.Empty<byte>();
            foreach (Instruction instruction in DecodeInstructions(il, Range.All, Describe(methodHandle)))
            {
                if (recorded.TryGetValue(instruction.Offset, out IReadOnlyList<TypeShape?>? atTarget))
                {
                    stack = (fallsThrough ? Merge(stack, atTarget) : atTarget).ToList();
                }
                else if (!fallsThrough)
                {
                    stack.Clear();
                }

                yield return Step(instruction, stack, variables);

                bool isLeave = instruction.OpCode == OpCodes.Leave || instruction.OpCode == OpCodes.Leave_S;
                foreach (int target in instruction.Targets)
                {
                    Record(recorded, target, isLeave ? Array.Empty<TypeShape?>() : stack.ToList());
                }

                fallsThrough = instruction.OpCode.FlowControl is not (FlowControl.Branch or FlowControl.Return or FlowControl.Throw);
            }
        }

        /// <summary>Applies one instruction to the stack and returns what a call, callvirt or newobj took from it.</summary>
        private FlowStep Step(Instruction instruction, List<TypeShape?> stack, VariableTypes variables)
        {
            OpCode opCode = instruction.OpCode;
            bool isCall = opCode == OpCodes.Call || opCode == OpCodes.Callvirt || opCode == OpCodes.Newobj;
            if (isCall)
            {
                MethodSignature<TypeShape> called = CallSignature(instruction.Operand)
                    ?? throw new InvalidDataException("A call names no method: " + instruction.Operand.Kind.ToString());
                bool takesInstance = called.Header.IsInstance && opCode != OpCodes.Newobj;
                IReadOnlyList<TypeShape?> taken = Pop(stack, called.ParameterTypes.Length + (takesInstance ? 1 : 0));
                if (opCode == OpCodes.Newobj)
                {
                    stack.Add(ShapeOfParent(ParentOf(instruction.Operand)));
                }
                else if (called.ReturnType.DefinitionName != "System.Void")
                {
                    stack.Add(called.ReturnType);
                }

                return new FlowStep(instruction, takesInstance ? taken[0] : null, takesInstance ? taken.Skip(1).ToList() : taken);
            }

            if (opCode == OpCodes.Calli)
            {
                // The function pointer comes on top of the arguments.
                MethodSignature<TypeShape> pointed = _reader.GetStandaloneSignature((StandaloneSignatureHandle)instruction.Operand).DecodeMethodSignature(_shapes, null);
                Pop(stack, pointed.ParameterTypes.Length + (pointed.Header.IsInstance ? 1 : 0) + 1);
                if (pointed.ReturnType.DefinitionName != "System.Void")
                {
                    stack.Add(pointed.ReturnType);
                }

                return new FlowStep(instruction, null, Array.Empty<TypeShape?>());
            }

            if (opCode == OpCodes.Ret || opCode == OpCodes.Jmp)
            {
                // Control leaves the method; the walk resumes at the next jump target.
                stack.Clear();
                return new FlowStep(instruction, null, Array.Empty<TypeShape?>());
            }

            // Read before the pops: dup (Pop1, Push1_push1) pushes the value it pops twice.
            TypeShape? pushed = PushedType(instruction, stack, variables);
            Pop(stack, PopCount(opCode.StackBehaviourPop));
            int pushes = PushCount(opCode.StackBehaviourPush);
            for (int push = 0; push < pushes; push++)
            {
                stack.Add(pushed);
            }

            return new FlowStep(instruction, null, Array.Empty<TypeShape?>());
        }

        /// <summary>The declared type an instruction other than a call pushes, given the stack before it pops; null when unknown.</summary>
        private TypeShape? PushedType(Instruction instruction, List<TypeShape?> stack, VariableTypes variables)
        {
            OpCode opCode = instruction.OpCode;
            if (opCode == OpCodes.Dup)
            {
                return stack.Count > 0 ? stack[^1] : null;
            }

            bool loadsArgument = opCode == OpCodes.Ldarg_0 || opCode == OpCodes.Ldarg_1 || opCode == OpCodes.Ldarg_2 || opCode == OpCodes.Ldarg_3
                || opCode == OpCodes.Ldarg_S || opCode == OpCodes.Ldarg;
            bool loadsLocal = opCode == OpCodes.Ldloc_0 || opCode == OpCodes.Ldloc_1 || opCode == OpCodes.Ldloc_2 || opCode == OpCodes.Ldloc_3
                || opCode == OpCodes.Ldloc_S || opCode == OpCodes.Ldloc;
            if (loadsArgument || loadsLocal)
            {
                IReadOnlyList<TypeShape> declared = loadsArgument ? variables.Arguments : variables.Locals;
                return instruction.Variable is int index && index < declared.Count ? declared[index] : null;
            }

            if (opCode == OpCodes.Ldfld || opCode == OpCodes.Ldsfld)
            {
                return FieldType(instruction.Operand);
            }

            bool castsTo = opCode == OpCodes.Castclass || opCode == OpCodes.Isinst || opCode == OpCodes.Unbox_Any;
            return castsTo ? ShapeOf(instruction.Operand) : null;
        }

        /// <summary>The signature of the method a call, callvirt or newobj names (generic parameters as placeholders); null for another operand.</summary>
        private MethodSignature<TypeShape>? CallSignature(EntityHandle operand) => operand.Kind switch
        {
            HandleKind.MethodDefinition => _reader.GetMethodDefinition((MethodDefinitionHandle)operand).DecodeSignature(_shapes, null),
            HandleKind.MemberReference when _reader.GetMemberReference((MemberReferenceHandle)operand).GetKind() == MemberReferenceKind.Method =>
                _reader.GetMemberReference((MemberReferenceHandle)operand).DecodeMethodSignature(_shapes, null),
            HandleKind.MethodSpecification => CallSignature(_reader.GetMethodSpecification((MethodSpecificationHandle)operand).Method),
            _ => null,
        };

        /// <summary>The type that declares the method a newobj names.</summary>
        private EntityHandle ParentOf(EntityHandle operand) => operand.Kind switch
        {
            HandleKind.MethodDefinition => _reader.GetMethodDefinition((MethodDefinitionHandle)operand).GetDeclaringType(),
            HandleKind.MemberReference => _reader.GetMemberReference((MemberReferenceHandle)operand).Parent,
            HandleKind.MethodSpecification => ParentOf(_reader.GetMethodSpecification((MethodSpecificationHandle)operand).Method),
            _ => throw new InvalidDataException("Not a method handle: " + operand.Kind.ToString()),
        };

        private TypeShape? FieldType(EntityHandle operand) => operand.Kind switch
        {
            HandleKind.FieldDefinition => _reader.GetFieldDefinition((FieldDefinitionHandle)operand).DecodeSignature(_shapes, null),
            HandleKind.MemberReference when _reader.GetMemberReference((MemberReferenceHandle)operand).GetKind() == MemberReferenceKind.Field =>
                _reader.GetMemberReference((MemberReferenceHandle)operand).DecodeFieldSignature(_shapes, null),
            _ => null,
        };

        /// <summary>Removes and returns the top <paramref name="count"/> values, deepest first; values the walk lost track of are null.</summary>
        private static IReadOnlyList<TypeShape?> Pop(List<TypeShape?> stack, int count)
        {
            int available = Math.Min(count, stack.Count);
            var taken = Enumerable.Repeat<TypeShape?>(null, count - available).Concat(stack.Skip(stack.Count - available)).ToList();
            stack.RemoveRange(stack.Count - available, available);
            return taken;
        }

        /// <summary>Keeps the stack recorded for a jump target, merged with any stack recorded for it before.</summary>
        private static void Record(Dictionary<int, IReadOnlyList<TypeShape?>> recorded, int target, IReadOnlyList<TypeShape?> stack) =>
            recorded[target] = recorded.TryGetValue(target, out IReadOnlyList<TypeShape?>? earlier) ? Merge(earlier, stack) : stack;

        /// <summary>The stack where two paths meet: a slot keeps its type when both agree; stacks of different depth (never in valid IL) become unknown.</summary>
        private static IReadOnlyList<TypeShape?> Merge(IReadOnlyList<TypeShape?> first, IReadOnlyList<TypeShape?> second) =>
            first.Count != second.Count
                ? Enumerable.Repeat<TypeShape?>(null, second.Count).ToList()
                : first.Zip(second, (one, other) => one is not null && other is not null && one.Display == other.Display ? one : null).ToList();

        private static int PopCount(StackBehaviour behaviour) => behaviour switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8
                or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8 or StackBehaviour.Popref_popi_popr4
                or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref or StackBehaviour.Popref_popi_pop1 => 3,
            _ => throw new InvalidDataException("Unexpected stack behaviour " + behaviour.ToString()),
        };

        private static int PushCount(StackBehaviour behaviour) => behaviour switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1_push1 => 2,
            StackBehaviour.Push1 or StackBehaviour.Pushi or StackBehaviour.Pushi8 or StackBehaviour.Pushr4 or StackBehaviour.Pushr8 or StackBehaviour.Pushref => 1,
            _ => throw new InvalidDataException("Unexpected stack behaviour " + behaviour.ToString()),
        };

        private readonly record struct Instruction(int Offset, OpCode OpCode, EntityHandle Operand)
        {
            /// <summary>The argument or local index the instruction names or implies (see VariableIndex); null for any other instruction.</summary>
            public int? Variable { get; init; }

            /// <summary>Where a branch or switch may jump; empty otherwise.</summary>
            public ImmutableArray<int> Targets { get; init; }
        }

        /// <summary>One instruction with the static types of the receiver and arguments it takes, when it is a call, callvirt or newobj.</summary>
        private readonly record struct FlowStep(Instruction Instruction, TypeShape? Instance, IReadOnlyList<TypeShape?> Arguments);

        /// <summary>The declared types of a method's arguments (this first, for an instance method) and of its locals.</summary>
        private sealed record VariableTypes
        {
            public required IReadOnlyList<TypeShape> Arguments { get; init; }

            public required IReadOnlyList<TypeShape> Locals { get; init; }
        }

        /// <summary>A catch or filter handler, with the location of its method and the method's IL.</summary>
        private readonly record struct Handler(string Location, ExceptionRegion Region, byte[] Il);

        /// <summary>Decodes signatures into <see cref="TypeShape"/>s.</summary>
        private sealed class ShapeProvider : ISignatureTypeProvider<TypeShape, object?>
        {
            private static readonly IReadOnlyDictionary<PrimitiveTypeCode, string> PrimitiveNames = new Dictionary<PrimitiveTypeCode, string>
            {
                [PrimitiveTypeCode.Boolean] = "System.Boolean", [PrimitiveTypeCode.Char] = "System.Char",
                [PrimitiveTypeCode.SByte] = "System.SByte", [PrimitiveTypeCode.Byte] = "System.Byte",
                [PrimitiveTypeCode.Int16] = "System.Int16", [PrimitiveTypeCode.UInt16] = "System.UInt16",
                [PrimitiveTypeCode.Int32] = "System.Int32", [PrimitiveTypeCode.UInt32] = "System.UInt32",
                [PrimitiveTypeCode.Int64] = "System.Int64", [PrimitiveTypeCode.UInt64] = "System.UInt64",
                [PrimitiveTypeCode.Single] = "System.Single", [PrimitiveTypeCode.Double] = "System.Double",
                [PrimitiveTypeCode.IntPtr] = "System.IntPtr", [PrimitiveTypeCode.UIntPtr] = "System.UIntPtr",
                [PrimitiveTypeCode.Object] = "System.Object", [PrimitiveTypeCode.String] = "System.String",
                [PrimitiveTypeCode.Void] = "System.Void", [PrimitiveTypeCode.TypedReference] = "System.TypedReference",
            };

            private readonly AssemblyScanner _owner;

            public ShapeProvider(AssemblyScanner owner) => _owner = owner;

            /// <summary>A type that is only its name: its own definition, mentioning no external type, no floating point and no native integer.</summary>
            public static TypeShape Plain(string name) => new()
            {
                Display = name,
                DefinitionName = name,
                ExternalTypes = ImmutableArray<string>.Empty,
                UsesFloatingPoint = false,
                UsesNativeInteger = false,
            };

            public TypeShape GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                string name = PrimitiveNames[typeCode];
                return Plain(name) with
                {
                    UsesFloatingPoint = typeCode is PrimitiveTypeCode.Single or PrimitiveTypeCode.Double,
                    UsesNativeInteger = typeCode is PrimitiveTypeCode.IntPtr or PrimitiveTypeCode.UIntPtr,
                };
            }

            public TypeShape GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                string name = _owner.FullName(handle);
                // A sibling assembly's type is no external platform type (see ApiPolicyViolations).
                return Plain(name) with
                {
                    ExternalTypes = _owner.IsUnityBoundSiblingType(handle) ? ImmutableArray<string>.Empty : ImmutableArray.Create(name),
                    UsesFloatingPoint = FloatingPointTypeNames.Contains(name),
                };
            }

            public TypeShape GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
                Plain(_owner.FullName(handle));

            public TypeShape GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
                reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

            public TypeShape GetSZArrayType(TypeShape elementType) => ArrayOf(elementType, elementType.Display + "[]");

            public TypeShape GetArrayType(TypeShape elementType, ArrayShape shape) =>
                ArrayOf(elementType, elementType.Display + "[" + new string(',', shape.Rank - 1) + "]");

            public TypeShape GetByReferenceType(TypeShape elementType) => Wrap(elementType, elementType.Display + "&");

            public TypeShape GetPointerType(TypeShape elementType) => Wrap(elementType, elementType.Display + "*");

            public TypeShape GetPinnedType(TypeShape elementType) => elementType;

            public TypeShape GetModifiedType(TypeShape modifier, TypeShape unmodifiedType, bool isRequired) => unmodifiedType;

            public TypeShape GetGenericInstantiation(TypeShape genericType, ImmutableArray<TypeShape> typeArguments) => new()
            {
                Display = genericType.Display + "<" + string.Join(",", typeArguments.Select(argument => argument.Display)) + ">",
                DefinitionName = genericType.DefinitionName,
                ExternalTypes = genericType.ExternalTypes.AddRange(typeArguments.SelectMany(argument => argument.ExternalTypes)),
                UsesFloatingPoint = genericType.UsesFloatingPoint || typeArguments.Any(argument => argument.UsesFloatingPoint),
                UsesNativeInteger = genericType.UsesNativeInteger || typeArguments.Any(argument => argument.UsesNativeInteger),
                TypeArguments = typeArguments,
            };

            public TypeShape GetGenericMethodParameter(object? genericContext, int index) =>
                Plain("!!" + index.ToString(CultureInfo.InvariantCulture));

            public TypeShape GetGenericTypeParameter(object? genericContext, int index) =>
                Plain("!" + index.ToString(CultureInfo.InvariantCulture));

            public TypeShape GetFunctionPointerType(MethodSignature<TypeShape> signature) => new()
            {
                Display = "method",
                DefinitionName = "method",
                ExternalTypes = signature.ParameterTypes.Append(signature.ReturnType).SelectMany(part => part.ExternalTypes).ToImmutableArray(),
                UsesFloatingPoint = UsesFloatingPoint(signature),
                UsesNativeInteger = signature.ReturnType.UsesNativeInteger || signature.ParameterTypes.Any(part => part.UsesNativeInteger),
            };

            /// <summary>An array of the element type: System.Array, with what the element mentions.</summary>
            private static TypeShape ArrayOf(TypeShape element, string display) => Wrap(element, display) with { DefinitionName = "System.Array" };

            /// <summary>A type built on the element type: the element's definition, external types and flags, without its type arguments.</summary>
            private static TypeShape Wrap(TypeShape element, string display) =>
                element with { Display = display, TypeArguments = ImmutableArray<TypeShape>.Empty };
        }
    }

    /// <summary>The calling method, needed by bans that exempt GetHashCode overrides, record members the compiler writes and iterator plumbing.</summary>
    internal sealed record CallerContext
    {
        public required string MethodName { get; init; }

        public required bool IsSynthesizedRecordMember { get; init; }

        public required bool IsIteratorPlumbing { get; init; }
    }

    /// <summary>Which platform types and members an assembly may use: <see cref="UnityBoundApiPolicy"/> or <see cref="MemberBansPolicy"/>.</summary>
    internal interface IPlatformApiPolicy
    {
        /// <summary>Completes "type ..." and "uses T, which ..." for a refused type, for example "is not on the Unity-bound allowlist".</summary>
        string RefusedTypeReason { get; }

        bool IsTypeAllowed(string typeName);

        /// <summary>Why this use of an allowed type's member breaks the policy; nothing when it does not.</summary>
        IEnumerable<string> MemberViolations(MemberUse use, CallerContext caller);
    }
}
