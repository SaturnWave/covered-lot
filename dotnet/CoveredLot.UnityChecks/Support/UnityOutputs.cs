using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;

namespace CoveredLot.UnityChecks.Support
{
    /// <summary>The two modes the Unity Test Framework runs tests in; each has a test assembly of its own.</summary>
    internal enum UnityTestMode
    {
        EditMode,
        PlayMode,
    }

    /// <summary>
    /// What Unity leaves for these checks: the assemblies it compiled from every asmdef under Assets/CoveredLot and
    /// Assets/Tests, in Library/ScriptAssemblies, and the NUnit report and editor log of each of the four test runs, in
    /// artifacts/local/unity under the names tools/check.sh gives them (EditMode-tr-TR.xml, EditMode-tr-TR.log, ...).
    /// </summary>
    internal static class UnityOutputs
    {
        public const string ScriptAssembliesFolder = "Library/ScriptAssemblies";
        public const string RunsFolder = "artifacts/local/unity";

        private const string ProjectVersionFile = "ProjectSettings/ProjectVersion.txt";
        private const string EditorVersionKey = "m_EditorVersion: ";

        /// <summary>The folders whose asmdefs make the CoveredLot assemblies: the game's and its tests'.</summary>
        private static readonly string[] AssemblyFolders = { "Assets/CoveredLot", "Assets/Tests" };

        private static readonly UnityTestMode[] Modes = { UnityTestMode.EditMode, UnityTestMode.PlayMode };

        /// <summary>One per asmdef under the assembly folders, named as the asmdef names it, in ordinal order.</summary>
        public static IReadOnlyList<UnityAssembly> Assemblies()
        {
            var assemblies = new List<UnityAssembly>();
            foreach (string folder in AssemblyFolders)
            {
                foreach (string asmdef in Directory.EnumerateFiles(RepositoryLayout.FullPath(folder), "*.asmdef", SearchOption.AllDirectories))
                {
                    assemblies.Add(AssemblyOf(asmdef));
                }
            }

            return assemblies.OrderBy(assembly => assembly.Name, StringComparer.Ordinal).ToList();
        }

        public static UnityAssembly Assembly(string name) =>
            Assemblies().SingleOrDefault(assembly => assembly.Name == name)
            ?? throw new InvalidOperationException("No asmdef under " + string.Join(" or ", AssemblyFolders) + " names " + name + ".");

        /// <summary>The four runs tools/check.sh makes, in its order: EditMode and PlayMode, each under tr-TR and sv-SE.</summary>
        public static IReadOnlyList<TestRun> Runs()
        {
            var runs = new List<TestRun>();
            foreach (UnityTestMode mode in Modes)
            {
                foreach (string culture in HostileCulture.AllowedCultureNames)
                {
                    runs.Add(new TestRun { Mode = mode, Culture = CultureInfo.GetCultureInfo(culture) });
                }
            }

            return runs;
        }

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

        /// <summary>The editor version the project pins (ProjectSettings/ProjectVersion.txt), which the log of every run must name.</summary>
        public static string PinnedEditorVersion()
        {
            string? line = File.ReadLines(RepositoryLayout.FullPath(ProjectVersionFile))
                .FirstOrDefault(candidate => candidate.StartsWith(EditorVersionKey, StringComparison.Ordinal));
            return line is null
                ? throw new InvalidDataException(ProjectVersionFile + " has no '" + EditorVersionKey.TrimEnd() + "' line.")
                : line[EditorVersionKey.Length..].Trim();
        }

        private static UnityAssembly AssemblyOf(string asmdef)
        {
            string relative = RepositoryLayout.Relative(asmdef);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmdef, Encoding.UTF8));
            string name = document.RootElement.GetProperty("name").GetString() ?? throw new InvalidDataException(relative + " names no assembly.");
            return new UnityAssembly { Name = name, Folder = relative[..relative.LastIndexOf('/')] };
        }
    }

    /// <summary>A CoveredLot assembly: its name, and the folder of its asmdef, from the repository root with '/' separators.</summary>
    internal sealed record UnityAssembly
    {
        public required string Name { get; init; }

        public required string Folder { get; init; }

        public string DllRelativePath => UnityOutputs.ScriptAssembliesFolder + "/" + Name + ".dll";

        public string DllPath => RepositoryLayout.FullPath(DllRelativePath);

        /// <summary>
        /// Every file whose change Unity compiles into the assembly: the .cs files, the asmdef and csc.rsp in its folder and
        /// below, leaving out a folder below that holds an asmdef of its own.
        /// </summary>
        public IReadOnlyList<string> Sources()
        {
            var sources = new List<string>();
            AddSources(RepositoryLayout.FullPath(Folder), sources);
            return sources;
        }

        /// <summary>Adds the sources in the folder, and in each folder below it that holds no asmdef of its own.</summary>
        private static void AddSources(string folder, List<string> sources)
        {
            sources.AddRange(Directory.EnumerateFiles(folder).Where(IsSource));
            foreach (string below in Directory.EnumerateDirectories(folder))
            {
                if (!Directory.EnumerateFiles(below, "*.asmdef").Any())
                {
                    AddSources(below, sources);
                }
            }
        }

        private static bool IsSource(string path) =>
            string.Equals(Path.GetExtension(path), ".cs", StringComparison.Ordinal)
            || string.Equals(Path.GetExtension(path), ".asmdef", StringComparison.Ordinal)
            || string.Equals(Path.GetFileName(path), "csc.rsp", StringComparison.Ordinal);
    }

    /// <summary>One of the four Unity test runs: its mode and the culture it was asked for, and the report and log it leaves.</summary>
    internal sealed record TestRun
    {
        public required UnityTestMode Mode { get; init; }

        public required CultureInfo Culture { get; init; }

        /// <summary>The mode as the Test Framework, the test assemblies and the file names spell it.</summary>
        public string ModeName => Mode switch
        {
            UnityTestMode.EditMode => "EditMode",
            UnityTestMode.PlayMode => "PlayMode",
            _ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Not a Unity test mode."),
        };

        public string Name => ModeName + "-" + Culture.Name;

        public string ReportRelativePath => UnityOutputs.RunsFolder + "/" + Name + ".xml";

        public string LogRelativePath => UnityOutputs.RunsFolder + "/" + Name + ".log";

        /// <summary>What the run's report has to show: the tests of this mode's assembly, run under this culture.</summary>
        public ExpectedTestRun Expected => new() { TestAssembly = "CoveredLot.Tests." + ModeName, Culture = Culture.Name };
    }
}
