using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The Unity side of the compile rules (design sections 3, 8.1 and 10a). The three CoveredLot assemblies are Core,
    /// the Client and the Editor, and each asmdef is pinned property by property: Core's keeps it Unity-free and lets it
    /// see only the two engine DLLs, and the .NET build of Core references no more; the Client's and the Editor's
    /// override the precompiled references with none, so neither compiles against the engine DLLs. No assembly allows
    /// unsafe code, and every CoveredLot assembly carries its own csc.rsp, because a csc.rsp at the Assets root would
    /// apply to every package as well. Unity and the .NET build compile the same files into Core: the .NET build takes
    /// every .cs under Assets/CoveredLot/Core, so no other asmdef or asmref may move files in or out of Core in Unity,
    /// and no source may sit where Unity does not import it.
    /// </summary>
    public sealed class AsmdefGuardTests
    {
        private const string CoveredLotFolder = "Assets/CoveredLot";
        private const string CoreFolder = CoveredLotFolder + "/Core";
        private const string CoreAsmdef = CoreFolder + "/CoveredLot.Core.asmdef";
        private const string ClientAsmdef = CoveredLotFolder + "/Client/CoveredLot.Client.asmdef";
        private const string EditorAsmdef = CoveredLotFolder + "/Editor/CoveredLot.Editor.asmdef";
        private const string CoreAssembly = "CoveredLot.Core";

        /// <summary>The assembly every netstandard2.1 library references; Unity gives it to every assembly as well.</summary>
        private const string NetStandard = "netstandard";

        /// <summary>The guid line of a Unity .meta file.</summary>
        private static readonly Regex MetaGuid = new(@"^guid: (?<guid>[0-9a-f]{32})\r?$", RegexOptions.CultureInvariant | RegexOptions.Multiline);

        /// <summary>Core's asmdef, property by property, each value in compact JSON.</summary>
        private static readonly IReadOnlyDictionary<string, string> ExpectedCoreAsmdef = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "\"CoveredLot.Core\"",
            ["rootNamespace"] = "\"CoveredLot.Core\"",
            ["references"] = "[]",
            ["includePlatforms"] = "[]",
            ["excludePlatforms"] = "[]",
            ["allowUnsafeCode"] = "false",
            ["overrideReferences"] = "true",
            ["precompiledReferences"] = "[\"PuzzleCore.dll\",\"PuzzleCore.Jam.dll\"]",
            ["autoReferenced"] = "false",
            ["defineConstraints"] = "[]",
            ["versionDefines"] = "[]",
            ["noEngineReferences"] = "true",
        };

        /// <summary>
        /// The Client's asmdef: it references Core and overrides the precompiled references with none. Without the
        /// override, Unity hands the game code the package DLLs whose import settings leave Auto Reference on, NUnit and
        /// Mono.Cecil among them (seen in Unity's response file for the Client when piece 9 was reviewed). A piece that
        /// gives the Client the engine DLLs names them here and in the asmdef in one commit.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> ExpectedClientAsmdef = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "\"CoveredLot.Client\"",
            ["rootNamespace"] = "\"CoveredLot.Client\"",
            ["references"] = "[\"CoveredLot.Core\"]",
            ["includePlatforms"] = "[]",
            ["excludePlatforms"] = "[]",
            ["allowUnsafeCode"] = "false",
            ["overrideReferences"] = "true",
            ["precompiledReferences"] = "[]",
            ["autoReferenced"] = "false",
            ["defineConstraints"] = "[]",
            ["versionDefines"] = "[]",
            ["noEngineReferences"] = "false",
        };

        /// <summary>
        /// The Editor's asmdef: editor only, in the CoveredLot.EditorTools namespace, referencing Core and the Client, with
        /// the precompiled references overridden with none. Unity still gives it, as every editor assembly, the Test
        /// Framework's assemblies and their NUnit.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> ExpectedEditorAsmdef = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "\"CoveredLot.Editor\"",
            ["rootNamespace"] = "\"CoveredLot.EditorTools\"",
            ["references"] = "[\"CoveredLot.Core\",\"CoveredLot.Client\"]",
            ["includePlatforms"] = "[\"Editor\"]",
            ["excludePlatforms"] = "[]",
            ["allowUnsafeCode"] = "false",
            ["overrideReferences"] = "true",
            ["precompiledReferences"] = "[]",
            ["autoReferenced"] = "false",
            ["defineConstraints"] = "[]",
            ["versionDefines"] = "[]",
            ["noEngineReferences"] = "false",
        };

        /// <summary>The csc.rsp options of every CoveredLot assembly; Core also checks overflow, like its .NET build.</summary>
        private static readonly string[] OptionsOfEveryAssembly = { "-warnaserror+", "-nullable:enable" };
        private const string CoreOnlyOption = "-checked+";

        /// <summary>A csc.rsp separates its options with spaces and line ends.</summary>
        private static readonly char[] OptionSeparators = { ' ', '\t', '\r', '\n' };

        [Fact]
        public void The_core_asmdef_is_unity_free_and_sees_only_the_two_engine_dlls()
        {
            AssertAsmdefHolds(CoreAsmdef, ExpectedCoreAsmdef);
        }

        [Fact]
        public void The_client_asmdef_references_Core_and_no_precompiled_dll()
        {
            AssertAsmdefHolds(ClientAsmdef, ExpectedClientAsmdef);
        }

        [Fact]
        public void The_editor_asmdef_is_editor_only_and_references_Core_the_Client_and_no_precompiled_dll()
        {
            AssertAsmdefHolds(EditorAsmdef, ExpectedEditorAsmdef);
        }

        /// <summary>An asmdef that this class does not pin would be an assembly whose references nothing checks.</summary>
        [Fact]
        public void The_CoveredLot_folder_holds_the_Core_Client_and_Editor_asmdefs_only()
        {
            IReadOnlyList<string> asmdefs = Sorted(FilesWithExtension(CoveredLotFolder, ".asmdef").Select(path => RepositoryLayout.Relative(path)));

            Assert.Equal(Sorted(new[] { CoreAsmdef, ClientAsmdef, EditorAsmdef }), asmdefs);
        }

        /// <summary>
        /// Unity gives Core only what its asmdef names (overrideReferences, noEngineReferences), so the .NET build may
        /// use no more, or a test could pass on code Unity cannot compile. The compiler drops a reference that nothing
        /// uses, so this is a subset, not an equality.
        /// </summary>
        [Fact]
        public void The_dotnet_build_of_Core_references_only_netstandard_and_the_dlls_its_asmdef_names()
        {
            ISet<string> allowed = PrecompiledReferences()
                .Select(dll => Path.GetFileNameWithoutExtension(dll))
                .Append(NetStandard)
                .ToHashSet(StringComparer.Ordinal);
            using var core = new AssemblyScanner(typeof(UnknownEngineCaseException).Assembly.Location);

            IReadOnlyList<string> unexpected = core.ReferencedAssemblyNames().Where(name => !allowed.Contains(name)).ToList();

            Assert.True(unexpected.Count == 0,
                CoreAssembly + " references " + string.Join(", ", unexpected) + ", which its asmdef does not give it in Unity.");
        }

        [Fact]
        public void No_asmdef_under_Assets_allows_unsafe_code()
        {
            IReadOnlyList<string> asmdefs = FilesWithExtension("Assets", ".asmdef");
            var unsafeOnes = new List<string>();
            foreach (string asmdef in asmdefs)
            {
                if (CompactProperties(asmdef).TryGetValue("allowUnsafeCode", out string? value) && value != "false")
                {
                    unsafeOnes.Add(RepositoryLayout.Relative(asmdef));
                }
            }

            Assert.NotEmpty(asmdefs);
            Assert.True(unsafeOnes.Count == 0, "these asmdefs allow unsafe code: " + string.Join(", ", unsafeOnes));
        }

        /// <summary>Unity compiles a folder that holds its own asmdef or asmref into another assembly.</summary>
        [Fact]
        public void The_Core_folder_holds_the_Core_asmdef_and_no_other_asmdef_or_asmref()
        {
            IEnumerable<string> definitions = FilesWithExtension(CoreFolder, ".asmdef")
                .Concat(FilesWithExtension(CoreFolder, ".asmref"))
                .Select(path => RepositoryLayout.Relative(path));

            Assert.Equal(new[] { CoreAsmdef }, definitions);
        }

        /// <summary>An asmref adds its folder's files to the assembly it names, in Unity only.</summary>
        [Fact]
        public void No_asmref_under_Assets_adds_files_to_Core()
        {
            ISet<string> namesOfCore = WaysToNameCore();

            IReadOnlyList<string> offenders = FilesWithExtension("Assets", ".asmref")
                .Where(asmref => namesOfCore.Contains(ReferenceOf(asmref)))
                .Select(path => RepositoryLayout.Relative(path))
                .ToList();

            Assert.True(offenders.Count == 0, "these asmrefs add their files to " + CoreAssembly + " in Unity only: " + string.Join(", ", offenders));
        }

        [Fact]
        public void No_Core_source_lies_where_Unity_does_not_import_it()
        {
            IReadOnlyList<string> sources = FilesWithExtension(CoreFolder, ".cs").Select(path => RepositoryLayout.Relative(path)).ToList();

            IReadOnlyList<string> hidden = sources.Where(UnityAssets.IsHidden).ToList();

            Assert.NotEmpty(sources);
            Assert.True(hidden.Count == 0, "Unity does not import these, but the .NET build compiles them into Core: " + string.Join(", ", hidden));
        }

        [Fact]
        public void Every_CoveredLot_asmdef_folder_has_a_csc_rsp_with_exactly_the_approved_options()
        {
            IReadOnlyList<string> asmdefs = FilesWithExtension("Assets/CoveredLot", ".asmdef");
            var problems = new List<string>();
            foreach (string asmdef in asmdefs)
            {
                string assembly = AssemblyName(asmdef);
                string rsp = Path.Join(Path.GetDirectoryName(asmdef.AsSpan()), "csc.rsp");
                if (!File.Exists(rsp))
                {
                    problems.Add(assembly + " has no csc.rsp beside its asmdef");
                    continue;
                }

                IReadOnlyList<string> options = Sorted(File.ReadAllText(rsp, Encoding.UTF8).Split(OptionSeparators, StringSplitOptions.RemoveEmptyEntries));
                IReadOnlyList<string> approved = Sorted(ApprovedOptions(assembly));
                if (!options.SequenceEqual(approved, StringComparer.Ordinal))
                {
                    problems.Add(assembly + "'s csc.rsp holds [" + string.Join(" ", options) + "], not [" + string.Join(" ", approved) + "]");
                }
            }

            Assert.NotEmpty(asmdefs);
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void There_is_no_csc_rsp_at_the_Assets_root()
        {
            Assert.False(File.Exists(RepositoryLayout.FullPath("Assets/csc.rsp")),
                "Assets/csc.rsp applies to every assembly, packages included; give each CoveredLot asmdef its own csc.rsp.");
        }

        /// <summary>Fails, naming the asmdef and each property that is missing, extra or set to another value, unless it holds exactly the pinned ones.</summary>
        private static void AssertAsmdefHolds(string relativeAsmdef, IReadOnlyDictionary<string, string> expected)
        {
            IReadOnlyDictionary<string, string> actual = CompactProperties(RepositoryLayout.FullPath(relativeAsmdef));

            IReadOnlyList<string> differences = expected.Keys.Union(actual.Keys, StringComparer.Ordinal)
                .Where(property => ValueOrMissing(actual, property) != ValueOrMissing(expected, property))
                .OrderBy(property => property, StringComparer.Ordinal)
                .Select(property => relativeAsmdef + " sets " + property + " to " + ValueOrMissing(actual, property) + ", not " + ValueOrMissing(expected, property))
                .ToList();

            Assert.True(differences.Count == 0, string.Join("\n", differences));
        }

        private static string ValueOrMissing(IReadOnlyDictionary<string, string> properties, string property) =>
            properties.TryGetValue(property, out string? value) ? value : "(missing)";

        private static IEnumerable<string> ApprovedOptions(string assembly) =>
            assembly == CoreAssembly ? OptionsOfEveryAssembly.Append(CoreOnlyOption) : OptionsOfEveryAssembly;

        /// <summary>Every file under the folder whose extension is exactly the given one, compared ordinally.</summary>
        private static IReadOnlyList<string> FilesWithExtension(string relativeFolder, string extension) => Directory
            .EnumerateFiles(RepositoryLayout.FullPath(relativeFolder), "*" + extension, SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), extension, StringComparison.Ordinal))
            .ToList();

        private static IReadOnlyList<string> PrecompiledReferences()
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(RepositoryLayout.FullPath(CoreAsmdef), Encoding.UTF8));
            return document.RootElement.GetProperty("precompiledReferences").EnumerateArray()
                .Select(reference => reference.GetString())
                .OfType<string>()
                .ToList();
        }

        /// <summary>The assembly an asmref names: an assembly name, or "GUID:" and the guid of the asmdef's .meta file.</summary>
        private static string ReferenceOf(string asmref)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmref, Encoding.UTF8));
            return document.RootElement.TryGetProperty("reference", out JsonElement reference) ? reference.GetString() ?? string.Empty : string.Empty;
        }

        /// <summary>Core's assembly name and, once Unity has written the asmdef's .meta file, its GUID reference.</summary>
        private static ISet<string> WaysToNameCore()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CoreAssembly };
            string meta = RepositoryLayout.FullPath(CoreAsmdef + ".meta");
            if (File.Exists(meta) && MetaGuid.Match(File.ReadAllText(meta, Encoding.UTF8)) is { Success: true } guidLine)
            {
                names.Add("GUID:" + guidLine.Groups["guid"].Value);
            }

            return names;
        }

        private static string AssemblyName(string asmdef)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmdef, Encoding.UTF8));
            return document.RootElement.GetProperty("name").GetString() ?? asmdef;
        }

        /// <summary>The asmdef's top-level properties, each value written as compact JSON.</summary>
        private static IReadOnlyDictionary<string, string> CompactProperties(string asmdef)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmdef, Encoding.UTF8));
            return document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => Compact(property.Value), StringComparer.Ordinal);
        }

        private static string Compact(JsonElement value)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                value.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        private static IReadOnlyList<string> Sorted(IEnumerable<string> options) =>
            options.OrderBy(option => option, StringComparer.Ordinal).ToList();
    }
}
