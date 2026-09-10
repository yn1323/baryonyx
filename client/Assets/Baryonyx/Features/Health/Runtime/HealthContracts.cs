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

        public HealthReadResult(HealthReadStatus status, HealthDay[] days = null)
        {
            Status = status;
            Days = days ?? Array.Empty<HealthDay>();
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

    public sealed class HealthSession
    {
        public string UserId { get; }
        public string Token { get; }
        public DateTimeOffset ExpiresAt { get; }

        public HealthSession(string userId, string token, DateTimeOffset expiresAt)
        {
            UserId = userId;
            Token = token;
            ExpiresAt = expiresAt;
        }
    }

    public interface IHealthApi
    {
        Task<long> BeginSyncAsync(
            HealthSession session,
            string sourceId,
            string provider,
            CancellationToken token
        );
        Task SaveAsync(
            HealthSession session,
            string sourceId,
            long revision,
            HealthDay[] days,
            CancellationToken token
        );
    }
}
