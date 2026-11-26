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
    /// runs, shown on doctored reports in the form a Unity run writes (dotnet/Shared/Samples/UnityReports): a report in which
    /// every test and the culture guard passed has no problem, and each way a report can fail to prove a run is reported.
    /// </summary>
    public sealed class UnityTestReportTests
    {
        private const string Reports = "dotnet/Shared/Samples/UnityReports/";
        private const string CultureGuard = "CoveredLot.Tests.EditMode.CultureGuardTests";

        /// <summary>Each doctored report that proves no run, with a fragment of the problem it must be refused for.</summary>
        public static TheoryData<string, string> RefusedReports => new()
        {
            { "failed.xml", "1 failed: CoveredLot.Tests.EditMode.PlantedFailureTests.Fails_on_purpose" },
            { "failed.xml", "2 of 3 passed" },
            { "no-test.xml", "ran no test (total is 0)" },
            { "no-test.xml", "holds no test of the culture guard " + CultureGuard },
            { "skipped.xml", "1 skipped or ignored, which no check allows: CoveredLot.Tests.EditMode.PlaceholderTests.Comes_later" },
            { "inconclusive.xml", "1 inconclusive: CoveredLot.Tests.EditMode.UndecidedTests.Assumes_too_much" },
            { "variable-unset.xml", "3 failed: " },
            { "variable-unset.xml", "the culture guard's " + CultureGuard + ".The_tests_run_under_the_culture_that_PUZZLE_TEST_CULTURE_names reported Failed" },
            { "no-culture-guard.xml", "holds no test of the culture guard " + CultureGuard },
            { "cut-short.xml", "does not parse as XML: " },
            { "other-format.xml", "is not an NUnit 3 report: its root element is not test-run" },
            { "no-total.xml", "its test-run element has no whole-number total count" },
        };

        [Fact]
        public void A_report_in_which_every_test_and_the_culture_guard_passed_has_no_problem()
        {
            string report = Text("passed.xml");

            IReadOnlyList<string> problems = UnityTestReport.Problems(report, CultureGuard);

            // The report the rule passes holds the culture guard's tests, named as a Unity run names them: their fullname
            // starts with the guard fixture of this test assembly, their classname is the base fixture that declares them.
            Assert.Contains("fullname=\"" + CultureGuard + ".", report, StringComparison.Ordinal);
            Assert.Contains("classname=\"CoveredLot.Tests.Support.CultureGuardFixture\"", report, StringComparison.Ordinal);
            Assert.True(problems.Count == 0, "passed.xml is refused: " + string.Join("; ", problems));
        }

        [Theory]
        [MemberData(nameof(RefusedReports))]
        public void Each_report_that_proves_no_run_is_refused_for_its_problem(string report, string problem)
        {
            IReadOnlyList<string> problems = UnityTestReport.Problems(Text(report), CultureGuard);

            Assert.True(problems.Any(found => found.Contains(problem, StringComparison.Ordinal)),
                report + " is not refused for '" + problem + "'; its problems: " + (problems.Count == 0 ? "none" : string.Join("; ", problems)));
        }

        /// <summary>A guard of the other mode does not stand in for this one's.</summary>
        [Fact]
        public void The_culture_guard_of_another_test_assembly_does_not_count()
        {
            IReadOnlyList<string> problems = UnityTestReport.Problems(Text("passed.xml"), "CoveredLot.Tests.PlayMode.CultureGuardTests");

            Assert.Contains("holds no test of the culture guard CoveredLot.Tests.PlayMode.CultureGuardTests", problems);
        }

        private static string Text(string report) => File.ReadAllText(RepositoryLayout.FullPath(Reports + report));
    }
}
