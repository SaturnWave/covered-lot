using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using Xunit;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// Rules of the code standard that the compiler does not fully enforce, checked on the .NET build of the Unity-free
    /// Core: no catch block at all, so none of the generic, bare, filtered or empty ones that CA1031 partly misses; no
    /// System.Random or UnityEngine.Random; no async state machine; and no static field that can be reassigned. The
    /// self-tests prove that the scanner sees each planted form in the samples compiled into this assembly and stays
    /// silent on the clean sample, as the engine's guard tests do.
    ///
    /// Overflow is checked on the sources and on the IL. Core compiles with overflow checks on (CheckForOverflowUnderflow
    /// in the .NET build and -checked+ in Unity, both pinned), so the integer arithmetic and numeric conversions it writes
    /// can wrap only inside an unchecked context, and no Core source may hold the word unchecked. That none wraps cannot be
    /// read from the IL: the compiler's own lowering of a foreach over an array, of a switch over an enum that starts at 1
    /// or of an index from the end emits a plain add or sub that cannot overflow. What the IL shows is that the build
    /// checked overflow: the .NET build of Core holds overflow-checked arithmetic (add.ovf, sub.ovf or mul.ovf, not counting
    /// the mul.ovf.un the compiler writes for a stackalloc's size in any context), and no Core source holds the word
    /// checked, so none opens a checked context of its own, where the compiler would write it whatever the build's
    /// setting. UnityChecks scans Unity's build of Core in the same way.
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
        private const string OverflowSamples = Samples + "OverflowSamples";
        private const string CoreFolder = "Assets/CoveredLot/Core";

        /// <summary>
        /// The keyword that opens an unchecked context. It also matches in comments and strings: telling those apart
        /// from code takes a C# lexer, and Core's comments and copy can say it another way.
        /// </summary>
        private static readonly Regex UncheckedKeyword = new(@"\bunchecked\b", RegexOptions.CultureInvariant);

        /// <summary>
        /// The keyword checked, wherever it stands. Every checked context, checked(...) or a checked block, starts with it,
        /// whatever follows it on that line or the next, and only the word written out is the keyword: a Unicode-escaped
        /// one is an identifier. Like the unchecked rule it reads text, so it matches in comments and strings as well,
        /// "overflow-checked" included, and Core's comments say it another way. Piece 4's rule, the word followed on its line
        /// by a parenthesis or a brace, passed a checked block in this repository's brace style (piece 4's review).
        /// </summary>
        private static readonly Regex CheckedKeyword = new(@"\bchecked\b", RegexOptions.CultureInvariant);

        /// <summary>mul.ovf.un (0xD9) right before localloc (0xFE 0x0F): the size check the compiler writes for a stackalloc.</summary>
        private static readonly byte[] StackallocSizeCheck = { 0xD9, 0xFE, 0x0F };

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

            IReadOnlyList<string> referenced = CodeHygiene.RandomTypeReferences(core);

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

            IReadOnlyList<string> uncheckedLines = LinesMatching(sources, UncheckedKeyword);

            Assert.NotEmpty(sources);
            Assert.True(uncheckedLines.Count == 0,
                "Core keeps its overflow checks; these lines name unchecked:\n" + string.Join("\n", uncheckedLines));
        }

        [Fact]
        public void The_dotnet_build_of_Core_holds_overflow_checked_arithmetic_so_it_was_built_with_overflow_checks()
        {
            using var core = CoreScanner();

            IReadOnlyList<LocatedInstruction> found = core.OverflowCheckedArithmetic();

            Assert.True(found.Count > 0,
                "The .NET build of CoveredLot.Core holds no add.ovf, sub.ovf or mul.ovf, so it was built without overflow checks (CheckForOverflowUnderflow).");
        }

        [Fact]
        public void No_Core_source_opens_a_checked_context_of_its_own()
        {
            IReadOnlyList<string> sources = CoreSources();

            IReadOnlyList<string> checkedLines = LinesMatching(sources, CheckedKeyword);

            Assert.NotEmpty(sources);
            Assert.True(checkedLines.Count == 0,
                "Core leaves overflow checks to its build, so that its IL shows the build's setting; these lines name checked:\n"
                + string.Join("\n", checkedLines));
        }

        /// <summary>Each planted source goes through the scan that the source rules make of every Core file, line by line.</summary>
        [Theory]
        [InlineData("int sum = checked(first + second);", 1)]
        [InlineData("long wide = checked (value * 2);", 1)]
        [InlineData("int next = value -checked(step + 1);", 1)]
        [InlineData("int next = checked /* overflow */ (value + 1);", 1)]
        [InlineData("int next = checked\n    (value + 1);", 1)]
        [InlineData("int Next(int value)\n{\n    checked\n    {\n        return value + 1;\n    }\n}", 3)]
        [InlineData("// The arithmetic\n// stays overflow-checked.", 2)]
        public void The_source_scan_reports_the_line_of_each_planted_checked_keyword(string source, int line)
        {
            Assert.Equal(new[] { line }, LineNumbersMatching(source.Split('\n'), CheckedKeyword));
        }

        [Theory]
        [InlineData("int sum = unchecked(first + second);")]
        [InlineData("bool isChecked = box.Checked;")]
        [InlineData("int checkedCount = total;\nint wasChecked = checkedCount;")]
        [InlineData("// The arithmetic keeps its overflow checks.")]
        public void The_source_scan_passes_unchecked_contexts_and_longer_words(string source)
        {
            Assert.Empty(LineNumbersMatching(source.Split('\n'), CheckedKeyword));
        }

        [Theory]
        [InlineData("Next", "add.ovf")]
        [InlineData("NextUnsigned", "add.ovf.un")]
        [InlineData("Previous", "sub.ovf")]
        [InlineData("PreviousUnsigned", "sub.ovf.un")]
        [InlineData("Doubled", "mul.ovf")]
        [InlineData("DoubledUnsigned", "mul.ovf.un")]
        public void The_scanner_reports_each_planted_overflow_checked_instruction(string method, string opCode)
        {
            using var samples = SampleScanner();

            IReadOnlyList<LocatedInstruction> found = samples.OverflowCheckedArithmetic();

            LocatedInstruction planted = Assert.Single(found, instruction => IsInOverflowSample(instruction, method));
            Assert.Equal(opCode, planted.OpCodeName);
        }

        [Fact]
        public void The_scanner_reports_no_overflow_checked_instruction_for_unchecked_arithmetic()
        {
            using var samples = SampleScanner();

            IReadOnlyList<LocatedInstruction> found = samples.OverflowCheckedArithmetic();

            // The scan read the sample type: its checked addition is reported.
            Assert.Contains(found, instruction => IsInOverflowSample(instruction, "Next"));
            Assert.DoesNotContain(found, instruction => IsInOverflowSample(instruction, "NextWrapping"));
        }

        /// <summary>
        /// The compiler checks a stackalloc's size with mul.ovf.un in any context, so a Core built without overflow checks
        /// passed both overflow tests as soon as it held a stackalloc whose size is not a constant (piece 4's review).
        /// </summary>
        [Fact]
        public void The_scanner_leaves_out_the_size_check_the_compiler_writes_for_a_stackalloc()
        {
            using var samples = SampleScanner();

            IReadOnlyList<LocatedInstruction> found = samples.OverflowCheckedArithmetic();

            // The premise, read through reflection apart from the scanner: the sample holds mul.ovf.un right before localloc.
            Assert.True(OverflowSampleIl(nameof(Shared.Samples.OverflowSamples.StackallocLength)).AsSpan().IndexOf(StackallocSizeCheck) >= 0,
                "OverflowSamples.StackallocLength holds no mul.ovf.un right before localloc.");
            Assert.Contains(found, instruction => IsInOverflowSample(instruction, "Next"));
            Assert.DoesNotContain(found, instruction => IsInOverflowSample(instruction, nameof(Shared.Samples.OverflowSamples.StackallocLength)));
        }

        [Fact]
        public void No_Core_source_names_a_culture_sensitive_string_comparison()
        {
            IReadOnlyList<string> sources = CoreSources();

            IReadOnlyList<string> comparisonLines = LinesMatching(sources, CultureSensitiveComparison);

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

            Assert.Equal(new[] { "System.Random" }, CodeHygiene.RandomTypeReferences(samples));
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
                .Where(site => IsIn(site.CallerType, CleanSample) && CodeHygiene.RandomTypes.Contains(site.DeclaringType, StringComparer.Ordinal));
            IReadOnlyList<string> findings = violations.Select(violation => violation.ToString())
                .Concat(randomUses.Select(site => site.ToString()))
                .ToList();

            Assert.True(findings.Count == 0, "the clean sample was reported:\n" + string.Join("\n", findings));
        }

        /// <summary>The .NET build of Core, as the test project references it.</summary>
        private static AssemblyScanner CoreScanner() => new(typeof(UnknownEngineCaseException).Assembly.Location);

        /// <summary>This test assembly, which compiles the planted samples of dotnet/Shared/Samples.</summary>
        private static AssemblyScanner SampleScanner() => new(typeof(HygieneTests).Assembly.Location);

        /// <summary>The files the .NET build compiles into Core: every .cs under the Core folder, in any letter case.</summary>
        private static IReadOnlyList<string> CoreSources() => RepositoryLayout.FilesWithExtension(CoreFolder, FileExtension.CSharp);

        /// <summary>Each line of the sources that the pattern matches, as path:line, source by source.</summary>
        private static IReadOnlyList<string> LinesMatching(IReadOnlyList<string> sources, Regex pattern)
        {
            var matching = new List<string>();
            foreach (string source in sources)
            {
                foreach (int line in LineNumbersMatching(File.ReadAllLines(source), pattern))
                {
                    matching.Add(RepositoryLayout.Relative(source) + ":" + line.ToString(CultureInfo.InvariantCulture));
                }
            }

            return matching;
        }

        /// <summary>The number, counted from 1, of each line that the pattern matches: the scan the source rules make of each file.</summary>
        private static IReadOnlyList<int> LineNumbersMatching(IReadOnlyList<string> lines, Regex pattern)
        {
            var numbers = new List<int>();
            for (int index = 0; index < lines.Count; index++)
            {
                if (pattern.IsMatch(lines[index]))
                {
                    numbers.Add(index + 1);
                }
            }

            return numbers;
        }

        /// <summary>
        /// Whether the instruction lies in that method of the overflow samples ("...OverflowSamples::Next+IL_0004"), and not
        /// in one whose name only starts the same.
        /// </summary>
        private static bool IsInOverflowSample(LocatedInstruction instruction, string method) =>
            instruction.Location.StartsWith(OverflowSamples + "::" + method + "+IL_", StringComparison.Ordinal);

        /// <summary>The IL bytes of a method of the overflow samples, read through reflection, apart from the scanner.</summary>
        private static byte[] OverflowSampleIl(string method) =>
            typeof(Shared.Samples.OverflowSamples).GetMethod(method)?.GetMethodBody()?.GetILAsByteArray()
            ?? throw new XunitException("OverflowSamples has no method " + method + " with a body.");

        /// <summary>Whether a location or type name lies in the type or in a type nested in it (a state machine, a closure).</summary>
        private static bool IsIn(string location, string type) =>
            location == type
            || location.StartsWith(type + "::", StringComparison.Ordinal)
            || location.StartsWith(type + "+", StringComparison.Ordinal);

        private static string Report(IReadOnlyList<Violation> violations) =>
            "CoveredLot.Core breaks the code standard:\n" + string.Join("\n", violations.Select(violation => "  " + violation));
    }
}
