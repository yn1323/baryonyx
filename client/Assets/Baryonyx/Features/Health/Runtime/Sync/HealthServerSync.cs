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
    // セッションはほかの機能と共有する AccountSessionRunner が持ち、ここは歩数の取得元（端末）のIDを持つ。
    public sealed class HealthServerSync : IHealthStepServer, IDisposable
    {
        private const string SourceKeyPrefix = "Health.RewardSourceId.";

        private readonly AccountApiClient accounts;
        private readonly HealthApiClient health;
        private readonly ExerciseRewardsApiClient rewards;
        private string sourceId;

        public HealthServerSync(
            AccountSessionRunner sessions,
            AccountApiClient accounts,
            HealthApiClient health,
            ExerciseRewardsApiClient rewards
        )
        {
            Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            this.health = health ?? throw new ArgumentNullException(nameof(health));
            this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
            Sessions.Started += OnSessionStarted;
        }

        /// <summary>The session shared with the other features' server sources.</summary>
        public AccountSessionRunner Sessions { get; }

        public bool IsSignedIn => Sessions.IsSignedIn;

        public async Task<bool> SignInAsync(string idToken, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                return false;
            Sessions.Start(await accounts.LoginAsync(idToken, token));
            return true;
        }

        public Task ConnectAsync(CancellationToken token) => Sessions.ConnectAsync(token);

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
            return Sessions.WithSessionAsync(
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
            Sessions.WithSessionAsync(
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

        public Task<long> ReadRunesAsync(CancellationToken token) =>
            Sessions.WithSessionAsync(
                async current => (await rewards.ReadBalanceAsync(current, token)).balance,
                token
            );

        public Task<HealthRuneClaim> ClaimRunesAsync(CancellationToken token) =>
            Sessions.WithSessionAsync(
                async current =>
                {
                    var claim = await rewards.ClaimAsync(
                        current,
                        sourceId,
                        Guid.NewGuid().ToString(),
                        token
                    );
                    return new HealthRuneClaim(claim.grantedRunes, claim.balance);
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

        // 歩数の取得元（端末）のIDは、ユーザーごとに端末へ保存したものを使う。
        private void OnSessionStarted(AccountSession created)
        {
            sourceId = PlayerPrefs.GetString(SourceKeyPrefix + created.UserId, "");
            if (!Guid.TryParse(sourceId, out _))
            {
                sourceId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(SourceKeyPrefix + created.UserId, sourceId);
                PlayerPrefs.Save();
            }
        }

        public async Task SignOutAsync(CancellationToken token)
        {
            var current = Sessions.End();
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
                ? Sessions.Current
                : throw new InvalidOperationException("Sign in to the reward service first.");

        public void Dispose()
        {
            Sessions.Started -= OnSessionStarted;
            Sessions.End();
            sourceId = null;
        }
    }
}
