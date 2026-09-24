using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using Baryonyx.Home;

namespace Baryonyx.App
{
    // Homeの歩数パネルへ、サーバーに保存済みの今日の歩数と同期操作をつなぐ。
    public sealed class HomeStepSource : IHomeStepSource
    {
        private readonly HealthStepLink link;

        public HomeStepSource(HealthStepLink link) =>
            this.link = link ?? throw new ArgumentNullException(nameof(link));

        public async Task<HomeStepReading> LoadAsync(CancellationToken token)
        {
            if (await link.CheckAsync(token) != HealthLinkStatus.Linked)
                return Unlinked(HomeStepResult.Unlinked);
            return await ReadAsync(token);
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
            return await ReadAsync(token);
        }

        private async Task<HomeStepReading> ReadAsync(CancellationToken token)
        {
            var today = await link.ReadTodayAsync(token);
            int steps = today.HasValue ? (int)Math.Min(int.MaxValue, today.Steps) : 0;
            return new HomeStepReading(HomeStepResult.Updated, HomeStepLink.Linked, steps);
        }

        private static HomeStepReading Unlinked(HomeStepResult result) =>
            new(result, HomeStepLink.Unlinked, 0);
    }
}
