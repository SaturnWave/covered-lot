using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The results rule (<see cref="UnityTestReport"/>), which UnityChecks applies to the reports of the four Unity test
    /// runs, shown on doctored reports in the form a Unity run writes (dotnet/Shared/Samples/UnityReports): a report of the
    /// EditMode tests under tr-TR in which every test and both tests of the culture guard passed, and the guard wrote that
    /// it ran under tr-TR, has no problem; each way a report can fail to prove that run is reported, a report of another
    /// culture or of the other mode's assembly among them.
    /// </summary>
    public sealed class UnityTestReportTests
    {
        private const string Reports = "dotnet/Shared/Samples/UnityReports/";
        private const string Guard = "CoveredLot.Tests.EditMode.CultureGuardTests.";
        private const string NameTest = Guard + "The_tests_run_under_the_culture_that_PUZZLE_TEST_CULTURE_names";
        private const string HostilityTest = Guard + "The_culture_breaks_at_least_one_invariant_assumption";

        /// <summary>The run every doctored report was written for.</summary>
        private static readonly ExpectedTestRun EditModeUnderTurkish = new() { TestAssembly = "CoveredLot.Tests.EditMode", Culture = "tr-TR" };

        /// <summary>Each doctored report that proves no run, with a fragment of the problem it must be refused for.</summary>
        public static TheoryData<string, string> RefusedReports => new()
        {
            { "failed.xml", "1 failed: CoveredLot.Tests.EditMode.PlantedFailureTests.Fails_on_purpose" },
            { "failed.xml", "2 of 3 passed" },
            { "no-test.xml", "ran no test (total is 0)" },
            { "no-test.xml", "holds no test of the test assembly CoveredLot.Tests.EditMode.dll" },
            { "no-test.xml", "holds no test " + NameTest + " of the culture guard" },
            { "skipped.xml", "1 skipped or ignored, which no check allows: CoveredLot.Tests.EditMode.PlaceholderTests.Comes_later" },
            { "inconclusive.xml", "1 inconclusive: CoveredLot.Tests.EditMode.UndecidedTests.Assumes_too_much" },
            { "variable-unset.xml", "3 failed: " },
            { "variable-unset.xml", "the culture guard's " + NameTest + " reported Failed" },
            { "variable-unset.xml", "the culture guard's " + HostilityTest + " reported Failed" },
            { "no-culture-guard.xml", "holds no test " + NameTest + " of the culture guard" },
            { "no-culture-guard.xml", "holds no test " + HostilityTest + " of the culture guard" },
            { "one-guard-test.xml", "holds no test " + HostilityTest + " of the culture guard" },
            { "no-culture-output.xml", "the culture guard's " + NameTest + " wrote no line \"culture under test: ...\"" },
            { "stray-test.xml", "runs tests of another assembly, Assembly-CSharp-Editor.dll, which carry neither [HostileCulture] nor our csc.rsp: StrayTests.Runs_unguarded" },
            { "cut-short.xml", "does not parse as XML: " },
            { "other-format.xml", "is not an NUnit 3 report: its root element is not test-run" },
            { "no-total.xml", "its test-run element has no whole-number total count" },
        };

        [Fact]
        public void A_report_in_which_every_test_and_the_culture_guard_passed_under_the_culture_asked_for_has_no_problem()
        {
            string report = Text("passed.xml");

            IReadOnlyList<string> problems = UnityTestReport.Problems(report, EditModeUnderTurkish);

            // The report the rule passes holds the culture guard's tests, named as a Unity run names them: their fullname
            // starts with the guard fixture of this test assembly, their classname is the base fixture that declares them.
            Assert.Contains("fullname=\"" + NameTest + "\"", report, StringComparison.Ordinal);
            Assert.Contains("classname=\"CoveredLot.Tests.Support.CultureGuardFixture\"", report, StringComparison.Ordinal);
            Assert.True(problems.Count == 0, "passed.xml is refused: " + string.Join("; ", problems));
        }

        [Theory]
        [MemberData(nameof(RefusedReports))]
        public void Each_report_that_proves_no_run_is_refused_for_its_problem(string report, string problem)
        {
            IReadOnlyList<string> problems = UnityTestReport.Problems(Text(report), EditModeUnderTurkish);

            Assert.True(problems.Any(found => found.Contains(problem, StringComparison.Ordinal)),
                report + " is not refused for '" + problem + "'; its problems: " + (problems.Count == 0 ? "none" : string.Join("; ", problems)));
        }

        /// <summary>The reports of a tr-TR and an sv-SE run differ in nothing else, so a tr-TR report must not pass for an sv-SE run.</summary>
        [Fact]
        public void A_report_of_another_culture_does_not_stand_in_for_this_one()
        {
            IReadOnlyList<string> problems = UnityTestReport.Problems(Text("passed.xml"), EditModeUnderTurkish with { Culture = "sv-SE" });

            Assert.Equal(new[] { "the culture guard's " + NameTest + " ran under tr-TR, not under sv-SE" }, problems);
        }

        /// <summary>A report of the other mode's test assembly does not stand in for this one's.</summary>
        [Fact]
        public void A_report_of_another_test_assembly_does_not_stand_in_for_this_one()
        {
            IReadOnlyList<string> problems = UnityTestReport.Problems(Text("passed.xml"), EditModeUnderTurkish with { TestAssembly = "CoveredLot.Tests.PlayMode" });

            Assert.Contains("holds no test of the test assembly CoveredLot.Tests.PlayMode.dll", problems);
            Assert.Contains(problems, problem => problem.StartsWith("runs tests of another assembly, CoveredLot.Tests.EditMode.dll", StringComparison.Ordinal));
            Assert.Contains("holds no test CoveredLot.Tests.PlayMode.CultureGuardTests.The_tests_run_under_the_culture_that_PUZZLE_TEST_CULTURE_names of the culture guard", problems);
        }

        private static string Text(string report) => File.ReadAllText(RepositoryLayout.FullPath(Reports + report));
    }
}
