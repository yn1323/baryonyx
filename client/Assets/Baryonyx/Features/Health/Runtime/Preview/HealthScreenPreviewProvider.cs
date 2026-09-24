#if UNITY_EDITOR || !UNITY_ANDROID
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Newtonsoft.Json.Linq;

namespace Baryonyx.Health
{
    // Used only by the local screen preview, never by the Android player or server sync.
    public sealed class HealthScreenPreviewProvider
        : IGoogleSignInProvider,
            IHealthDataProvider,
            IHealthStepProvider
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

        public string ProviderId => "preview";

        public Task<GoogleSignInStatus> SignInAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(GoogleSignInStatus.Success);
        }

        public Task<bool> SignOutAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }

        public Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(HealthAvailability.Available);
        }

        public Task<HealthPermission> GetPermissionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Permission);
        }

        public Task<HealthPermission> RequestPermissionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Permission = HealthPermission.Granted;
            return Task.FromResult(Permission);
        }

        // プレビューは歩数とほかの記録の許可を区別しない。
        public Task<HealthPermission> GetStepsPermissionAsync(CancellationToken token) =>
            GetPermissionAsync(token);

        public Task<HealthPermission> RequestStepsPermissionAsync(CancellationToken token) =>
            RequestPermissionAsync(token);

        public Task<HealthReadResult> ReadRecentStepsAsync(CancellationToken token) =>
            ReadRecentDaysAsync(token);

        public Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Permission != HealthPermission.Granted)
                return Task.FromResult(new HealthReadResult(HealthReadStatus.PermissionRequired));
            var now = clock().ToOffset(TimeSpan.FromHours(9));
            var today = new DateTimeOffset(now.Date, now.Offset);
            long[] steps = { 6432, 0, 0, 8214, 10532, 4218, 7500 };
            var days = new HealthDay[steps.Length];
            var jsonDays = new JArray();
            for (int i = 0; i < days.Length; i++)
            {
                var start = today.AddDays(-i);
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
                var jsonDay = JObject.FromObject(days[i]);
                jsonDay["sample"] = true;
                jsonDay["stepsStatus"] = days[i].hasValue ? "success" : "empty";
                jsonDay["records"] = new JObject
                {
                    ["bloodPressure"] = new JObject
                    {
                        ["status"] = i < 2 ? "success" : "empty",
                        ["truncated"] = false,
                        ["records"] =
                            i < 2
                                ? new JArray(
                                    new JObject
                                    {
                                        ["time"] = start.ToString(
                                            "O",
                                            CultureInfo.InvariantCulture
                                        ),
                                        ["sourceApp"] = "example.preview.health",
                                        ["systolicMmHg"] = 118,
                                        ["diastolicMmHg"] = 76,
                                    }
                                )
                                : new JArray(),
                    },
                    ["weight"] = new JObject
                    {
                        ["status"] = "permission_required",
                        ["truncated"] = false,
                        ["records"] = new JArray(),
                    },
                };
                jsonDays.Add(jsonDay);
            }
            return Task.FromResult(
                new HealthReadResult(
                    HealthReadStatus.Success,
                    days,
                    new JObject { ["status"] = "success", ["days"] = jsonDays }.ToString()
                )
            );
        }

        // Editorには設定画面がないため、設定で許可した場合と同じ状態にする。
        public void OpenSettings() => Permission = HealthPermission.Granted;
    }
}
#endif
