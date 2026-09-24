using UnityEditor;
using UnityEngine;

namespace Baryonyx.App.Editor
{
    // EditorのPlayで接続するゲームサーバーを選ぶ。選択は開発者ごとのUserSettingsに保存し、
    // APKのビルドには影響しない。
    public static class ServerEnvironmentMenu
    {
        private const string Root = "Baryonyx/Server/";
        private const string LocalPath = Root + "Local (127.0.0.1:4000)";
        private const string DevPath = Root + "Dev";
        private const string ProdPath = Root + "Prod";

        [MenuItem(LocalPath, priority = 1)]
        private static void UseLocal() => Select(ServerEnvironment.Local);

        [MenuItem(DevPath, priority = 2)]
        private static void UseDev() => Select(ServerEnvironment.Dev);

        [MenuItem(ProdPath, priority = 3)]
        private static void UseProd() => Select(ServerEnvironment.Prod);

        [MenuItem(LocalPath, true)]
        private static bool ValidateLocal() => Mark(LocalPath, ServerEnvironment.Local);

        [MenuItem(DevPath, true)]
        private static bool ValidateDev() => Mark(DevPath, ServerEnvironment.Dev);

        [MenuItem(ProdPath, true)]
        private static bool ValidateProd() => Mark(ProdPath, ServerEnvironment.Prod);

        private static void Select(ServerEnvironment environment)
        {
            ServerEndpoint.EditorEnvironment = environment;
            Debug.Log(
                $"EditorのPlayで接続するゲームサーバーを {environment} にしました。次のPlayから反映します。"
            );
        }

        private static bool Mark(string path, ServerEnvironment environment)
        {
            Menu.SetChecked(path, ServerEndpoint.EditorEnvironment == environment);
            return true;
        }
    }
}
