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
    /// scans read; and each of the four test runs left its report and its log. A DLL older than its sources would let the
    /// IL checks pass on code that is no longer there. tools/check.sh deletes the reports, the logs and the CoveredLot DLLs
    /// before its Unity runs, so all of them are written again by those runs. Unity compiles by content, so after a source's
    /// time changes without its content (a touch, a checkout) its DLL stays older than it, and a run of these checks alone
    /// fails until tools/check.sh writes the DLL again.
    /// </summary>
    public sealed class InputsTests
    {
        public static TheoryData<string> AssemblyNames => new(UnityOutputs.Assemblies().Select(assembly => assembly.Name));

        public static TheoryData<string> RunNames => new(UnityOutputs.Runs().Select(run => run.Name));

        [Theory]
        [MemberData(nameof(AssemblyNames))]
        public void Unity_built_the_assembly_from_its_current_sources(string name)
        {
            UnityAssembly assembly = UnityOutputs.Assembly(name);
            IReadOnlyList<string> sources = assembly.Sources();

            Assert.True(File.Exists(assembly.DllPath), assembly.DllRelativePath + " is missing: run the Unity tests first, as tools/check.sh does.");
            Assert.NotEmpty(sources);
            DateTime built = File.GetLastWriteTimeUtc(assembly.DllPath);
            string newest = sources.OrderByDescending(File.GetLastWriteTimeUtc).First();
            DateTime changed = File.GetLastWriteTimeUtc(newest);
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

            IReadOnlyList<string> built = Directory.EnumerateFiles(RepositoryLayout.FullPath(UnityOutputs.ScriptAssembliesFolder), "CoveredLot*.dll")
                .Select(dll => Path.GetFileNameWithoutExtension(dll))
                .OrderBy(dll => dll, StringComparer.Ordinal)
                .ToList();

            Assert.NotEmpty(expected);
            Assert.Equal(expected, built);
        }

        [Theory]
        [MemberData(nameof(RunNames))]
        public void The_test_run_left_its_report_and_its_log(string name)
        {
            TestRun run = UnityOutputs.Run(name);

            Assert.True(File.Exists(RepositoryLayout.FullPath(run.ReportRelativePath)), run.ReportRelativePath + " is missing: the " + name + " run wrote no report.");
            Assert.True(File.Exists(RepositoryLayout.FullPath(run.LogRelativePath)), run.LogRelativePath + " is missing: the " + name + " run wrote no log.");
        }

        private static string Time(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " UTC";
    }
}
