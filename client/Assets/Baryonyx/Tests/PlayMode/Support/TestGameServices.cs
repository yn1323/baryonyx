using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat.Records;
using Baryonyx.Economy;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.Health;
using Baryonyx.Library;
using Baryonyx.Networking;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Temple;
using Baryonyx.Training;
using Baryonyx.UI;
using Baryonyx.Workshop;
using Baryonyx.WorldMap;

namespace Baryonyx.Tests.PlayMode
{
    // Top・Home・案内人の画面・冒険のシーンテストで、Health Connectとゲームサーバーを端末内の代役へ差し替える。
    // 設定アセットのサーバーURLへは接続しない。冒険はテストごとに新しい、アプリ内だけの冒険を使う。
    // 画面の切り替えを待つ時間を縮めるため、遷移演出も短くする。演出の動きを確かめるテストは
    // SceneTransitionController.DurationScale を1に戻す。
    // Unityの乱数は決まった種から始め、終わると、テストの中で変えたアプリ全体の状態（static な Session など）を
    // 初めに戻す。前のテストの状態が次のテストへ残らない。
    public sealed class TestGameServices : IDisposable
    {
        private const float FastTransitions = 0.05f;

        private readonly FixedRandom random = FixedRandom.Use();

        private TestGameServices(HealthPermission permission)
        {
            SceneTransitionController.DurationScale = FastTransitions;
            Provider = new HealthScreenPreviewProvider(permission: permission);
            Server = new TestStepServer();
            // 冒険の道と中身も、いつも同じ種から作る。
            Adventure = new AdventureLocalSource(seed: FixedRandom.DefaultSeed);
            AdventureSession.Reset();
            // 開示した弱点は、テストごとに空のアプリ内の記録から始める。
            RevealedWeaknessSession.Reset();
            // 装備も、テストごとにアプリ内の初めの装備から始める。前のテストの付け替えを残さない。
            EquipmentSession.Reset();
            GameServices.Override(
                new GameServices(
                    new HealthStepLink(Provider, Server, new MemoryLinkStore()),
                    true,
                    adventure: new DelayedAdventure(this)
                )
            );
        }

        public HealthScreenPreviewProvider Provider { get; }
        public TestStepServer Server { get; }

        // 冒険の代役。テストで冒険を始めた状態や、ルーンの残高を作るときに直接呼ぶ。
        public AdventureLocalSource Adventure { get; }

        // 画面が冒険を読むときの返事の遅れ（秒）。サーバーのように、開く演出の途中で読み終える場合を作る。
        public float AdventureLoadSeconds { get; set; }

        public static TestGameServices Use(
            HealthPermission permission = HealthPermission.Granted
        ) => new(permission);

        public void Dispose()
        {
            GameServices.Override(null);
            AdventureSession.Reset();
            RevealedWeaknessSession.Reset();
            EquipmentSession.Reset();
            PartySession.Reset();
            PartyUniqueSkills.Reset();
            PartySkillAwakenings.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
            TrainingSession.Reset();
            TrainingBond.Reset();
            LibrarySession.Reset();
            WorkshopSession.Reset();
            EnhancementSession.Reset();
            TempleSummonSession.Reset();
            WorldMapSession.Reset();
            RuneBalance.Reset();
            SaveQueue.Reset();
            SceneTransitionController.DurationScale = 1f;
            random.Dispose();
        }

        // 画面からの冒険の読み込みだけを AdventureLoadSeconds 遅らせ、ほかは代役へそのまま渡す。
        private sealed class DelayedAdventure : IAdventureSource
        {
            private readonly TestGameServices owner;

            public DelayedAdventure(TestGameServices owner) => this.owner = owner;

            public async Task<AdventureState> LoadAsync(CancellationToken token)
            {
                if (owner.AdventureLoadSeconds > 0f)
                    await UnityEngine.Awaitable.WaitForSecondsAsync(
                        owner.AdventureLoadSeconds,
                        token
                    );
                return await owner.Adventure.LoadAsync(token);
            }

            public Task<AdventureState> StartAsync(string destinationId, CancellationToken token) =>
                owner.Adventure.StartAsync(destinationId, token);

            public Task<AdventureState> MoveAsync(AdventureRoom room, CancellationToken token) =>
                owner.Adventure.MoveAsync(room, token);

            public Task<AdventureState> ClearAsync(
                string roomId,
                CancellationToken token,
                IReadOnlyList<AdventureEnemyTally> enemies = null,
                AdventureClearOptions options = null
            ) => owner.Adventure.ClearAsync(roomId, token, enemies, options);

            public Task<AdventureState> OpenAsync(string roomId, CancellationToken token) =>
                owner.Adventure.OpenAsync(roomId, token);

            public Task<AdventureState> BuyAsync(
                string roomId,
                int index,
                CancellationToken token
            ) => owner.Adventure.BuyAsync(roomId, index, token);

            public Task<AdventureState> ReviveAsync(string roomId, CancellationToken token) =>
                owner.Adventure.ReviveAsync(roomId, token);

            public Task<AdventureState> EndAsync(
                AdventureEndReason reason,
                CancellationToken token,
                IReadOnlyList<AdventureEnemyTally> enemies = null
            ) => owner.Adventure.EndAsync(reason, token, enemies);
        }

        public sealed class TestStepServer : IHealthStepServer
        {
            private readonly LocalHealthStepServer store = new();

            public bool Fail { get; set; }
            public int Saves { get; private set; }

            public Task ConnectAsync(CancellationToken token)
            {
                ThrowIfFailing();
                return store.ConnectAsync(token);
            }

            public Task SaveAsync(HealthDay[] days, CancellationToken token)
            {
                ThrowIfFailing();
                Saves++;
                return store.SaveAsync(days, token);
            }

            public Task<HealthDay[]> ReadAsync(CancellationToken token)
            {
                ThrowIfFailing();
                return store.ReadAsync(token);
            }

            public Task<long> ReadRunesAsync(CancellationToken token)
            {
                ThrowIfFailing();
                return store.ReadRunesAsync(token);
            }

            public Task<HealthRuneClaim> SaveAndClaimAsync(HealthDay[] days, CancellationToken token)
            {
                ThrowIfFailing();
                Saves++;
                return store.SaveAndClaimAsync(days, token);
            }

            private void ThrowIfFailing()
            {
                if (Fail)
                    throw new InvalidOperationException("Test server is offline.");
            }
        }

        private sealed class MemoryLinkStore : IHealthLinkStore
        {
            public int PermissionDenials { get; set; }
        }
    }
}
