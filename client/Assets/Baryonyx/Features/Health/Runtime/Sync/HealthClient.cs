using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Uralstech.UMoth.GoogleSignIn;
#endif

namespace Baryonyx.Health
{
    // サーバー同期を組み込む画面向けの入口。ローカル表示画面からは呼ばない。
    // Initialize後もログイン・権限要求は明示操作で開始する。
    public sealed class HealthClient : MonoBehaviour
    {
        private HealthApiClient api;
        private IHealthDataProvider provider;
        private HealthSyncService sync;
        private HealthSession session;
        private CancellationTokenSource lifetime;
        private string sourceId;
        private bool linked;
        private bool authBusy;
        private bool permissionBusy;
        private bool paused;
        private bool focused = true;
        private int generation;
        private DateTimeOffset lastAttempt;
#if UNITY_ANDROID && !UNITY_EDITOR
        private GoogleSignInManager google;
#endif
        public HealthSyncStatus Status => sync?.Status ?? HealthSyncStatus.Idle;
        public DateTimeOffset? LastSavedAt => sync?.LastSavedAt;
        public bool IsSignedIn => session != null && session.ExpiresAt > DateTimeOffset.UtcNow;

        public void Initialize(string serverUrl, string googleServerClientId)
        {
            if (sync != null)
                throw new InvalidOperationException("Already initialized.");
            if (string.IsNullOrWhiteSpace(googleServerClientId))
                throw new ArgumentException("Google server client ID is required.");
            api = new HealthApiClient(serverUrl);
            provider = new HealthConnectProvider();
            lifetime = new CancellationTokenSource();
            sourceId = Guid.NewGuid().ToString();
            sync = new HealthSyncService(provider, api, sourceId);
            sync.SetForeground(!paused && focused);
#if UNITY_ANDROID && !UNITY_EDITOR
            google = gameObject.AddComponent<GoogleSignInManager>();
            google.ServerClientId = googleServerClientId;
#endif
        }

        public async Task<bool> SignInAsync()
        {
            RequireInitialized();
            if (authBusy || permissionBusy || paused || !focused)
                return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            authBusy = true;
            int attempt = ++generation;
            var previousSession = session;
            session = null;
            linked = false;
            sync.SetSession(null);
            try
            {
                if (previousSession != null)
                {
                    try
                    {
                        await api.LogoutAsync(previousSession, lifetime.Token);
                    }
                    catch (HealthApiException) { }
                }
                // UMothのOS操作は完了まで待ち、キャンセル後の古い通知を次の操作へ混ぜない。
                var (credential, _) = await google.SignInAsync(
                    filterByAuthorizedAccount: false,
                    autoSelectSignIn: false
                );
                if (credential == null || attempt != generation || lifetime.IsCancellationRequested)
                    return false;
                while (paused || !focused)
                {
                    await Task.Yield();
                    lifetime.Token.ThrowIfCancellationRequested();
                }
                var created = await api.LoginAsync(credential.IdToken, lifetime.Token);
                if (attempt != generation)
                {
                    await api.LogoutAsync(created, lifetime.Token);
                    return false;
                }
                session = created;
                // 端末の取得元IDはユーザーごとに分離し、アカウント切り替え時に共有しない。
                string sourceKey = "Health.SourceId." + created.UserId;
                sourceId = PlayerPrefs.GetString(sourceKey, "");
                if (!Guid.TryParse(sourceId, out _))
                {
                    sourceId = Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(sourceKey, sourceId);
                    PlayerPrefs.Save();
                }
                sync.Dispose();
                sync = new HealthSyncService(provider, api, sourceId);
                sync.SetForeground(!paused && focused);
                sync.SetSession(session);
                return true;
            }
            finally
            {
                authBusy = false;
                TryResumeSync();
            }
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public async Task SignOutAsync()
        {
            RequireInitialized();
            generation++;
            var previous = session;
            session = null;
            linked = false;
            sync.SetSession(null);
            try
            {
                if (previous != null)
                    await api.LogoutAsync(previous, lifetime.Token);
            }
            catch (HealthApiException)
            { /* オフライン時もローカルのログアウトを維持する。 */
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!authBusy)
            {
                authBusy = true;
                try
                {
                    await google.SignOutAsync();
                }
                finally
                {
                    authBusy = false;
                }
            }
#endif
        }

        public async Task<HealthPermission> ConnectHealthAsync()
        {
            RequireInitialized();
            if (permissionBusy || authBusy || paused || !focused)
                throw new InvalidOperationException("Wait for the current operation.");
            permissionBusy = true;
            int attempt = generation;
            try
            {
                var permission = await provider.RequestPermissionAsync(lifetime.Token);
                if (attempt == generation)
                    linked = true;
                return permission;
            }
            finally
            {
                permissionBusy = false;
                TryResumeSync();
            }
        }

        public Task<HealthSyncStatus> SyncNowAsync()
        {
            RequireInitialized();
            if (authBusy || permissionBusy)
                return Task.FromResult(HealthSyncStatus.Cancelled);
            return sync.SyncAsync();
        }

        public async Task<HealthApiClient.StoredDays> ReadSavedAsync()
        {
            RequireInitialized();
            if (!IsSignedIn)
                throw new InvalidOperationException("Sign in first.");
            var current = session;
            var result = await api.ReadSavedAsync(current, sourceId, lifetime.Token);
            if (!ReferenceEquals(current, session))
                throw new OperationCanceledException();
            return result;
        }

        public Task<HealthAvailability> GetHealthAvailabilityAsync()
        {
            RequireInitialized();
            return provider.GetAvailabilityAsync(lifetime.Token);
        }

        public void OpenHealthSettings()
        {
            RequireInitialized();
            provider.OpenSettings();
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            ApplyForeground();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            ApplyForeground();
        }

        private void ApplyForeground()
        {
            sync?.SetForeground(!paused && focused);
            TryResumeSync();
        }

        private void TryResumeSync()
        {
            if (
                sync == null
                || paused
                || !focused
                || authBusy
                || permissionBusy
                || !linked
                || !IsSignedIn
                || (
                    DateTimeOffset.UtcNow - lastAttempt < TimeSpan.FromSeconds(1)
                    && Status != HealthSyncStatus.Running
                    && Status != HealthSyncStatus.Cancelled
                )
            )
                return;
            lastAttempt = DateTimeOffset.UtcNow;
            _ = sync.SyncAsync();
        }

        private void RequireInitialized()
        {
            if (sync == null)
                throw new InvalidOperationException("Call Initialize first.");
        }

        private void OnDestroy()
        {
            generation++;
            sync?.Dispose();
            lifetime?.Cancel();
            lifetime?.Dispose();
        }
    }
}
