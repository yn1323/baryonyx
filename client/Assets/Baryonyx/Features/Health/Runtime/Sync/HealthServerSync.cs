using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.ExerciseRewards;
using UnityEngine;

namespace Baryonyx.Health
{
    // Health Connectの読み取り結果を、サーバー保存とルーン請求へ一度だけ渡す。
    // 歩数をゲーム中に監視せず、起動・更新・明示請求の操作境界でだけ実行する。
    public sealed class HealthServerSync : IDisposable
    {
        private readonly AccountApiClient accounts;
        private readonly HealthApiClient health;
        private readonly ExerciseRewardsApiClient rewards;
        private readonly string sourceKeyPrefix;
        private AccountSession session;
        private string sourceId;

        public HealthServerSync(
            AccountApiClient accounts,
            HealthApiClient health,
            ExerciseRewardsApiClient rewards
        )
        {
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            sourceKeyPrefix = "Health.RewardSourceId.";
        }

        public bool IsSignedIn => session != null && session.ExpiresAt > DateTimeOffset.UtcNow;

        public async Task<bool> SignInAsync(string idToken, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                return false;
            var created = await accounts.LoginAsync(idToken, token);
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
                await accounts.LogoutAsync(current, token);
        }

        public async Task<ExerciseRewardsApiClient.RewardClaim> SyncAndClaimAsync(
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
            var revision = await health.BeginSyncAsync(current, sourceId, "health_connect", token);
            await health.SaveAsync(current, sourceId, revision, days, token);
            return await ClaimAsync(token);
        }

        public Task<ExerciseRewardsApiClient.RewardClaim> ClaimAsync(CancellationToken token)
        {
            var current = RequireSession();
            return rewards.ClaimAsync(current, sourceId, Guid.NewGuid().ToString(), token);
        }

        public Task<ExerciseRewardsApiClient.RewardDays> ReadDaysAsync(CancellationToken token)
        {
            var current = RequireSession();
            return rewards.ReadDaysAsync(current, sourceId, token);
        }

        public Task<ExerciseRewardsApiClient.RuneBalance> ReadBalanceAsync(CancellationToken token)
        {
            var current = RequireSession();
            return rewards.ReadBalanceAsync(current, token);
        }

        private AccountSession RequireSession() =>
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
