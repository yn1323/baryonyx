using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Editor
{
    public static class AssetFolders
    {
        // Assets/ からのフォルダーを、途中のフォルダーも含めてUnityの機能で作る。
        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"Invalid asset folder path: {path}");
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // パスのScriptableObjectを読み、なければ作る。値の設定は呼び出し側が行う。
        public static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
