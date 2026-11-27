using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CoveredLot.EditorTools
{
    /// <summary>
    /// The Android development build (design sections 8.6 and 11): an APK, not an App Bundle or a Gradle project, of the
    /// enabled scenes in the build (Main only, which ProjectSetup sets), with Unity's development player and the debug
    /// keystore. tools/build-android.sh runs it with <c>unity build --execute-method
    /// CoveredLot.EditorTools.DevelopmentBuild.Android --output-path &lt;apk&gt;</c>: the CLI forwards the output path as
    /// <c>-buildOutput</c> and returns this method's exit code. The editor exits with 1 when the path or the scenes are
    /// missing, or when the build does not succeed.
    /// </summary>
    public static class DevelopmentBuild
    {
        private const string BuildOutputArgument = "-buildOutput";

        public static void Android()
        {
            string? apkPath = BuildOutputOf(Environment.GetCommandLineArgs());
            if (apkPath == null)
            {
                Fail("no " + BuildOutputArgument + " path was given; run tools/build-android.sh");
                return;
            }

            string[] scenes = EditorBuildSettings.globalScenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("the build holds no enabled scene; run ProjectSetup.Apply");
                return;
            }

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            });

            BuildSummary summary = report.summary;
            Debug.Log("DevelopmentBuild: " + summary.result.ToString() + ", "
                + summary.totalErrors.ToString(CultureInfo.InvariantCulture) + " errors, "
                + summary.totalWarnings.ToString(CultureInfo.InvariantCulture) + " warnings, "
                + summary.totalSize.ToString(CultureInfo.InvariantCulture) + " bytes, " + summary.outputPath);
            if (summary.result != BuildResult.Succeeded)
            {
                Fail("the build did not succeed");
            }
        }

        /// <summary>The path that follows <c>-buildOutput</c>; null when the argument is missing or is the last one.</summary>
        private static string? BuildOutputOf(string[] arguments)
        {
            int index = Array.IndexOf(arguments, BuildOutputArgument);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        private static void Fail(string reason)
        {
            Debug.LogError("DevelopmentBuild: " + reason + ".");
            EditorApplication.Exit(1);
        }
    }
}
