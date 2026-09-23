using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.Health
{
    public sealed class HealthApiClient
    {
        private readonly ServerApi server;

        public HealthApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public async Task<long> BeginSyncAsync(
            AccountSession session,
            string sourceId,
            string provider,
            CancellationToken token
        )
        {
            var reply = await server.SendAsync<RevisionReply>(
                "/v1/health/syncs",
                "POST",
                new SyncRequest { sourceId = sourceId, provider = provider },
                session.Token,
                token
            );
            return reply.revision;
        }

        public Task SaveAsync(
            AccountSession session,
            string sourceId,
            long revision,
            HealthDay[] days,
            CancellationToken token
        ) =>
            server.SendAsync<EmptyReply>(
                "/v1/health/sources/" + Uri.EscapeDataString(sourceId) + "/days",
                "PUT",
                new SaveRequest { revision = revision, days = days },
                session.Token,
                token
            );

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
        private sealed class EmptyReply { }
    }
}
