using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.ExerciseRewards;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.Health
{
    // Health Connectの読み取り結果を、サーバー保存とルーン請求へ一度だけ渡す。
    // 歩数をゲーム中に監視せず、起動・更新・明示請求の操作境界でだけ実行する。
    // Google未接続でも、端末の秘密値によるゲストのセッションで保存と取得を行う。
    public sealed class HealthServerSync : IHealthStepServer, IDisposable
    {
        // 期限の直前に送った要求が途中で失効しないよう、早めにセッションを取り直す。
        private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

        private readonly AccountApiClient accounts;
        private readonly HealthApiClient health;
        private readonly ExerciseRewardsApiClient rewards;
        private readonly Func<string> guestSecret;
        private readonly string sourceKeyPrefix;
        private AccountSession session;
        private string sourceId;

        public HealthServerSync(
            AccountApiClient accounts,
            HealthApiClient health,
            ExerciseRewardsApiClient rewards,
            Func<string> guestSecret = null
        )
        {
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            this.guestSecret = guestSecret ?? GuestCredential.GetOrCreate;
            sourceKeyPrefix = "Health.RewardSourceId.";
        }

        public bool IsSignedIn => session != null && session.ExpiresAt > DateTimeOffset.UtcNow;

        public async Task<bool> SignInAsync(string idToken, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                return false;
            StartSession(await accounts.LoginAsync(idToken, token));
            return true;
        }

        public async Task ConnectAsync(CancellationToken token)
        {
            if (session != null && session.ExpiresAt - RenewBefore > DateTimeOffset.UtcNow)
                return;
            StartSession(await accounts.GuestLoginAsync(guestSecret(), token));
        }

        public Task SaveAsync(HealthDay[] days, CancellationToken token)
        {
            if (days == null || days.Length != 7)
                throw new ArgumentException(
                    "Exactly seven health days are required.",
                    nameof(days)
                );
            // サーバーは古い日から連続した区間とUTCの時刻を要求する。取得元ごとの並び順や
            // 時差の表記に依存しないよう、送信用の複製をそろえる。
            var ordered = days.OrderBy(day =>
                    DateTimeOffset.Parse(day.startAt, CultureInfo.InvariantCulture)
                )
                .Select(ToServerDay)
                .ToArray();
            return WithSessionAsync(
                async current =>
                {
                    var revision = await health.BeginSyncAsync(
                        current,
                        sourceId,
                        "health_connect",
                        token
                    );
                    await health.SaveAsync(current, sourceId, revision, ordered, token);
                    return true;
                },
                token
            );
        }

        public Task<HealthDay[]> ReadAsync(CancellationToken token) =>
            WithSessionAsync(
                async current =>
                {
                    try
                    {
                        return await health.ReadDaysAsync(current, sourceId, token);
                    }
                    catch (ServerApiException exception) when (exception.StatusCode == 404)
                    {
                        return Array.Empty<HealthDay>();
                    }
                },
                token
            );

        private static HealthDay ToServerDay(HealthDay day) =>
            new()
            {
                day = day.day,
                zone = day.zone,
                startAt = Utc(day.startAt),
                endAt = Utc(day.endAt),
                hasValue = day.hasValue,
                steps = day.hasValue ? day.steps : 0,
                observedAt = Utc(day.observedAt),
            };

        internal static string Utc(string timestamp) =>
            DateTimeOffset
                .Parse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        // 失効・取り消し済みのセッションは一度だけゲストで取り直して再実行する。
        private async Task<T> WithSessionAsync<T>(
            Func<AccountSession, Task<T>> operation,
            CancellationToken token
        )
        {
            await ConnectAsync(token);
            try
            {
                return await operation(session);
            }
            catch (ServerApiException exception) when (exception.StatusCode == 401)
            {
                session = null;
                await ConnectAsync(token);
                return await operation(session);
            }
        }

        private void StartSession(AccountSession created)
        {
            session = created;
            sourceId = PlayerPrefs.GetString(sourceKeyPrefix + created.UserId, "");
            if (!Guid.TryParse(sourceId, out _))
            {
                sourceId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(sourceKeyPrefix + created.UserId, sourceId);
                PlayerPrefs.Save();
            }
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
