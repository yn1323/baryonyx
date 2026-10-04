using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using Baryonyx.Home;

namespace Baryonyx.App
{
    // HomeのUPTパネルへ、サーバーに保存済みの今日の歩数・所持ルーンと、同期とルーンへの変換をつなぐ。
    public sealed class HomeStepSource : IHomeStepSource
    {
        private readonly HealthStepLink link;

        public HomeStepSource(HealthStepLink link) =>
            this.link = link ?? throw new ArgumentNullException(nameof(link));

        public async Task<HomeStepReading> LoadAsync(CancellationToken token)
        {
            long balance = await link.ReadRunesAsync(token);
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
            // サーバーは日ごとに変換済みの歩数を保存しており、前回の請求から増えたUPTだけをルーンにする。
            var claim = await link.ClaimRunesAsync(token);
            return await ReadAsync(claim.Balance, claim.Granted, token);
        }

        private async Task<HomeStepReading> ReadAsync(
            long balance,
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
