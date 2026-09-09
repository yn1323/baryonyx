using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Baryonyx.Editor.CI
{
    public static class WebBuild
    {
        public static string[] GetEnabledScenePaths(EditorBuildSettingsScene[] settings)
        {
            var scenes = settings
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("Enable at least one scene in Build Profiles.");
            if (scenes.Any(path => !File.Exists(path)))
                throw new BuildFailedException("An enabled build scene does not exist.");
            if (scenes.Distinct().Count() != scenes.Length)
                throw new BuildFailedException("An enabled build scene is listed more than once.");
            return scenes;
        }

        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new BuildFailedException("Install Web Build Support for this Unity version.");

            var scenes = GetEnabledScenePaths(EditorBuildSettings.scenes);
            var arguments = Environment.GetCommandLineArgs();
            var outputIndex = Array.IndexOf(arguments, "-customBuildPath");
            var output =
                outputIndex >= 0 && outputIndex + 1 < arguments.Length
                    ? arguments[outputIndex + 1]
                    : "Builds/WebGL";

            var compression = PlayerSettings.WebGL.compressionFormat;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            var threads = PlayerSettings.WebGL.threadsSupport;
            try
            {
                // The preview works on a static host without custom HTTP encoding headers.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.threadsSupport = false;
                var report = BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = output,
                        target = BuildTarget.WebGL,
                        options = BuildOptions.None,
                    }
                );
                Directory.CreateDirectory("Logs");
                File.WriteAllText(
                    "Logs/web-build.txt",
                    $"Unity {Application.unityVersion}\nResult: {report.summary.result}\n"
                        + $"Scenes: {string.Join(", ", scenes)}\nSize: {report.summary.totalSize}\n"
                );
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"Web build failed: {report.summary.result}");
                Debug.Log($"BARYONYX_WEB_BUILD_OK: {output}");
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.threadsSupport = threads;
            }
        }
    }
}
