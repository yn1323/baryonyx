using System.IO;
using Baryonyx.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Health.Editor
{
    public static class HealthScreenAssets
    {
        public const string SettingsPath =
            "Assets/Baryonyx/Features/Health/Data/HealthConnectionSettings.asset";

        // Explicit command: create the connection settings once and keep an existing asset's GUID.
        [MenuItem("Baryonyx/Health/Create Screen Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play Mode first.");
            if (TMP_Settings.instance == null)
                throw new System.InvalidOperationException("Import TMP Essential Resources first.");
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            AssetDatabase.Refresh();
            GameFontAssets.SetAsDefault(GameFontAssets.GetOrCreate());
            if (AssetDatabase.LoadAssetAtPath<HealthConnectionSettings>(SettingsPath) == null)
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<HealthConnectionSettings>(),
                    SettingsPath
                );
            AssetDatabase.SaveAssets();
        }
    }
}
