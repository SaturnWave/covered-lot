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
    /// randomness, clock, time zone, environment, thread identity, reflection beyond typeof comparisons, native call or
    /// native-sized integer, and none of the members it bans for culture-dependent text or text order, the platform
    /// newline, randomized hashing, enumeration in hash order or unstable sorts. Unity does not run the .NET analyzers,
    /// so for code that only Unity builds an IL scan like this is the only guard; this class scans the .NET build of
    /// Core alone. The self-tests prove that the scanners report each planted violation at its member and stay silent
    /// on the clean samples, and that the member bans alone (<see cref="MemberBansPolicy"/>) report the planted bans but
    /// refuse no type.
    /// </summary>
    public sealed class ApiPolicyTests
    {
        private const string Samples = "CoveredLot.Shared.Samples.";
        private const string ViolationsSample = Samples + "DeterminismViolationsSample";
        private const string CoreAssembly = "CoveredLot.Core";
        private const string CoreException = "CoveredLot.Core.UnknownEngineCaseException";
        private const string Constructor = ".ctor";

        /// <summary>
        /// Code that keeps every rule these scans check: realistic integer-only code, a record whose compiler-written
        /// members must pass, and the hygiene tests' clean sample of a specific catch, an iterator, a using and a cached
        /// lambda.
        /// </summary>
        private static readonly string[] CleanSamples =
        {
            Samples + "CleanIntegerOnlySample", Samples + "CleanCell", Samples + "CleanSample",
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
        [InlineData("DoubleField", "floating-point field")]
        [InlineData("HalveWithFloatLocal", "floating-point local variable")]
        [InlineData("HalveWithFloatLocal", "floating-point instruction conv.r4")]
        [InlineData("Price", "floating-point parameter or return type")]
        [InlineData("FloorOfDoubleLiteral", "floating-point instruction ldc.r8")]
        [InlineData("FloorOfDoubleLiteral", "uses floating point via System.Math::Floor(System.Double)")]
        [InlineData("NormalizedVector", "uses floating point via System.Numerics.Vector2::Normalize(System.Numerics.Vector2)")]
        public void The_floating_point_scanner_reports_each_planted_use_at_its_member(string member, string reason)
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> violations = samples.FloatingPointUses();

            Assert.Contains(violations, violation => violation.Location == ViolationsSample + "::" + member && violation.Reason == reason);
        }

        [Theory]
        [InlineData("NextFromSystemRandom", "uses System.Random")]
        [InlineData("ReadTheClock", "uses System.DateTime")]
        [InlineData("LocalTimeConversion", "uses System.DateTime")]
        [InlineData("NormalizedVector", "uses System.Numerics.Vector2")]
        [InlineData("PlainStringHash", "hash codes are randomized per process")]
        [InlineData("OrdinalComparerHash", "hash codes are randomized per process")]
        [InlineData("TupleHash", "hash codes are randomized per process")]
        [InlineData("IntegerToTextByConcatenation", "depends on the device culture")]
        [InlineData("FormatWithoutCulture", "depends on the device culture")]
        [InlineData("AppendInteger", "depends on the device culture")]
        [InlineData("AppendPlatformNewline", "Environment.NewLine differs")]
        [InlineData("UpperWithDeviceCulture", "depends on the device culture")]
        [InlineData("CompareWithDeviceCulture", "depends on the device culture")]
        [InlineData("ChangeTypeThroughDouble", "uses System.Convert")]
        [InlineData("TypeByName", "may be used on System.Type")]
        [InlineData("ProcessorCount", "may be used on System.Environment")]
        [InlineData("ThreadIdentity", "thread identity must not influence results")]
        [InlineData("NativeSizedSum", "native-sized integer")]
        [InlineData("DictionaryOrder", "enumeration order is an implementation detail")]
        [InlineData("NativeTime", "native call (P/Invoke) is not allowed")]
        [InlineData("SortWithListSort", "unstable sort")]
        [InlineData("SortWithArraySort", "unstable sort")]
        [InlineData("SortWithSpanSort", "unstable sort")]
        [InlineData("OrderTextWithoutComparer", "orders text by the device culture")]
        [InlineData("OrderTextByCurrentCulture", "orders text by the device culture")]
        [InlineData("OrderByTupleHoldingText", "orders text by the device culture")]
        [InlineData("SortedDictionaryWithoutComparer", "orders text by the device culture")]
        [InlineData("SortedSetWithoutComparer", "orders text by the device culture")]
        [InlineData("CompareWithDefaultComparer", "orders text by the device culture")]
        [InlineData("ReadOnlyDictionaryOrder", "enumeration order is an implementation detail")]
        [InlineData("ReadOnlyDictionaryKeys", "enumeration order is an implementation detail")]
        [InlineData("DictionaryThroughLinq", "enumeration order is an implementation detail")]
        [InlineData("HashSetToList", "enumeration order is an implementation detail")]
        [InlineData("HashSetJoined", "enumeration order is an implementation detail")]
        [InlineData("HashSetThroughLambda", "enumeration order is an implementation detail")]
        [InlineData("PairsOfUnknownOrder", "enumeration order is an implementation detail")]
        [InlineData("InterpolatedWithDeviceCulture", "depends on the device culture")]
        public void The_api_scanner_reports_each_planted_violation_at_its_method(string method, string reasonFragment)
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> violations = samples.ApiPolicyViolations(UnityBoundApiPolicy.Default);

            Assert.True(
                violations.Any(violation => IsAt(violation, method) && violation.Reason.Contains(reasonFragment, StringComparison.Ordinal)),
                NotFoundAt(method, reasonFragment, violations));
        }

        /// <summary>The planted member bans, and the native-sized integer and native call that the scan refuses under every policy.</summary>
        [Theory]
        [InlineData("PlainStringHash", "hash codes are randomized per process")]
        [InlineData("OrdinalComparerHash", "hash codes are randomized per process")]
        [InlineData("TupleHash", "hash codes are randomized per process")]
        [InlineData("IntegerToTextByConcatenation", "depends on the device culture")]
        [InlineData("FormatWithoutCulture", "depends on the device culture")]
        [InlineData("AppendInteger", "depends on the device culture")]
        [InlineData("AppendPlatformNewline", "Environment.NewLine differs")]
        [InlineData("UpperWithDeviceCulture", "depends on the device culture")]
        [InlineData("CompareWithDeviceCulture", "depends on the device culture")]
        [InlineData("ThreadIdentity", "thread identity must not influence results")]
        [InlineData("NativeSizedSum", "native-sized integer")]
        [InlineData("DictionaryOrder", "enumeration order is an implementation detail")]
        [InlineData("NativeTime", "native call (P/Invoke) is not allowed")]
        [InlineData("SortWithListSort", "unstable sort")]
        [InlineData("SortWithArraySort", "unstable sort")]
        [InlineData("SortWithSpanSort", "unstable sort")]
        [InlineData("OrderTextWithoutComparer", "orders text by the device culture")]
        [InlineData("OrderTextByCurrentCulture", "orders text by the device culture")]
        [InlineData("OrderByTupleHoldingText", "orders text by the device culture")]
        [InlineData("SortedDictionaryWithoutComparer", "orders text by the device culture")]
        [InlineData("SortedSetWithoutComparer", "orders text by the device culture")]
        [InlineData("CompareWithDefaultComparer", "orders text by the device culture")]
        [InlineData("ReadOnlyDictionaryOrder", "enumeration order is an implementation detail")]
        [InlineData("ReadOnlyDictionaryKeys", "enumeration order is an implementation detail")]
        [InlineData("DictionaryThroughLinq", "enumeration order is an implementation detail")]
        [InlineData("HashSetToList", "enumeration order is an implementation detail")]
        [InlineData("HashSetJoined", "enumeration order is an implementation detail")]
        [InlineData("HashSetThroughLambda", "enumeration order is an implementation detail")]
        [InlineData("PairsOfUnknownOrder", "enumeration order is an implementation detail")]
        [InlineData("InterpolatedWithDeviceCulture", "depends on the device culture")]
        public void The_member_bans_alone_report_each_planted_ban_at_its_method(string method, string reasonFragment)
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> violations = samples.ApiPolicyViolations(MemberBansPolicy.Default);

            Assert.True(
                violations.Any(violation => IsAt(violation, method) && violation.Reason.Contains(reasonFragment, StringComparison.Ordinal)),
                NotFoundAt(method, reasonFragment, violations));
        }

        /// <summary>Methods whose only fault is a type, or a member of an allowed type, outside the Unity-bound allowlist.</summary>
        [Theory]
        [InlineData("NextFromSystemRandom")]
        [InlineData("ReadTheClock")]
        [InlineData("LocalTimeConversion")]
        [InlineData("NormalizedVector")]
        [InlineData("ChangeTypeThroughDouble")]
        [InlineData("TypeByName")]
        [InlineData("ProcessorCount")]
        public void The_member_bans_alone_pass_a_method_that_the_allowlist_refuses(string method)
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> refused = samples.ApiPolicyViolations(UnityBoundApiPolicy.Default)
                .Where(violation => IsAt(violation, method))
                .ToList();
            IReadOnlyList<Violation> banned = samples.ApiPolicyViolations(MemberBansPolicy.Default)
                .Where(violation => IsAt(violation, method))
                .ToList();

            Assert.True(refused.Count > 0, "The Unity-bound policy reports nothing at " + method + ".");
            Assert.True(refused.All(violation => IsRefusal(violation, UnityBoundApiPolicy.Default)),
                Report("The Unity-bound policy reports more than an allowlist refusal at " + method, refused));
            Assert.True(banned.Count == 0, Report("The member bans alone report " + method + ", which only the allowlist refuses", banned));
        }

        [Fact]
        public void The_member_bans_alone_refuse_no_type_and_no_member_of_an_allowed_type()
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> banned = samples.ApiPolicyViolations(MemberBansPolicy.Default);
            IReadOnlyList<Violation> refusals = banned.Where(violation => IsRefusal(violation, MemberBansPolicy.Default)).ToList();

            // The same assembly holds types and members that the allowlist refuses, and planted bans to report.
            Assert.Contains(samples.ApiPolicyViolations(UnityBoundApiPolicy.Default), violation => IsRefusal(violation, UnityBoundApiPolicy.Default));
            Assert.NotEmpty(banned);
            Assert.True(refusals.Count == 0, Report("The member bans alone refuse these", refusals));
        }

        [Fact]
        public void The_scanners_stay_silent_on_clean_integer_only_code()
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> violations = samples.FloatingPointUses()
                .Concat(samples.ApiPolicyViolations(UnityBoundApiPolicy.Default))
                .Concat(samples.ApiPolicyViolations(MemberBansPolicy.Default))
                .Where(violation => CleanSamples.Any(type => IsIn(violation, type)))
                .ToList();
            IReadOnlyList<MemberReferenceSite> sites = samples.MemberReferences();
            IReadOnlyList<string> unread = CleanSamples.Where(type => !sites.Any(site => IsIn(site, type))).ToList();

            // The scans read every method body, as the member reference list does, so each clean sample's code was read.
            Assert.True(unread.Count == 0, "no member use was read in " + string.Join(", ", unread));
            Assert.True(violations.Count == 0, Report("The scans report the clean samples", violations));
        }

        /// <summary>The .NET build of Core, as the test project references it.</summary>
        private static AssemblyScanner CoreScanner() => new(typeof(UnknownEngineCaseException).Assembly.Location);

        /// <summary>This test assembly, which compiles the planted samples of dotnet/Shared/Samples.</summary>
        private static AssemblyScanner SampleScanner() => new(typeof(ApiPolicyTests).Assembly.Location);

        /// <summary>Whether the violation lies in the planted violations sample's method, at an IL offset or on the method itself.</summary>
        private static bool IsAt(Violation violation, string method) =>
            violation.Location == ViolationsSample + "::" + method
            || violation.Location.StartsWith(ViolationsSample + "::" + method + "+IL_", StringComparison.Ordinal);

        /// <summary>Whether the violation lies in the type or in a type nested in it (a closure, a state machine).</summary>
        private static bool IsIn(Violation violation, string type) =>
            violation.Location.StartsWith(type + "::", StringComparison.Ordinal)
            || violation.Location.StartsWith(type + "+", StringComparison.Ordinal);

        /// <summary>Whether the member use was read in the type or in a type nested in it.</summary>
        private static bool IsIn(MemberReferenceSite site, string type) =>
            site.CallerType == type || site.CallerType.StartsWith(type + "+", StringComparison.Ordinal);

        /// <summary>A type the policy refuses ("type ..." or "uses T, which ..."), or a member of an allowed type its allowlist leaves out.</summary>
        private static bool IsRefusal(Violation violation, IPlatformApiPolicy policy) =>
            violation.Reason.EndsWith(policy.RefusedTypeReason, StringComparison.Ordinal)
            || violation.Reason.Contains(" may be used on ", StringComparison.Ordinal);

        private static string NotFoundAt(string method, string reasonFragment, IReadOnlyList<Violation> violations) =>
            "No violation containing '" + reasonFragment + "' at " + method + ". Found there:\n"
            + string.Join("\n", violations.Where(violation => IsAt(violation, method)));

        private static string Report(string heading, IReadOnlyList<Violation> violations) =>
            heading + ":\n" + string.Join("\n", violations.Select(violation => "  " + violation));

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
