using System;
using System.Collections.Generic;
using System.Linq;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// Rules of the code standard that the compiler does not fully enforce, checked on the .NET build of the Unity-free
    /// Core: no generic, bare, filtered or empty catch (CA1031 misses the filtered and empty ones), no System.Random or
    /// UnityEngine.Random, no async state machine, and no arithmetic that wraps silently on overflow. The self-tests
    /// prove that the scanner sees each planted form in the samples compiled into this assembly and stays silent on
    /// the clean sample, as the engine's guard tests do.
    /// </summary>
    public sealed class HygieneTests
    {
        private const string Samples = "CoveredLot.Shared.Samples.";
        private const string ForbiddenCatchSample = Samples + "ForbiddenCatchSample";
        private const string RandomSample = Samples + "RandomSample";
        private const string AsyncSample = Samples + "AsyncSample";
        private const string UncheckedArithmeticSample = Samples + "UncheckedArithmeticSample";
        private const string CleanSample = Samples + "CleanSample";

        private static readonly string[] RandomTypes = { "System.Random", "UnityEngine.Random" };

        [Fact]
        public void Core_has_no_generic_bare_filtered_or_empty_catch_block()
        {
            using var core = CoreScanner();

            IReadOnlyList<Violation> violations = core.GenericOrEmptyCatchBlocks();

            Assert.True(violations.Count == 0, Report(violations));
        }

        [Fact]
        public void Core_references_neither_system_random_nor_unity_random()
        {
            using var core = CoreScanner();

            IReadOnlyList<string> referenced = RandomTypes.Where(core.References).ToList();

            Assert.True(referenced.Count == 0, "CoveredLot.Core references " + string.Join(" and ", referenced) + ".");
        }

        [Fact]
        public void Core_has_no_async_state_machine()
        {
            using var core = CoreScanner();

            IReadOnlyList<Violation> violations = core.AsyncStateMachines();

            Assert.True(violations.Count == 0, Report(violations));
        }

        [Fact]
        public void Core_has_no_unchecked_addition_subtraction_or_multiplication()
        {
            using var core = CoreScanner();

            IReadOnlyList<Violation> violations = core.UncheckedArithmetic();

            Assert.True(violations.Count == 0, Report(violations));
        }

        [Theory]
        [InlineData("CatchesEverything", "generic catch (System.Exception)")]
        [InlineData("BareCatch", "generic catch (System.Object)")]
        [InlineData("CatchesEverythingWithAFilter", "generic catch with a when-filter")]
        [InlineData("SwallowsSilently", "empty catch block")]
        public void The_scanner_reports_each_planted_catch_block(string method, string reason)
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> violations = samples.GenericOrEmptyCatchBlocks();

            Assert.Contains(violations, violation =>
                violation.Location.StartsWith(ForbiddenCatchSample + "::" + method + "+IL_", StringComparison.Ordinal)
                && violation.Reason == reason);
        }

        [Fact]
        public void The_scanner_reports_the_planted_system_random_call()
        {
            using var samples = SampleScanner();

            Assert.True(samples.References("System.Random"));
            Assert.Contains(samples.MemberReferences(), site =>
                site.CallerType == RandomSample && site.CallerMethod == "RollADie" && site.DeclaringType == "System.Random");
        }

        [Fact]
        public void The_scanner_reports_the_planted_async_method()
        {
            using var samples = SampleScanner();

            Assert.Contains(samples.AsyncStateMachines(), violation => IsIn(violation.Location, AsyncSample));
        }

        [Theory]
        [InlineData("AddsWithoutCheck", "unchecked add")]
        [InlineData("SubtractsWithoutCheck", "unchecked sub")]
        [InlineData("MultipliesWithoutCheck", "unchecked mul")]
        public void The_scanner_reports_each_planted_unchecked_operation(string method, string reason)
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> violations = samples.UncheckedArithmetic();

            Assert.Contains(violations, violation =>
                violation.Location.StartsWith(UncheckedArithmeticSample + "::" + method + "+IL_", StringComparison.Ordinal)
                && violation.Reason == reason);
        }

        [Fact]
        public void The_scanner_finds_nothing_in_the_clean_sample()
        {
            using var samples = SampleScanner();

            IEnumerable<Violation> violations = samples.GenericOrEmptyCatchBlocks()
                .Concat(samples.AsyncStateMachines())
                .Concat(samples.UncheckedArithmetic())
                .Where(violation => IsIn(violation.Location, CleanSample));
            IEnumerable<MemberReferenceSite> randomUses = samples.MemberReferences()
                .Where(site => IsIn(site.CallerType, CleanSample) && RandomTypes.Contains(site.DeclaringType, StringComparer.Ordinal));
            IReadOnlyList<string> findings = violations.Select(violation => violation.ToString())
                .Concat(randomUses.Select(site => site.ToString()))
                .ToList();

            Assert.True(findings.Count == 0, "the clean sample was reported:\n" + string.Join("\n", findings));
        }

        /// <summary>The .NET build of Core, as the test project references it.</summary>
        private static AssemblyScanner CoreScanner() => new(typeof(UnknownEngineCaseException).Assembly.Location);

        /// <summary>This test assembly, which compiles the planted samples of dotnet/Shared/Samples.</summary>
        private static AssemblyScanner SampleScanner() => new(typeof(HygieneTests).Assembly.Location);

        /// <summary>Whether a location or type name lies in the type or in a type nested in it (a state machine, a closure).</summary>
        private static bool IsIn(string location, string type) =>
            location == type
            || location.StartsWith(type + "::", StringComparison.Ordinal)
            || location.StartsWith(type + "+", StringComparison.Ordinal);

        private static string Report(IReadOnlyList<Violation> violations) =>
            "CoveredLot.Core breaks the code standard:\n" + string.Join("\n", violations.Select(violation => "  " + violation));
    }
}
