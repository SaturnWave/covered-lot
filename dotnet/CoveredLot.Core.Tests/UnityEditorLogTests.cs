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
    /// The log rule (<see cref="UnityEditorLog"/>), which UnityChecks applies to the editor logs of the four Unity test runs,
    /// shown on doctored logs (dotnet/Shared/Samples/UnityLogs): a finished log of the pinned editor passes, and an empty
    /// log, a log cut short and a log of another editor are refused; warnings in packages and other folders, and lines that
    /// name our folders without a warning, pass; each warning about a file under Assets/CoveredLot or Assets/Tests is
    /// reported with its line, its file and its code, whichever slash the path is written with, unless the log compiles
    /// the same assembly again later; and the assemblies a log compiles are those Bee writes a compile line for, so a log
    /// in which Bee only copies its earlier build compiles none, and Bee's "[BUSY ...]" progress line is no compile line.
    /// </summary>
    public sealed class UnityEditorLogTests
    {
        private const string Logs = "dotnet/Shared/Samples/UnityLogs/";

        /// <summary>The editor the doctored logs were written for; the project's own pin is checked by UnityProjectFilesTests.</summary>
        private const string EditorVersion = "6000.3.29f1";

        private static readonly string[] CoveredLotAssemblies =
        {
            "CoveredLot.Core", "CoveredLot.Client", "CoveredLot.Editor",
            "CoveredLot.Tests.Support", "CoveredLot.Tests.EditMode", "CoveredLot.Tests.PlayMode",
        };

        public static TheoryData<string> FinishedLogs => new() { "clean.txt", "own-warnings.txt", "stale-pass.txt", "no-compile.txt" };

        /// <summary>Each doctored log that proves no finished run of the pinned editor, with a fragment of the problem it must be refused for.</summary>
        public static TheoryData<string, string> UnfinishedLogs => new()
        {
            { "empty.txt", "is empty" },
            { "cut-short.txt", "never records the end of a test run (\"Test run completed.\")" },
            { "other-editor.txt", "has no start line of editor 6000.3.29f1" },
        };

        [Theory]
        [MemberData(nameof(FinishedLogs))]
        public void A_finished_log_of_the_pinned_editor_has_no_run_problem(string log)
        {
            IReadOnlyList<string> problems = UnityEditorLog.FinishedRunProblems(Lines(log), EditorVersion);

            Assert.True(problems.Count == 0, log + " is refused: " + string.Join("; ", problems));
        }

        [Theory]
        [MemberData(nameof(UnfinishedLogs))]
        public void Each_log_that_proves_no_finished_run_is_refused_for_its_problem(string log, string problem)
        {
            IReadOnlyList<string> problems = UnityEditorLog.FinishedRunProblems(Lines(log), EditorVersion);

            Assert.True(problems.Any(found => found.Contains(problem, StringComparison.Ordinal)),
                log + " is not refused for '" + problem + "'; its problems: " + (problems.Count == 0 ? "none" : string.Join("; ", problems)));
        }

        [Fact]
        public void A_log_with_warnings_only_in_packages_and_other_folders_passes()
        {
            IReadOnlyList<string> lines = Lines("clean.txt");

            IReadOnlyList<string> found = UnityEditorLog.OwnCompilerWarnings(lines);

            // The log holds compiler warnings and names our folders, just never both on one line.
            Assert.Contains(lines, line => line.Contains("warning CS", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("Assets/CoveredLot/", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("Assets/Tests/", StringComparison.Ordinal));
            Assert.True(found.Count == 0, "clean.txt is refused: " + string.Join("; ", found));
        }

        [Fact]
        public void Each_warning_about_our_code_is_reported_with_its_line_file_and_code()
        {
            IReadOnlyList<string> found = UnityEditorLog.OwnCompilerWarnings(Lines("own-warnings.txt"));

            Assert.Equal(
                new[]
                {
                    "line 7: Assets/CoveredLot/Client/GameRoot.cs warning CS0168",
                    "line 9: Assets/Tests/EditMode/CoreInUnityTests.cs warning CS0219",
                    @"line 12: Assets\CoveredLot\Core\Copy.cs warning CS0414",
                },
                found);
        }

        /// <summary>
        /// The support assembly's warning comes from a compile that Bee repeats after its first pass ran on an out-of-date
        /// build graph, so it does not count; the client's comes from its only compile, and counts.
        /// </summary>
        [Fact]
        public void A_compile_that_the_log_repeats_for_the_same_assembly_does_not_count()
        {
            IReadOnlyList<string> lines = Lines("stale-pass.txt");

            IReadOnlyList<string> found = UnityEditorLog.OwnCompilerWarnings(lines);

            Assert.Contains(lines, line => line.Contains(@"Assets\Tests\Support\TestCulture.cs(23,29): warning CS8632", StringComparison.Ordinal));
            Assert.Equal(new[] { @"line 7: Assets\CoveredLot\Client\GameRoot.cs warning CS0168" }, found);
        }

        [Fact]
        public void The_assemblies_a_log_compiles_are_those_bee_writes_a_compile_line_for()
        {
            Assert.Equal(
                new[] { "CoveredLot.Client", "CoveredLot.Core", "CoveredLot.Editor", "CoveredLot.Tests.EditMode", "CoveredLot.Tests.PlayMode",
                    "CoveredLot.Tests.Support", "ThirdParty.Plugin", "Unity.InputSystem", "Unity.RenderPipelines.Core.Runtime" },
                UnityEditorLog.CompiledAssemblies(Lines("clean.txt")));
            Assert.Equal(new[] { "CoveredLot.Client", "CoveredLot.Tests.Support" }, UnityEditorLog.CompiledAssemblies(Lines("stale-pass.txt")));
        }

        /// <summary>
        /// A log in which Bee only copies its earlier build holds no compiler output, so it shows nothing about warnings; the
        /// "[BUSY ...]" progress line Bee writes while a compile runs is no compile of its own.
        /// </summary>
        [Fact]
        public void Logs_that_only_copy_an_earlier_build_leave_every_assembly_uncompiled()
        {
            IReadOnlyList<string> copiesOnly = Lines("no-compile.txt");

            Assert.Contains(copiesOnly, line => line.Contains("CopyFiles Library/ScriptAssemblies/CoveredLot.Core.dll", StringComparison.Ordinal));
            Assert.Contains(copiesOnly, line => line.StartsWith("[BUSY ", StringComparison.Ordinal)
                && line.Contains("] Csc Library/Bee/artifacts/0123abcd.dag/CoveredLot.Core.dll", StringComparison.Ordinal));
            Assert.Equal(CoveredLotAssemblies, UnityEditorLog.UncompiledAssemblies(new[] { copiesOnly }, CoveredLotAssemblies));
            Assert.Equal(new[] { "CoveredLot.Core", "CoveredLot.Editor", "CoveredLot.Tests.EditMode", "CoveredLot.Tests.PlayMode" },
                UnityEditorLog.UncompiledAssemblies(new[] { copiesOnly, Lines("stale-pass.txt") }, CoveredLotAssemblies));
            Assert.Empty(UnityEditorLog.UncompiledAssemblies(new[] { copiesOnly, Lines("clean.txt") }, CoveredLotAssemblies));
        }

        private static IReadOnlyList<string> Lines(string log) => File.ReadAllLines(RepositoryLayout.FullPath(Logs + log));
    }
}
