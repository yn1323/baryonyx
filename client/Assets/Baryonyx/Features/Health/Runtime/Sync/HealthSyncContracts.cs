using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
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
