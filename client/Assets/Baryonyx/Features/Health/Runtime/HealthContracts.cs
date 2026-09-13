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
        Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token);
        void OpenSettings();
    }
}
