using System;
using System.Security.Cryptography;
using System.Text;
using Baryonyx.Account;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.App.Editor
{
    // ローカルサーバーへ `pnpm db:reset` で入れた開発用のプレイヤー（seed）として、EditorのPlayで接続する。
    // ゲストの秘密値を差し替えるだけで、元の秘密値はEditorPrefsに控え、「自分のゲストに戻す」で戻す。
    // プレイヤーの一覧と秘密値の決め方は server/scripts/seed.ts と同じにする（doc/rules/backend-design.md）。
    public static class SeedPlayerMenu
    {
        private const string Root = "Baryonyx/Server/Seed Player/";
        private const string VeteranPath = Root + "遊び込んだプレイヤー (veteran)";
        private const string AdventurerPath = Root + "冒険の途中のプレイヤー (adventurer)";
        private const string NewcomerPath = Root + "始めたばかりのプレイヤー (newcomer)";
        private const string OwnPath = Root + "自分のゲストに戻す";
        private const string SavedKey = "Baryonyx.SeedPlayer.OwnGuestSecret";

        [MenuItem(VeteranPath, priority = 20)]
        private static void UseVeteran() => Use("veteran");

        [MenuItem(AdventurerPath, priority = 21)]
        private static void UseAdventurer() => Use("adventurer");

        [MenuItem(NewcomerPath, priority = 22)]
        private static void UseNewcomer() => Use("newcomer");

        [MenuItem(OwnPath, priority = 40)]
        private static void UseOwn()
        {
            string own = EditorPrefs.GetString(SavedKey, "");
            if (string.IsNullOrEmpty(own))
                PlayerPrefs.DeleteKey(GuestCredential.Key);
            else
                PlayerPrefs.SetString(GuestCredential.Key, own);
            PlayerPrefs.Save();
            EditorPrefs.DeleteKey(SavedKey);
            Debug.Log("自分のゲストに戻しました。次のPlayから反映します。");
        }

        [MenuItem(VeteranPath, true)]
        private static bool ValidateVeteran() => Mark(VeteranPath, "veteran");

        [MenuItem(AdventurerPath, true)]
        private static bool ValidateAdventurer() => Mark(AdventurerPath, "adventurer");

        [MenuItem(NewcomerPath, true)]
        private static bool ValidateNewcomer() => Mark(NewcomerPath, "newcomer");

        // seedのプレイヤーのゲストの秘密値。`baryonyx-seed:<id>` のSHA-256を小文字の16進数にした値。
        public static string SecretFor(string playerId)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"baryonyx-seed:{playerId}"));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static void Use(string playerId)
        {
            string current = PlayerPrefs.GetString(GuestCredential.Key, "");
            // 自分の秘密値だけを控える。seedのプレイヤー同士で切り替えたときは控えを上書きしない。
            if (!IsSeed(current) && !EditorPrefs.HasKey(SavedKey))
                EditorPrefs.SetString(SavedKey, current);
            PlayerPrefs.SetString(GuestCredential.Key, SecretFor(playerId));
            PlayerPrefs.Save();
            if (ServerEndpoint.EditorEnvironment != ServerEnvironment.Local)
                Debug.LogWarning(
                    "seedのプレイヤーはローカルサーバーにだけ入っています。Baryonyx > Server > Local を選んでください。"
                );
            Debug.Log($"seedのプレイヤー {playerId} として接続します。次のPlayから反映します。");
        }

        private static bool IsSeed(string secret) =>
            secret == SecretFor("veteran")
            || secret == SecretFor("adventurer")
            || secret == SecretFor("newcomer");

        private static bool Mark(string path, string playerId)
        {
            Menu.SetChecked(
                path,
                PlayerPrefs.GetString(GuestCredential.Key, "") == SecretFor(playerId)
            );
            return true;
        }
    }
}
