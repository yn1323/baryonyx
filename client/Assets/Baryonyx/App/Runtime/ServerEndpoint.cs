using System;
using System.IO;
using Baryonyx.Health;
using UnityEngine;

namespace Baryonyx.App
{
    public enum ServerEnvironment
    {
        Local,
        Dev,
        Prod,
    }

    // ゲームサーバーの接続先を決める。APKはビルド時に選んだ環境、Editorは開発者ごとの選択を使う。
    // 設定アセットを接続先ごとに書き換えずに、ローカル・Dev・Prodを行き来できるようにする。
    public static class ServerEndpoint
    {
        public const string LocalUrl = "http://127.0.0.1:4000";

        // Unityが開発者ごとの設定を置くフォルダー。Gitの対象外。
        public const string EditorSelectionPath = "UserSettings/BaryonyxServer.json";

        public static string Resolve(HealthConnectionSettings settings)
        {
#if UNITY_EDITOR
            return Resolve(settings, true, EditorEnvironment);
#else
            return Resolve(settings, false, ServerEnvironment.Dev);
#endif
        }

        internal static string Resolve(
            HealthConnectionSettings settings,
            bool editor,
            ServerEnvironment editorEnvironment
        )
        {
            if (settings == null)
                return "";
            if (editor)
                return ForEnvironment(settings, editorEnvironment);
            // AndroidBuild以外の方法でビルドしたAPKは、既定のDevへ接続する。
            return !string.IsNullOrWhiteSpace(settings.BuildServerUrl)
                ? settings.BuildServerUrl.Trim()
                : (settings.DevServerUrl ?? "").Trim();
        }

        public static string ForEnvironment(
            HealthConnectionSettings settings,
            ServerEnvironment environment
        ) =>
            environment switch
            {
                ServerEnvironment.Local => LocalUrl,
                ServerEnvironment.Prod => (settings.ProdServerUrl ?? "").Trim(),
                _ => (settings.DevServerUrl ?? "").Trim(),
            };

        // ビルドや設定で使う環境名を読む。PreviewのAPKは当面Devへ接続する。
        public static bool TryParse(string name, out ServerEnvironment environment)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "local":
                    environment = ServerEnvironment.Local;
                    return true;
                case "":
                case "dev":
                case "preview":
                    environment = ServerEnvironment.Dev;
                    return true;
                case "prod":
                    environment = ServerEnvironment.Prod;
                    return true;
                default:
                    environment = ServerEnvironment.Dev;
                    return false;
            }
        }

#if UNITY_EDITOR
        // 選択がなければローカルのサーバーへ接続する。
        public static ServerEnvironment EditorEnvironment
        {
            get
            {
                try
                {
                    if (!File.Exists(EditorSelectionPath))
                        return ServerEnvironment.Local;
                    var selection = JsonUtility.FromJson<Selection>(
                        File.ReadAllText(EditorSelectionPath)
                    );
                    return TryParse(selection?.environment, out var environment)
                        ? environment
                        : ServerEnvironment.Local;
                }
                catch (Exception exception) when (exception is IOException or ArgumentException)
                {
                    return ServerEnvironment.Local;
                }
            }
            set
            {
                Directory.CreateDirectory(Path.GetDirectoryName(EditorSelectionPath));
                File.WriteAllText(
                    EditorSelectionPath,
                    JsonUtility.ToJson(
                        new Selection { environment = value.ToString().ToLowerInvariant() },
                        true
                    )
                );
            }
        }

        [Serializable]
        private sealed class Selection
        {
            public string environment;
        }
#endif
    }
}
