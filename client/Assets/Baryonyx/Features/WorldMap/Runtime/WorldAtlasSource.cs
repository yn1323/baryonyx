using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;

namespace Baryonyx.WorldMap
{
    /// <summary>What the nested world map keeps on the server: the squares paid for, and the runes.</summary>
    public sealed class WorldAtlasState
    {
        public WorldAtlasState(IEnumerable<(string Map, string Square)> opened, int runes)
        {
            Opened = new List<(string, string)>(opened);
            Runes = runes;
        }

        public IReadOnlyList<(string Map, string Square)> Opened { get; }
        public int Runes { get; }
    }

    /// <summary>Where the squares paid for on the nested world map are kept.</summary>
    public interface IWorldAtlasSource
    {
        Task<WorldAtlasState> LoadAsync(CancellationToken token);

        // マスを1つ、ルーンを払って開け、払ったあとの残高を返す。値段はサーバーが決める。
        // 開けたマスを送り直しても払わない。足りない・つながっていないときは例外になる。
        Task<int> OpenAsync(string requestId, string map, string square, CancellationToken token);
    }

    /// <summary>Keeps the squares on the game server with the shared session.</summary>
    public sealed class WorldAtlasServerSource : IWorldAtlasSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly WorldAtlasApiClient api;

        public WorldAtlasServerSource(IAccountSessionRunner sessions, WorldAtlasApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<WorldAtlasState> LoadAsync(CancellationToken token)
        {
            var state = await sessions.WithSessionAsync(
                session => api.ReadAsync(session, token),
                token
            );
            var opened = new List<(string, string)>();
            foreach (var square in state?.opened ?? Array.Empty<WorldAtlasApiClient.Square>())
                if (square != null && !string.IsNullOrEmpty(square.map))
                    opened.Add((square.map, square.square));
            return new WorldAtlasState(opened, state?.runes ?? 0);
        }

        public async Task<int> OpenAsync(
            string requestId,
            string map,
            string square,
            CancellationToken token
        ) =>
            (
                await sessions.WithSessionAsync(
                    session => api.OpenAsync(session, requestId, map, square, token),
                    token
                )
            ).balanceAfter;
    }

    /// <summary>
    /// The squares without a server (the showcase, tests, or no server URL): runes that last
    /// while the app runs, and the prices of the layouts.
    /// </summary>
    public sealed class WorldAtlasMemorySource : IWorldAtlasSource
    {
        private readonly HashSet<(string, string)> opened = new();
        private readonly Func<string, string, int> priceOf;

        public WorldAtlasMemorySource(int runes, Func<string, string, int> priceOf)
        {
            Runes = runes;
            this.priceOf = priceOf ?? throw new ArgumentNullException(nameof(priceOf));
        }

        public int Runes { get; private set; }

        public Task<WorldAtlasState> LoadAsync(CancellationToken token) =>
            Task.FromResult(new WorldAtlasState(opened, Runes));

        public Task<int> OpenAsync(
            string requestId,
            string map,
            string square,
            CancellationToken token
        )
        {
            if (opened.Contains((map, square)))
                return Task.FromResult(Runes);
            int price = priceOf(map, square);
            if (price > Runes)
                throw new ServerApiException(409, WorldAtlasSession.InsufficientRunesError);
            Runes -= price;
            opened.Add((map, square));
            return Task.FromResult(Runes);
        }
    }
}
