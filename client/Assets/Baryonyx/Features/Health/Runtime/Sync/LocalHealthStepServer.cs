using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    // サーバーURLを設定しない実行環境で、最後に保存した7日分をメモリだけに保持する。
    public sealed class LocalHealthStepServer : IHealthStepServer
    {
        private HealthDay[] days = Array.Empty<HealthDay>();

        public Task ConnectAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task SaveAsync(HealthDay[] days, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            this.days = days ?? Array.Empty<HealthDay>();
            return Task.CompletedTask;
        }

        public Task<HealthDay[]> ReadAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(days);
        }
    }
}
