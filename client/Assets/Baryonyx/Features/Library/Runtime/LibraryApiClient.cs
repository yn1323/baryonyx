using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.Library
{
    /// <summary>
    /// The server's library API: what the player has found and the daily steps
    /// (server/src/features/library/routes.ts).
    /// </summary>
    public sealed class LibraryApiClient
    {
        private readonly ServerApi server;

        public LibraryApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>("/v1/library", "GET", null, session.Token, token);

        public Task<Steps> ReadStepsAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<Steps>("/v1/library/steps", "GET", null, session.Token, token);

        [Serializable]
        public sealed class State
        {
            public Discovery[] discoveries;
        }

        [Serializable]
        public sealed class Discovery
        {
            public string kind;
            public string entryId;
            public string discoveredAt;
            public int defeated;
        }

        [Serializable]
        public sealed class Steps
        {
            public Day[] days;
        }

        [Serializable]
        public sealed class Day
        {
            public string day;
            public int steps;
        }
    }
}
