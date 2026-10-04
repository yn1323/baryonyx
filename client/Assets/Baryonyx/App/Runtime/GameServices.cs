using System;
using Baryonyx.Account;
using Baryonyx.ExerciseRewards;
using Baryonyx.Health;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.StepBonus;
using UnityEngine;

namespace Baryonyx.App
{
    // アプリの起動から終了までTopとHomeで共有する接続。シーンを切り替えてもセッションを保つ。
    // Android版だけがHealth Connectを使い、それ以外の実行環境はサンプルの歩数を使う。
    public sealed class GameServices
    {
        private static GameServices current;

        internal GameServices(
            HealthStepLink health,
            bool preview,
            IStepBonusSource stepBonus = null,
            IPartySource party = null
        )
        {
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Preview = preview;
            StepBonus = stepBonus;
            Party = party;
        }

        public HealthStepLink Health { get; }
        public bool Preview { get; }

        // UPTボーナスの持ち物と枠を読み書きするサーバー。接続先がなければnull。
        public IStepBonusSource StepBonus { get; }

        // 酒場の編成・カード・レベルを読み書きするサーバー。接続先がなければnull。
        public IPartySource Party { get; }

        public static GameServices GetOrCreate(HealthConnectionSettings settings)
        {
            if (current == null)
                Use(Create(settings));
            return current;
        }

        // テストで端末とサーバーを差し替える。nullで次回の取得時に作り直す。
        internal static void Override(GameServices services) => Use(services);

        // 酒場の画面は機能の外からこの接続を知らないため、共有のセッションへ渡す。
        private static void Use(GameServices services)
        {
            current = services;
            StepBonusSession.Source = services?.StepBonus;
            PartySession.Source = services?.Party;
        }

        // Domain Reloadを省略したPlay開始でも、前回の接続先やセッションを持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => current = null;

        private static GameServices Create(HealthConnectionSettings settings)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            IHealthStepProvider provider = new HealthConnectProvider();
            const bool preview = false;
#else
            IHealthStepProvider provider = new HealthScreenPreviewProvider(
                permission: settings != null && settings.PreviewStartsUnlinked
                    ? HealthPermission.NotGranted
                    : HealthPermission.Granted
            );
            const bool preview = true;
#endif
            var sync = CreateServerSync(settings, out var server);
            return new GameServices(
                new HealthStepLink(
                    provider,
                    (IHealthStepServer)sync ?? new LocalHealthStepServer(),
                    new PlayerPrefsHealthLinkStore()
                ),
                preview,
                sync != null
                    ? new StepBonusServerSource(sync, new StepBonusApiClient(server))
                    : null,
                sync != null ? new PartyServerSource(sync, new PartyApiClient(server)) : null
            );
        }

        // 接続先のゲームサーバーへ、ログイン・歩数の保存・ルーンの請求を順に行う同期。
        // 接続先がない、またはURLが無効なときはnullを返し、歩数を端末内だけに保持させる。
        internal static HealthServerSync CreateServerSync(HealthConnectionSettings settings) =>
            CreateServerSync(settings, out _);

        private static HealthServerSync CreateServerSync(
            HealthConnectionSettings settings,
            out ServerApi server
        )
        {
            server = null;
            var url = ServerEndpoint.Resolve(settings);
            if (string.IsNullOrWhiteSpace(url))
                return null;
            try
            {
                server = new ServerApi(url);
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
                return null;
            }
        }
    }
}
