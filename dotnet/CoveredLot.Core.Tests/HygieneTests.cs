using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// Rules of the code standard that the compiler does not fully enforce, checked on the .NET build of the Unity-free
    /// Core: no catch block at all, so none of the generic, bare, filtered or empty ones that CA1031 partly misses; no
    /// System.Random or UnityEngine.Random; no async state machine; and no static field that can be reassigned. The
    /// self-tests prove that the scanner sees each planted form in the samples compiled into this assembly and stays
    /// silent on the clean sample, as the engine's guard tests do.
    ///
    /// Overflow is checked on the sources instead. Core compiles with overflow checks on (CheckForOverflowUnderflow in
    /// the .NET build and -checked+ in Unity, both pinned), so the integer arithmetic and numeric conversions it writes
    /// can wrap only inside an unchecked context, and no Core source may open one. The IL cannot show this: the
    /// compiler's own lowering of a foreach over an array, of a switch over an enum that starts at 1 or of an index
    /// from the end emits a plain add or sub that cannot overflow.
    ///
    /// Culture-sensitive string comparison is checked on the sources too. The API policy (ApiPolicyTests) lets a string
    /// member through whenever it takes a StringComparison, whatever value it is given, and the analyzers flag such a
    /// value only for Equals and Compare, and only in the .NET build. So no Core source may name StringComparison's
    /// CurrentCulture, CurrentCultureIgnoreCase, InvariantCulture or InvariantCultureIgnoreCase. Like the unchecked rule,
    /// this reads text: it matches in comments and strings as well, and a value cast from an integer gets past it.
    /// </summary>
    public sealed class HygieneTests
    {
        private const string Samples = "CoveredLot.Shared.Samples.";
        private const string ForbiddenCatchSample = Samples + "ForbiddenCatchSample";
        private const string RandomSample = Samples + "RandomSample";
        private const string AsyncSample = Samples + "AsyncSample";
        private const string MutableStaticSample = Samples + "MutableStaticSample";
        private const string CleanSample = Samples + "CleanSample";
        private const string CoreFolder = "Assets/CoveredLot/Core";

        private static readonly string[] RandomTypes = { "System.Random", "UnityEngine.Random" };

        /// <summary>
        /// The keyword that opens an unchecked context. It also matches in comments and strings: telling those apart
        /// from code takes a C# lexer, and Core's comments and copy can say it another way.
        /// </summary>
        private static readonly Regex UncheckedKeyword = new(@"\bunchecked\b", RegexOptions.CultureInvariant);

        /// <summary>A StringComparison value that compares by a culture's rules, qualified or not by its namespace.</summary>
        private static readonly Regex CultureSensitiveComparison = new(
            @"\bStringComparison\s*\.\s*(CurrentCulture|CurrentCultureIgnoreCase|InvariantCulture|InvariantCultureIgnoreCase)\b",
            RegexOptions.CultureInvariant);

        [Fact]
        public void Core_has_no_generic_bare_filtered_or_empty_catch_block()
        {
            using var core = CoreScanner();

            IReadOnlyList<Violation> violations = core.GenericOrEmptyCatchBlocks();

            Assert.True(violations.Count == 0, Report(violations));
        }

        [Fact]
        public void Core_has_no_catch_block_at_all()
        {
            using var core = CoreScanner();

            IReadOnlyList<Violation> violations = core.CatchBlocks();

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
        public void Core_has_no_static_field_that_can_be_reassigned()
        {
            using var core = CoreScanner();

            IReadOnlyList<Violation> violations = core.ReassignableStaticFields();

            Assert.True(violations.Count == 0, Report(violations));
        }

        [Fact]
        public void No_Core_source_opens_an_unchecked_context()
        {
            IReadOnlyList<string> sources = CoreSources();

            IReadOnlyList<string> uncheckedLines = sources.SelectMany(source => LinesMatching(source, UncheckedKeyword)).ToList();

            Assert.NotEmpty(sources);
            Assert.True(uncheckedLines.Count == 0,
                "Core keeps its overflow checks; these lines name unchecked:\n" + string.Join("\n", uncheckedLines));
        }

        [Fact]
        public void No_Core_source_names_a_culture_sensitive_string_comparison()
        {
            IReadOnlyList<string> sources = CoreSources();

            IReadOnlyList<string> comparisonLines = sources.SelectMany(source => LinesMatching(source, CultureSensitiveComparison)).ToList();

            Assert.NotEmpty(sources);
            Assert.True(comparisonLines.Count == 0,
                "Core compares text ordinally; these lines name a culture-sensitive StringComparison:\n" + string.Join("\n", comparisonLines));
        }

        [Theory]
        [InlineData("bool done = text.EndsWith(\"xy\", StringComparison.InvariantCulture);")]
        [InlineData("int at = text.IndexOf(\"in\", StringComparison.CurrentCultureIgnoreCase);")]
        [InlineData("bool same = string.Equals(first, second, System.StringComparison.CurrentCulture);")]
        [InlineData("int order = string.Compare(first, second, StringComparison . InvariantCultureIgnoreCase);")]
        public void The_source_scan_reports_each_planted_culture_sensitive_comparison(string line)
        {
            Assert.Matches(CultureSensitiveComparison, line);
        }

        [Theory]
        [InlineData("bool done = text.EndsWith(\"xy\", StringComparison.Ordinal);")]
        [InlineData("bool same = string.Equals(first, second, StringComparison.OrdinalIgnoreCase);")]
        [InlineData("var comparer = StringComparer.InvariantCulture;")]
        [InlineData("var mode = LevelStringComparison.CurrentCulture;")]
        public void The_source_scan_passes_ordinal_comparisons_and_other_names(string line)
        {
            Assert.DoesNotMatch(CultureSensitiveComparison, line);
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

        /// <summary>Core's stricter rule also reports the specific catch that the clean sample is allowed to keep.</summary>
        [Theory]
        [InlineData(ForbiddenCatchSample, "CatchesEverything")]
        [InlineData(ForbiddenCatchSample, "BareCatch")]
        [InlineData(ForbiddenCatchSample, "CatchesEverythingWithAFilter")]
        [InlineData(ForbiddenCatchSample, "SwallowsSilently")]
        [InlineData(CleanSample, "ParseOrExplain")]
        public void The_scanner_reports_every_catch_block_for_Core(string type, string method)
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> violations = samples.CatchBlocks();

            Assert.Contains(violations, violation => violation.Location.StartsWith(type + "::" + method + "+IL_", StringComparison.Ordinal));
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
        [InlineData("_count")]
        [InlineData("<Total>k__BackingField")]
        public void The_scanner_reports_each_planted_reassignable_static_field(string field)
        {
            using var samples = SampleScanner();

            IReadOnlyList<Violation> violations = samples.ReassignableStaticFields();

            Assert.Contains(violations, violation => violation.Location == MutableStaticSample + "::" + field);
        }

        [Theory]
        [InlineData("int sum = unchecked(first + second);")]
        [InlineData("unchecked { total *= step; }")]
        [InlineData("short low = unchecked((short)value);")]
        public void The_source_scan_reports_each_planted_unchecked_context(string line)
        {
            Assert.Matches(UncheckedKeyword, line);
        }

        [Theory]
        [InlineData("int sum = checked(first + second);")]
        [InlineData("int total = first + second;")]
        [InlineData("int uncheckedTotal = total;")]
        public void The_source_scan_passes_checked_code_and_longer_words(string line)
        {
            Assert.DoesNotMatch(UncheckedKeyword, line);
        }

        [Fact]
        public void The_scanner_finds_nothing_in_the_clean_sample()
        {
            using var samples = SampleScanner();

            IEnumerable<Violation> violations = samples.GenericOrEmptyCatchBlocks()
                .Concat(samples.AsyncStateMachines())
                .Concat(samples.ReassignableStaticFields())
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

        /// <summary>The files the .NET build compiles into Core.</summary>
        private static IReadOnlyList<string> CoreSources() => Directory
            .EnumerateFiles(RepositoryLayout.FullPath(CoreFolder), "*.cs", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".cs", StringComparison.Ordinal))
            .ToList();

        /// <summary>Each line of the source that the pattern matches, as path:line.</summary>
        private static IEnumerable<string> LinesMatching(string source, Regex pattern) => File.ReadAllLines(source)
            .Select((text, index) => (Text: text, Number: index + 1))
            .Where(line => pattern.IsMatch(line.Text))
            .Select(line => RepositoryLayout.Relative(source) + ":" + line.Number.ToString(CultureInfo.InvariantCulture));

        /// <summary>Whether a location or type name lies in the type or in a type nested in it (a state machine, a closure).</summary>
        private static bool IsIn(string location, string type) =>
            location == type
            || location.StartsWith(type + "::", StringComparison.Ordinal)
            || location.StartsWith(type + "+", StringComparison.Ordinal);

        private static string Report(IReadOnlyList<Violation> violations) =>
            "CoveredLot.Core breaks the code standard:\n" + string.Join("\n", violations.Select(violation => "  " + violation));
    }
}
