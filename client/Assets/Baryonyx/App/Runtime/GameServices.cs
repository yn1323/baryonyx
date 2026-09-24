using System;
using Baryonyx.Account;
using Baryonyx.ExerciseRewards;
using Baryonyx.Health;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.App
{
    // アプリの起動から終了までTopとHomeで共有する接続。シーンを切り替えてもセッションを保つ。
    // Android版だけがHealth Connectを使い、それ以外の実行環境はサンプルの歩数を使う。
    public sealed class GameServices
    {
        private static GameServices current;

        internal GameServices(HealthStepLink health, bool preview)
        {
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Preview = preview;
        }

        public HealthStepLink Health { get; }
        public bool Preview { get; }

        public static GameServices GetOrCreate(HealthConnectionSettings settings) =>
            current ??= Create(settings);

        // テストで端末とサーバーを差し替える。nullで次回の取得時に作り直す。
        internal static void Override(GameServices services) => current = services;

        // Domain Reloadを省略したPlay開始でも、前回の接続先やセッションを持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => current = null;

        private static GameServices Create(HealthConnectionSettings settings)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            IHealthDataProvider provider = new HealthConnectProvider();
            const bool preview = false;
#else
            IHealthDataProvider provider = new HealthScreenPreviewProvider(
                permission: settings != null && settings.PreviewStartsUnlinked
                    ? HealthPermission.NotGranted
                    : HealthPermission.Granted
            );
            const bool preview = true;
#endif
            return new GameServices(
                new HealthStepLink(
                    provider,
                    CreateServer(settings),
                    new PlayerPrefsHealthLinkStore()
                ),
                preview
            );
        }

        private static IHealthStepServer CreateServer(HealthConnectionSettings settings)
        {
            var url = ServerEndpoint.Resolve(settings);
            if (string.IsNullOrWhiteSpace(url))
                return new LocalHealthStepServer();
            try
            {
                var server = new ServerApi(url);
                return new HealthServerSync(
                    new AccountApiClient(server),
                    new HealthApiClient(server),
                    new ExerciseRewardsApiClient(server)
                );
            }
            catch (Exception exception) when (exception is ArgumentException or UriFormatException)
            {
                Debug.LogWarning(
                    "サーバーURLが無効なため、歩数を端末内だけに保持します。" + exception.Message
                );
                return new LocalHealthStepServer();
            }
        }
    }
}
