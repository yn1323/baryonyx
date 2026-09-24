using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.App;
using Baryonyx.Health;

namespace Baryonyx.Tests.PlayMode
{
    // TopとHomeのシーンテストで、Health Connectとゲームサーバーを端末内の代役へ差し替える。
    // 設定アセットのサーバーURLへは接続しない。
    public sealed class TestGameServices : IDisposable
    {
        private TestGameServices(HealthPermission permission)
        {
            Provider = new HealthScreenPreviewProvider(permission: permission);
            Server = new TestStepServer();
            GameServices.Override(
                new GameServices(new HealthStepLink(Provider, Server, new MemoryLinkStore()), true)
            );
        }

        public HealthScreenPreviewProvider Provider { get; }
        public TestStepServer Server { get; }

        public static TestGameServices Use(
            HealthPermission permission = HealthPermission.Granted
        ) => new(permission);

        public void Dispose() => GameServices.Override(null);

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
