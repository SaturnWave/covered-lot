using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using CoveredLot.UnityChecks.Support;
using Xunit;

namespace CoveredLot.UnityChecks
{
    /// <summary>
    /// The verdicts of the four Unity test runs (design section 10.4). The editor exits 0 even when no test ran, so each
    /// run's NUnit report must show at least one test, every test passed (none failed, skipped, ignored or inconclusive) and
    /// the culture guard of that mode among them; and each run's editor log must report no compiler warning about a file
    /// under Assets/CoveredLot or Assets/Tests. CoveredLot.Core.Tests shows both rules on doctored reports and logs
    /// (UnityTestReportTests and UnityEditorLogTests there).
    /// </summary>
    public sealed class TestRunTests
    {
        public static TheoryData<string> RunNames => new(UnityOutputs.Runs().Select(run => run.Name));

        [Theory]
        [MemberData(nameof(RunNames))]
        public void The_run_passed_every_test_and_its_culture_guard(string name)
        {
            TestRun run = UnityOutputs.Run(name);

            IReadOnlyList<string> problems = UnityTestReport.Problems(File.ReadAllText(Existing(run.ReportRelativePath)), run.CultureGuardClass);

            Assert.True(problems.Count == 0, run.ReportRelativePath + ":\n" + string.Join("\n", problems.Select(problem => "  " + problem)));
        }

        [Theory]
        [MemberData(nameof(RunNames))]
        public void The_runs_log_reports_no_compiler_warning_about_our_code(string name)
        {
            TestRun run = UnityOutputs.Run(name);

            IReadOnlyList<string> warnings = UnityEditorLog.OwnCompilerWarnings(File.ReadLines(Existing(run.LogRelativePath)));

            Assert.True(warnings.Count == 0, run.LogRelativePath + " reports compiler warnings about our code:\n" + string.Join("\n", warnings.Select(warning => "  " + warning)));
        }

        /// <summary>The full path of a run's file; fails, naming it from the repository root, when the run left none.</summary>
        private static string Existing(string relativePath)
        {
            string path = RepositoryLayout.FullPath(relativePath);
            Assert.True(File.Exists(path), relativePath + " is missing: run the Unity tests first, as tools/check.sh does.");
            return path;
        }
    }
}
