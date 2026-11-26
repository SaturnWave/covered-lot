using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;

namespace CoveredLot.UnityChecks.Support
{
    /// <summary>
    /// What Unity leaves for these checks: the assemblies it compiled from every asmdef under Assets/CoveredLot and
    /// Assets/Tests, in Library/ScriptAssemblies, and the NUnit report and editor log of each of the four test runs, in
    /// artifacts/local/unity under the names tools/check.sh gives them (EditMode-tr-TR.xml, EditMode-tr-TR.log, ...).
    /// </summary>
    internal static class UnityOutputs
    {
        public const string ScriptAssembliesFolder = "Library/ScriptAssemblies";
        public const string RunsFolder = "artifacts/local/unity";

        /// <summary>The folders whose asmdefs make the CoveredLot assemblies: the game's and its tests'.</summary>
        private static readonly string[] AssemblyFolders = { "Assets/CoveredLot", "Assets/Tests" };

        private static readonly string[] Modes = { "EditMode", "PlayMode" };

        /// <summary>One per asmdef under the assembly folders, named as the asmdef names it, in ordinal order.</summary>
        public static IReadOnlyList<UnityAssembly> Assemblies() => AssemblyFolders
            .SelectMany(folder => Directory.EnumerateFiles(RepositoryLayout.FullPath(folder), "*.asmdef", SearchOption.AllDirectories))
            .Select(AssemblyOf)
            .OrderBy(assembly => assembly.Name, StringComparer.Ordinal)
            .ToList();

        public static UnityAssembly Assembly(string name) =>
            Assemblies().SingleOrDefault(assembly => assembly.Name == name)
            ?? throw new InvalidOperationException("No asmdef under " + string.Join(" or ", AssemblyFolders) + " names " + name + ".");

        /// <summary>The four runs tools/check.sh makes: EditMode and PlayMode, each under tr-TR and sv-SE.</summary>
        public static IReadOnlyList<TestRun> Runs() =>
            Modes.SelectMany(mode => HostileCulture.AllowedCultureNames.Select(culture => new TestRun(mode, culture))).ToList();

        public static TestRun Run(string name) =>
            Runs().SingleOrDefault(run => run.Name == name) ?? throw new InvalidOperationException("No test run is named " + name + ".");

        /// <summary>A scanner over the assembly Unity built; fails, naming the file, when Unity has not built it.</summary>
        public static AssemblyScanner Scan(string assemblyName)
        {
            UnityAssembly assembly = Assembly(assemblyName);
            if (!File.Exists(assembly.DllPath))
            {
                throw new FileNotFoundException(assembly.DllRelativePath + " is missing: run the Unity tests first, as tools/check.sh does.", assembly.DllPath);
            }

            return new AssemblyScanner(assembly.DllPath);
        }

        private static UnityAssembly AssemblyOf(string asmdef)
        {
            string relative = RepositoryLayout.Relative(asmdef);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmdef, Encoding.UTF8));
            string name = document.RootElement.GetProperty("name").GetString() ?? throw new InvalidDataException(relative + " names no assembly.");
            return new UnityAssembly(name, relative[..relative.LastIndexOf('/')]);
        }
    }

    /// <summary>A CoveredLot assembly: its name, and the folder of its asmdef, from the repository root with '/' separators.</summary>
    internal sealed record UnityAssembly(string Name, string Folder)
    {
        public string DllRelativePath => UnityOutputs.ScriptAssembliesFolder + "/" + Name + ".dll";

        public string DllPath => RepositoryLayout.FullPath(DllRelativePath);

        /// <summary>
        /// Every file whose change Unity compiles into the assembly: the .cs files, the asmdef and csc.rsp in its folder and
        /// below, leaving out a folder below that holds an asmdef of its own.
        /// </summary>
        public IReadOnlyList<string> Sources() => SourcesUnder(RepositoryLayout.FullPath(Folder), isAssemblyFolder: true).ToList();

        private static IEnumerable<string> SourcesUnder(string folder, bool isAssemblyFolder)
        {
            if (!isAssemblyFolder && Directory.EnumerateFiles(folder, "*.asmdef").Any())
            {
                return Array.Empty<string>();
            }

            IEnumerable<string> here = Directory.EnumerateFiles(folder).Where(IsSource);
            return here.Concat(Directory.EnumerateDirectories(folder).SelectMany(below => SourcesUnder(below, isAssemblyFolder: false)));
        }

        private static bool IsSource(string path) =>
            string.Equals(Path.GetExtension(path), ".cs", StringComparison.Ordinal)
            || string.Equals(Path.GetExtension(path), ".asmdef", StringComparison.Ordinal)
            || string.Equals(Path.GetFileName(path), "csc.rsp", StringComparison.Ordinal);
    }

    /// <summary>One of the four Unity test runs: its mode and culture, and the report and log it leaves.</summary>
    internal sealed record TestRun(string Mode, string Culture)
    {
        public string Name => Mode + "-" + Culture;

        public string ReportRelativePath => UnityOutputs.RunsFolder + "/" + Name + ".xml";

        public string LogRelativePath => UnityOutputs.RunsFolder + "/" + Name + ".log";

        /// <summary>The culture guard's fixture in this mode's test assembly, as the report names it.</summary>
        public string CultureGuardClass => "CoveredLot.Tests." + Mode + ".CultureGuardTests";
    }
}
