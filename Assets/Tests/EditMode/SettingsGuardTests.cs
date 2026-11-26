using System;
using System.Linq;
using CoveredLot.Core;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CoveredLot.Tests.EditMode
{
    /// <summary>
    /// The project settings that design section 12 pins in Unity, read through the editor's own APIs (PlayerSettings,
    /// EditorBuildSettings, Application): every row whose "Pinned by" names this guard, the editor version, and Main as the
    /// only scene in the build. ProjectSetup.Apply writes all of them but the API compatibility level, which the template
    /// already holds, and the editor version; the .NET UnityProjectFilesTests pin the rows that the YAML shows plainly. A
    /// failure names the setting and both values.
    /// </summary>
    [HostileCulture]
    public sealed class SettingsGuardTests
    {
        private const string MainScenePath = "Assets/CoveredLot/Scenes/Main.unity";

        [Test]
        public void The_default_orientation_is_portrait()
        {
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait), "PlayerSettings.defaultInterfaceOrientation");
        }

        /// <summary>
        /// Section 12's value, written out: ProjectSetup writes Copy.GameTitle, so a comparison with that constant alone would
        /// pass a changed title too. The title the game shows must still be the product name.
        /// </summary>
        [Test]
        public void The_product_name_is_Covered_Lot_the_title_the_game_shows()
        {
            Assert.That(PlayerSettings.productName, Is.EqualTo("Covered Lot"), "PlayerSettings.productName");
            Assert.That(Copy.GameTitle, Is.EqualTo(PlayerSettings.productName), "Copy.GameTitle, the title the game shows");
        }

        /// <summary>A temporary id that Google Play cannot accept, so no build can ship by mistake; P11 picks the real one.</summary>
        [Test]
        public void The_android_application_id_is_the_temporary_development_id()
        {
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), Is.EqualTo("com.example.coveredlot.dev"),
                "PlayerSettings.GetApplicationIdentifier(Android)");
        }

        [Test]
        public void The_android_build_uses_il2cpp()
        {
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android), Is.EqualTo(ScriptingImplementation.IL2CPP),
                "PlayerSettings.GetScriptingBackend(Android)");
        }

        [Test]
        public void The_android_build_targets_arm64_only()
        {
            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64), "PlayerSettings.Android.targetArchitectures");
        }

        /// <summary>
        /// AndroidSdkVersions starts at AndroidApiLevel25 in 6.3, and the getter reads a lower saved level as 25, so this
        /// sees only a level above the floor; UnityProjectFilesTests pins the saved value.
        /// </summary>
        [Test]
        public void The_android_build_needs_api_level_25()
        {
            Assert.That(PlayerSettings.Android.minSdkVersion, Is.EqualTo(AndroidSdkVersions.AndroidApiLevel25), "PlayerSettings.Android.minSdkVersion");
        }

        /// <summary>The setter parses the category's name ignoring case (ProjectSetup writes "game"), so the guard reads it so.</summary>
        [Test]
        public void The_android_app_category_is_game()
        {
            string category = PlayerSettings.Android.appCategory;

            Assert.That(string.Equals(category, "game", StringComparison.OrdinalIgnoreCase), Is.True,
                "PlayerSettings.Android.appCategory is '" + category + "', not 'game' in any case");
        }

        [Test]
        public void The_android_build_uses_dotnet_standard_2_1()
        {
            Assert.That(PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Android), Is.EqualTo(ApiCompatibilityLevel.NET_Standard),
                "PlayerSettings.GetApiCompatibilityLevel(Android)");
        }

        /// <summary>Minimal strips no user code, so no link.xml is needed.</summary>
        [Test]
        public void The_android_build_strips_managed_code_minimally()
        {
            Assert.That(PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android), Is.EqualTo(ManagedStrippingLevel.Minimal),
                "PlayerSettings.GetManagedStrippingLevel(Android)");
        }

        [Test]
        public void The_editor_is_6000_3_29f1()
        {
            Assert.That(Application.unityVersion, Is.EqualTo("6000.3.29f1"), "Application.unityVersion");
        }

        /// <summary>
        /// ProjectSetup writes the shared list, which every platform and build profile builds from unless it overrides it;
        /// the active profile's list is the one a build takes.
        /// </summary>
        [Test]
        public void Main_is_the_only_scene_in_the_build()
        {
            Assert.That(Describe(EditorBuildSettings.globalScenes), Is.EqualTo(new[] { MainScenePath + " (enabled)" }), "EditorBuildSettings.globalScenes");
            Assert.That(Describe(EditorBuildSettings.scenes), Is.EqualTo(new[] { MainScenePath + " (enabled)" }), "EditorBuildSettings.scenes");
        }

        private static string[] Describe(EditorBuildSettingsScene[] scenes) =>
            scenes.Select(scene => scene.path + (scene.enabled ? " (enabled)" : " (disabled)")).ToArray();
    }
}
