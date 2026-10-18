using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.Combat.Records
{
    /// <summary>
    /// The server's API of revealed weaknesses: those a player has revealed, and adding those
    /// revealed in a battle (server/src/features/combat/routes.ts).
    /// </summary>
    public sealed class RevealedWeaknessApiClient
    {
        private const string Path = "/v1/combat/weaknesses";
        private readonly ServerApi server;

        public RevealedWeaknessApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>(Path, "GET", null, session.Token, token);

        public Task<State> RevealAsync(
            AccountSession session,
            Weakness[] weaknesses,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                Path,
                "POST",
                new RevealRequest { weaknesses = weaknesses },
                session.Token,
                token
            );

        [Serializable]
        private sealed class RevealRequest
        {
            public Weakness[] weaknesses;
        }

        [Serializable]
        public sealed class State
        {
            public Weakness[] weaknesses;
        }

        [Serializable]
        public sealed class Weakness
        {
            public string enemyId;
            public string element;

            // 最初に開示した日時。送るときは空のまま（サーバーが付ける）。
            public string revealedAt;
        }
    }
}
