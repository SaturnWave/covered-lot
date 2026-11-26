using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CoveredLot.Core.Tests.Support;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The Unity project's committed settings that no editor run checks (design sections 4 and 12), read from the files:
    /// the editor version; the Input System as the only active input handling; entering Play mode reloads the domain
    /// and the scene; the engine DLLs are referenced only by the assemblies that name them and have their references
    /// validated; the package manifest holds no local package, none of the template's authoring packages, the pinned
    /// test frameworks and the UI Toolkit module. And because the repository is public, no text file of the project
    /// holds a drive-letter path or a user folder. The settings that ProjectSetup applies through the PlayerSettings
    /// API are pinned inside Unity instead (piece 10).
    /// </summary>
    public sealed class UnityProjectFilesTests
    {
        private const string ManifestPath = "Packages/manifest.json";

        /// <summary>Unity 6.3 shows only these options; 0 (no DisableDomainReload or DisableSceneReload) reads "Reload Domain and Scene".</summary>
        private const string ReloadDomainAndScene = "0";

        /// <summary>The template's authoring packages, which design section 12 removes.</summary>
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
        };

        /// <summary>The folders Unity reads the project from.</summary>
        private static readonly string[] ProjectFolders = { "Assets", "Packages", "ProjectSettings" };

        /// <summary>The types .gitattributes stores as binary; every other file of the project is text.</summary>
        private static readonly string[] BinaryExtensions = { ".dll", ".png", ".jpg", ".jpeg", ".ttf", ".otf" };

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
            Assert.Equal("6000.3.29f1", SettingOf("ProjectSettings/ProjectVersion.txt", "m_EditorVersion"));
        }

        [Fact]
        public void The_input_system_is_the_only_active_input_handling()
        {
            Assert.Equal("1", SettingOf("ProjectSettings/ProjectSettings.asset", "  activeInputHandler"));
        }

        /// <summary>
        /// Unity 6.3 keeps m_EnterPlayModeOptionsEnabled at 1 and stores the choice in the options themselves: its
        /// Editor settings page has no checkbox for them any more, and setting enterPlayModeOptionsEnabled to false
        /// writes options 0.
        /// </summary>
        [Fact]
        public void Entering_play_mode_reloads_the_domain_and_the_scene()
        {
            Assert.Equal(ReloadDomainAndScene, SettingOf("ProjectSettings/EditorSettings.asset", "  m_EnterPlayModeOptions"));
        }

        /// <summary>Auto Reference off: an assembly compiles against the DLL only if its asmdef names it.</summary>
        [Theory]
        [MemberData(nameof(EngineDllMetas))]
        public void An_engine_dll_is_referenced_only_by_name_and_its_references_are_validated(string meta)
        {
            Assert.Equal("1", SettingOf(meta, "  isExplicitlyReferenced"));
            Assert.Equal("1", SettingOf(meta, "  validateReferences"));
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
            Assert.True(local.Count == 0, ManifestPath + " installs these from local files: " + string.Join(", ", local));
        }

        [Fact]
        public void The_manifest_has_none_of_the_removed_authoring_packages()
        {
            IReadOnlyDictionary<string, string> dependencies = ManifestDependencies();

            IReadOnlyList<string> present = RemovedPackages.Where(dependencies.ContainsKey).ToList();

            Assert.NotEmpty(dependencies);
            Assert.True(present.Count == 0, ManifestPath + " still has " + string.Join(", ", present));
        }

        [Theory]
        [MemberData(nameof(PinnedPackages))]
        public void The_manifest_pins_the_test_framework_versions(string package, string version)
        {
            Assert.True(ManifestDependencies().TryGetValue(package, out string? actual), ManifestPath + " has no " + package);
            Assert.Equal(version, actual);
        }

        [Fact]
        public void The_manifest_has_the_ui_toolkit_module()
        {
            Assert.Contains("com.unity.modules.uielements", ManifestDependencies().Keys);
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

        /// <summary>
        /// The value of the one line of the file that sets the key, written with its indentation; Unity writes each
        /// setting of these files on a line of its own.
        /// </summary>
        private static string SettingOf(string relativePath, string indentedKey)
        {
            string prefix = indentedKey + ":";
            IReadOnlyList<string> lines = File.ReadAllLines(RepositoryLayout.FullPath(relativePath))
                .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            Assert.True(lines.Count == 1,
                relativePath + " sets '" + indentedKey.Trim() + "' on " + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines, not on one.");
            return lines[0][prefix.Length..].Trim();
        }

        private static IReadOnlyDictionary<string, string> ManifestDependencies()
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(RepositoryLayout.FullPath(ManifestPath)));
            return manifest.RootElement.GetProperty("dependencies").EnumerateObject()
                .ToDictionary(dependency => dependency.Name, dependency => dependency.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
        }

        /// <summary>Every file git knows under the project folders, except the binary ones.</summary>
        private static IReadOnlyList<string> ProjectTextFiles() => RepositoryLayout.Files()
            .Where(file => ProjectFolders.Any(folder => file.StartsWith(folder + "/", StringComparison.Ordinal)))
            .Where(file => !BinaryExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
}
