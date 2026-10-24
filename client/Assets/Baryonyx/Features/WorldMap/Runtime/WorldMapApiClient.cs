using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The server's API of the highway's progress: reading it, and keeping the square the party
    /// reached, which gives a treasure square's contents (server/src/features/world-map/routes.ts).
    /// </summary>
    public sealed class WorldMapApiClient
    {
        private const string Path = "/v1/world-map";
        private readonly ServerApi server;

        public WorldMapApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public Task<State> ReadAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<State>(Path, "GET", null, session.Token, token);

        public Task<State> MoveAsync(
            AccountSession session,
            string squareId,
            bool clear,
            CancellationToken token
        ) =>
            server.SendAsync<State>(
                Path + "/move",
                "POST",
                new MoveRequest { squareId = squareId, clear = clear },
                session.Token,
                token
            );

        [Serializable]
        private sealed class MoveRequest
        {
            public string squareId;
            public bool clear;
        }

        [Serializable]
        public sealed class State
        {
            public string at;
            public string[] cleared;

            // 宝箱のマスをクリアした移動で、その宝箱の中身。ほかの移動と読み込みでは空になる。
            public Drop[] chest;
        }

        // 宝箱の1つの中身。category は weapon・armor・accessory・skill・material。
        [Serializable]
        public sealed class Drop
        {
            public string category;
            public string id;

            // 倍率（×1.37なら1.37）と品質（S・A・B・C）。強化素材は項目がなく、JsonUtilityでは0と空になる。
            public float multiplier;
            public string quality;
        }
    }
}
