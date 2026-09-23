using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.ExerciseRewards
{
    public sealed class ExerciseRewardsApiClient
    {
        private readonly ServerApi server;

        public ExerciseRewardsApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<RewardClaim> ClaimAsync(
            AccountSession session,
            string sourceId,
            string requestId,
            CancellationToken token
        ) =>
            server.SendAsync<RewardClaim>(
                "/v1/exercise/rewards/claim",
                "POST",
                new ClaimRequest { sourceId = sourceId, requestId = requestId },
                session.Token,
                token
            );

        public Task<RewardDays> ReadDaysAsync(
            AccountSession session,
            string sourceId,
            CancellationToken token
        ) =>
            server.SendAsync<RewardDays>(
                "/v1/exercise/rewards/days?sourceId=" + Uri.EscapeDataString(sourceId),
                "GET",
                null,
                session.Token,
                token
            );

        public Task<RuneBalance> ReadBalanceAsync(
            AccountSession session,
            CancellationToken token
        ) => server.SendAsync<RuneBalance>("/v1/runes/balance", "GET", null, session.Token, token);

        [Serializable]
        private sealed class ClaimRequest
        {
            public string sourceId;
            public string requestId;
        }

        [Serializable]
        public sealed class RewardClaim
        {
            public long grantedRunes;
            public long balance;
            public RewardDay[] days;
        }

        [Serializable]
        public sealed class RewardDays
        {
            public RewardDay[] days;
        }

        [Serializable]
        public sealed class RewardDay
        {
            public string day;
            public string zone;
            public string activityType;
            public string metricType;
            public bool hasValue;
            public long observedValue;
            public long creditedThroughValue;
            public long creditedRunes;
            public string ruleVersion;
            public string lastObservedAt;
            public string updatedAt;
        }

        [Serializable]
        public sealed class RuneBalance
        {
            public long balance;
            public string updatedAt;
        }
    }
}
