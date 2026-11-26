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
    /// culture or of the other mode's assembly among them. A fixture that failed in its one-time tear-down leaves every
    /// test passed, and is refused through the run's own result and its own, in both forms Unity writes; a suite that
    /// failed only through a child is not named, and the test assembly's suite is named by its name, not its full name.
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
            { "two-cultures.xml", "the culture guard's " + NameTest + " ran under sv-SE, not under tr-TR" },
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

        /// <summary>
        /// Unity records a failed one-time tear-down on its fixture alone, so every test passed, the counts are whole and the
        /// editor exits 0; only the run's own result and the fixture's show it: with site TearDown for an [OneTimeTearDown],
        /// and with no site for an [UnityOneTimeTearDown] (both forms as a Unity run wrote them). The suites above the
        /// fixture, which failed only through it, are not named.
        /// </summary>
        [Theory]
        [InlineData("teardown-failed.xml", "the suite CoveredLot.Tests.EditMode.PlantedOneTimeTearDownTests failed outside its tests (site TearDown): "
            + "Unhandled log message: '[Error] planted'. Use UnityEngine.TestTools.LogAssert.Expect")]
        [InlineData("unity-teardown-failed.xml", "the suite CoveredLot.Tests.EditMode.PlantedUnityOneTimeTearDownTests failed outside its tests (no site): "
            + "OneTimeTearDown : Unhandled log message: '[Error] planted'. Use UnityEngine.TestTools.LogAssert.Expect")]
        public void A_report_whose_tests_all_passed_is_refused_when_a_fixture_failed_in_its_one_time_tear_down(string report, string suiteProblem)
        {
            IReadOnlyList<string> problems = UnityTestReport.Problems(Text(report), EditModeUnderTurkish);

            Assert.Equal(new[] { "the run's own result is Failed(Child), not Passed", suiteProblem }, problems);
        }

        /// <summary>
        /// An Assembly suite's full name is the DLL's path on the machine that ran it, so a failed one is named by its name,
        /// with the first line of its message.
        /// </summary>
        [Fact]
        public void A_failed_test_assembly_suite_is_named_by_its_name_and_the_first_line_of_its_message()
        {
            IReadOnlyList<string> problems = UnityTestReport.Problems(Text("assembly-failed.xml"), EditModeUnderTurkish);

            Assert.Equal(new[]
            {
                "the run's own result is Failed(Child), not Passed",
                "the suite CoveredLot.Tests.EditMode.dll failed outside its tests (site TearDown): OneTimeTearDown : System.InvalidOperationException : planted",
            }, problems);
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
