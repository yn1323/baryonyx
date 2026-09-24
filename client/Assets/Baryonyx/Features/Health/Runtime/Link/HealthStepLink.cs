using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Health
{
    public enum HealthLinkStatus
    {
        Linked,
        PermissionRequired,

        // 許可画面を2回続けて閉じると、Health Connectは許可画面を表示しなくなる。
        SettingsRequired,
        InstallRequired,
        UpdateRequired,
    }

    public enum HealthSyncStatus
    {
        Synced,
        PermissionRequired,
        ReadFailed,
    }

    public readonly struct HealthStepReading
    {
        public HealthStepReading(bool hasValue, long steps)
        {
            HasValue = hasValue;
            Steps = steps;
        }

        public bool HasValue { get; }
        public long Steps { get; }
    }

    // 許可画面を閉じた回数。0に戻すのは許可を確認できたときだけ。
    public interface IHealthLinkStore
    {
        int PermissionDenials { get; set; }
    }

    public sealed class PlayerPrefsHealthLinkStore : IHealthLinkStore
    {
        private const string Key = "Health.PermissionDenials";

        public int PermissionDenials
        {
            get => PlayerPrefs.GetInt(Key, 0);
            set
            {
                PlayerPrefs.SetInt(Key, Math.Max(0, value));
                PlayerPrefs.Save();
            }
        }
    }

    // Health Connectの連携状態を確かめ、直近7日分をサーバーへ同期する。
    // TopとHomeで共有し、画面の状態や文言は持たない。
    public sealed class HealthStepLink
    {
        // Health Connectは許可画面を2回続けて閉じると、以降は許可画面を表示しない。
        // https://developer.android.com/health-and-fitness/health-connect/ui/permissions
        internal const int DenialsBeforeSettings = 2;

        private readonly IHealthStepProvider provider;
        private readonly IHealthStepServer server;
        private readonly IHealthLinkStore store;
        private readonly Func<DateTime> today;

        public HealthStepLink(
            IHealthStepProvider provider,
            IHealthStepServer server,
            IHealthLinkStore store,
            Func<DateTime> today = null
        )
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.today = today ?? (() => DateTime.Today);
        }

        public Task ConnectAsync(CancellationToken token) => server.ConnectAsync(token);

        public async Task<HealthLinkStatus> CheckAsync(CancellationToken token)
        {
            var availability = await provider.GetAvailabilityAsync(token);
            if (availability == HealthAvailability.Unavailable)
                return HealthLinkStatus.InstallRequired;
            if (availability == HealthAvailability.UpdateRequired)
                return HealthLinkStatus.UpdateRequired;
            if (await provider.GetStepsPermissionAsync(token) == HealthPermission.Granted)
            {
                store.PermissionDenials = 0;
                return HealthLinkStatus.Linked;
            }
            return store.PermissionDenials >= DenialsBeforeSettings
                ? HealthLinkStatus.SettingsRequired
                : HealthLinkStatus.PermissionRequired;
        }

        // 許可画面を表示できる状態のときだけOSの許可画面を開く。
        public async Task<HealthLinkStatus> RequestAsync(CancellationToken token)
        {
            var status = await CheckAsync(token);
            if (status != HealthLinkStatus.PermissionRequired)
                return status;
            if (await provider.RequestStepsPermissionAsync(token) == HealthPermission.Granted)
            {
                store.PermissionDenials = 0;
                return HealthLinkStatus.Linked;
            }
            store.PermissionDenials++;
            return store.PermissionDenials >= DenialsBeforeSettings
                ? HealthLinkStatus.SettingsRequired
                : HealthLinkStatus.PermissionRequired;
        }

        public void OpenSettings() => provider.OpenSettings();

        // Health Connectの直近7日分を読み、サーバーへ保存する。通信の失敗は例外で返す。
        public async Task<HealthSyncStatus> SyncAsync(CancellationToken token)
        {
            var read = await provider.ReadRecentStepsAsync(token);
            if (read.Status == HealthReadStatus.PermissionRequired)
                return HealthSyncStatus.PermissionRequired;
            if (read.Status != HealthReadStatus.Success || read.Days.Length != 7)
                return HealthSyncStatus.ReadFailed;
            // 読み取り全体が成功しても、日ごとの歩数が未許可・失敗なら欠測として保存しない。
            if (read.Days.Any(day => day.stepsStatus == "permission_required"))
                return HealthSyncStatus.PermissionRequired;
            if (read.Days.Any(day => day.stepsStatus == "failed"))
                return HealthSyncStatus.ReadFailed;
            await server.SaveAsync(read.Days, token);
            return HealthSyncStatus.Synced;
        }

        // サーバーに保存済みの今日の歩数を返す。
        public async Task<HealthStepReading> ReadTodayAsync(CancellationToken token)
        {
            string day = today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var match = (await server.ReadAsync(token)).FirstOrDefault(value =>
                value != null && value.day == day
            );
            return match != null && match.hasValue
                ? new HealthStepReading(true, match.steps)
                : new HealthStepReading(false, 0);
        }
    }
}
