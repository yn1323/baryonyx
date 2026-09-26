using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using Baryonyx.Home;

namespace Baryonyx.App
{
    // Homeの歩数パネルへ、サーバーに保存済みの今日の歩数・所持ルーンと、同期とルーンへの変換をつなぐ。
    public sealed class HomeStepSource : IHomeStepSource
    {
        private readonly HealthStepLink link;

        // 偽なら歩数の同期だけを行い、所持ルーンの読み取りとルーンへの変換をしない（仮データのルーンを使う場合）。
        private readonly bool runes;

        public HomeStepSource(HealthStepLink link, bool runes = true)
        {
            this.link = link ?? throw new ArgumentNullException(nameof(link));
            this.runes = runes;
        }

        public async Task<HomeStepReading> LoadAsync(CancellationToken token)
        {
            long? balance = runes ? await link.ReadRunesAsync(token) : null;
            if (await link.CheckAsync(token) != HealthLinkStatus.Linked)
                return Unlinked(HomeStepResult.Unlinked, balance);
            return await ReadAsync(balance, 0, token);
        }

        public async Task<HomeStepReading> SyncAsync(CancellationToken token)
        {
            var status = await link.CheckAsync(token);
            if (status == HealthLinkStatus.PermissionRequired)
                status = await link.RequestAsync(token);
            if (status == HealthLinkStatus.PermissionRequired)
                return Unlinked(HomeStepResult.Unlinked);
            if (status != HealthLinkStatus.Linked)
            {
                link.OpenSettings();
                return Unlinked(HomeStepResult.SettingsOpened);
            }
            var result = await link.SyncAsync(token);
            if (result == HealthSyncStatus.PermissionRequired)
                return Unlinked(HomeStepResult.Unlinked);
            if (result == HealthSyncStatus.ReadFailed)
                return new HomeStepReading(HomeStepResult.Failed, HomeStepLink.Linked, 0);
            if (!runes)
                return await ReadAsync(null, 0, token);
            var claim = await link.ClaimRunesAsync(token);
            return await ReadAsync(claim.Balance, claim.Granted, token);
        }

        private async Task<HomeStepReading> ReadAsync(
            long? balance,
            long grantedRunes,
            CancellationToken token
        )
        {
            var today = await link.ReadTodayAsync(token);
            int steps = today.HasValue ? (int)Math.Min(int.MaxValue, today.Steps) : 0;
            return new HomeStepReading(
                HomeStepResult.Updated,
                HomeStepLink.Linked,
                steps,
                today.Day,
                balance,
                grantedRunes
            );
        }

        private static HomeStepReading Unlinked(HomeStepResult result, long? runes = null) =>
            new(result, HomeStepLink.Unlinked, 0, runes: runes);
    }
}
