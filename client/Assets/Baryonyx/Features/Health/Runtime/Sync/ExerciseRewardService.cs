using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Health
{
    // Health Connectの読み取り結果を、サーバー保存とルーン請求へ一度だけ渡す。
    // 歩数をゲーム中に監視せず、起動・更新・明示請求の操作境界でだけ実行する。
    public sealed class ExerciseRewardService : IDisposable
    {
        private readonly HealthApiClient api;
        private readonly string sourceKeyPrefix;
        private HealthSession session;
        private string sourceId;

        public ExerciseRewardService(HealthApiClient api)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            sourceKeyPrefix = "Health.RewardSourceId.";
        }

        public bool IsSignedIn => session != null && session.ExpiresAt > DateTimeOffset.UtcNow;

        public async Task<bool> SignInAsync(string idToken, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                return false;
            var created = await api.LoginAsync(idToken, token);
            session = created;
            sourceId = PlayerPrefs.GetString(sourceKeyPrefix + created.UserId, "");
            if (!Guid.TryParse(sourceId, out _))
            {
                sourceId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(sourceKeyPrefix + created.UserId, sourceId);
                PlayerPrefs.Save();
            }
            return true;
        }

        public async Task SignOutAsync(CancellationToken token)
        {
            var current = session;
            session = null;
            sourceId = null;
            if (current != null)
                await api.LogoutAsync(current, token);
        }

        public async Task<HealthApiClient.RewardClaim> SyncAndClaimAsync(
            HealthDay[] days,
            CancellationToken token
        )
        {
            var current = RequireSession();
            if (days == null || days.Length != 7)
                throw new ArgumentException(
                    "Exactly seven health days are required.",
                    nameof(days)
                );
            var revision = await api.BeginSyncAsync(current, sourceId, "health_connect", token);
            await api.SaveAsync(current, sourceId, revision, days, token);
            return await ClaimAsync(token);
        }

        public Task<HealthApiClient.RewardClaim> ClaimAsync(CancellationToken token)
        {
            var current = RequireSession();
            return api.ClaimRewardsAsync(current, sourceId, Guid.NewGuid().ToString(), token);
        }

        public Task<HealthApiClient.RewardDays> ReadDaysAsync(CancellationToken token)
        {
            var current = RequireSession();
            return api.ReadRewardDaysAsync(current, sourceId, token);
        }

        public Task<HealthApiClient.RuneBalance> ReadBalanceAsync(CancellationToken token)
        {
            var current = RequireSession();
            return api.ReadRuneBalanceAsync(current, token);
        }

        private HealthSession RequireSession() =>
            IsSignedIn
                ? session
                : throw new InvalidOperationException("Sign in to the reward service first.");

        public void Dispose()
        {
            session = null;
            sourceId = null;
        }
    }
}
