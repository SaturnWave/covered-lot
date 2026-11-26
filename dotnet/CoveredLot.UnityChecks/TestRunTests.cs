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
    /// run's NUnit report must show at least one test, every test passed (none failed, skipped, ignored or inconclusive),
    /// the run's own result Passed and no suite failed outside its tests (Unity records a failed one-time tear-down on its
    /// fixture alone, so the counts stay whole and the editor still exits 0), every test from the run's own test assembly,
    /// and both tests of that mode's culture guard among them, the guard having written that it ran under the run's culture
    /// and no other. Each run's editor log must be a finished log of the editor the
    /// project pins and report no compiler warning about a file under Assets/CoveredLot or Assets/Tests; and the four logs
    /// together must show Unity compiling every CoveredLot assembly, or there is no compiler output for that rule to read.
    /// CoveredLot.Core.Tests shows each rule on doctored reports and logs (UnityTestReportTests and UnityEditorLogTests there).
    /// </summary>
    public sealed class TestRunTests
    {
        public static TheoryData<string> RunNames => new(UnityOutputs.Runs().Select(run => run.Name));

        [Theory]
        [MemberData(nameof(RunNames))]
        public void The_run_passed_every_test_of_its_assembly_and_its_culture_guard_under_its_culture(string name)
        {
            TestRun run = UnityOutputs.Run(name);

            IReadOnlyList<string> problems = UnityTestReport.Problems(File.ReadAllText(Existing(run.ReportRelativePath)), run.Expected);

            Assert.True(problems.Count == 0, run.ReportRelativePath + ":\n" + string.Join("\n", problems.Select(problem => "  " + problem)));
        }

        [Theory]
        [MemberData(nameof(RunNames))]
        public void The_runs_log_is_a_finished_log_of_the_pinned_editor_with_no_compiler_warning_about_our_code(string name)
        {
            TestRun run = UnityOutputs.Run(name);
            IReadOnlyList<string> lines = File.ReadAllLines(Existing(run.LogRelativePath));

            IReadOnlyList<string> problems = UnityEditorLog.FinishedRunProblems(lines, UnityOutputs.PinnedEditorVersion())
                .Concat(UnityEditorLog.OwnCompilerWarnings(lines).Select(warning => "reports a compiler warning about our code, " + warning))
                .ToList();

            Assert.True(problems.Count == 0, run.LogRelativePath + ":\n" + string.Join("\n", problems.Select(problem => "  " + problem)));
        }

        /// <summary>
        /// When no source changed since Unity's last build, Unity copies that build and its log holds no compiler output, so
        /// the warning rule would read nothing. tools/check.sh deletes Unity's build of the CoveredLot assemblies before its
        /// runs, so that its first run compiles every one of them.
        /// </summary>
        [Fact]
        public void The_four_runs_together_compiled_every_CoveredLot_assembly()
        {
            IReadOnlyList<string> assemblies = UnityOutputs.Assemblies().Select(assembly => assembly.Name).ToList();
            var logs = new List<IReadOnlyList<string>>();
            foreach (TestRun run in UnityOutputs.Runs())
            {
                logs.Add(File.ReadAllLines(Existing(run.LogRelativePath)));
            }

            IReadOnlyList<string> uncompiled = UnityEditorLog.UncompiledAssemblies(logs, assemblies);

            Assert.NotEmpty(assemblies);
            Assert.True(uncompiled.Count == 0, "No run's log shows Unity compiling " + string.Join(", ", uncompiled)
                + ", so no log holds their compiler output; run tools/check.sh, which deletes Unity's build of them before its runs.");
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
