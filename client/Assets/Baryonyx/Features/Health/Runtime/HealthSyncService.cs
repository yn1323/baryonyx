using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    public enum HealthSyncStatus
    {
        Idle,
        Running,
        Saved,
        NoValues,
        PermissionRequired,
        Unavailable,
        SignInRequired,
        Failed,
        Cancelled,
    }

    public sealed class HealthSyncService : IDisposable
    {
        private readonly IHealthDataProvider provider;
        private readonly IHealthApi api;
        private readonly string sourceId;
        private CancellationTokenSource active;
        private Task<HealthSyncStatus> running;
        private Task<HealthSyncStatus> restart;
        private HealthSession session;
        private bool foreground = true;

        public HealthSyncStatus Status { get; private set; }
        public DateTimeOffset? LastSavedAt { get; private set; }

        public HealthSyncService(IHealthDataProvider provider, IHealthApi api, string sourceId)
        {
            this.provider = provider;
            this.api = api;
            this.sourceId = sourceId;
        }

        public void SetSession(HealthSession value)
        {
            active?.Cancel();
            session = value;
            LastSavedAt = null;
            Status = value == null ? HealthSyncStatus.SignInRequired : HealthSyncStatus.Idle;
        }

        public void SetForeground(bool value)
        {
            foreground = value;
            if (!value)
                active?.Cancel();
        }

        public Task<HealthSyncStatus> SyncAsync()
        {
            if (running != null && !running.IsCompleted)
            {
                if (foreground && active?.IsCancellationRequested == true)
                {
                    if (restart == null || restart.IsCompleted)
                        restart = RestartAfterAsync(running);
                    return restart;
                }
                return running;
            }
            running = RunAsync();
            return running;
        }

        private async Task<HealthSyncStatus> RestartAfterAsync(Task<HealthSyncStatus> previous)
        {
            await previous;
            return await SyncAsync();
        }

        private async Task<HealthSyncStatus> RunAsync()
        {
            var current = session;
            if (current == null || current.ExpiresAt <= DateTimeOffset.UtcNow)
                return Status = HealthSyncStatus.SignInRequired;
            if (!foreground)
                return Status = HealthSyncStatus.Cancelled;
            using var cancellation = new CancellationTokenSource();
            active = cancellation;
            var token = cancellation.Token;
            Status = HealthSyncStatus.Running;
            try
            {
                if (await provider.GetAvailabilityAsync(token) != HealthAvailability.Available)
                    return Complete(HealthSyncStatus.Unavailable);
                if (await provider.GetPermissionAsync(token) == HealthPermission.NotGranted)
                    return Complete(HealthSyncStatus.PermissionRequired);
                var revision = await api.BeginSyncAsync(
                    current,
                    sourceId,
                    provider.ProviderId,
                    token
                );
                EnsureCurrent();
                var result = await provider.ReadRecentDaysAsync(token);
                EnsureCurrent();
                if (result.Status != HealthReadStatus.Success)
                    return Complete(
                        result.Status == HealthReadStatus.PermissionRequired
                            ? HealthSyncStatus.PermissionRequired
                            : HealthSyncStatus.Failed
                    );
                if (result.Days.Length != 7)
                    return Complete(HealthSyncStatus.Failed);
                await api.SaveAsync(current, sourceId, revision, result.Days, token);
                EnsureCurrent();
                LastSavedAt = DateTimeOffset.UtcNow;
                return Complete(
                    result.Days.Any(day => day.hasValue)
                        ? HealthSyncStatus.Saved
                        : HealthSyncStatus.NoValues
                );
            }
            catch (OperationCanceledException)
            {
                return Complete(HealthSyncStatus.Cancelled);
            }
            catch (HealthApiException exception)
            {
                return Complete(
                    exception.StatusCode == 401
                        ? HealthSyncStatus.SignInRequired
                        : HealthSyncStatus.Failed
                );
            }
            catch (Exception)
            {
                return Complete(HealthSyncStatus.Failed);
            }
            finally
            {
                if (active == cancellation)
                    active = null;
            }

            void EnsureCurrent()
            {
                token.ThrowIfCancellationRequested();
                if (!foreground || !ReferenceEquals(current, session))
                    throw new OperationCanceledException();
            }
            HealthSyncStatus Complete(HealthSyncStatus status)
            {
                if (ReferenceEquals(current, session))
                    Status = status;
                return status;
            }
        }

        public void Dispose()
        {
            SetSession(null);
        }
    }
}
