using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The server's API of the nested world map: the squares paid for and the runes, and opening
    /// one square for its runes (server/src/features/world-atlas/routes.ts).
    /// </summary>
    public sealed class WorldAtlasApiClient
    {
        private const string Path = "/v1/world-atlas";
        private readonly ServerApi server;

        public WorldAtlasApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>(Path, "GET", null, session.Token, token);

        public Task<Opened> OpenAsync(
            AccountSession session,
            string requestId,
            string map,
            string squareId,
            CancellationToken token
        ) =>
            server.SendAsync<Opened>(
                Path + "/open",
                "POST",
                new OpenRequest
                {
                    requestId = requestId,
                    map = map,
                    squareId = squareId,
                },
                session.Token,
                token
            );

        [Serializable]
        private sealed class OpenRequest
        {
            public string requestId;
            public string map;
            public string squareId;
        }

        [Serializable]
        public sealed class State
        {
            public Square[] opened;
            public int runes;
        }

        [Serializable]
        public sealed class Square
        {
            public string map;
            public string square;
        }

        // 開けたマスと、払ったルーン（開けてあったマスは0）、払ったあとの残高。
        [Serializable]
        public sealed class Opened
        {
            public string map;
            public string squareId;
            public int runes;
            public int balanceAfter;
        }
    }
}
