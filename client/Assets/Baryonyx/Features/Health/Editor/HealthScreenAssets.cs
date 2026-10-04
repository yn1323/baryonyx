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
            EditorGuard.RequireEditMode();
            if (TMP_Settings.instance == null)
                throw new System.InvalidOperationException("Import TMP Essential Resources first.");
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            AssetDatabase.Refresh();
            GameFontAssets.SetAsDefault(GameFontAssets.GetOrCreate());
            AssetFolders.LoadOrCreate<HealthConnectionSettings>(SettingsPath);
            AssetDatabase.SaveAssets();
        }
    }
}
