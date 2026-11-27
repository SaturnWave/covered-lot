using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The Unity side of the compile rules (design sections 3, 8.1, 8.7 and 10a). The three CoveredLot assemblies are Core,
    /// the Client and the Editor, and each asmdef is pinned property by property: Core's keeps it Unity-free and lets it
    /// see only the two engine DLLs, and the .NET build of Core references no more; the Client's overrides the
    /// precompiled references with the two engine DLLs alone, whose types Core's pictures and session hand it, and the
    /// Editor's with none, so it does not compile against the engine DLLs. The three test
    /// assemblies under Assets/Tests (Support, EditMode and PlayMode) are pinned the same way: each is built only with the
    /// tests (UNITY_INCLUDE_TESTS), referenced by no assembly that does not name it, and given NUnit, and the engine DLLs
    /// where its tests call the engine, by name. No other asmdef may sit under Assets/CoveredLot or Assets/Tests. No
    /// assembly allows unsafe code, and every assembly of both folders carries its own csc.rsp, because a csc.rsp at the
    /// Assets root would apply to every package as well. Unity and the .NET build compile the same files into Core: the
    /// .NET build takes every .cs under Assets/CoveredLot/Core, so no other asmdef or asmref may move files in or out of
    /// Core in Unity, and no source may sit where Unity does not import it. Every C# source under Assets lies in the
    /// folder of one of the six asmdefs, and no asmref sits anywhere under Assets, so Unity compiles each source into one
    /// of the six assemblies: none into Assembly-CSharp, and none into a package's assembly. Nor does Unity find code
    /// anywhere else: the Packages folder holds only the manifest and its lock file, so no embedded package is compiled,
    /// and no DLL lies under Assets but the two engine DLLs. Every walk compares extensions in any letter case, as MSBuild
    /// on Windows takes Copy.CS for a C# source.
    /// </summary>
    public sealed class AsmdefGuardTests
    {
        private const string CoveredLotFolder = "Assets/CoveredLot";
        private const string CoreFolder = CoveredLotFolder + "/Core";
        private const string CoreAsmdef = CoreFolder + "/CoveredLot.Core.asmdef";
        private const string ClientAsmdef = CoveredLotFolder + "/Client/CoveredLot.Client.asmdef";
        private const string EditorAsmdef = CoveredLotFolder + "/Editor/CoveredLot.Editor.asmdef";
        private const string TestsFolder = "Assets/Tests";
        private const string SupportAsmdef = TestsFolder + "/Support/CoveredLot.Tests.Support.asmdef";
        private const string EditModeAsmdef = TestsFolder + "/EditMode/CoveredLot.Tests.EditMode.asmdef";
        private const string PlayModeAsmdef = TestsFolder + "/PlayMode/CoveredLot.Tests.PlayMode.asmdef";
        private const string CoreAssembly = "CoveredLot.Core";
        private const string PackagesFolder = "Packages";

        /// <summary>The assembly every netstandard2.1 library references; Unity gives it to every assembly as well.</summary>
        private const string NetStandard = "netstandard";

        /// <summary>The only entries of the Packages folder: the manifest Unity resolves packages from, and its lock file.</summary>
        private static readonly string[] PackagesFolderEntries = { PackagesFolder + "/manifest.json", PackagesFolder + "/packages-lock.json" };

        /// <summary>The only DLLs under Assets: the engine's two, which tools/sync-engine.sh stages and Assets/Generated's checksums pin.</summary>
        private static readonly string[] EngineDlls = { "Assets/Generated/PuzzleCore/PuzzleCore.dll", "Assets/Generated/PuzzleCore/PuzzleCore.Jam.dll" };

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
        /// The Client's asmdef: it references Core and overrides the precompiled references with the two engine DLLs alone,
        /// since the pictures and the session it shows hand it the engine's types (PieceId, Option, ReadOnlyArray); piece
        /// 11 named them here and in the asmdef in one commit. Without the override, Unity hands the game code the package
        /// DLLs whose import settings leave Auto Reference on, NUnit and Mono.Cecil among them (seen in Unity's response
        /// file for the Client when piece 9 was reviewed).
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
            ["precompiledReferences"] = "[\"PuzzleCore.dll\",\"PuzzleCore.Jam.dll\"]",
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

        /// <summary>
        /// The test support assembly: [HostileCulture], TestCulture and the culture guard both test assemblies run. It is
        /// built for every platform, because the PlayMode tests use it, and only with the tests.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> ExpectedSupportAsmdef = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "\"CoveredLot.Tests.Support\"",
            ["rootNamespace"] = "\"CoveredLot.Tests.Support\"",
            ["references"] = "[\"UnityEngine.TestRunner\"]",
            ["includePlatforms"] = "[]",
            ["excludePlatforms"] = "[]",
            ["allowUnsafeCode"] = "false",
            ["overrideReferences"] = "true",
            ["precompiledReferences"] = "[\"nunit.framework.dll\"]",
            ["autoReferenced"] = "false",
            ["defineConstraints"] = "[\"UNITY_INCLUDE_TESTS\"]",
            ["versionDefines"] = "[]",
            ["noEngineReferences"] = "false",
        };

        /// <summary>The EditMode tests: editor only, with the three CoveredLot assemblies, and the engine DLLs for the Core checks.</summary>
        private static readonly IReadOnlyDictionary<string, string> ExpectedEditModeAsmdef = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "\"CoveredLot.Tests.EditMode\"",
            ["rootNamespace"] = "\"CoveredLot.Tests.EditMode\"",
            ["references"] = "[\"CoveredLot.Core\",\"CoveredLot.Client\",\"CoveredLot.Editor\",\"CoveredLot.Tests.Support\","
                + "\"UnityEngine.TestRunner\",\"UnityEditor.TestRunner\",\"Unity.UI.TestFramework.Runtime\"]",
            ["includePlatforms"] = "[\"Editor\"]",
            ["excludePlatforms"] = "[]",
            ["allowUnsafeCode"] = "false",
            ["overrideReferences"] = "true",
            ["precompiledReferences"] = "[\"nunit.framework.dll\",\"PuzzleCore.dll\",\"PuzzleCore.Jam.dll\"]",
            ["autoReferenced"] = "false",
            ["defineConstraints"] = "[\"UNITY_INCLUDE_TESTS\"]",
            ["versionDefines"] = "[]",
            ["noEngineReferences"] = "false",
        };

        /// <summary>The PlayMode tests: every platform, Core and the Client but not the Editor, and no engine DLL.</summary>
        private static readonly IReadOnlyDictionary<string, string> ExpectedPlayModeAsmdef = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "\"CoveredLot.Tests.PlayMode\"",
            ["rootNamespace"] = "\"CoveredLot.Tests.PlayMode\"",
            ["references"] = "[\"CoveredLot.Core\",\"CoveredLot.Client\",\"CoveredLot.Tests.Support\",\"UnityEngine.TestRunner\",\"Unity.UI.TestFramework.Runtime\"]",
            ["includePlatforms"] = "[]",
            ["excludePlatforms"] = "[]",
            ["allowUnsafeCode"] = "false",
            ["overrideReferences"] = "true",
            ["precompiledReferences"] = "[\"nunit.framework.dll\"]",
            ["autoReferenced"] = "false",
            ["defineConstraints"] = "[\"UNITY_INCLUDE_TESTS\"]",
            ["versionDefines"] = "[]",
            ["noEngineReferences"] = "false",
        };

        /// <summary>The csc.rsp options of every CoveredLot assembly, the tests' included; Core also checks overflow, like its .NET build.</summary>
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
        public void The_client_asmdef_references_Core_and_only_the_two_engine_dlls()
        {
            AssertAsmdefHolds(ClientAsmdef, ExpectedClientAsmdef);
        }

        [Fact]
        public void The_editor_asmdef_is_editor_only_and_references_Core_the_Client_and_no_precompiled_dll()
        {
            AssertAsmdefHolds(EditorAsmdef, ExpectedEditorAsmdef);
        }

        [Fact]
        public void The_test_support_asmdef_is_built_only_with_the_tests_for_every_platform()
        {
            AssertAsmdefHolds(SupportAsmdef, ExpectedSupportAsmdef);
        }

        [Fact]
        public void The_edit_mode_test_asmdef_is_editor_only_and_names_the_engine_dlls_its_tests_call()
        {
            AssertAsmdefHolds(EditModeAsmdef, ExpectedEditModeAsmdef);
        }

        [Fact]
        public void The_play_mode_test_asmdef_references_Core_the_Client_and_the_support_and_no_engine_dll()
        {
            AssertAsmdefHolds(PlayModeAsmdef, ExpectedPlayModeAsmdef);
        }

        /// <summary>An asmdef that this class does not pin would be an assembly whose references nothing checks.</summary>
        [Fact]
        public void The_CoveredLot_folder_holds_the_Core_Client_and_Editor_asmdefs_only()
        {
            IReadOnlyList<string> asmdefs = Sorted(RepositoryLayout.FilesWithExtension(CoveredLotFolder, FileExtension.Asmdef).Select(path => RepositoryLayout.Relative(path)));

            Assert.Equal(Sorted(new[] { CoreAsmdef, ClientAsmdef, EditorAsmdef }), asmdefs);
        }

        [Fact]
        public void The_Tests_folder_holds_the_Support_EditMode_and_PlayMode_asmdefs_only()
        {
            IReadOnlyList<string> asmdefs = Sorted(RepositoryLayout.FilesWithExtension(TestsFolder, FileExtension.Asmdef).Select(path => RepositoryLayout.Relative(path)));

            Assert.Equal(Sorted(new[] { SupportAsmdef, EditModeAsmdef, PlayModeAsmdef }), asmdefs);
        }

        /// <summary>
        /// Unity compiles a script outside every asmdef folder into Assembly-CSharp or Assembly-CSharp-Editor. Those carry no
        /// csc.rsp of ours, FixtureGuardTests does not look at them, and a test in them runs without [HostileCulture].
        /// </summary>
        [Fact]
        public void Every_source_under_Assets_lies_in_the_folder_of_one_of_the_six_asmdefs()
        {
            IReadOnlyList<string> folders = new[] { CoreAsmdef, ClientAsmdef, EditorAsmdef, SupportAsmdef, EditModeAsmdef, PlayModeAsmdef }
                .Select(asmdef => asmdef[..(asmdef.LastIndexOf('/') + 1)])
                .ToList();
            IReadOnlyList<string> sources = RepositoryLayout.FilesWithExtension("Assets", FileExtension.CSharp).Select(path => RepositoryLayout.Relative(path)).ToList();

            IReadOnlyList<string> outside = sources.Where(source => !folders.Any(folder => source.StartsWith(folder, StringComparison.Ordinal))).ToList();

            Assert.NotEmpty(sources);
            Assert.True(outside.Count == 0,
                "Unity compiles these into Assembly-CSharp or Assembly-CSharp-Editor, outside every CoveredLot assembly: " + string.Join(", ", outside));
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
            IReadOnlyList<string> asmdefs = RepositoryLayout.FilesWithExtension("Assets", FileExtension.Asmdef);
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
            IEnumerable<string> definitions = RepositoryLayout.FilesWithExtension(CoreFolder, FileExtension.Asmdef)
                .Concat(RepositoryLayout.FilesWithExtension(CoreFolder, FileExtension.Asmref))
                .Select(path => RepositoryLayout.Relative(path));

            Assert.Equal(new[] { CoreAsmdef }, definitions);
        }

        /// <summary>
        /// Unity compiles the folder of an asmref, and the folders below it, into the assembly the asmref names, a package's
        /// included, and not into the assembly of the asmdef above it. A source there still lies in an asmdef's folder, but
        /// escapes that assembly's csc.rsp and every scan of it; an asmref that names Core adds files to Core in Unity only.
        /// The design has no asmref.
        /// </summary>
        [Fact]
        public void No_asmref_lies_under_Assets()
        {
            IReadOnlyList<string> asmrefs = RepositoryLayout.FilesWithExtension("Assets", FileExtension.Asmref).Select(path => RepositoryLayout.Relative(path)).ToList();

            Assert.True(asmrefs.Count == 0,
                "Unity compiles the folder of each of these into the assembly it names, not into the assembly of the asmdef above it: " + string.Join(", ", asmrefs));
        }

        /// <summary>
        /// Unity compiles a package folder under Packages (an embedded package) with no entry in the manifest, into
        /// assemblies of its own that neither our csc.rsp nor any scan reaches; the game takes its packages from the manifest.
        /// </summary>
        [Fact]
        public void The_Packages_folder_holds_only_the_manifest_and_its_lock_file()
        {
            IReadOnlyList<string> entries = Sorted(Directory.EnumerateFileSystemEntries(RepositoryLayout.FullPath(PackagesFolder))
                .Select(path => RepositoryLayout.Relative(path)));

            Assert.Equal(Sorted(PackagesFolderEntries), entries);
        }

        /// <summary>
        /// Unity loads every DLL under Assets as a plug-in that any assembly may be given, and no check reads its code; so no
        /// build output may land there, and the engine's two DLLs are the only ones.
        /// </summary>
        [Fact]
        public void No_dll_lies_under_Assets_but_the_two_engine_dlls()
        {
            IReadOnlyList<string> dlls = Sorted(RepositoryLayout.FilesWithExtension("Assets", FileExtension.Dll).Select(path => RepositoryLayout.Relative(path)));

            Assert.Equal(Sorted(EngineDlls), dlls);
        }

        [Fact]
        public void No_Core_source_lies_where_Unity_does_not_import_it()
        {
            IReadOnlyList<string> sources = RepositoryLayout.FilesWithExtension(CoreFolder, FileExtension.CSharp).Select(path => RepositoryLayout.Relative(path)).ToList();

            IReadOnlyList<string> hidden = sources.Where(UnityAssets.IsHidden).ToList();

            Assert.NotEmpty(sources);
            Assert.True(hidden.Count == 0, "Unity does not import these, but the .NET build compiles them into Core: " + string.Join(", ", hidden));
        }

        [Fact]
        public void Every_asmdef_folder_of_the_game_and_its_tests_has_a_csc_rsp_with_exactly_the_approved_options()
        {
            IReadOnlyList<string> asmdefs = RepositoryLayout.FilesWithExtension(CoveredLotFolder, FileExtension.Asmdef).Concat(RepositoryLayout.FilesWithExtension(TestsFolder, FileExtension.Asmdef)).ToList();
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

            Assert.Contains(asmdefs, asmdef => RepositoryLayout.Relative(asmdef).StartsWith(TestsFolder + "/", StringComparison.Ordinal));
            Assert.Contains(asmdefs, asmdef => RepositoryLayout.Relative(asmdef).StartsWith(CoveredLotFolder + "/", StringComparison.Ordinal));
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

        private static IReadOnlyList<string> PrecompiledReferences()
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(RepositoryLayout.FullPath(CoreAsmdef), Encoding.UTF8));
            return document.RootElement.GetProperty("precompiledReferences").EnumerateArray()
                .Select(reference => reference.GetString())
                .OfType<string>()
                .ToList();
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
