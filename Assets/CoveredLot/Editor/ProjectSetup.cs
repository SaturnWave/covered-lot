using System;
using System.IO;
using System.Linq;
using System.Text;
using CoveredLot.Client;
using CoveredLot.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CoveredLot.EditorTools
{
    /// <summary>
    /// Sets up the Unity project (design sections 8.6 and 12): the player and editor settings this tool owns, the UI
    /// theme, the panel settings, the Main scene, and Main as the only scene in the build. Run it in batch mode with
    /// <c>-executeMethod CoveredLot.EditorTools.ProjectSetup.Apply</c>. It can run again: a setting given the value it
    /// already holds leaves its file as it was, the theme and the panel settings are written only when they differ, and
    /// a Main scene that already holds what this tool builds is kept. It leaves alone two rows of section 12 that the
    /// template already holds: API compatibility at .NET Standard 2.1, and the Input System as the only active input
    /// handling.
    /// </summary>
    public static class ProjectSetup
    {
        /// <summary>A temporary id that Google Play cannot accept, so no build can ship by mistake; P11 picks the real one.</summary>
        private const string AndroidApplicationId = "com.example.coveredlot.dev";

        /// <summary>PlayerSettings.Android.appCategory parses this name, ignoring case, into Unity's Game category.</summary>
        private const string GameCategory = "game";

        private const string CoveredLotFolder = "Assets/CoveredLot";
        private const string ThemePath = CoveredLotFolder + "/UI/CoveredLotTheme.tss";
        private const string PanelPath = CoveredLotFolder + "/UI/CoveredLotPanel.asset";
        private const string ScenesFolderName = "Scenes";
        private const string ScenesFolder = CoveredLotFolder + "/" + ScenesFolderName;
        private const string MainScenePath = ScenesFolder + "/Main.unity";
        private const string CameraName = "Main Camera";
        private const string CameraTag = "MainCamera";
        private const string GameName = "Game";

        /// <summary>Unity's default runtime theme, then the game's style sheet, which sits in the same folder.</summary>
        private const string ThemeText = "@import url(\"unity-theme://default\");\n@import url(\"CoveredLot.uss\");\nVisualElement {}\n";

        /// <summary>Matched on the width (0), not the height (1), so the panel is always 360 units wide.</summary>
        private const float MatchWidth = 0f;

        /// <summary>A 360dp-wide portrait phone, so one panel unit is about one dp (design section 6).</summary>
        private static readonly Vector2Int ReferenceResolution = new Vector2Int(360, 800);

        /// <summary>The page colour of CoveredLot.uss (#F4F1EA): the camera clears to the colour the panel draws.</summary>
        private static readonly Color32 PageColour = new Color32(0xF4, 0xF1, 0xEA, 0xFF);

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static void Apply()
        {
            ApplyPlayerSettings();
            ApplyEditorSettings();
            ThemeStyleSheet theme = WriteTheme();
            WritePanelSettings(theme);
            WriteMainScene();
            BuildMainSceneOnly();
            AssetDatabase.SaveAssets();
            Debug.Log("ProjectSetup: applied");
        }

        /// <summary>
        /// A portrait game named after the title; the Android development build uses IL2CPP for ARM64 only, from API 25,
        /// in the Game category, strips no user code (Minimal, so no link.xml is needed), and is signed with the debug
        /// keystore.
        /// </summary>
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.productName = Copy.GameTitle;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.appCategory = GameCategory;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            PlayerSettings.Android.useCustomKeystore = false;
        }

        /// <summary>
        /// Entering Play mode reloads the domain and the scene, and new scripts get Unix line ends. Unity 6.3's Editor
        /// settings page shows only the options, where None reads "Reload Domain and Scene"; setting
        /// enterPlayModeOptionsEnabled to false writes the same None and leaves m_EnterPlayModeOptionsEnabled at 1.
        /// </summary>
        private static void ApplyEditorSettings()
        {
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
            EditorSettings.lineEndingsForNewScripts = LineEndingsMode.Unix;
        }

        private static ThemeStyleSheet WriteTheme()
        {
            byte[] wanted = Utf8WithoutBom.GetBytes(ThemeText);
            if (File.Exists(ThemePath) && File.ReadAllBytes(ThemePath).SequenceEqual(wanted))
            {
                Debug.Log("ProjectSetup: kept " + ThemePath);
            }
            else
            {
                File.WriteAllBytes(ThemePath, wanted);
                AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate);
                Debug.Log("ProjectSetup: wrote " + ThemePath);
            }

            return LoadAsset<ThemeStyleSheet>(ThemePath);
        }

        /// <summary>An existing asset is updated in place, so the scene's reference to it stays valid.</summary>
        private static void WritePanelSettings(ThemeStyleSheet theme)
        {
            PanelSettings? existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (existing == null)
            {
                PanelSettings created = ScriptableObject.CreateInstance<PanelSettings>();
                Configure(created, theme);
                AssetDatabase.CreateAsset(created, PanelPath);
                Debug.Log("ProjectSetup: wrote " + PanelPath);
                return;
            }

            if (IsConfigured(existing, theme))
            {
                Debug.Log("ProjectSetup: kept " + PanelPath);
                return;
            }

            Configure(existing, theme);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            Debug.Log("ProjectSetup: updated " + PanelPath);
        }

        /// <summary>A screen-space overlay that scales with the screen from the reference size, matched on the width, in the theme.</summary>
        private static void Configure(PanelSettings panel, ThemeStyleSheet theme)
        {
            panel.renderMode = PanelRenderMode.ScreenSpaceOverlay;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = ReferenceResolution;
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = MatchWidth;
            panel.themeStyleSheet = theme;
        }

        private static bool IsConfigured(PanelSettings panel, ThemeStyleSheet theme) =>
            panel.renderMode == PanelRenderMode.ScreenSpaceOverlay
            && panel.scaleMode == PanelScaleMode.ScaleWithScreenSize
            && panel.referenceResolution == ReferenceResolution
            && panel.screenMatchMode == PanelScreenMatchMode.MatchWidthOrHeight
            && panel.match == MatchWidth
            && panel.themeStyleSheet == theme;

        /// <summary>
        /// Main.unity: a camera that clears to the page colour, and the Game object with the UIDocument on the panel
        /// settings and GameRoot. There is no EventSystem: UI Toolkit's own event system reads the Input System. Unity
        /// gives every new object a new file id, so writing the scene again would change the file on every run: a scene
        /// that already holds exactly these two objects is kept, and any other scene at that path is built again.
        /// </summary>
        private static void WriteMainScene()
        {
            if (File.Exists(MainScenePath) && HoldsTheCameraAndTheGame(EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single)))
            {
                Debug.Log("ProjectSetup: kept " + MainScenePath);
                return;
            }

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder(CoveredLotFolder, ScenesFolderName);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            AddGame(PanelAfterSceneChange());
            if (!EditorSceneManager.SaveScene(scene, MainScenePath))
            {
                throw new InvalidOperationException("ProjectSetup could not save " + MainScenePath + ".");
            }

            Debug.Log("ProjectSetup: wrote " + MainScenePath);
        }

        private static bool HoldsTheCameraAndTheGame(Scene scene)
        {
            PanelSettings panel = PanelAfterSceneChange();
            GameObject[] roots = scene.GetRootGameObjects();
            return roots.Length == 2 && roots.Any(IsTheCamera) && roots.Any(root => IsTheGame(root, panel));
        }

        private static bool IsTheCamera(GameObject root) =>
            root.name == CameraName
            && root.CompareTag(CameraTag)
            && root.TryGetComponent(out Camera camera)
            && camera.clearFlags == CameraClearFlags.SolidColor
            && camera.backgroundColor == (Color)PageColour;

        private static bool IsTheGame(GameObject root, PanelSettings panel) =>
            root.name == GameName
            && root.TryGetComponent(out UIDocument document)
            && document.panelSettings == panel
            && root.TryGetComponent(out GameRoot _);

        private static void AddCamera()
        {
            var cameraObject = new GameObject(CameraName) { tag = CameraTag };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PageColour;
        }

        private static void AddGame(PanelSettings panel)
        {
            var game = new GameObject(GameName);
            UIDocument document = game.AddComponent<UIDocument>();
            document.panelSettings = panel;
            game.AddComponent<GameRoot>();
        }

        /// <summary>
        /// Opening a scene in single mode unloads the assets that nothing loaded refers to, the panel settings among
        /// them, and a reference to the unloaded object is saved as none; so the panel settings are loaded again after
        /// each scene change.
        /// </summary>
        private static PanelSettings PanelAfterSceneChange() => LoadAsset<PanelSettings>(PanelPath);

        /// <summary>Main is the only scene in the shared list that every platform profile builds from.</summary>
        private static void BuildMainSceneOnly()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.globalScenes;
            if (scenes.Length == 1 && scenes[0].path == MainScenePath && scenes[0].enabled)
            {
                return;
            }

            EditorBuildSettings.globalScenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            Debug.Log("ProjectSetup: the build now holds " + MainScenePath + " only");
        }

        /// <summary>The asset at the path; a missing one means the step before failed, so the run stops there.</summary>
        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            T? asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("ProjectSetup could not load " + path + " as " + typeof(T).Name + ".");
            }

            return asset;
        }
    }
}
