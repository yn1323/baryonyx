using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Baryonyx.Health
{
    public sealed class HealthApiException : Exception
    {
        public long StatusCode { get; }

        public HealthApiException(long statusCode)
            : base("Health API request failed")
        {
            StatusCode = statusCode;
        }
    }

    public sealed class HealthApiClient : IHealthApi
    {
        private readonly string baseUrl;

        public HealthApiClient(string baseUrl)
        {
            var uri = new Uri(baseUrl);
            if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
                throw new ArgumentException("HTTPS is required except for loopback development.");
            if (
                !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
            )
                throw new ArgumentException(
                    "Use a server base URL without credentials, query or fragment."
                );
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<HealthSession> LoginAsync(string idToken, CancellationToken token)
        {
            var reply = await SendAsync<LoginReply>(
                "/v1/auth/google",
                "POST",
                new LoginRequest { idToken = idToken },
                null,
                token
            );
            return new HealthSession(
                reply.userId,
                reply.token,
                DateTimeOffset.Parse(reply.expiresAt)
            );
        }

        public Task LogoutAsync(HealthSession session, CancellationToken token) =>
            SendAsync<EmptyReply>("/v1/auth/logout", "POST", null, session, token);

        public async Task<long> BeginSyncAsync(
            HealthSession session,
            string sourceId,
            string provider,
            CancellationToken token
        )
        {
            var reply = await SendAsync<RevisionReply>(
                "/v1/health/syncs",
                "POST",
                new SyncRequest { sourceId = sourceId, provider = provider },
                session,
                token
            );
            return reply.revision;
        }

        public Task SaveAsync(
            HealthSession session,
            string sourceId,
            long revision,
            HealthDay[] days,
            CancellationToken token
        ) =>
            SendAsync<EmptyReply>(
                SourcePath(sourceId),
                "PUT",
                new SaveRequest { revision = revision, days = days },
                session,
                token
            );

        public Task<StoredDays> ReadSavedAsync(
            HealthSession session,
            string sourceId,
            CancellationToken token
        ) => SendAsync<StoredDays>(SourcePath(sourceId), "GET", null, session, token);

        public Task<RewardClaim> ClaimRewardsAsync(
            HealthSession session,
            string sourceId,
            string requestId,
            CancellationToken token
        ) =>
            SendAsync<RewardClaim>(
                "/v1/exercise/rewards/claim",
                "POST",
                new ClaimRequest { sourceId = sourceId, requestId = requestId },
                session,
                token
            );

        public Task<RewardDays> ReadRewardDaysAsync(
            HealthSession session,
            string sourceId,
            CancellationToken token
        ) =>
            SendAsync<RewardDays>(
                "/v1/exercise/rewards/days?sourceId=" + Uri.EscapeDataString(sourceId),
                "GET",
                null,
                session,
                token
            );

        public Task<RuneBalance> ReadRuneBalanceAsync(
            HealthSession session,
            CancellationToken token
        ) => SendAsync<RuneBalance>("/v1/runes/balance", "GET", null, session, token);

        private static string SourcePath(string sourceId) =>
            "/v1/health/sources/" + Uri.EscapeDataString(sourceId) + "/days";

        private async Task<T> SendAsync<T>(
            string path,
            string method,
            object body,
            HealthSession session,
            CancellationToken token
        )
            where T : new()
        {
            token.ThrowIfCancellationRequested();
            using var request = new UnityWebRequest(baseUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 30,
                redirectLimit = 0,
            };
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(body))
                );
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (session != null)
                request.SetRequestHeader("Authorization", "Bearer " + session.Token);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    request.Abort();
                    token.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
            token.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
                throw new HealthApiException(request.responseCode);
            return request.responseCode == 204
                ? new T()
                : JsonUtility.FromJson<T>(request.downloadHandler.text);
        }

        [Serializable]
        private sealed class LoginRequest
        {
            public string idToken;
        }

        [Serializable]
        private sealed class LoginReply
        {
            public string token;
            public string userId;
            public string expiresAt;
        }

        [Serializable]
        private sealed class SyncRequest
        {
            public string sourceId;
            public string provider;
        }

        [Serializable]
        private sealed class SaveRequest
        {
            public long revision;
            public HealthDay[] days;
        }

        [Serializable]
        private sealed class RevisionReply
        {
            public long revision;
        }

        [Serializable]
        private sealed class ClaimRequest
        {
            public string sourceId;
            public string requestId;
        }

        [Serializable]
        private sealed class EmptyReply { }

        [Serializable]
        public sealed class StoredDays
        {
            public string provider;
            public long revision;
            public StoredDay[] days;
        }

        [Serializable]
        public sealed class StoredDay
        {
            public string day;
            public string zone;
            public bool hasValue;
            public long steps;
            public bool hasLastKnownValue;
            public long lastKnownSteps;
            public string lastKnownObservedAt;
            public string observedAt;
            public string receivedAt;
            public long revision;
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
