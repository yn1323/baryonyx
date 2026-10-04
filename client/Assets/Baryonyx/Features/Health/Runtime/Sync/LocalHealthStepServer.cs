using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    // サーバーURLを設定しない実行環境で、最後に保存した7日分をメモリだけに保持する。
    // ルーンはサーバーと同じく、日ごとに変換済みの歩数との差分を、1歩＝1ACT・1ACT＝1ルーンで付与する。
    public sealed class LocalHealthStepServer : IHealthStepServer
    {
        private readonly Dictionary<string, long> creditedSteps = new();
        private HealthDay[] days = Array.Empty<HealthDay>();
        private long runes;

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

        public Task<long> ReadRunesAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(runes);
        }

        public Task<HealthRuneClaim> ClaimRunesAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            long granted = 0;
            foreach (var day in days)
            {
                if (day == null || !day.hasValue)
                    continue;
                creditedSteps.TryGetValue(day.day, out long credited);
                // 歩数が後から減っても、付与済みのルーンは取り消さない。
                if (day.steps <= credited)
                    continue;
                granted += day.steps - credited;
                creditedSteps[day.day] = day.steps;
            }
            runes += granted;
            return Task.FromResult(new HealthRuneClaim(granted, runes));
        }
    }
}
