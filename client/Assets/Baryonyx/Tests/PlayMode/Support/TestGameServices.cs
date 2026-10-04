using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Health;
using Baryonyx.UI;

namespace Baryonyx.Tests.PlayMode
{
    // Top・Home・案内人の画面・冒険のシーンテストで、Health Connectとゲームサーバーを端末内の代役へ差し替える。
    // 設定アセットのサーバーURLへは接続しない。冒険はテストごとに新しい、アプリ内だけの冒険を使う。
    // 画面の切り替えを待つ時間を縮めるため、遷移演出も短くする。演出の動きを確かめるテストは
    // SceneTransitionController.DurationScale を1に戻す。
    public sealed class TestGameServices : IDisposable
    {
        private const float FastTransitions = 0.05f;

        private TestGameServices(HealthPermission permission)
        {
            SceneTransitionController.DurationScale = FastTransitions;
            Provider = new HealthScreenPreviewProvider(permission: permission);
            Server = new TestStepServer();
            Adventure = new AdventureLocalSource();
            AdventureSession.Reset();
            GameServices.Override(
                new GameServices(
                    new HealthStepLink(Provider, Server, new MemoryLinkStore()),
                    true,
                    adventure: Adventure
                )
            );
        }

        public HealthScreenPreviewProvider Provider { get; }
        public TestStepServer Server { get; }

        // 冒険の代役。テストで冒険を始めた状態や、ルーンの残高を作るときに直接呼ぶ。
        public AdventureLocalSource Adventure { get; }

        public static TestGameServices Use(
            HealthPermission permission = HealthPermission.Granted
        ) => new(permission);

        public void Dispose()
        {
            GameServices.Override(null);
            AdventureSession.Reset();
            SceneTransitionController.DurationScale = 1f;
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

            public Task<HealthRuneClaim> ClaimRunesAsync(CancellationToken token)
            {
                ThrowIfFailing();
                return store.ClaimRunesAsync(token);
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
