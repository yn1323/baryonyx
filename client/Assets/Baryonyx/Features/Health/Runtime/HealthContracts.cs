using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    public enum HealthAvailability
    {
        Available,
        Unavailable,
        UpdateRequired,
    }

    public enum HealthPermission
    {
        Granted,
        NotGranted,
        Unknown,
    }

    public enum HealthReadStatus
    {
        Success,
        PermissionRequired,
        Unavailable,
        Failed,
    }

    [Serializable]
    public sealed class HealthDay
    {
        public string day;
        public string zone;
        public string startAt;
        public string endAt;
        public bool hasValue;
        public long steps;
        public string observedAt;

        // 取得元での歩数の状態（success・empty・permission_required・failed）。サーバーへは送らない。
        public string stepsStatus;
    }

    public sealed class HealthReadResult
    {
        public HealthReadStatus Status { get; }
        public HealthDay[] Days { get; }
        public string RawJson { get; }

        public HealthReadResult(
            HealthReadStatus status,
            HealthDay[] days = null,
            string rawJson = null
        )
        {
            Status = status;
            Days = days ?? Array.Empty<HealthDay>();
            RawJson = rawJson;
        }
    }

    public interface IHealthDataProvider
    {
        string ProviderId { get; }
        Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token);
        Task<HealthPermission> GetPermissionAsync(CancellationToken token);
        Task<HealthPermission> RequestPermissionAsync(CancellationToken token);

        // 歩数の読み取り権限だけを確かめる。ほかの記録だけを許可した状態は未許可として返す。
        Task<HealthPermission> GetStepsPermissionAsync(CancellationToken token);
        Task<HealthPermission> RequestStepsPermissionAsync(CancellationToken token);
        Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token);
        void OpenSettings();
    }
}
