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
    internal sealed record Violation(string Location, string Reason)
    {
        public override string ToString() => Location + ": " + Reason;
    }

    /// <summary>
    /// One member a method body names (a call, a field access or a member token): the calling type and method, and
    /// the member's declaring type (generic arguments stripped, for example System.Collections.Generic.List`1) and
    /// name. Nested and compiler-written types keep their full name, for example "CoveredLot.Core.X+&lt;&gt;c".
    /// </summary>
    internal sealed record MemberReferenceSite(string CallerType, string CallerMethod, string DeclaringType, string MemberName)
    {
        public override string ToString() => CallerType + "::" + CallerMethod + " -> " + DeclaringType + "::" + MemberName;
    }

    /// <summary>
    /// Reads a compiled assembly's metadata and IL without loading it, to prove rules the compiler cannot enforce.
    /// Everything is checked on the compiled output, so a rule cannot be dodged with var, generics or a helper
    /// method. A trimmed port of the engine's AssemblyInspection (SaturnWave/puzzle-engine,
    /// tests/PuzzleCore.Tests/Support/AssemblyInspection.cs): its catch regions, type, member and assembly references
    /// and IL opcode walk are kept; its floating-point, platform-API and stack-flow checks are left out. Two rules of
    /// Core's own are added: no catch block at all, and no static field that can be reassigned.
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

        /// <summary>Instructions an empty catch compiles to: drop or store the exception, then leave.</summary>
        private static readonly IReadOnlySet<short> EmptyHandlerOpCodes = new[]
        {
            OpCodes.Nop, OpCodes.Pop, OpCodes.Leave, OpCodes.Leave_S,
            OpCodes.Stloc, OpCodes.Stloc_S, OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3,
        }.Select(opCode => opCode.Value).ToHashSet();

        private readonly PEReader _peReader;
        private readonly MetadataReader _reader;
        private readonly DefinitionNameProvider _names;

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

            _names = new DefinitionNameProvider(this);
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
                found.AddRange(Instructions(methodHandle)
                    .Where(instruction => !instruction.Operand.IsNil)
                    .Select(instruction => MemberNamedBy(instruction.Operand))
                    .OfType<MemberName>()
                    .Select(member => new MemberReferenceSite(callerType, callerMethod, member.DeclaringType, member.Name)));
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
            .Select(handler => new Violation(At(handler.Location, handler.Region.HandlerOffset), DescribeCatch(handler.Region)))
            .ToList();

        /// <summary>
        /// The state machines the compiler writes for async methods: types that implement IAsyncStateMachine. Each
        /// holds a compiler-written generic catch that hands the exception to the returned task.
        /// </summary>
        public IReadOnlyList<Violation> AsyncStateMachines() => _reader.TypeDefinitions
            .Where(ImplementsAsyncStateMachine)
            .Select(type => new Violation(FullName(type), "async state machine"))
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
                    .Select(field => new Violation(typeName + "::" + _reader.GetString(field.Name), "static field that can be reassigned")));
            }

            return found;
        }

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
            if (region.Kind == ExceptionRegionKind.Catch && IsCatchAllType(TypeName(region.CatchType)))
            {
                yield return new Violation(at, "generic catch (" + TypeName(region.CatchType) + ")");
            }

            if (region.Kind == ExceptionRegionKind.Filter)
            {
                IReadOnlyList<string> filteredTypes = DecodeInstructions(handler.Il, region.FilterOffset, region.HandlerOffset, handler.Location)
                    .Where(instruction => instruction.OpCode == OpCodes.Isinst && !instruction.Operand.IsNil)
                    .Select(instruction => TypeName(instruction.Operand))
                    .ToList();
                if (filteredTypes.Count == 0 || filteredTypes.Any(IsCatchAllType))
                {
                    yield return new Violation(at, "generic catch with a when-filter");
                }
            }

            bool isEmpty = DecodeInstructions(handler.Il, region.HandlerOffset, region.HandlerOffset + region.HandlerLength, handler.Location)
                .All(instruction => EmptyHandlerOpCodes.Contains(instruction.OpCode.Value));
            if (isEmpty)
            {
                yield return new Violation(at, "empty catch block");
            }
        }

        private string DescribeCatch(ExceptionRegion region) => region.Kind == ExceptionRegionKind.Catch
            ? "catch block (" + TypeName(region.CatchType) + ")"
            : "catch block with a when-filter";

        private static bool IsCatchAllType(string typeName) => typeName is "System.Exception" or "System.Object";

        private static bool IsReassignableStatic(FieldDefinition field) =>
            (field.Attributes & FieldAttributes.Static) != 0
            && (field.Attributes & (FieldAttributes.Literal | FieldAttributes.InitOnly)) == 0;

        private bool ImplementsAsyncStateMachine(TypeDefinitionHandle handle) => _reader.GetTypeDefinition(handle)
            .GetInterfaceImplementations()
            .Select(implementation => TypeName(_reader.GetInterfaceImplementation(implementation).Interface))
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
                if (hasUnspeakableName && HasCompilerGeneratedAttribute(type))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasCompilerGeneratedAttribute(TypeDefinition type) => type.GetCustomAttributes()
            .Select(attribute => AttributeTypeName(_reader.GetCustomAttribute(attribute)))
            .Contains(CompilerGeneratedAttribute, StringComparer.Ordinal);

        private string AttributeTypeName(CustomAttribute attribute) => attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => DeclaringTypeName(_reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent),
            HandleKind.MethodDefinition => FullName(_reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()),
            _ => throw new InvalidDataException("Unexpected attribute constructor kind " + attribute.Constructor.Kind.ToString()),
        };

        /// <summary>The member an IL operand names; null when the operand is a type or a signature.</summary>
        private MemberName? MemberNamedBy(EntityHandle operand)
        {
            switch (operand.Kind)
            {
                case HandleKind.MemberReference:
                    MemberReference member = _reader.GetMemberReference((MemberReferenceHandle)operand);
                    return new MemberName(DeclaringTypeName(member.Parent), _reader.GetString(member.Name));
                case HandleKind.MethodDefinition:
                    MethodDefinition method = _reader.GetMethodDefinition((MethodDefinitionHandle)operand);
                    return new MemberName(FullName(method.GetDeclaringType()), _reader.GetString(method.Name));
                case HandleKind.FieldDefinition:
                    FieldDefinition field = _reader.GetFieldDefinition((FieldDefinitionHandle)operand);
                    return new MemberName(FullName(field.GetDeclaringType()), _reader.GetString(field.Name));
                case HandleKind.MethodSpecification:
                    return MemberNamedBy(_reader.GetMethodSpecification((MethodSpecificationHandle)operand).Method);
                default:
                    return null;
            }
        }

        private string DeclaringTypeName(EntityHandle parent) => parent.Kind switch
        {
            HandleKind.TypeReference or HandleKind.TypeSpecification or HandleKind.TypeDefinition => TypeName(parent),
            HandleKind.MethodDefinition => FullName(_reader.GetMethodDefinition((MethodDefinitionHandle)parent).GetDeclaringType()),
            HandleKind.ModuleReference => "<module>",
            _ => throw new InvalidDataException("Unexpected member parent kind " + parent.Kind.ToString()),
        };

        /// <summary>The name of the type's definition: generic arguments stripped, arrays as System.Array.</summary>
        private string TypeName(EntityHandle type) => type.Kind switch
        {
            HandleKind.TypeReference => FullName((TypeReferenceHandle)type),
            HandleKind.TypeDefinition => FullName((TypeDefinitionHandle)type),
            HandleKind.TypeSpecification => _reader.GetTypeSpecification((TypeSpecificationHandle)type).DecodeSignature(_names, null),
            _ => throw new InvalidDataException("Not a type handle: " + type.Kind.ToString()),
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
            return DecodeInstructions(il, 0, il.Length, Describe(methodHandle));
        }

        private static IEnumerable<Instruction> DecodeInstructions(byte[] il, int startOffset, int endOffset, string where)
        {
            int offset = startOffset;
            while (offset < endOffset)
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
                offset += OperandSize(opCode.OperandType, il, offset);
                yield return new Instruction(start, opCode, operand);
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

        private readonly record struct Instruction(int Offset, OpCode OpCode, EntityHandle Operand);

        /// <summary>A catch or filter handler, with the location of its method and the method's IL.</summary>
        private readonly record struct Handler(string Location, ExceptionRegion Region, byte[] Il);

        private sealed record MemberName(string DeclaringType, string Name);

        /// <summary>
        /// Decodes a type in a signature to the name of its definition: generic arguments stripped, arrays as
        /// System.Array, by-reference and pointer types as their element type, generic parameters as !n and !!n.
        /// </summary>
        private sealed class DefinitionNameProvider : ISignatureTypeProvider<string, object?>
        {
            private readonly AssemblyScanner _owner;

            public DefinitionNameProvider(AssemblyScanner owner) => _owner = owner;

            /// <summary>Every primitive type code is named after its System type (Int32 is System.Int32).</summary>
            public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + typeCode.ToString();

            public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => _owner.FullName(handle);

            public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => _owner.FullName(handle);

            public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
                reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

            public string GetSZArrayType(string elementType) => "System.Array";

            public string GetArrayType(string elementType, ArrayShape shape) => "System.Array";

            public string GetByReferenceType(string elementType) => elementType;

            public string GetPointerType(string elementType) => elementType;

            public string GetPinnedType(string elementType) => elementType;

            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType;

            public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index.ToString(CultureInfo.InvariantCulture);

            public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index.ToString(CultureInfo.InvariantCulture);

            public string GetFunctionPointerType(MethodSignature<string> signature) => "method";
        }
    }
}
