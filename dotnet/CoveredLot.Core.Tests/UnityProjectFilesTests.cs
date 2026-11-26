using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using CoveredLot.Core.Tests.Support;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The Unity project's committed files, read as text (design sections 4 and 12): the editor version; the Input
    /// System as the only active input handling; entering Play mode reloads the domain and the scene; the engine DLLs
    /// are referenced only by the assemblies that name them and have their references validated; the URP asset is
    /// saved in the form of the pinned URP, which would otherwise rewrite it in any editor session that does not quit
    /// at once; the package manifest holds no local package, none of the packages design section 12 removes, the
    /// pinned test frameworks and the UI Toolkit module; and every Assets path that a project setting names exists. Because the
    /// repository is public, the project is also linked to no Unity Cloud project, keeps no keystore of its own,
    /// keeps the package policy that was decoded and reviewed, and holds no drive-letter path or user folder in any
    /// text file. The other settings that ProjectSetup applies through the PlayerSettings API are pinned inside Unity, by
    /// the EditMode SettingsGuardTests.
    /// </summary>
    public sealed class UnityProjectFilesTests
    {
        /// <summary>Unity 6.3 shows only these options; 0 (no DisableDomainReload or DisableSceneReload) reads "Reload Domain and Scene".</summary>
        private const string ReloadDomainAndScene = "0";

        /// <summary>
        /// The asset version of the pinned URP, 17.3.0 (its k_LastVersion). A URP upgrade that raises it changes this
        /// value and the upgraded asset in one commit.
        /// </summary>
        private const string UrpAssetVersion = "13";

        /// <summary>
        /// Unity 6.3 writes the package security policy as one encoded line. Decoded when piece 9 was reviewed, this one
        /// holds Unity's default (m_PolicyLevel 0, trust on) and a checksum of it, and nothing tied to an account or a
        /// machine. No check here can read the encoding, so the reviewed bytes are pinned.
        /// </summary>
        private const string ReviewedPackagePolicy =
            "R1JEXQNfDxhSVGNcRhJfDlYORFBpXG42VgkLVhh6VEJRWwwUUjkLPWNCQEpAJV8SWVYIBAcLEVYzXFsDVgUIA1FRVFBVBVIHU1ZUClAEUlRcBlMDBVJSVQk=";

        /// <summary>How many leading bytes git reads to decide that a file is binary: one NUL among them makes it so.</summary>
        private const int BinaryProbeLength = 8000;

        private static readonly ProjectFile ProjectVersionFile = new("ProjectSettings/ProjectVersion.txt");
        private static readonly ProjectFile PlayerSettingsFile = new("ProjectSettings/ProjectSettings.asset");
        private static readonly ProjectFile EditorSettingsFile = new("ProjectSettings/EditorSettings.asset");
        private static readonly ProjectFile PackagePolicyFile = new("ProjectSettings/PackageManager.policy");
        private static readonly ProjectFile UrpAssetFile = new("Assets/Settings/UniversalRP.asset");
        private static readonly ProjectFile ManifestFile = new("Packages/manifest.json");

        /// <summary>The packages design section 12 removes: the template's authoring packages, and one that -createProject adds.</summary>
        private static readonly string[] RemovedPackages =
        {
            "com.unity.collab-proxy",
            "com.unity.visualscripting",
            "com.unity.timeline",
            "com.unity.2d.animation",
            "com.unity.2d.psdimporter",
            "com.unity.2d.spriteshape",
            "com.unity.2d.tilemap.extras",
            "com.unity.2d.aseprite",
            "com.unity.2d.tooling",
            // Added by -createProject, not by the template. Nothing in the game uses it, and its runtime assembly,
            // Unity.Multiplayer.Center.Common, is built for every platform, so a player build would carry it.
            "com.unity.multiplayer.center",
        };

        /// <summary>The folders Unity reads the project from.</summary>
        private static readonly string[] ProjectFolders = { "Assets", "Packages", "ProjectSettings" };

        /// <summary>A path into the Assets folder, from "Assets/" to a quote or the end of the line.</summary>
        private static readonly Regex AssetsPath = new(@"(?<![A-Za-z0-9._/-])Assets/[^'""\r\n]*", RegexOptions.CultureInvariant);

        public static TheoryData<string> EngineDllMetas => new()
        {
            "Assets/Generated/PuzzleCore/PuzzleCore.dll.meta",
            "Assets/Generated/PuzzleCore/PuzzleCore.Jam.dll.meta",
        };

        public static TheoryData<string, string> PinnedPackages => new()
        {
            { "com.unity.test-framework", "1.6.0" },
            { "com.unity.ui.test-framework", "6.3.0" },
        };

        [Fact]
        public void The_project_opens_with_editor_6000_3_29f1()
        {
            AssertSetting(new Setting(ProjectVersionFile, "m_EditorVersion"), "6000.3.29f1");
        }

        [Fact]
        public void The_input_system_is_the_only_active_input_handling()
        {
            AssertSetting(new Setting(PlayerSettingsFile, "  activeInputHandler"), "1");
        }

        /// <summary>
        /// Unity 6.3 keeps m_EnterPlayModeOptionsEnabled at 1 and stores the choice in the options themselves: its
        /// Editor settings page has no checkbox for them any more, and setting enterPlayModeOptionsEnabled to false
        /// writes options 0.
        /// </summary>
        [Fact]
        public void Entering_play_mode_reloads_the_domain_and_the_scene()
        {
            AssertSetting(new Setting(EditorSettingsFile, "  m_EnterPlayModeOptions"), ReloadDomainAndScene);
        }

        /// <summary>Auto Reference off: an assembly compiles against the DLL only if its asmdef names it.</summary>
        [Theory]
        [MemberData(nameof(EngineDllMetas))]
        public void An_engine_dll_is_referenced_only_by_name_and_its_references_are_validated(string meta)
        {
            AssertSetting(new Setting(new ProjectFile(meta), "  isExplicitlyReferenced"), "1");
            AssertSetting(new Setting(new ProjectFile(meta), "  validateReferences"), "1");
        }

        /// <summary>
        /// URP upgrades an asset saved in an older form as soon as the editor loads it, and writes the upgrade in any
        /// session that does not quit at once: a GUI open, Play mode, a test run. Saved in the pinned URP's form, the
        /// asset is left as it is by every session.
        /// </summary>
        [Theory]
        [InlineData("  k_AssetVersion")]
        [InlineData("  k_AssetPreviousVersion")]
        public void The_urp_asset_is_saved_in_the_form_of_the_pinned_urp(string indentedKey)
        {
            AssertSetting(new Setting(UrpAssetFile, indentedKey), UrpAssetVersion);
        }

        /// <summary>A file: dependency points at a folder or tarball on one machine, which a fresh clone does not have.</summary>
        [Fact]
        public void The_manifest_has_no_file_dependency()
        {
            IReadOnlyDictionary<string, string> dependencies = ManifestDependencies();

            IReadOnlyList<string> local = dependencies
                .Where(dependency => dependency.Value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                .Select(dependency => dependency.Key)
                .ToList();

            Assert.NotEmpty(dependencies);
            Assert.True(local.Count == 0, ManifestFile.RelativePath + " installs these from local files: " + string.Join(", ", local));
        }

        [Fact]
        public void The_manifest_has_none_of_the_removed_packages()
        {
            IReadOnlyDictionary<string, string> dependencies = ManifestDependencies();

            IReadOnlyList<string> present = RemovedPackages.Where(dependencies.ContainsKey).ToList();

            Assert.NotEmpty(dependencies);
            Assert.True(present.Count == 0, ManifestFile.RelativePath + " still has " + string.Join(", ", present));
        }

        [Theory]
        [MemberData(nameof(PinnedPackages))]
        public void The_manifest_pins_the_test_framework_versions(string package, string version)
        {
            Assert.True(ManifestDependencies().TryGetValue(package, out string? actual), ManifestFile.RelativePath + " has no " + package);
            Assert.True(actual == version, ManifestFile.RelativePath + " pins " + package + " at " + actual + ", not " + version);
        }

        [Fact]
        public void The_manifest_has_the_ui_toolkit_module()
        {
            Assert.True(ManifestDependencies().ContainsKey("com.unity.modules.uielements"),
                ManifestFile.RelativePath + " has no com.unity.modules.uielements, which UI Toolkit needs");
        }

        /// <summary>A setting that names a deleted or moved asset, such as a template's sample scene, is a dangling reference.</summary>
        [Fact]
        public void Every_assets_path_that_a_project_setting_names_exists()
        {
            var named = new List<string>();
            var missing = new List<string>();
            foreach (string file in RepositoryLayout.Files().Where(known => known.StartsWith("ProjectSettings/", StringComparison.Ordinal)))
            {
                foreach (string path in AssetsPathsIn(file))
                {
                    named.Add(path);
                    string fullPath = RepositoryLayout.FullPath(path);
                    if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                    {
                        missing.Add(file + " names " + path + ", which does not exist");
                    }
                }
            }

            Assert.NotEmpty(named);
            Assert.True(missing.Count == 0, string.Join("\n", missing));
        }

        /// <summary>
        /// Linking the project to Unity Cloud writes its project id, name and organisation here. The failure names the
        /// key, never the value.
        /// </summary>
        [Theory]
        [InlineData("  cloudProjectId")]
        [InlineData("  projectName")]
        [InlineData("  organizationId")]
        public void The_project_is_linked_to_no_unity_cloud_project(string indentedKey)
        {
            string value = ValueOf(new Setting(PlayerSettingsFile, indentedKey));

            Assert.True(value.Length == 0, PlayerSettingsFile.RelativePath + " sets '" + indentedKey.Trim()
                + "': the project is linked to Unity Cloud, and the repository is public. Decide what of the link may be committed before committing it.");
        }

        /// <summary>Design section 12: the development build signs with Unity's debug keystore, and no keystore path is committed.</summary>
        [Fact]
        public void The_android_build_signs_with_the_debug_keystore()
        {
            AssertSetting(new Setting(PlayerSettingsFile, "  androidUseCustomKeystore"), "0");
            AssertSetting(new Setting(PlayerSettingsFile, "  AndroidKeystoreName"), string.Empty);
            AssertSetting(new Setting(PlayerSettingsFile, "  AndroidKeyaliasName"), string.Empty);
        }

        /// <summary>A new policy, or a Unity that encodes it otherwise, fails here until the new bytes are decoded and reviewed.</summary>
        [Fact]
        public void The_package_policy_is_the_reviewed_default()
        {
            Assert.True(File.ReadAllText(PackagePolicyFile.FullPath) == ReviewedPackagePolicy,
                PackagePolicyFile.RelativePath + " is not the reviewed default policy; decode and review it before it is committed.");
        }

        [Fact]
        public void No_text_file_of_the_project_holds_a_drive_letter_path_or_a_user_folder()
        {
            IReadOnlyList<string> files = ProjectTextFiles();
            var offenders = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(RepositoryLayout.FullPath(file));
                if (MachinePaths.DriveLetterOrSharePath.IsMatch(text))
                {
                    offenders.Add(file + " holds a drive-letter path or a network share");
                }

                if (MachinePaths.UserFolder.IsMatch(text))
                {
                    offenders.Add(file + " holds a user folder");
                }
            }

            Assert.All(ProjectFolders, folder => Assert.Contains(files, file => file.StartsWith(folder + "/", StringComparison.Ordinal)));
            Assert.True(offenders.Count == 0, string.Join("\n", offenders));
        }

        /// <summary>Fails, naming the file, the key and both values, unless the setting holds the expected value.</summary>
        private static void AssertSetting(Setting setting, string expected)
        {
            string actual = ValueOf(setting);

            Assert.True(actual == expected, setting + " is '" + actual + "', not '" + expected + "'.");
        }

        /// <summary>
        /// The value on the one line of the file that sets the key, trimmed; Unity writes each setting of these files on a
        /// line of its own, and an empty value as the key, a colon and a space.
        /// </summary>
        private static string ValueOf(Setting setting)
        {
            string prefix = setting.IndentedKey + ":";
            IReadOnlyList<string> lines = File.ReadAllLines(setting.File.FullPath)
                .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            Assert.True(lines.Count == 1, setting + " is set on " + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines, not on one.");
            return lines[0][prefix.Length..].Trim();
        }

        private static IReadOnlyDictionary<string, string> ManifestDependencies()
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(ManifestFile.FullPath));
            return manifest.RootElement.GetProperty("dependencies").EnumerateObject()
                .ToDictionary(dependency => dependency.Name, dependency => dependency.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
        }

        /// <summary>Every path into Assets that the file names, as written.</summary>
        private static IEnumerable<string> AssetsPathsIn(string relativePath)
        {
            var paths = new List<string>();
            foreach (string line in File.ReadLines(RepositoryLayout.FullPath(relativePath)))
            {
                paths.AddRange(AssetsPath.Matches(line).Select(match => match.Value.TrimEnd()));
            }

            return paths;
        }

        /// <summary>Every file git knows under the project folders that git would store as text.</summary>
        private static IReadOnlyList<string> ProjectTextFiles()
        {
            var textFiles = new List<string>();
            foreach (string file in RepositoryLayout.Files().Where(IsInTheProject))
            {
                if (!IsBinary(file))
                {
                    textFiles.Add(file);
                }
            }

            return textFiles;
        }

        private static bool IsInTheProject(string relativePath) =>
            ProjectFolders.Any(folder => relativePath.StartsWith(folder + "/", StringComparison.Ordinal));

        /// <summary>Git's own test: a file is binary when a NUL byte is among its first 8000 bytes.</summary>
        private static bool IsBinary(string relativePath)
        {
            var start = new byte[BinaryProbeLength];
            using FileStream stream = File.OpenRead(RepositoryLayout.FullPath(relativePath));
            int read = stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
            return Array.IndexOf(start, (byte)0, 0, read) >= 0;
        }

        /// <summary>A file of the repository, by its path from the root with '/' separators.</summary>
        private readonly record struct ProjectFile(string RelativePath)
        {
            public string FullPath => RepositoryLayout.FullPath(RelativePath);
        }

        /// <summary>A setting of a Unity text file: the file, and the key as Unity writes it, with its indentation.</summary>
        private readonly record struct Setting(ProjectFile File, string IndentedKey)
        {
            public override string ToString() => File.RelativePath + " '" + IndentedKey.Trim() + "'";
        }
    }
}
