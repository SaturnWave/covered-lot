using System;
using System.Collections.Generic;
using System.Linq;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The engine's Unity-bound API policy over the .NET build of the Unity-free Core, ported from the engine's
    /// DeterminismGuardTests. The same actions must give the same results on every device and on .NET, Mono and IL2CPP,
    /// so Core may hold no floating point and may use only the platform types and members the policy allows: no
    /// randomness, clock, time zone, environment, thread identity, native call or native-sized integer, no System.Type
    /// member beyond what typeof comparisons and records compile to (Object.GetType stays allowed), and none of the
    /// members it bans for culture-dependent text or text order, the platform newline, randomized hashing, enumeration in
    /// hash order or unstable sorts. The bans do not see every way to that behaviour: the policy's remarks list the known
    /// gaps, and HygieneTests checks Core's sources for one of them, a culture-sensitive StringComparison. Unity does not
    /// run the .NET analyzers, so for code that only Unity builds an IL scan like this is the only guard; this class
    /// scans the .NET build of Core alone. The self-tests prove that the scanners report each planted violation at its
    /// member, among them a planted use of every member ban (but two that match no member, which a test of their own
    /// shows), and stay silent on the clean samples, whose declared types the allowlist all allows; and that the member
    /// bans alone (<see cref="MemberBansPolicy"/>) report the planted bans but refuse no type.
    /// </summary>
    public sealed class ApiPolicyTests
    {
        private const string Samples = "CoveredLot.Shared.Samples.";
        private const string CoreAssembly = "CoveredLot.Core";
        private const string CoreException = "CoveredLot.Core.UnknownEngineCaseException";
        private const string Constructor = ".ctor";

        private const string Culture = "depends on the device culture";
        private const string TextOrder = "orders text by the device culture";
        private const string RandomizedHash = "hash codes are randomized per process";
        private const string HashOrder = "enumeration order is an implementation detail";
        private const string UnstableSort = "unstable sort";
        private const string ThreadIdentity = "thread identity must not influence results";
        private const string Newline = "Environment.NewLine differs";

        /// <summary>
        /// Code that keeps every rule these scans check: realistic integer-only code, records whose compiler-written members
        /// must pass (one of them only through the record exemption), a GetHashCode override that passes only through the
        /// hash-code exemption, and the hygiene tests' clean sample of a specific catch, an iterator, a using and a cached
        /// lambda.
        /// </summary>
        private static readonly string[] CleanSamples =
        {
            Samples + "CleanIntegerOnlySample", Samples + "CleanCell", Samples + "CleanSample",
            Samples + "CleanNamedCell", Samples + "CleanHashedName",
        };

        /// <summary>
        /// The bans that no code can break, as Type::Member: the engine bans these getters of System.Collections.Comparer,
        /// but Default and DefaultInvariant are static fields (see the comparer test below).
        /// </summary>
        private static readonly string[] UnmatchableBans =
        {
            "System.Collections.Comparer::get_Default", "System.Collections.Comparer::get_DefaultInvariant",
        };

        /// <summary>The scans of this test assembly, made once for the whole run: the assembly cannot change while its own tests run.</summary>
        private static readonly Lazy<SampleScans> Scans = new(ScanSamples);

        /// <summary>
        /// The planted bans of dotnet/Shared/Samples/PolicyEdgeSamples.cs, for both policies: a set whose type the walk learns
        /// from one kind of instruction each, each hash-ordered type and sequence parameter type, the receiver and
        /// LINQ-over-pairs rules, and hand-written members that carry the names of the members the compiler writes.
        /// </summary>
        public static TheoryData<string, string> PlantedEdgeBans => new()
        {
            { "PolicyEdgeSamples::SetFromLocal", HashOrder },
            { "PolicyEdgeSamples::SetFromField", HashOrder },
            { "PolicyEdgeSamples::SetFromNewobj", HashOrder },
            { "PolicyEdgeSamples::SetFromDup", HashOrder },
            { "PolicyEdgeSamples::SetFromCast", HashOrder },
            { "PolicyEdgeSamples::SetAcrossBranch", HashOrder },
            { "PolicyEdgeSamples::DictionaryIntoList", HashOrder },
            { "PolicyEdgeSamples::KeyCollectionToList", HashOrder },
            { "PolicyEdgeSamples::ValueCollectionToList", HashOrder },
            { "PolicyEdgeSamples::HashSetToList", HashOrder },
            { "PolicyEdgeSamples::SetInterfaceToList", HashOrder },
            { "PolicyEdgeSamples::ReadOnlySetToList", HashOrder },
            { "PolicyEdgeSamples::ReadOnlyDictionaryIntoList", HashOrder },
            { "PolicyEdgeSamples::DictionaryInterfaceIntoList", HashOrder },
            { "PolicyEdgeSamples::ConcurrentDictionaryIntoList", HashOrder },
            { "PolicyEdgeSamples::ConcurrentBagToList", HashOrder },
            { "PolicyEdgeSamples::ImmutableHashSetToList", HashOrder },
            { "PolicyEdgeSamples::ImmutableDictionaryIntoList", HashOrder },
            { "PolicyEdgeSamples::ImmutableSetInterfaceToList", HashOrder },
            { "PolicyEdgeSamples::ImmutableDictionaryInterfaceIntoList", HashOrder },
            { "PolicyEdgeSamples::SetAsNonGenericSequence", HashOrder },
            { "PolicyEdgeSamples::SetAsCollection", HashOrder },
            { "PolicyEdgeSamples::SetAsReadOnlyCollection", HashOrder },
            { "PolicyEdgeSamples::SumOfSetInterface", HashOrder },
            { "PolicyEdgeSamples::ValuesOfPairs", HashOrder },
            { "HandWrittenMembers::.ctor", ThreadIdentity },
            { "HandWrittenMembers::ToString", Culture },
            { "HandWrittenMembers::GetHashCode", Culture },
        };

        /// <summary>
        /// One planted use of each member ban but the two unmatchable ones (dotnet/Shared/Samples/MemberBanSamples.cs): the
        /// sample member, the banned member it uses, as Type::Member without generic arguments, and a fragment of the ban's
        /// reason.
        /// </summary>
        public static TheoryData<string, string, string> PlantedBans => new()
        {
            { "MemberBanSamples::ByteToString", "System.Byte::ToString", Culture },
            { "MemberBanSamples::ByteParse", "System.Byte::Parse", Culture },
            { "MemberBanSamples::ByteTryParse", "System.Byte::TryParse", Culture },
            { "MemberBanSamples::SByteToString", "System.SByte::ToString", Culture },
            { "MemberBanSamples::SByteParse", "System.SByte::Parse", Culture },
            { "MemberBanSamples::SByteTryParse", "System.SByte::TryParse", Culture },
            { "MemberBanSamples::Int16ToString", "System.Int16::ToString", Culture },
            { "MemberBanSamples::Int16Parse", "System.Int16::Parse", Culture },
            { "MemberBanSamples::Int16TryParse", "System.Int16::TryParse", Culture },
            { "MemberBanSamples::UInt16ToString", "System.UInt16::ToString", Culture },
            { "MemberBanSamples::UInt16Parse", "System.UInt16::Parse", Culture },
            { "MemberBanSamples::UInt16TryParse", "System.UInt16::TryParse", Culture },
            { "MemberBanSamples::Int32ToString", "System.Int32::ToString", Culture },
            { "MemberBanSamples::Int32Parse", "System.Int32::Parse", Culture },
            { "MemberBanSamples::Int32TryParse", "System.Int32::TryParse", Culture },
            { "MemberBanSamples::UInt32ToString", "System.UInt32::ToString", Culture },
            { "MemberBanSamples::UInt32Parse", "System.UInt32::Parse", Culture },
            { "MemberBanSamples::UInt32TryParse", "System.UInt32::TryParse", Culture },
            { "MemberBanSamples::Int64ToString", "System.Int64::ToString", Culture },
            { "MemberBanSamples::Int64Parse", "System.Int64::Parse", Culture },
            { "MemberBanSamples::Int64TryParse", "System.Int64::TryParse", Culture },
            { "MemberBanSamples::UInt64ToString", "System.UInt64::ToString", Culture },
            { "MemberBanSamples::UInt64Parse", "System.UInt64::Parse", Culture },
            { "MemberBanSamples::UInt64TryParse", "System.UInt64::TryParse", Culture },
            { "MemberBanSamples::StringToUpper", "System.String::ToUpper", Culture },
            { "MemberBanSamples::StringToLower", "System.String::ToLower", Culture },
            { "MemberBanSamples::StringCompare", "System.String::Compare", Culture },
            { "MemberBanSamples::StringCompareTo", "System.String::CompareTo", Culture },
            { "MemberBanSamples::StringStartsWith", "System.String::StartsWith", Culture },
            { "MemberBanSamples::StringEndsWith", "System.String::EndsWith", Culture },
            { "MemberBanSamples::StringIndexOf", "System.String::IndexOf", Culture },
            { "MemberBanSamples::StringLastIndexOf", "System.String::LastIndexOf", Culture },
            { "MemberBanSamples::StringFormat", "System.String::Format", Culture },
            { "MemberBanSamples::StringConcat", "System.String::Concat", Culture },
            { "MemberBanSamples::StringJoin", "System.String::Join", Culture },
            { "MemberBanSamples::CharToUpper", "System.Char::ToUpper", Culture },
            { "MemberBanSamples::CharToLower", "System.Char::ToLower", Culture },
            { "MemberBanSamples::StringBuilderAppend", "System.Text.StringBuilder::Append", Culture },
            { "MemberBanSamples::StringBuilderInsert", "System.Text.StringBuilder::Insert", Culture },
            { "MemberBanSamples::StringBuilderAppendFormat", "System.Text.StringBuilder::AppendFormat", Culture },
            { "MemberBanSamples::StringBuilderAppendLine", "System.Text.StringBuilder::AppendLine", Newline },
            { "MemberBanSamples::InterpolatedString", "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor", Culture },
            { "MemberBanSamples::InterpolatedAppend", "System.Text.StringBuilder+AppendInterpolatedStringHandler::.ctor", Culture },
            { "MemberBanSamples::CurrentCulture", "System.Globalization.CultureInfo::get_CurrentCulture", Culture },
            { "MemberBanSamples::CurrentUICulture", "System.Globalization.CultureInfo::get_CurrentUICulture", Culture },
            { "MemberBanSamples::CurrentCultureComparer", "System.StringComparer::get_CurrentCulture", TextOrder },
            { "MemberBanSamples::CurrentCultureIgnoreCaseComparer", "System.StringComparer::get_CurrentCultureIgnoreCase", TextOrder },
            { "MemberBanSamples::InvariantCultureComparer", "System.StringComparer::get_InvariantCulture", TextOrder },
            { "MemberBanSamples::InvariantCultureIgnoreCaseComparer", "System.StringComparer::get_InvariantCultureIgnoreCase", TextOrder },
            { "MemberBanSamples::CreatedComparer", "System.StringComparer::Create", TextOrder },
            { "MemberBanSamples::ComparerFromComparison", "System.StringComparer::FromComparison", TextOrder },
            { "MemberBanSamples::DefaultTextComparer", "System.Collections.Generic.Comparer`1::get_Default", TextOrder },
            { "MemberBanSamples::SortedDictionaryOfText", "System.Collections.Generic.SortedDictionary`2::.ctor", TextOrder },
            { "MemberBanSamples::SortedSetOfText", "System.Collections.Generic.SortedSet`1::.ctor", TextOrder },
            { "MemberBanSamples::SortedListOfText", "System.Collections.Generic.SortedList`2::.ctor", TextOrder },
            { "MemberBanSamples::OrderByText", "System.Linq.Enumerable::OrderBy", TextOrder },
            { "MemberBanSamples::OrderByDescendingText", "System.Linq.Enumerable::OrderByDescending", TextOrder },
            { "MemberBanSamples::ThenByText", "System.Linq.Enumerable::ThenBy", TextOrder },
            { "MemberBanSamples::ThenByDescendingText", "System.Linq.Enumerable::ThenByDescending", TextOrder },
            { "MemberBanSamples::OrderText", "System.Linq.Enumerable::Order", TextOrder },
            { "MemberBanSamples::OrderDescendingText", "System.Linq.Enumerable::OrderDescending", TextOrder },
            { "MemberBanSamples::MinText", "System.Linq.Enumerable::Min", TextOrder },
            { "MemberBanSamples::MaxText", "System.Linq.Enumerable::Max", TextOrder },
            { "MemberBanSamples::MinByText", "System.Linq.Enumerable::MinBy", TextOrder },
            { "MemberBanSamples::MaxByText", "System.Linq.Enumerable::MaxBy", TextOrder },
            { "MemberBanSamples::ObjectGetHashCode", "System.Object::GetHashCode", RandomizedHash },
            { "ValueTypeHashSample::BaseHash", "System.ValueType::GetHashCode", RandomizedHash },
            { "MemberBanSamples::StringGetHashCode", "System.String::GetHashCode", RandomizedHash },
            { "MemberBanSamples::StringComparerGetHashCode", "System.StringComparer::GetHashCode", RandomizedHash },
            { "MemberBanSamples::EqualityComparerGetHashCode", "System.Collections.Generic.EqualityComparer`1::GetHashCode", RandomizedHash },
            { "MemberBanSamples::EqualityComparerInterfaceGetHashCode", "System.Collections.Generic.IEqualityComparer`1::GetHashCode", RandomizedHash },
            { "MemberBanSamples::RuntimeHelpersGetHashCode", "System.Runtime.CompilerServices.RuntimeHelpers::GetHashCode", RandomizedHash },
            { "MemberBanSamples::CurrentManagedThreadId", "System.Environment::get_CurrentManagedThreadId", ThreadIdentity },
            { "MemberBanSamples::DictionaryGetEnumerator", "System.Collections.Generic.Dictionary`2::GetEnumerator", HashOrder },
            { "MemberBanSamples::DictionaryKeys", "System.Collections.Generic.Dictionary`2::get_Keys", HashOrder },
            { "MemberBanSamples::DictionaryValues", "System.Collections.Generic.Dictionary`2::get_Values", HashOrder },
            { "MemberBanSamples::HashSetGetEnumerator", "System.Collections.Generic.HashSet`1::GetEnumerator", HashOrder },
            { "MemberBanSamples::ReadOnlyDictionaryKeys", "System.Collections.Generic.IReadOnlyDictionary`2::get_Keys", HashOrder },
            { "MemberBanSamples::ReadOnlyDictionaryValues", "System.Collections.Generic.IReadOnlyDictionary`2::get_Values", HashOrder },
            { "MemberBanSamples::DictionaryInterfaceKeys", "System.Collections.Generic.IDictionary`2::get_Keys", HashOrder },
            { "MemberBanSamples::DictionaryInterfaceValues", "System.Collections.Generic.IDictionary`2::get_Values", HashOrder },
            { "MemberBanSamples::ListSort", "System.Collections.Generic.List`1::Sort", UnstableSort },
            { "MemberBanSamples::ArraySort", "System.Array::Sort", UnstableSort },
            { "MemberBanSamples::SpanSort", "System.MemoryExtensions::Sort", UnstableSort },
            { "MemberBanSamples::ImmutableArraySort", "System.Collections.Immutable.ImmutableArray`1::Sort", UnstableSort },
            { "MemberBanSamples::ImmutableArrayBuilderSort", "System.Collections.Immutable.ImmutableArray`1+Builder::Sort", UnstableSort },
            { "MemberBanSamples::ImmutableListSort", "System.Collections.Immutable.ImmutableList`1::Sort", UnstableSort },
            { "MemberBanSamples::ImmutableListBuilderSort", "System.Collections.Immutable.ImmutableList`1+Builder::Sort", UnstableSort },
        };

        [Fact]
        public void Core_contains_no_floating_point_types_or_instructions()
        {
            using var core = CoreScanner();

            IReadOnlyList<Violation> violations = core.FloatingPointUses();

            // The floating-point scan reads the bodies of every method of every type, as the member reference list does:
            // the walk had the exception's constructor and the calls in it to read.
            Assert.Contains(core.MemberReferences(), site => site.CallerType == CoreException && site.CallerMethod == Constructor
                && site.DeclaringType == "System.String" && site.MemberName == "Concat");
            Assert.True(violations.Count == 0, Report(CoreAssembly + " holds floating point", violations));
        }

        [Fact]
        public void Core_uses_only_allowlisted_platform_apis()
        {
            using var core = CoreScanner();
            var policy = new RecordingPolicy(UnityBoundApiPolicy.Default);

            IReadOnlyList<Violation> violations = core.ApiPolicyViolations(policy);

            // The walk judged the calls in the exception's constructor: the base constructor with the exception itself as
            // the receiver it tracked on the stack, and the concatenation that builds the message.
            Assert.Contains(policy.Judged, judged => judged.Caller.MethodName == Constructor
                && judged.Use.Display == "System.InvalidOperationException::.ctor(System.String)"
                && judged.Use.Instance?.Display == CoreException);
            Assert.Contains(policy.Judged, judged => judged.Caller.MethodName == Constructor
                && judged.Use.Display == "System.String::Concat(System.String, System.String, System.String)");
            // The type references the policy checks hold the base type Core names.
            Assert.True(core.References("System.InvalidOperationException"));
            Assert.True(violations.Count == 0, Report(CoreAssembly + " uses what the Unity-bound API policy refuses", violations));
        }

        [Theory]
        [InlineData("DeterminismViolationsSample::DoubleField", "floating-point field")]
        [InlineData("DeterminismViolationsSample::HalveWithFloatLocal", "floating-point local variable")]
        [InlineData("DeterminismViolationsSample::HalveWithFloatLocal", "floating-point instruction conv.r4")]
        [InlineData("DeterminismViolationsSample::Price", "floating-point parameter or return type")]
        [InlineData("DeterminismViolationsSample::FloorOfDoubleLiteral", "floating-point instruction ldc.r8")]
        [InlineData("DeterminismViolationsSample::FloorOfDoubleLiteral", "uses floating point via System.Math::Floor(System.Double)")]
        [InlineData("DeterminismViolationsSample::NormalizedVector", "uses floating point via System.Numerics.Vector2::Normalize(System.Numerics.Vector2)")]
        [InlineData("PolicyEdgeSamples::EmptyPrices", "floating-point parameter or return type")]
        public void The_floating_point_scanner_reports_each_planted_use_at_its_member(string member, string reason)
        {
            Assert.Contains(Scans.Value.FloatingPoint, violation => violation.Location == Samples + member && violation.Reason == reason);
        }

        [Theory]
        [InlineData("DeterminismViolationsSample::NextFromSystemRandom", "uses System.Random")]
        [InlineData("DeterminismViolationsSample::ReadTheClock", "uses System.DateTime")]
        [InlineData("DeterminismViolationsSample::LocalTimeConversion", "uses System.DateTime")]
        [InlineData("DeterminismViolationsSample::NormalizedVector", "uses System.Numerics.Vector2")]
        [InlineData("DeterminismViolationsSample::PlainStringHash", RandomizedHash)]
        [InlineData("DeterminismViolationsSample::OrdinalComparerHash", RandomizedHash)]
        [InlineData("DeterminismViolationsSample::TupleHash", RandomizedHash)]
        [InlineData("DeterminismViolationsSample::IntegerToTextByConcatenation", Culture)]
        [InlineData("DeterminismViolationsSample::FormatWithoutCulture", Culture)]
        [InlineData("DeterminismViolationsSample::AppendInteger", Culture)]
        [InlineData("DeterminismViolationsSample::AppendPlatformNewline", Newline)]
        [InlineData("DeterminismViolationsSample::UpperWithDeviceCulture", Culture)]
        [InlineData("DeterminismViolationsSample::CompareWithDeviceCulture", Culture)]
        [InlineData("DeterminismViolationsSample::ChangeTypeThroughDouble", "uses System.Convert")]
        [InlineData("DeterminismViolationsSample::TypeByName", "may be used on System.Type")]
        [InlineData("DeterminismViolationsSample::ProcessorCount", "may be used on System.Environment")]
        [InlineData("DeterminismViolationsSample::ThreadIdentity", ThreadIdentity)]
        [InlineData("DeterminismViolationsSample::NativeSizedSum", "native-sized integer")]
        [InlineData("DeterminismViolationsSample::DictionaryOrder", HashOrder)]
        [InlineData("DeterminismViolationsSample::NativeTime", "native call (P/Invoke) is not allowed")]
        [InlineData("DeterminismViolationsSample::SortWithListSort", UnstableSort)]
        [InlineData("DeterminismViolationsSample::SortWithArraySort", UnstableSort)]
        [InlineData("DeterminismViolationsSample::SortWithSpanSort", UnstableSort)]
        [InlineData("DeterminismViolationsSample::OrderTextWithoutComparer", TextOrder)]
        [InlineData("DeterminismViolationsSample::OrderTextByCurrentCulture", TextOrder)]
        [InlineData("DeterminismViolationsSample::OrderByTupleHoldingText", TextOrder)]
        [InlineData("DeterminismViolationsSample::SortedDictionaryWithoutComparer", TextOrder)]
        [InlineData("DeterminismViolationsSample::SortedSetWithoutComparer", TextOrder)]
        [InlineData("DeterminismViolationsSample::CompareWithDefaultComparer", TextOrder)]
        [InlineData("DeterminismViolationsSample::ReadOnlyDictionaryOrder", HashOrder)]
        [InlineData("DeterminismViolationsSample::ReadOnlyDictionaryKeys", HashOrder)]
        [InlineData("DeterminismViolationsSample::DictionaryThroughLinq", HashOrder)]
        [InlineData("DeterminismViolationsSample::HashSetToList", HashOrder)]
        [InlineData("DeterminismViolationsSample::HashSetJoined", HashOrder)]
        [InlineData("DeterminismViolationsSample::HashSetThroughLambda", HashOrder)]
        [InlineData("DeterminismViolationsSample::PairsOfUnknownOrder", HashOrder)]
        [InlineData("DeterminismViolationsSample::InterpolatedWithDeviceCulture", Culture)]
        [InlineData("PolicyEdgeSamples::TextInSwedish", "may be used on System.Globalization.CultureInfo")]
        [InlineData("PolicyEdgeSamples::DefaultEncodingBytes", "may be used on System.Text.Encoding")]
        [InlineData("PolicyEdgeSamples::HashInOneCall", "may be used on System.Security.Cryptography.SHA256")]
        [InlineData("PolicyEdgeSamples::LastHash", "may be used on System.Security.Cryptography.HashAlgorithm")]
        [InlineData("PolicyEdgeSamples::HoldsReferences", "may be used on System.Runtime.CompilerServices.RuntimeHelpers")]
        [InlineData("PolicyEdgeSamples::CountOfTimes", "uses System.DateTime")]
        [MemberData(nameof(PlantedEdgeBans))]
        public void The_api_scanner_reports_each_planted_violation_at_its_method(string member, string reasonFragment)
        {
            AssertReported(Scans.Value.UnityBound, new PlantedViolation { Member = Samples + member, ReasonFragment = reasonFragment });
        }

        /// <summary>The planted member bans, and the native-sized integer and native call that the scan refuses under every policy.</summary>
        [Theory]
        [InlineData("DeterminismViolationsSample::PlainStringHash", RandomizedHash)]
        [InlineData("DeterminismViolationsSample::OrdinalComparerHash", RandomizedHash)]
        [InlineData("DeterminismViolationsSample::TupleHash", RandomizedHash)]
        [InlineData("DeterminismViolationsSample::IntegerToTextByConcatenation", Culture)]
        [InlineData("DeterminismViolationsSample::FormatWithoutCulture", Culture)]
        [InlineData("DeterminismViolationsSample::AppendInteger", Culture)]
        [InlineData("DeterminismViolationsSample::AppendPlatformNewline", Newline)]
        [InlineData("DeterminismViolationsSample::UpperWithDeviceCulture", Culture)]
        [InlineData("DeterminismViolationsSample::CompareWithDeviceCulture", Culture)]
        [InlineData("DeterminismViolationsSample::ThreadIdentity", ThreadIdentity)]
        [InlineData("DeterminismViolationsSample::NativeSizedSum", "native-sized integer")]
        [InlineData("DeterminismViolationsSample::DictionaryOrder", HashOrder)]
        [InlineData("DeterminismViolationsSample::NativeTime", "native call (P/Invoke) is not allowed")]
        [InlineData("DeterminismViolationsSample::SortWithListSort", UnstableSort)]
        [InlineData("DeterminismViolationsSample::SortWithArraySort", UnstableSort)]
        [InlineData("DeterminismViolationsSample::SortWithSpanSort", UnstableSort)]
        [InlineData("DeterminismViolationsSample::OrderTextWithoutComparer", TextOrder)]
        [InlineData("DeterminismViolationsSample::OrderTextByCurrentCulture", TextOrder)]
        [InlineData("DeterminismViolationsSample::OrderByTupleHoldingText", TextOrder)]
        [InlineData("DeterminismViolationsSample::SortedDictionaryWithoutComparer", TextOrder)]
        [InlineData("DeterminismViolationsSample::SortedSetWithoutComparer", TextOrder)]
        [InlineData("DeterminismViolationsSample::CompareWithDefaultComparer", TextOrder)]
        [InlineData("DeterminismViolationsSample::ReadOnlyDictionaryOrder", HashOrder)]
        [InlineData("DeterminismViolationsSample::ReadOnlyDictionaryKeys", HashOrder)]
        [InlineData("DeterminismViolationsSample::DictionaryThroughLinq", HashOrder)]
        [InlineData("DeterminismViolationsSample::HashSetToList", HashOrder)]
        [InlineData("DeterminismViolationsSample::HashSetJoined", HashOrder)]
        [InlineData("DeterminismViolationsSample::HashSetThroughLambda", HashOrder)]
        [InlineData("DeterminismViolationsSample::PairsOfUnknownOrder", HashOrder)]
        [InlineData("DeterminismViolationsSample::InterpolatedWithDeviceCulture", Culture)]
        [MemberData(nameof(PlantedEdgeBans))]
        public void The_member_bans_alone_report_each_planted_ban_at_its_method(string member, string reasonFragment)
        {
            AssertReported(Scans.Value.MemberBans, new PlantedViolation { Member = Samples + member, ReasonFragment = reasonFragment });
        }

        [Theory]
        [MemberData(nameof(PlantedBans))]
        public void Each_member_ban_reports_its_planted_use_under_both_policies(string member, string bannedMember, string reasonFragment)
        {
            string at = Samples + member;
            bool Reports(IReadOnlyList<Violation> violations) => violations.Any(violation => IsAt(violation, at)
                && BannedMemberOf(violation) == bannedMember && violation.Reason.Contains(reasonFragment, StringComparison.Ordinal));

            Assert.True(Reports(Scans.Value.UnityBound),
                "The Unity-bound policy reports no " + bannedMember + " ban ('" + reasonFragment + "') at " + at + ". " + FoundAt(at, Scans.Value.UnityBound));
            Assert.True(Reports(Scans.Value.MemberBans),
                "The member bans alone report no " + bannedMember + " ban ('" + reasonFragment + "') at " + at + ". " + FoundAt(at, Scans.Value.MemberBans));
        }

        [Fact]
        public void Every_member_ban_has_a_planted_use()
        {
            IReadOnlyList<string> banned = UnityBoundApiPolicy.Default.BannedMembers;
            var planted = new HashSet<string>(PlantedBans.Select(row => row[1]).OfType<string>(), StringComparer.Ordinal);

            IReadOnlyList<string> unplanted = banned
                .Where(member => !planted.Contains(member) && !UnmatchableBans.Contains(member, StringComparer.Ordinal))
                .ToList();

            Assert.NotEmpty(banned);
            // The unmatchable bans are still the policy's: once they name members that exist, they need planted uses too.
            Assert.All(UnmatchableBans, member => Assert.Contains(member, banned));
            Assert.True(unplanted.Count == 0, "No sample in MemberBanSamples plants a use of " + string.Join(", ", unplanted) + ".");
        }

        /// <summary>
        /// The engine bans the getters get_Default and get_DefaultInvariant of System.Collections.Comparer, whose defaults
        /// order text by the device culture or the platform's collation tables, but Default and DefaultInvariant are static
        /// fields: a read of either names the field, and no ban matches it. The allowlist refuses the type all the same;
        /// the member bans alone let both reads through.
        /// </summary>
        [Theory]
        [InlineData("MemberBanSamples::NonGenericDefaultComparer", "Default")]
        [InlineData("MemberBanSamples::NonGenericDefaultInvariantComparer", "DefaultInvariant")]
        public void A_read_of_a_comparer_default_breaks_no_ban_as_it_reads_a_field(string member, string field)
        {
            string at = Samples + member;
            SampleScans scans = Scans.Value;

            Assert.Contains(scans.MemberReferences, site => site.CallerType + "::" + site.CallerMethod == at
                && site.DeclaringType == "System.Collections.Comparer" && site.MemberName == field);
            Assert.Contains(scans.UnityBound, violation => IsAt(violation, at)
                && violation.Reason == "uses System.Collections.Comparer, which " + UnityBoundApiPolicy.Default.RefusedTypeReason);
            Assert.DoesNotContain(scans.MemberBans, violation => IsAt(violation, at));
        }

        /// <summary>Methods whose only fault is a type, or a member of an allowed type, outside the Unity-bound allowlist.</summary>
        [Theory]
        [InlineData("DeterminismViolationsSample::NextFromSystemRandom")]
        [InlineData("DeterminismViolationsSample::ReadTheClock")]
        [InlineData("DeterminismViolationsSample::LocalTimeConversion")]
        [InlineData("DeterminismViolationsSample::NormalizedVector")]
        [InlineData("DeterminismViolationsSample::ChangeTypeThroughDouble")]
        [InlineData("DeterminismViolationsSample::TypeByName")]
        [InlineData("DeterminismViolationsSample::ProcessorCount")]
        [InlineData("PolicyEdgeSamples::TextInSwedish")]
        [InlineData("PolicyEdgeSamples::DefaultEncodingBytes")]
        [InlineData("PolicyEdgeSamples::HashInOneCall")]
        [InlineData("PolicyEdgeSamples::LastHash")]
        [InlineData("PolicyEdgeSamples::HoldsReferences")]
        [InlineData("PolicyEdgeSamples::CountOfTimes")]
        public void The_member_bans_alone_pass_a_method_that_the_allowlist_refuses(string member)
        {
            string at = Samples + member;

            IReadOnlyList<Violation> refused = Scans.Value.UnityBound.Where(violation => IsAt(violation, at)).ToList();
            IReadOnlyList<Violation> banned = Scans.Value.MemberBans.Where(violation => IsAt(violation, at)).ToList();

            Assert.True(refused.Count > 0, "The Unity-bound policy reports nothing at " + at + ".");
            Assert.True(refused.All(violation => IsRefusal(violation, UnityBoundApiPolicy.Default)),
                Report("The Unity-bound policy reports more than an allowlist refusal at " + at, refused));
            Assert.True(banned.Count == 0, Report("The member bans alone report " + at + ", which only the allowlist refuses", banned));
        }

        [Fact]
        public void The_member_bans_alone_refuse_no_type_and_no_member_of_an_allowed_type()
        {
            IReadOnlyList<Violation> banned = Scans.Value.MemberBans;
            IReadOnlyList<Violation> refusals = banned.Where(violation => IsRefusal(violation, MemberBansPolicy.Default)).ToList();

            // The same assembly holds types and members that the allowlist refuses, and planted bans to report.
            Assert.Contains(Scans.Value.UnityBound, violation => IsRefusal(violation, UnityBoundApiPolicy.Default));
            Assert.NotEmpty(banned);
            Assert.True(refusals.Count == 0, Report("The member bans alone refuse these", refusals));
        }

        [Fact]
        public void The_api_scanner_refuses_a_type_that_only_a_signature_names()
        {
            const string Member = Samples + "PolicyEdgeSamples::KeepTime";
            const string TimeOnly = "System.TimeOnly";
            SampleScans scans = Scans.Value;

            // KeepTime's body names no type, so the walk reports nothing at it; its signature names TimeOnly, which only the
            // type reference table then holds, unlocated.
            Assert.DoesNotContain(scans.UnityBound, violation => IsAt(violation, Member));
            Assert.Contains(scans.DeclaredTypes, use => use.DeclaringType == Samples + "PolicyEdgeSamples" && use.ExternalType == TimeOnly);
            Assert.Contains(scans.UnityBound, violation => violation.Location == "type reference " + TimeOnly
                && violation.Reason == "type " + UnityBoundApiPolicy.Default.RefusedTypeReason);
        }

        [Fact]
        public void The_scanners_stay_silent_on_clean_integer_only_code()
        {
            SampleScans scans = Scans.Value;

            IReadOnlyList<Violation> violations = scans.FloatingPoint
                .Concat(scans.UnityBound)
                .Concat(scans.MemberBans)
                .Where(violation => CleanSamples.Any(type => IsIn(violation, type)))
                .ToList();
            IReadOnlyList<string> unread = CleanSamples.Where(type => !scans.MemberReferences.Any(site => IsIn(site, type))).ToList();
            IReadOnlyList<string> declared = scans.DeclaredTypes
                .Where(use => CleanSamples.Any(type => IsIn(use, type)))
                .Select(use => use.ExternalType)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            IReadOnlyList<string> refused = declared.Where(type => !UnityBoundApiPolicy.Default.IsTypeAllowed(type)).ToList();

            // The scans read every method body, as the member reference list does, so each clean sample's code was read.
            Assert.True(unread.Count == 0, "no member use was read in " + string.Join(", ", unread));
            // A refused type that only a declaration names is reported once, unlocated, so the filter above cannot see it:
            // the clean samples' declared types are checked against the allowlist here.
            Assert.Contains("System.Collections.Generic.IReadOnlyList`1", declared);
            Assert.True(refused.Count == 0, "The allowlist refuses types the clean samples declare: " + string.Join(", ", refused));
            Assert.True(violations.Count == 0, Report("The scans report the clean samples", violations));
        }

        /// <summary>The .NET build of Core, as the test project references it.</summary>
        private static AssemblyScanner CoreScanner() => new(typeof(UnknownEngineCaseException).Assembly.Location);

        /// <summary>Scans this test assembly, which compiles the planted samples of dotnet/Shared/Samples.</summary>
        private static SampleScans ScanSamples()
        {
            using var samples = new AssemblyScanner(typeof(ApiPolicyTests).Assembly.Location);
            return new SampleScans
            {
                FloatingPoint = samples.FloatingPointUses(),
                UnityBound = samples.ApiPolicyViolations(UnityBoundApiPolicy.Default),
                MemberBans = samples.ApiPolicyViolations(MemberBansPolicy.Default),
                MemberReferences = samples.MemberReferences(),
                DeclaredTypes = samples.DeclaredExternalTypes(),
            };
        }

        private static void AssertReported(IReadOnlyList<Violation> violations, PlantedViolation planted)
        {
            Assert.True(
                violations.Any(violation => IsAt(violation, planted.Member) && violation.Reason.Contains(planted.ReasonFragment, StringComparison.Ordinal)),
                "No violation containing '" + planted.ReasonFragment + "' at " + planted.Member + ". " + FoundAt(planted.Member, violations));
        }

        /// <summary>Whether the violation lies in the member (Type::Method), at an IL offset or on the member itself.</summary>
        private static bool IsAt(Violation violation, string member) =>
            violation.Location == member || violation.Location.StartsWith(member + "+IL_", StringComparison.Ordinal);

        /// <summary>Whether the violation lies in the type or in a type nested in it (a closure, a state machine).</summary>
        private static bool IsIn(Violation violation, string type) =>
            violation.Location.StartsWith(type + "::", StringComparison.Ordinal)
            || violation.Location.StartsWith(type + "+", StringComparison.Ordinal);

        /// <summary>Whether the member use was read in the type or in a type nested in it.</summary>
        private static bool IsIn(MemberReferenceSite site, string type) =>
            site.CallerType == type || site.CallerType.StartsWith(type + "+", StringComparison.Ordinal);

        /// <summary>Whether the type or a type nested in it declares the external type.</summary>
        private static bool IsIn(DeclaredTypeUse use, string type) =>
            use.DeclaringType == type || use.DeclaringType.StartsWith(type + "+", StringComparison.Ordinal);

        /// <summary>
        /// The member a member-use violation names ("Display: reason"), as Type::Member with the type's generic arguments
        /// stripped, for example System.Collections.Generic.Dictionary`2::get_Keys; null for a violation of another kind.
        /// </summary>
        private static string? BannedMemberOf(Violation violation)
        {
            string reason = violation.Reason;
            int separator = reason.IndexOf("::", StringComparison.Ordinal);
            int parameters = separator < 0 ? -1 : reason.IndexOf('(', separator);
            if (parameters < 0)
            {
                return null;
            }

            string type = reason[..separator];
            int generic = type.IndexOf('<', StringComparison.Ordinal);
            return (generic < 0 ? type : type[..generic]) + reason[separator..parameters];
        }

        /// <summary>A type the policy refuses ("type ..." or "uses T, which ..."), or a member of an allowed type its allowlist leaves out.</summary>
        private static bool IsRefusal(Violation violation, IPlatformApiPolicy policy) =>
            violation.Reason.EndsWith(policy.RefusedTypeReason, StringComparison.Ordinal)
            || violation.Reason.Contains(" may be used on ", StringComparison.Ordinal);

        private static string FoundAt(string member, IReadOnlyList<Violation> violations) =>
            "Found there:\n" + string.Join("\n", violations.Where(violation => IsAt(violation, member)));

        private static string Report(string heading, IReadOnlyList<Violation> violations) =>
            heading + ":\n" + string.Join("\n", violations.Select(violation => "  " + violation));

        /// <summary>The scans the sample tests share: each scan's results are immutable records.</summary>
        private sealed record SampleScans
        {
            public required IReadOnlyList<Violation> FloatingPoint { get; init; }

            public required IReadOnlyList<Violation> UnityBound { get; init; }

            public required IReadOnlyList<Violation> MemberBans { get; init; }

            public required IReadOnlyList<MemberReferenceSite> MemberReferences { get; init; }

            public required IReadOnlyList<DeclaredTypeUse> DeclaredTypes { get; init; }
        }

        /// <summary>A planted violation to look for: the member (Type::Method) and a fragment of its reason, each set by name.</summary>
        private sealed record PlantedViolation
        {
            public required string Member { get; init; }

            public required string ReasonFragment { get; init; }
        }

        /// <summary>A policy that judges as another one does and keeps every member use the walk handed it, with its caller.</summary>
        private sealed class RecordingPolicy : IPlatformApiPolicy
        {
            private readonly IPlatformApiPolicy _judge;
            private readonly List<JudgedUse> _judged = new();

            public RecordingPolicy(IPlatformApiPolicy judge) => _judge = judge;

            /// <summary>The member uses the walk handed to <see cref="MemberViolations"/>, in walk order.</summary>
            public IReadOnlyList<JudgedUse> Judged => _judged;

            public string RefusedTypeReason => _judge.RefusedTypeReason;

            public bool IsTypeAllowed(string typeName) => _judge.IsTypeAllowed(typeName);

            public IEnumerable<string> MemberViolations(MemberUse use, CallerContext caller)
            {
                _judged.Add(new JudgedUse(use, caller));
                return _judge.MemberViolations(use, caller);
            }
        }

        private sealed record JudgedUse(MemberUse Use, CallerContext Caller);
    }
}
