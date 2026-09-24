using System;
using System.Security.Cryptography;
using UnityEngine;

namespace Baryonyx.Account
{
    // ゲストを識別する端末内の秘密値。サーバーはハッシュだけを保存し、同じ値で同じユーザーへ戻す。
    // Google接続による引き継ぎを行うまでは、アプリのデータを消すとゲストのデータへ戻れない。
    public static class GuestCredential
    {
        private const string Key = "Account.GuestSecret";

        public static string GetOrCreate()
        {
            string secret = PlayerPrefs.GetString(Key, "");
            if (IsValid(secret))
                return secret;
            secret = Create();
            PlayerPrefs.SetString(Key, secret);
            PlayerPrefs.Save();
            return secret;
        }

        internal static bool IsValid(string secret)
        {
            if (secret == null || secret.Length != 64)
                return false;
            foreach (char c in secret)
                if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f'))
                    return false;
            return true;
        }

        internal static string Create()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
