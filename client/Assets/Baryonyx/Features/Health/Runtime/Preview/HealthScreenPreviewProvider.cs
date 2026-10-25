#if UNITY_EDITOR || !UNITY_ANDROID
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    // Editorとテストで使うサンプルの歩数。Androidの実機とサーバーの同期では使わない。
    public sealed class HealthScreenPreviewProvider : IHealthStepProvider
    {
        private readonly Func<DateTimeOffset> clock;

        public HealthScreenPreviewProvider(
            Func<DateTimeOffset> clock = null,
            HealthPermission permission = HealthPermission.Granted
        )
        {
            this.clock = clock ?? (() => DateTimeOffset.UtcNow);
            Permission = permission;
        }

        // 未連携の画面をEditorで確認するための状態。許可を要求すると許可済みに変わる。
        public HealthPermission Permission { get; set; }

        public Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(HealthAvailability.Available);
        }

        public Task<HealthPermission> GetStepsPermissionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Permission);
        }

        public Task<HealthPermission> RequestStepsPermissionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Permission = HealthPermission.Granted;
            return Task.FromResult(Permission);
        }

        public Task<HealthReadResult> ReadRecentStepsAsync(CancellationToken token) =>
            ReadRecentDaysAsync(token);

        // 直近7日分のサンプルの歩数（朝4時区切りの日本の日）。
        public Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Permission != HealthPermission.Granted)
                return Task.FromResult(new HealthReadResult(HealthReadStatus.PermissionRequired));
            var now = clock().ToOffset(TimeSpan.FromHours(9));
            var today = new DateTimeOffset(HealthDays.DayOf(now.DateTime), now.Offset);
            long[] steps = { 6432, 0, 0, 8214, 10532, 4218, 7500 };
            var days = new HealthDay[steps.Length];
            for (int i = 0; i < days.Length; i++)
            {
                var start = today.AddDays(-i).AddHours(HealthDays.StartHour);
                days[i] = new HealthDay
                {
                    day = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    zone = "Asia/Tokyo",
                    startAt = start.ToString("O", CultureInfo.InvariantCulture),
                    endAt = (i == 0 ? now : start.AddDays(1)).ToString(
                        "O",
                        CultureInfo.InvariantCulture
                    ),
                    hasValue = i != 1,
                    steps = steps[i],
                    observedAt = now.ToString("O", CultureInfo.InvariantCulture),
                };
            }
            return Task.FromResult(new HealthReadResult(HealthReadStatus.Success, days));
        }

        // Editorには設定画面がないため、設定で許可した場合と同じ状態にする。
        public void OpenSettings() => Permission = HealthPermission.Granted;
    }
}
#endif
