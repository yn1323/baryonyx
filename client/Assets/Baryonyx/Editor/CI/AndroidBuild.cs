using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Baryonyx.Editor.CI
{
    public static class AndroidBuild
    {
        public const string ApplicationId = "dev.baryonyx.ci";
        public const int MinimumSdk = 26;
        public const int TargetSdk = 36;
        public const string OutputPath = "Builds/Android/baryonyx.apk";

        public static void Build()
        {
            if (
                !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)
            )
                throw new BuildFailedException(
                    "Install Android Build Support, SDK, NDK and OpenJDK."
                );
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Start Unity with -buildTarget Android.");

            var scenes = BuildScenes.GetEnabledScenePaths(EditorBuildSettings.scenes);
            var target = NamedBuildTarget.Android;
            var identifier = PlayerSettings.GetApplicationIdentifier(target);
            var backend = PlayerSettings.GetScriptingBackend(target);
            var architectures = PlayerSettings.Android.targetArchitectures;
            var minimum = PlayerSettings.Android.minSdkVersion;
            var maximum = PlayerSettings.Android.targetSdkVersion;
            var customKeystore = PlayerSettings.Android.useCustomKeystore;
            var appBundle = EditorUserBuildSettings.buildAppBundle;
            var exportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            var split = PlayerSettings.Android.buildApkPerCpuArchitecture;
            var versionCode = PlayerSettings.Android.bundleVersionCode;
            try
            {
                PlayerSettings.SetApplicationIdentifier(target, ApplicationId);
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinimumSdk;
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.buildApkPerCpuArchitecture = false;
                PlayerSettings.Android.bundleVersionCode = 1;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                // Never let a failed build leave an old APK looking like a new result.
                if (File.Exists(OutputPath))
                    File.Delete(OutputPath);
                var report = BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = OutputPath,
                        target = BuildTarget.Android,
                        options = BuildOptions.Development,
                    }
                );
                if (report.summary.result != BuildResult.Succeeded || !File.Exists(OutputPath))
                    throw new BuildFailedException(
                        $"Android build failed: {report.summary.result}"
                    );
                Debug.Log($"BARYONYX_ANDROID_BUILD_OK: {OutputPath}");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(target, identifier);
                PlayerSettings.SetScriptingBackend(target, backend);
                PlayerSettings.Android.targetArchitectures = architectures;
                PlayerSettings.Android.minSdkVersion = minimum;
                PlayerSettings.Android.targetSdkVersion = maximum;
                PlayerSettings.Android.useCustomKeystore = customKeystore;
                PlayerSettings.Android.buildApkPerCpuArchitecture = split;
                PlayerSettings.Android.bundleVersionCode = versionCode;
                EditorUserBuildSettings.buildAppBundle = appBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = exportProject;
            }
        }
    }
}
