using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using CoveredLot.UnityChecks.Support;
using Xunit;

namespace CoveredLot.UnityChecks
{
    /// <summary>
    /// The inputs of every other check here are present and current (design section 10.4): Unity built one assembly per
    /// CoveredLot asmdef, none older than the newest source of its assembly (.cs, .asmdef, csc.rsp), and each holds code the
    /// scans read; it built no assembly from a script outside the asmdef folders; and each of the four test runs left its
    /// report and its log, both written after the newest CoveredLot DLL. A DLL older than its sources would let the IL checks
    /// pass on code that is no longer there, and a report or log older than the DLLs would judge code that is no longer the
    /// code they scan. tools/check.sh deletes the reports, the logs and Unity's build of the CoveredLot assemblies before its
    /// Unity runs, so its first run compiles the assemblies and every report and log comes after them. Unity compiles by
    /// content, so after a source's time changes without its content (a touch, a checkout) its DLL stays older than it, and
    /// a run of these checks alone fails until tools/check.sh builds the DLL again.
    /// </summary>
    public sealed class InputsTests
    {
        private const string RunTheChecks = "run the Unity tests first, as tools/check.sh does.";

        public static TheoryData<string> AssemblyNames => new(UnityOutputs.Assemblies().Select(assembly => assembly.Name));

        public static TheoryData<string> RunNames => new(UnityOutputs.Runs().Select(run => run.Name));

        [Theory]
        [MemberData(nameof(AssemblyNames))]
        public void Unity_built_the_assembly_from_its_current_sources(string name)
        {
            UnityAssembly assembly = UnityOutputs.Assembly(name);
            IReadOnlyList<string> sources = assembly.Sources();

            Assert.True(File.Exists(assembly.DllPath), assembly.DllRelativePath + " is missing: " + RunTheChecks);
            Assert.NotEmpty(sources);
            DateTime built = File.GetLastWriteTimeUtc(assembly.DllPath);
            (string newest, DateTime changed) = Newest(sources);
            Assert.True(built >= changed, assembly.DllRelativePath + " was written at " + Time(built) + ", before " + RepositoryLayout.Relative(newest)
                + " changed at " + Time(changed) + "; run the Unity tests again, as tools/check.sh does.");
        }

        /// <summary>The scans of the other checks read method bodies, so an assembly whose bodies they cannot read passes them all.</summary>
        [Theory]
        [MemberData(nameof(AssemblyNames))]
        public void The_assembly_holds_code_the_scans_read(string name)
        {
            using AssemblyScanner scanner = UnityOutputs.Scan(name);

            Assert.True(scanner.MemberReferences().Count > 0, UnityOutputs.Assembly(name).DllRelativePath + " names no member in any method body.");
        }

        [Fact]
        public void Unity_built_no_CoveredLot_assembly_that_no_asmdef_names()
        {
            IReadOnlyList<string> expected = UnityOutputs.Assemblies().Select(assembly => assembly.Name).ToList();

            IReadOnlyList<string> built = Directory.EnumerateFiles(ExistingScriptAssembliesFolder(), "CoveredLot*.dll")
                .Select(dll => Path.GetFileNameWithoutExtension(dll))
                .OrderBy(dll => dll, StringComparer.Ordinal)
                .ToList();

            Assert.NotEmpty(expected);
            Assert.Equal(expected, built);
        }

        /// <summary>
        /// Unity builds a script outside every asmdef folder into Assembly-CSharp or Assembly-CSharp-Editor (or their
        /// firstpass forms), which none of these checks scans and whose tests run without [HostileCulture]. tools/check.sh
        /// deletes them before its runs, so one found here comes from those runs.
        /// </summary>
        [Fact]
        public void Unity_built_no_assembly_from_a_script_outside_the_asmdef_folders()
        {
            IReadOnlyList<string> loose = Directory.EnumerateFiles(ExistingScriptAssembliesFolder(), "Assembly-CSharp*.dll")
                .Select(dll => Path.GetFileName(dll))
                .OrderBy(dll => dll, StringComparer.Ordinal)
                .ToList();

            Assert.True(loose.Count == 0, UnityOutputs.ScriptAssembliesFolder + " holds " + string.Join(", ", loose)
                + ", which Unity builds from scripts outside every asmdef folder; move each script into a CoveredLot assembly's folder.");
        }

        [Theory]
        [MemberData(nameof(RunNames))]
        public void The_test_run_left_its_report_and_its_log(string name)
        {
            TestRun run = UnityOutputs.Run(name);

            Assert.True(File.Exists(RepositoryLayout.FullPath(run.ReportRelativePath)), run.ReportRelativePath + " is missing: the " + name + " run wrote no report.");
            Assert.True(File.Exists(RepositoryLayout.FullPath(run.LogRelativePath)), run.LogRelativePath + " is missing: the " + name + " run wrote no log.");
        }

        /// <summary>
        /// A report or a log written before the newest CoveredLot DLL, as after an editor session that rebuilt the DLLs,
        /// judges code that the other checks no longer scan.
        /// </summary>
        [Theory]
        [MemberData(nameof(RunNames))]
        public void The_runs_report_and_log_were_written_after_every_CoveredLot_dll(string name)
        {
            TestRun run = UnityOutputs.Run(name);
            IReadOnlyList<string> dlls = Directory.EnumerateFiles(ExistingScriptAssembliesFolder(), "CoveredLot*.dll").ToList();

            Assert.NotEmpty(dlls);
            (string newestDll, DateTime built) = Newest(dlls);
            foreach (string output in new[] { run.ReportRelativePath, run.LogRelativePath })
            {
                string path = RepositoryLayout.FullPath(output);
                Assert.True(File.Exists(path), output + " is missing: the " + name + " run did not write it.");
                DateTime written = File.GetLastWriteTimeUtc(path);
                Assert.True(written > built, output + " was written at " + Time(written) + ", before " + RepositoryLayout.Relative(newestDll)
                    + " was built at " + Time(built) + ", so it judges other code than these checks scan; run tools/check.sh.");
            }
        }

        /// <summary>The full path of Library/ScriptAssemblies; fails, naming it from the repository root, when Unity has built nothing.</summary>
        private static string ExistingScriptAssembliesFolder()
        {
            string folder = RepositoryLayout.FullPath(UnityOutputs.ScriptAssembliesFolder);
            Assert.True(Directory.Exists(folder), UnityOutputs.ScriptAssembliesFolder + " is missing: " + RunTheChecks);
            return folder;
        }

        /// <summary>The file of the list that was written last, and when; the list must not be empty.</summary>
        private static (string Path, DateTime WrittenUtc) Newest(IReadOnlyList<string> paths)
        {
            (string Path, DateTime WrittenUtc) newest = (paths[0], File.GetLastWriteTimeUtc(paths[0]));
            foreach (string path in paths.Skip(1))
            {
                DateTime written = File.GetLastWriteTimeUtc(path);
                if (written > newest.WrittenUtc)
                {
                    newest = (path, written);
                }
            }

            return newest;
        }

        private static string Time(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " UTC";
    }
}
