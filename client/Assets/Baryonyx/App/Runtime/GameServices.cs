using System;
using Baryonyx.Account;
using Baryonyx.Adventure;
using Baryonyx.Combat.Records;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.ExerciseRewards;
using Baryonyx.Health;
using Baryonyx.Library;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.Temple;
using Baryonyx.Workshop;
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
            IPartySource party = null,
            IAdventureSource adventure = null,
            IEquipmentSource equipment = null,
            IRevealedWeaknessSource weaknesses = null,
            ILibrarySource library = null,
            IWorkshopSource workshop = null,
            IEnhancementSource enhancement = null,
            ITempleSummonSource summon = null
        )
        {
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Preview = preview;
            Party = party;
            Adventure = adventure;
            Equipment = equipment;
            Weaknesses = weaknesses;
            Library = library;
            Workshop = workshop;
            Enhancement = enhancement;
            Summon = summon;
        }

        public HealthStepLink Health { get; }
        public bool Preview { get; }

        // 酒場の編成・カード・レベルを読み書きするサーバー。接続先がなければnull。
        public IPartySource Party { get; }

        // 冒険の状態を読み書きするサーバー。接続先がなければnull。
        public IAdventureSource Adventure { get; }

        // 編成の装備（持っている装備と付け替え）を読み書きするサーバー。接続先がなければnull。
        public IEquipmentSource Equipment { get; }

        // 戦闘で開示した敵の弱点を読み書きするサーバー。接続先がなければnull。
        public IRevealedWeaknessSource Weaknesses { get; }

        // ライブラリの見つけた記録と日々の歩数を読むサーバー。接続先がなければnull。
        public ILibrarySource Library { get; }

        // 商会で買うサーバー。接続先がなければnull。
        public IWorkshopSource Workshop { get; }

        // 商会で強化するサーバー。接続先がなければnull。
        public IEnhancementSource Enhancement { get; }

        // 神殿で召喚するサーバー。接続先がなければnull。
        public ITempleSummonSource Summon { get; }

        // デバッグルームの一覧の「ルーン+10000」。サーバーがなければ画面の残高に足す。
        public DebugRunes DebugRunes { get; private set; } = DebugRunes.Offline;

        // 作った接続。まだ作っていなければnull（シーンを単体で開いたときなど）。
        public static GameServices Current => current;

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
            PartySession.Source = services?.Party;
            AdventureSession.Source = services?.Adventure;
            EquipmentSession.Source = services?.Equipment;
            RevealedWeaknessSession.Source = services?.Weaknesses;
            LibrarySession.Source = services?.Library;
            WorkshopSession.Source = services?.Workshop;
            EnhancementSession.Source = services?.Enhancement;
            TempleSummonSession.Source = services?.Summon;
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
            var services = new GameServices(
                new HealthStepLink(
                    provider,
                    (IHealthStepServer)sync ?? new LocalHealthStepServer(),
                    new PlayerPrefsHealthLinkStore()
                ),
                preview,
                sync != null
                    ? new PartyServerSource(sync.Sessions, new PartyApiClient(server))
                    : null,
                sync != null
                    ? new AdventureServerSource(sync.Sessions, new AdventureApiClient(server))
                    : null,
                sync != null
                    ? new EquipmentServerSource(sync.Sessions, new EquipmentApiClient(server))
                    : null,
                sync != null
                    ? new RevealedWeaknessServerSource(
                        sync.Sessions,
                        new RevealedWeaknessApiClient(server)
                    )
                    : null,
                sync != null
                    ? new LibraryServerSource(sync.Sessions, new LibraryApiClient(server))
                    : null,
                sync != null
                    ? new WorkshopServerSource(sync.Sessions, new WorkshopApiClient(server))
                    : null,
                sync != null
                    ? new EnhancementServerSource(sync.Sessions, new EnhancementApiClient(server))
                    : null,
                sync != null
                    ? new TempleSummonServerSource(sync.Sessions, new TempleSummonApiClient(server))
                    : null
            );
            if (sync != null)
                services.DebugRunes = new DebugRunes(sync.Sessions, server);
            return services;
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
                var accounts = new AccountApiClient(server);
                return new HealthServerSync(
                    new AccountSessionRunner(accounts),
                    accounts,
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
