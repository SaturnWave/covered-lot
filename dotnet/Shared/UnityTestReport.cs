using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The run a Unity test report has to show: the test assembly of its mode and the culture the run was asked to use
    /// (PUZZLE_TEST_CULTURE), which the report's file name gives and the report itself never records.
    /// </summary>
    internal sealed record ExpectedTestRun
    {
        /// <summary>The test assembly, for example CoveredLot.Tests.EditMode: the report may run its tests and no others.</summary>
        public required string TestAssembly { get; init; }

        /// <summary>The culture the run was asked to use, for example tr-TR.</summary>
        public required string Culture { get; init; }

        /// <summary>The culture guard's fixture in the test assembly, which starts the full name of each of its tests.</summary>
        public string CultureGuardFixture => TestAssembly + ".CultureGuardTests";
    }

    /// <summary>
    /// The results rule of design section 10.4, over the NUnit 3 XML report that a Unity test run writes (<c>unity test
    /// --output</c>). The editor exits 0 when no test ran, so the exit code alone proves nothing. A report proves the run
    /// only when:
    /// <list type="bullet">
    /// <item><description>it holds at least one test and every test passed: none failed, none was skipped, ignored or
    /// inconclusive;</description></item>
    /// <item><description>the run's own result is Passed, and no suite failed outside its tests. Unity records a failed
    /// one-time tear-down ([OneTimeTearDown] or [UnityOneTimeTearDown] that throws, or logs an error that no LogAssert
    /// expects) on its fixture alone: every test keeps its Passed, so the counts stay whole and the editor still exits 0,
    /// and only the fixture's result and the run's own (Failed(Child)) show it. Each such suite is named with its site and
    /// the first line of its message; a suite that failed only through its children (site Child) or its parent (site
    /// Parent) is not, since the test or suite that failed is. An Assembly suite is named by its name, because its full
    /// name is the DLL's path on the machine that ran it;</description></item>
    /// <item><description>every test in it belongs to the expected test assembly: a test compiled into any other assembly
    /// (Assembly-CSharp-Editor, say, from a script outside the asmdef folders) carries neither [HostileCulture] nor our
    /// csc.rsp, and FixtureGuardTests never sees it;</description></item>
    /// <item><description>both tests of the culture guard are in it and passed, and the guard's name test wrote the culture
    /// it ran under (a line "culture under test: tr-TR", which CultureGuardFixture writes), the expected one and no other.
    /// Nothing else in a report shows the culture: the reports of a tr-TR run and an sv-SE run differ only in their times,
    /// ids and seeds.</description></item>
    /// </list>
    /// The guard's tests are found by their full name, which starts with the fixture that runs them; their classname names
    /// the class that declares them, the shared base class CoveredLot.Tests.Support.CultureGuardFixture. UnityTestReportTests
    /// shows each problem on a doctored report (dotnet/Shared/Samples/UnityReports).
    /// </summary>
    internal static class UnityTestReport
    {
        /// <summary>What the guard's name test writes before the culture's name; CultureGuardFixture writes the same text.</summary>
        public const string CultureLinePrefix = "culture under test: ";

        /// <summary>The guard's test that checks the culture against PUZZLE_TEST_CULTURE and writes it.</summary>
        public const string CultureNameTest = "The_tests_run_under_the_culture_that_PUZZLE_TEST_CULTURE_names";

        /// <summary>The guard's test that checks the culture breaks an invariant assumption.</summary>
        public const string HostilityTest = "The_culture_breaks_at_least_one_invariant_assumption";

        private const string PassedResult = "Passed";
        private const string FailedResult = "Failed";

        /// <summary>The type NUnit gives the suite of a test assembly.</summary>
        private const string AssemblySuite = "Assembly";

        /// <summary>The sites of a suite that failed only because a child failed, or because its parent failed before it ran.</summary>
        private const string ChildSite = "Child";
        private const string ParentSite = "Parent";

        /// <summary>How many test names a problem lists before it only counts the rest.</summary>
        private const int NamesShown = 20;

        private static readonly string[] CountNames = { "total", "passed", "failed", "skipped", "inconclusive" };

        private static readonly string[] GuardTests = { CultureNameTest, HostilityTest };

        /// <summary>What keeps the report from proving the expected run; empty when it proves it.</summary>
        public static IReadOnlyList<string> Problems(string xml, ExpectedTestRun expected)
        {
            XDocument report;
            try
            {
                report = XDocument.Parse(xml);
            }
            catch (XmlException error)
            {
                // A report cut short by a crash or a timeout: the parser's own message says where it ends.
                return new[] { "does not parse as XML: " + error.Message };
            }

            if (!(report.Root is XElement run) || run.Name.LocalName != "test-run")
            {
                return new[] { "is not an NUnit 3 report: its root element is not test-run" };
            }

            if (!(CountOf(run, "total") is int total && CountOf(run, "passed") is int passed && CountOf(run, "failed") is int failed
                && CountOf(run, "skipped") is int skipped && CountOf(run, "inconclusive") is int inconclusive))
            {
                return CountNames.Where(name => CountOf(run, name) is null)
                    .Select(name => "its test-run element has no whole-number " + name + " count")
                    .ToList();
            }

            var counts = new RunCounts { Total = total, Passed = passed, Failed = failed, Skipped = skipped, Inconclusive = inconclusive };
            IReadOnlyList<XElement> cases = run.Descendants("test-case").ToList();
            return CountProblems(counts, cases)
                .Concat(ResultProblems(run))
                .Concat(AssemblyProblems(run, expected))
                .Concat(CultureGuardProblems(cases, expected))
                .ToList();
        }

        private static IEnumerable<string> CountProblems(RunCounts counts, IReadOnlyList<XElement> cases)
        {
            if (counts.Total == 0)
            {
                yield return "ran no test (total is 0)";
            }

            if (counts.Failed > 0)
            {
                yield return Text(counts.Failed) + " failed: " + Names(TestsWithResult(cases, "Failed"));
            }

            if (counts.Skipped > 0)
            {
                yield return Text(counts.Skipped) + " skipped or ignored, which no check allows: " + Names(TestsWithResult(cases, "Skipped"));
            }

            if (counts.Inconclusive > 0)
            {
                yield return Text(counts.Inconclusive) + " inconclusive: " + Names(TestsWithResult(cases, "Inconclusive"));
            }

            if (counts.Passed != counts.Total)
            {
                yield return Text(counts.Passed) + " of " + Text(counts.Total) + " passed";
            }
        }

        /// <summary>
        /// The run's own result must be Passed, and each suite that failed outside its tests is named. A test's failure
        /// gives each suite above it the result Failed with site Child, and its counts already name the test.
        /// </summary>
        private static IEnumerable<string> ResultProblems(XElement run)
        {
            string? result = (string?)run.Attribute("result");
            if (result != PassedResult)
            {
                yield return result is null ? "its test-run element has no result" : "the run's own result is " + result + ", not " + PassedResult;
            }

            foreach (XElement suite in run.Descendants("test-suite").Where(FailedOutsideItsTests))
            {
                yield return "the suite " + SuiteName(suite) + " failed outside its tests (" + SiteOf(suite) + "): " + FirstLineOfMessage(suite);
            }
        }

        /// <summary>
        /// A suite that failed on its own account: in a one-time set-up or tear-down (site SetUp or TearDown), or with no
        /// site, which is how Unity records an [UnityOneTimeTearDown] that logs an error no LogAssert expects.
        /// </summary>
        private static bool FailedOutsideItsTests(XElement suite) =>
            (string?)suite.Attribute("result") == FailedResult && (string?)suite.Attribute("site") is not (ChildSite or ParentSite);

        private static string SiteOf(XElement suite) => (string?)suite.Attribute("site") is string site ? "site " + site : "no site";

        /// <summary>The suite's full name; an Assembly suite's name, since its full name is the DLL's path on the machine that ran it.</summary>
        private static string SuiteName(XElement suite) =>
            (string?)suite.Attribute("type") == AssemblySuite ? NameOf(suite) : (string?)suite.Attribute("fullname") ?? NameOf(suite);

        private static string FirstLineOfMessage(XElement suite) =>
            ((string?)suite.Element("failure")?.Element("message") ?? string.Empty)
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0)
            ?? "it gives no message";

        /// <summary>The report's test assemblies, as NUnit's Assembly suites name them, must be the expected one alone.</summary>
        private static IEnumerable<string> AssemblyProblems(XElement run, ExpectedTestRun expected)
        {
            string expectedSuite = expected.TestAssembly + ".dll";
            IReadOnlyList<XElement> suites = run.Descendants("test-suite").Where(suite => (string?)suite.Attribute("type") == AssemblySuite).ToList();
            if (!suites.Any(suite => NameOf(suite) == expectedSuite))
            {
                yield return "holds no test of the test assembly " + expectedSuite;
            }

            foreach (XElement other in suites.Where(suite => NameOf(suite) != expectedSuite))
            {
                yield return "runs tests of another assembly, " + NameOf(other) + ", which carry neither [HostileCulture] nor our csc.rsp: "
                    + Names(other.Descendants("test-case").ToList());
            }
        }

        private static IEnumerable<string> CultureGuardProblems(IReadOnlyList<XElement> cases, ExpectedTestRun expected)
        {
            foreach (string test in GuardTests)
            {
                string fullName = expected.CultureGuardFixture + "." + test;
                XElement? guard = cases.FirstOrDefault(testCase => (string?)testCase.Attribute("fullname") == fullName);
                if (guard is null)
                {
                    yield return "holds no test " + fullName + " of the culture guard";
                }
                else if ((string?)guard.Attribute("result") != PassedResult)
                {
                    yield return "the culture guard's " + fullName + " reported " + ((string?)guard.Attribute("result") ?? "no result");
                }
                else if (test == CultureNameTest && CultureProblem(guard, expected.Culture) is string problem)
                {
                    yield return "the culture guard's " + fullName + " " + problem;
                }
            }
        }

        /// <summary>Why the guard's output does not show that the run used the expected culture; null when it shows it.</summary>
        private static string? CultureProblem(XElement guard, string expectedCulture)
        {
            IReadOnlyList<string> cultures = ((string?)guard.Element("output") ?? string.Empty)
                .Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.StartsWith(CultureLinePrefix, StringComparison.Ordinal))
                .Select(line => line[CultureLinePrefix.Length..])
                .ToList();
            if (cultures.Count == 0)
            {
                return "wrote no line \"" + CultureLinePrefix + "...\", so the report does not show the culture the run used";
            }

            IReadOnlyList<string> others = cultures.Where(culture => culture != expectedCulture).ToList();
            return others.Count == 0 ? null : "ran under " + string.Join(" and ", others) + ", not under " + expectedCulture;
        }

        /// <summary>The count the attribute holds, written as a whole number without sign or separators; null when it holds none.</summary>
        private static int? CountOf(XElement run, string attribute) =>
            int.TryParse((string?)run.Attribute(attribute), NumberStyles.None, CultureInfo.InvariantCulture, out int count) ? count : null;

        private static IReadOnlyList<XElement> TestsWithResult(IReadOnlyList<XElement> cases, string result) =>
            cases.Where(testCase => (string?)testCase.Attribute("result") == result).ToList();

        private static string Names(IReadOnlyList<XElement> tests)
        {
            string shown = string.Join(", ", tests.Take(NamesShown).Select(FullNameOf));
            return tests.Count > NamesShown ? shown + " and " + Text(tests.Count - NamesShown) + " more" : shown;
        }

        private static string FullNameOf(XElement testCase) => (string?)testCase.Attribute("fullname") ?? NameOf(testCase);

        private static string NameOf(XElement element) => (string?)element.Attribute("name") ?? "an unnamed " + element.Name.LocalName;

        private static string Text(int count) => count.ToString(CultureInfo.InvariantCulture);

        /// <summary>The five counts of the report's test-run element.</summary>
        private sealed record RunCounts
        {
            public required int Total { get; init; }

            public required int Passed { get; init; }

            public required int Failed { get; init; }

            public required int Skipped { get; init; }

            public required int Inconclusive { get; init; }
        }
    }
}
