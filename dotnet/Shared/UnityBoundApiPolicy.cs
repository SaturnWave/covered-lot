using System;
using System.Collections.Generic;
using System.Linq;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The platform APIs that Unity-bound code may use, ported from the engine's policy for its own Unity-bound assemblies
    /// (SaturnWave/puzzle-engine, tests/PuzzleCore.Tests/Support/UnityBoundApiPolicy.cs). The rule it serves: the same
    /// actions give the same results on every device and on .NET, Mono and IL2CPP alike. It is an ALLOWLIST: a type that
    /// is not listed is rejected, so a new type that reads the clock, draws randomness, depends on the device culture,
    /// platform or time zone, or does floating-point math cannot slip in unnoticed. Within allowed types, member bans
    /// remove the culture-, platform- and process-dependent members they name; the remarks list the uses of allowed
    /// members that they do not see. Adding a type or lifting a ban is a reviewed change with a reason in the comment
    /// next to it. Members the compiler synthesizes for records (ToString, PrintMembers, Equals, GetHashCode...) are
    /// exempt, on the rule that no result is ever built from their output.
    /// </summary>
    /// <remarks>
    /// Known gaps, which the engine's policy has too. Each of these passes this policy and <see cref="MemberBansPolicy"/>:
    /// <list type="bullet">
    /// <item><description>A number formatted through System.Object.ToString: boxed, held in a Nullable, a ValueTuple or a
    /// KeyValuePair, or passed as a generic T. The call then names no number type.</description></item>
    /// <item><description>A null IFormatProvider, passed to an overload that takes one.</description></item>
    /// <item><description>A culture-sensitive StringComparison, passed to a string member that takes one (Equals, Compare,
    /// StartsWith, EndsWith, IndexOf, LastIndexOf and others): the walk does not read the constant. For Core, the hygiene
    /// tests refuse the four culture-sensitive names in its sources instead.</description></item>
    /// <item><description>BinarySearch over text without a comparer (List&lt;T&gt;, Array), and CompareTo called through
    /// IComparable or IComparable&lt;T&gt;.</description></item>
    /// <item><description>HashSet&lt;T&gt;.CopyTo, which copies the elements in hash order.</description></item>
    /// <item><description>A dictionary or set handed on as an IEnumerable&lt;T&gt; through a helper's return value or an
    /// interface-typed local (see <see cref="AssemblyScanner"/>).</description></item>
    /// </list>
    /// One more passes <see cref="MemberBansPolicy"/> only: System.Collections.Comparer.Default and DefaultInvariant, which
    /// order text by the device culture or the platform's collation tables. The bans name get_Default and
    /// get_DefaultInvariant, but both are static fields, so the bans never match; the allowlist refuses the type.
    /// </remarks>
    internal sealed class UnityBoundApiPolicy : IPlatformApiPolicy
    {
        private const string Culture = "depends on the device culture; pass CultureInfo.InvariantCulture or use an ordinal overload";
        private const string RandomizedHash = "hash codes are randomized per process; use the engine's own FNV-1a hash";
        private const string Newline = "Environment.NewLine differs between Windows and other platforms; append '\\n' explicitly";
        private const string UnorderedEnumeration = "enumeration order is an implementation detail that may differ between .NET and Unity; keep a sorted or explicit order";
        private const string UnstableSort = "unstable sort: equal keys may come out in a different order on Mono/IL2CPP and .NET; use OrderBy";
        private const string LinguisticOrder = "orders text by the device culture or by the platform's collation tables (ICU, NLS and Mono differ); use StringComparer.Ordinal";

        private static readonly string[] IntegerTypes =
        {
            "System.Byte", "System.SByte", "System.Int16", "System.UInt16", "System.Int32", "System.UInt32", "System.Int64", "System.UInt64",
        };

        public static UnityBoundApiPolicy Default { get; } = new UnityBoundApiPolicy(DefaultTypes(), DefaultBans());

        private readonly IReadOnlyDictionary<string, TypeRule> _types;
        private readonly IReadOnlyList<MemberBan> _bans;

        private UnityBoundApiPolicy(IEnumerable<TypeRule> types, IReadOnlyList<MemberBan> bans)
        {
            _types = types.ToDictionary(rule => rule.TypeName, StringComparer.Ordinal);
            _bans = bans;
        }

        public string RefusedTypeReason => "is not on the Unity-bound allowlist";

        public bool IsTypeAllowed(string typeName) => _types.ContainsKey(typeName);

        public IEnumerable<string> MemberViolations(MemberUse use, CallerContext caller)
        {
            string typeName = use.DeclaringType.DefinitionName;
            if (_types.TryGetValue(typeName, out TypeRule? rule) && rule is OnlyMembers only && !only.Members.Contains(use.MemberName))
            {
                yield return "only " + string.Join(", ", only.Members.OrderBy(name => name, StringComparer.Ordinal)) + " may be used on " + typeName;
            }

            foreach (string reason in BannedMemberUses(use, caller))
            {
                yield return reason;
            }
        }

        /// <summary>Every banned member as Type::Member (the type without generic arguments), each once; the self-tests plant a use of each.</summary>
        public IReadOnlyList<string> BannedMembers => _bans.Select(ban => ban.TypeName + "::" + ban.MemberName).Distinct(StringComparer.Ordinal).ToList();

        /// <summary>
        /// The member bans alone, without the allowlist: the members DefaultBans names for culture-dependent text and text
        /// order, the platform newline, randomized hashing, Environment.CurrentManagedThreadId, enumeration in hash order
        /// and unstable sorts, and the hash-order rules of <see cref="UnorderedSequences"/>. <see cref="MemberBansPolicy"/>
        /// applies them to code that may use every platform type.
        /// </summary>
        public IEnumerable<string> BannedMemberUses(MemberUse use, CallerContext caller)
        {
            string typeName = use.DeclaringType.DefinitionName;
            foreach (MemberBan ban in _bans.Where(ban => ban.TypeName == typeName && ban.MemberName == use.MemberName))
            {
                bool isExempt = caller.IsSynthesizedRecordMember
                    || (ban.ExemptInHashCodeOverrides && caller.MethodName == "GetHashCode")
                    || (ban.ExemptInIteratorPlumbing && caller.IsIteratorPlumbing);
                if (!isExempt && ban.AppliesTo(use))
                {
                    yield return ban.Reason;
                }
            }

            if (!caller.IsSynthesizedRecordMember && UnorderedSequences.EnumeratesInHashOrder(use))
            {
                yield return UnorderedEnumeration;
            }
        }

        private static IEnumerable<TypeRule> DefaultTypes()
        {
            // Core language types. Floating-point types are absent on purpose.
            foreach (string name in IntegerTypes.Concat(new[]
            {
                "System.Object", "System.ValueType", "System.Enum", "System.String", "System.Boolean", "System.Char", "System.Void",
                "System.Array", "System.Math", "System.Nullable`1", "System.Index", "System.Range", "System.MemoryExtensions",
                "System.Span`1", "System.ReadOnlySpan`1", "System.IEquatable`1", "System.IComparable", "System.IComparable`1",
                "System.IDisposable", "System.IFormatProvider", "System.StringComparison", "System.StringComparer",
                // An options enum of string.Split (the char overload takes it as an optional argument); no culture or platform input.
                "System.StringSplitOptions",
                "System.ValueTuple`2", "System.ValueTuple`3", "System.ValueTuple`4",
                "System.Func`1", "System.Func`2", "System.Func`3", "System.Func`4", "System.Func`5",
                "System.Action", "System.Action`1", "System.Action`2", "System.Action`3", "System.Predicate`1", "System.Comparison`1",
                "System.RuntimeTypeHandle", "System.RuntimeFieldHandle",
            }))
            {
                yield return new AllMembers(name);
            }

            // Exceptions Unity-bound code may throw or catch by name.
            foreach (string name in new[]
            {
                "System.Exception", "System.ArgumentException", "System.ArgumentNullException", "System.ArgumentOutOfRangeException",
                "System.InvalidOperationException", "System.NotSupportedException", "System.FormatException", "System.OverflowException",
                "System.IndexOutOfRangeException", "System.Collections.Generic.KeyNotFoundException",
            })
            {
                yield return new AllMembers(name);
            }

            // Collections. Dictionary/HashSet enumeration is banned below; lookups are fine.
            foreach (string name in new[]
            {
                "System.Collections.IEnumerable", "System.Collections.IEnumerator",
                "System.Collections.Generic.IEnumerable`1", "System.Collections.Generic.IEnumerator`1",
                "System.Collections.Generic.ICollection`1", "System.Collections.Generic.IList`1",
                "System.Collections.Generic.IReadOnlyCollection`1", "System.Collections.Generic.IReadOnlyList`1",
                "System.Collections.Generic.IReadOnlyDictionary`2", "System.Collections.Generic.IDictionary`2",
                "System.Collections.Generic.List`1", "System.Collections.Generic.List`1+Enumerator",
                "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.HashSet`1",
                "System.Collections.Generic.Queue`1", "System.Collections.Generic.Stack`1",
                "System.Collections.Generic.SortedDictionary`2", "System.Collections.Generic.SortedDictionary`2+Enumerator",
                "System.Collections.Generic.SortedSet`1", "System.Collections.Generic.SortedSet`1+Enumerator",
                "System.Collections.Generic.KeyValuePair`2", "System.Collections.Generic.IComparer`1",
                "System.Collections.Generic.IEqualityComparer`1", "System.Collections.Generic.Comparer`1",
                "System.Collections.Generic.EqualityComparer`1", "System.Collections.ObjectModel.ReadOnlyCollection`1",
                "System.Linq.Enumerable", "System.Linq.IOrderedEnumerable`1", "System.Linq.IGrouping`2", "System.Linq.ILookup`2",
            })
            {
                yield return new AllMembers(name);
            }

            // Text, hashing and binary helpers, which the engine needs for records, rules fingerprints and traces.
            yield return new AllMembers("System.Text.StringBuilder");
            yield return new AllMembers("System.Buffers.Binary.BinaryPrimitives");
            yield return new OnlyMembers("System.Text.Encoding", ["get_UTF8", "GetBytes", "GetString", "GetByteCount"]);
            yield return new AllMembers("System.Text.UTF8Encoding");
            yield return new OnlyMembers("System.Globalization.CultureInfo", ["get_InvariantCulture"]);
            yield return new AllMembers("System.Globalization.NumberStyles");
            yield return new OnlyMembers("System.Security.Cryptography.SHA256", ["Create"]);
            yield return new OnlyMembers("System.Security.Cryptography.HashAlgorithm", ["ComputeHash", "Dispose"]);

            // Iterator methods (yield return) compile to a state machine whose constructor and
            // GetEnumerator read Environment.CurrentManagedThreadId; a ban below allows only that.
            yield return new OnlyMembers("System.Environment", ["get_CurrentManagedThreadId"]);

            // Of System.Type, only what records and typeof comparisons compile to. Object.GetType stays allowed, as every
            // member of System.Object is, and so are Object's members on the Type it returns (ToString gives its name).
            yield return new OnlyMembers("System.Type", ["GetTypeFromHandle", "op_Equality", "op_Inequality"]);
            // EnsureSufficientExecutionStack is what record ToString/PrintMembers compile to.
            yield return new OnlyMembers("System.Runtime.CompilerServices.RuntimeHelpers", ["InitializeArray", "GetSubArray", "EnsureSufficientExecutionStack"]);

            // Attributes the compiler and SDK attach to every assembly; they carry no behaviour.
            foreach (string name in new[]
            {
                "System.Attribute", "System.AttributeUsageAttribute", "System.AttributeTargets", "System.FlagsAttribute",
                "System.ObsoleteAttribute", "System.ParamArrayAttribute", "System.Reflection.DefaultMemberAttribute",
                "System.Reflection.AssemblyCompanyAttribute", "System.Reflection.AssemblyConfigurationAttribute",
                "System.Reflection.AssemblyFileVersionAttribute", "System.Reflection.AssemblyInformationalVersionAttribute",
                "System.Reflection.AssemblyProductAttribute", "System.Reflection.AssemblyTitleAttribute",
                "System.Reflection.AssemblyVersionAttribute", "System.Reflection.AssemblyMetadataAttribute",
                "System.Runtime.Versioning.TargetFrameworkAttribute",
                "System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
                "System.Runtime.CompilerServices.RuntimeCompatibilityAttribute",
                "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
                "System.Runtime.CompilerServices.IsReadOnlyAttribute", "System.Runtime.CompilerServices.IsByRefLikeAttribute",
                // `in` parameters on interface and abstract members carry a modreq(InAttribute); a marker, no behaviour.
                "System.Runtime.InteropServices.InAttribute",
                "System.Runtime.CompilerServices.IsExternalInit", "System.Runtime.CompilerServices.ExtensionAttribute",
                "System.Runtime.CompilerServices.TupleElementNamesAttribute", "System.Runtime.CompilerServices.IteratorStateMachineAttribute",
                "System.Runtime.CompilerServices.NullableAttribute", "System.Runtime.CompilerServices.NullableContextAttribute",
                "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
                "System.Diagnostics.DebuggableAttribute", "System.Diagnostics.DebuggableAttribute+DebuggingModes",
                "System.Diagnostics.DebuggerBrowsableAttribute", "System.Diagnostics.DebuggerBrowsableState",
                "System.Diagnostics.DebuggerHiddenAttribute", "System.Diagnostics.DebuggerStepThroughAttribute",
                "System.Diagnostics.DebuggerDisplayAttribute", "System.Diagnostics.DebuggerNonUserCodeAttribute",
                "System.Diagnostics.CodeAnalysis.NotNullWhenAttribute", "System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute",
                "System.Diagnostics.CodeAnalysis.NotNullAttribute", "System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute",
                "System.Diagnostics.CodeAnalysis.DisallowNullAttribute", "System.Diagnostics.CodeAnalysis.AllowNullAttribute",
                "System.Diagnostics.CodeAnalysis.MaybeNullAttribute", "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
            })
            {
                yield return new AllMembers(name);
            }
        }

        private static IReadOnlyList<MemberBan> DefaultBans()
        {
            var bans = new List<MemberBan>();

            // An overload that takes an IFormatProvider passes whatever provider it is given, a null one included, which
            // formats with the device culture (see the remarks).
            foreach (string type in IntegerTypes)
            {
                bans.Add(new MemberBan { TypeName = type, MemberName = "ToString", Reason = Culture,
                    AppliesTo = WithParameters(parameters => !parameters.Contains("System.IFormatProvider")) });
                bans.Add(new MemberBan { TypeName = type, MemberName = "Parse", Reason = Culture,
                    AppliesTo = WithParameters(parameters => !parameters.Contains("System.IFormatProvider")) });
                bans.Add(new MemberBan { TypeName = type, MemberName = "TryParse", Reason = Culture,
                    AppliesTo = WithParameters(parameters => !parameters.Contains("System.IFormatProvider")) });
            }

            bans.Add(new MemberBan { TypeName = "System.String", MemberName = "ToUpper", Reason = Culture,
                AppliesTo = WithParameters(parameters => parameters.Count == 0) });
            bans.Add(new MemberBan { TypeName = "System.String", MemberName = "ToLower", Reason = Culture,
                AppliesTo = WithParameters(parameters => parameters.Count == 0) });
            // Compare and the four searches let through an overload with a StringComparison parameter, whatever value it is
            // given: the walk does not read the constant, so StringComparison.CurrentCulture passes too (see the remarks).
            bans.Add(new MemberBan { TypeName = "System.String", MemberName = "Compare", Reason = Culture,
                AppliesTo = WithParameters(parameters => !parameters.Contains("System.StringComparison")) });
            bans.Add(new MemberBan { TypeName = "System.String", MemberName = "CompareTo", Reason = Culture, AppliesTo = _ => true });
            foreach (string search in new[] { "StartsWith", "EndsWith", "IndexOf", "LastIndexOf" })
            {
                bans.Add(new MemberBan { TypeName = "System.String", MemberName = search, Reason = Culture,
                    AppliesTo = WithParameters(parameters => parameters.Count > 0 && parameters[0] == "System.String" && !parameters.Contains("System.StringComparison")) });
            }

            // Format and AppendFormat let through an overload that takes a provider first, a null provider included.
            bans.Add(new MemberBan { TypeName = "System.String", MemberName = "Format", Reason = Culture,
                AppliesTo = WithParameters(parameters => parameters.Count == 0 || parameters[0] != "System.IFormatProvider") });
            // Concat<T> and Join<T> call T.ToString(); with T = string that is the text itself.
            bans.Add(new MemberBan { TypeName = "System.String", MemberName = "Concat", Reason = Culture,
                AppliesTo = use => use.Parameters.Any(parameter => IsObjectOrGeneric(parameter.Display)) && !JoinsOnlyText(use) });
            bans.Add(new MemberBan { TypeName = "System.String", MemberName = "Join", Reason = Culture,
                AppliesTo = use => use.Parameters.Skip(1).Any(parameter => IsObjectOrGeneric(parameter.Display)) && !JoinsOnlyText(use) });
            bans.Add(new MemberBan { TypeName = "System.Char", MemberName = "ToUpper", Reason = Culture,
                AppliesTo = WithParameters(parameters => parameters.Count == 1) });
            bans.Add(new MemberBan { TypeName = "System.Char", MemberName = "ToLower", Reason = Culture,
                AppliesTo = WithParameters(parameters => parameters.Count == 1) });

            bans.Add(new MemberBan { TypeName = "System.Text.StringBuilder", MemberName = "Append", Reason = Culture,
                AppliesTo = WithParameters(parameters => parameters.Count == 1 && (IntegerTypes.Contains(parameters[0]) || IsObjectOrGeneric(parameters[0]))) });
            bans.Add(new MemberBan { TypeName = "System.Text.StringBuilder", MemberName = "Insert", Reason = Culture,
                AppliesTo = WithParameters(parameters => parameters.Count == 2 && (IntegerTypes.Contains(parameters[1]) || IsObjectOrGeneric(parameters[1]))) });
            bans.Add(new MemberBan { TypeName = "System.Text.StringBuilder", MemberName = "AppendFormat", Reason = Culture,
                AppliesTo = WithParameters(parameters => parameters.Count == 0 || parameters[0] != "System.IFormatProvider") });
            bans.Add(new MemberBan { TypeName = "System.Text.StringBuilder", MemberName = "AppendLine", Reason = Newline, AppliesTo = _ => true });

            // C# 10 and later compile $"...{n}" to these handlers; without a provider they format with the device culture, and
            // CA1305 does not see it (string.Create(CultureInfo.InvariantCulture, $"...") passes one). C# 9 compiles $"..." to
            // string.Format, banned above.
            bans.Add(new MemberBan { TypeName = "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler", MemberName = ".ctor", Reason = Culture,
                AppliesTo = WithParameters(parameters => !parameters.Contains("System.IFormatProvider")) });
            bans.Add(new MemberBan { TypeName = "System.Text.StringBuilder+AppendInterpolatedStringHandler", MemberName = ".ctor", Reason = Culture,
                AppliesTo = WithParameters(parameters => !parameters.Contains("System.IFormatProvider")) });
            bans.Add(new MemberBan { TypeName = "System.Globalization.CultureInfo", MemberName = "get_CurrentCulture", Reason = Culture, AppliesTo = _ => true });
            bans.Add(new MemberBan { TypeName = "System.Globalization.CultureInfo", MemberName = "get_CurrentUICulture", Reason = Culture, AppliesTo = _ => true });

            // Text order: only an ordinal comparison orders strings the same on every device and runtime. A comparer-less
            // sort, sorted collection or Comparer<T>.Default over text compares by the device culture.
            foreach (string comparer in new[]
            {
                "get_CurrentCulture", "get_CurrentCultureIgnoreCase", "get_InvariantCulture", "get_InvariantCultureIgnoreCase", "Create", "FromComparison",
            })
            {
                bans.Add(new MemberBan { TypeName = "System.StringComparer", MemberName = comparer, Reason = LinguisticOrder, AppliesTo = _ => true });
            }

            // These two bans, the engine's, name property getters, but Comparer.Default and DefaultInvariant are static fields
            // (IL reads them with ldsfld), so neither ban ever matches. The allowlist refuses the type, which keeps both out
            // of Unity-bound code; the member bans alone let them through (see the remarks).
            bans.Add(new MemberBan { TypeName = "System.Collections.Comparer", MemberName = "get_Default", Reason = LinguisticOrder, AppliesTo = _ => true });
            bans.Add(new MemberBan { TypeName = "System.Collections.Comparer", MemberName = "get_DefaultInvariant", Reason = LinguisticOrder, AppliesTo = _ => true });
            bans.Add(new MemberBan { TypeName = "System.Collections.Generic.Comparer`1", MemberName = "get_Default", Reason = LinguisticOrder,
                AppliesTo = use => OrdersText(TypeArgument(use.DeclaringType, 0)) });
            foreach (string sorted in new[] { "System.Collections.Generic.SortedDictionary`2", "System.Collections.Generic.SortedSet`1", "System.Collections.Generic.SortedList`2" })
            {
                bans.Add(new MemberBan { TypeName = sorted, MemberName = ".ctor", Reason = LinguisticOrder,
                    AppliesTo = use => !TakesComparer(use) && OrdersText(TypeArgument(use.DeclaringType, 0)) });
            }

            foreach (string ordering in new[] { "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "Order", "OrderDescending", "Min", "Max", "MinBy", "MaxBy" })
            {
                bans.Add(new MemberBan { TypeName = "System.Linq.Enumerable", MemberName = ordering, Reason = LinguisticOrder,
                    AppliesTo = use => !TakesComparer(use) && OrdersText(SortKey(use)) });
            }

            foreach (string type in new[]
            {
                "System.Object", "System.ValueType", "System.String", "System.StringComparer",
                "System.Collections.Generic.EqualityComparer`1", "System.Collections.Generic.IEqualityComparer`1",
                "System.Runtime.CompilerServices.RuntimeHelpers",
            })
            {
                bans.Add(new MemberBan { TypeName = type, MemberName = "GetHashCode", Reason = RandomizedHash, AppliesTo = _ => true, ExemptInHashCodeOverrides = true });
            }

            bans.Add(new MemberBan { TypeName = "System.Environment", MemberName = "get_CurrentManagedThreadId", Reason = "thread identity must not influence results",
                AppliesTo = _ => true, ExemptInIteratorPlumbing = true });

            bans.Add(new MemberBan { TypeName = "System.Collections.Generic.Dictionary`2", MemberName = "GetEnumerator", Reason = UnorderedEnumeration, AppliesTo = _ => true });
            bans.Add(new MemberBan { TypeName = "System.Collections.Generic.Dictionary`2", MemberName = "get_Keys", Reason = UnorderedEnumeration, AppliesTo = _ => true });
            bans.Add(new MemberBan { TypeName = "System.Collections.Generic.Dictionary`2", MemberName = "get_Values", Reason = UnorderedEnumeration, AppliesTo = _ => true });
            bans.Add(new MemberBan { TypeName = "System.Collections.Generic.HashSet`1", MemberName = "GetEnumerator", Reason = UnorderedEnumeration, AppliesTo = _ => true });
            // Through an interface the dictionary behind it is unknown, and most are hash tables. Enumerating one as a
            // sequence (foreach, LINQ) is UnorderedSequences' part.
            foreach (string view in new[] { "System.Collections.Generic.IReadOnlyDictionary`2", "System.Collections.Generic.IDictionary`2" })
            {
                bans.Add(new MemberBan { TypeName = view, MemberName = "get_Keys", Reason = UnorderedEnumeration, AppliesTo = _ => true });
                bans.Add(new MemberBan { TypeName = view, MemberName = "get_Values", Reason = UnorderedEnumeration, AppliesTo = _ => true });
            }

            // List<T>.Sort, Array.Sort and the span and immutable sorts are introsorts: not stable, and the order of equal keys
            // depends on the runtime's implementation. Enumerable.OrderBy is a stable sort, so it orders equal keys the same everywhere.
            bans.Add(new MemberBan { TypeName = "System.Collections.Generic.List`1", MemberName = "Sort", Reason = UnstableSort, AppliesTo = _ => true });
            bans.Add(new MemberBan { TypeName = "System.Array", MemberName = "Sort", Reason = UnstableSort, AppliesTo = _ => true });
            bans.Add(new MemberBan { TypeName = "System.MemoryExtensions", MemberName = "Sort", Reason = UnstableSort, AppliesTo = _ => true });
            foreach (string immutable in new[]
            {
                "System.Collections.Immutable.ImmutableArray`1", "System.Collections.Immutable.ImmutableArray`1+Builder",
                "System.Collections.Immutable.ImmutableList`1", "System.Collections.Immutable.ImmutableList`1+Builder",
            })
            {
                bans.Add(new MemberBan { TypeName = immutable, MemberName = "Sort", Reason = UnstableSort, AppliesTo = _ => true });
            }

            return bans;
        }

        private static Func<MemberUse, bool> WithParameters(Func<IReadOnlyList<string>, bool> test) =>
            use => test(use.Parameters.Select(parameter => parameter.Display).ToList());

        private static bool IsObjectOrGeneric(string parameter) =>
            parameter == "System.Object" || parameter == "System.Object[]" || parameter.StartsWith('!')
            || parameter.StartsWith("System.Collections.Generic.IEnumerable`1<!", StringComparison.Ordinal)
            || parameter.StartsWith("System.ReadOnlySpan`1<System.Object>", StringComparison.Ordinal);

        /// <summary>A generic Concat or Join over strings only, such as string.Join&lt;string&gt;(", ", names).</summary>
        private static bool JoinsOnlyText(MemberUse use) =>
            use.MethodTypeArguments.Count > 0 && use.MethodTypeArguments.All(argument => argument.DefinitionName == "System.String");

        private static bool TakesComparer(MemberUse use) =>
            use.Parameters.Any(parameter => parameter.DefinitionName == "System.Collections.Generic.IComparer`1");

        private static TypeShape? TypeArgument(TypeShape type, int index) => index < type.TypeArguments.Length ? type.TypeArguments[index] : null;

        /// <summary>
        /// The key an ordering method compares: what its selector (the Func`2 parameter) returns, or else the element type
        /// (Order&lt;T&gt;, Min&lt;T&gt;); null for a non-generic overload such as Min(IEnumerable&lt;int&gt;).
        /// </summary>
        private static TypeShape? SortKey(MemberUse use)
        {
            TypeShape? selector = use.Parameters.FirstOrDefault(parameter => parameter.DefinitionName == "System.Func`2");
            if (selector is not null)
            {
                return TypeArgument(selector, 1) is TypeShape result ? use.Resolve(result) : null;
            }

            return use.MethodTypeArguments.Count > 0 ? use.MethodTypeArguments[0] : null;
        }

        /// <summary>
        /// Whether the default comparer of a key compares text by culture: string, object (which compares strings so), and
        /// tuples or pairs that hold one. A generic parameter the scan cannot see through counts as no text.
        /// </summary>
        private static bool OrdersText(TypeShape? key) =>
            key is not null
            && (key.DefinitionName is "System.String" or "System.Object"
                || (IsTupleLike(key.DefinitionName) && key.TypeArguments.Any(argument => OrdersText(argument))));

        private static bool IsTupleLike(string definitionName) =>
            definitionName.StartsWith("System.ValueTuple`", StringComparison.Ordinal) || definitionName.StartsWith("System.Tuple`", StringComparison.Ordinal)
            || definitionName == "System.Collections.Generic.KeyValuePair`2";

        private abstract record TypeRule(string TypeName);

        private sealed record AllMembers(string TypeName) : TypeRule(TypeName);

        /// <summary>An allowed type of which only the named members may be used; the names come as one list, apart from the type.</summary>
        private sealed record OnlyMembers : TypeRule
        {
            public OnlyMembers(string typeName, IEnumerable<string> members)
                : base(typeName) => Members = new HashSet<string>(members, StringComparer.Ordinal);

            public IReadOnlySet<string> Members { get; }
        }

        /// <summary>A banned member of an allowed type: the uses it applies to, why, and the callers it exempts. Every part is set by name.</summary>
        private sealed record MemberBan
        {
            public required string TypeName { get; init; }

            public required string MemberName { get; init; }

            public required Func<MemberUse, bool> AppliesTo { get; init; }

            public required string Reason { get; init; }

            /// <summary>Whether a GetHashCode override may still call it: a hash code it combines stays a hash code.</summary>
            public bool ExemptInHashCodeOverrides { get; init; }

            /// <summary>Whether the constructor and GetEnumerator of a compiler-written iterator may still call it.</summary>
            public bool ExemptInIteratorPlumbing { get; init; }
        }
    }
}
