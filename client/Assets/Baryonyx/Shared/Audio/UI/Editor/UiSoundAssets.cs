using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Baryonyx.Audio.Editor
{
    /// <summary>
    /// Sets up the UI sounds (<see cref="UiSounds"/>): the clips' import settings and the sound bank.
    /// A build carries the bank in Player Settings' Preloaded Assets (<see cref="UiSoundBuildStep"/>).
    /// </summary>
    public static class UiSoundAssets
    {
        public const string Folder = "Assets/Baryonyx/Shared/Audio/UI";
        public const string PressPath = Folder + "/UiPress.wav";
        public const string DeniedPath = Folder + "/UiDenied.wav";

        [MenuItem("Baryonyx/Audio/Create UI Sound Assets")]
        public static void CreateAssets()
        {
            ApplyImportSettings(PressPath);
            ApplyImportSettings(DeniedPath);
            var bank = AssetDatabase.LoadAssetAtPath<UiSoundBank>(UiSoundBank.AssetPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<UiSoundBank>();
                AssetDatabase.CreateAsset(bank, UiSoundBank.AssetPath);
            }
            bank.Press = AssetDatabase.LoadAssetAtPath<AudioClip>(PressPath);
            bank.Denied = AssetDatabase.LoadAssetAtPath<AudioClip>(DeniedPath);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
        }

        // doc/rules/sound.md#unityの取り込み設定 の「1秒前後までの効果音」。
        private static void ApplyImportSettings(string path)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                throw new System.InvalidOperationException("Missing sound: " + path);
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// Puts the sound bank in Preloaded Assets while a build runs and takes it out afterwards, the
    /// way the Input System carries its settings, so the build loads it when the game starts and
    /// Player Settings stay as they are.
    /// </summary>
    public sealed class UiSoundBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static Object added;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var bank = AssetDatabase.LoadAssetAtPath<UiSoundBank>(UiSoundBank.AssetPath);
            if (bank == null)
                throw new BuildFailedException(
                    "Missing UI sounds: run Baryonyx > Audio > Create UI Sound Assets."
                );
            var assets = PlayerSettings.GetPreloadedAssets();
            if (assets.Contains(bank))
                return;
            PlayerSettings.SetPreloadedAssets(assets.Append(bank).ToArray());
            added = bank;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (added == null)
                return;
            PlayerSettings.SetPreloadedAssets(
                PlayerSettings.GetPreloadedAssets().Where(a => a != added).ToArray()
            );
            added = null;
        }
    }
}
