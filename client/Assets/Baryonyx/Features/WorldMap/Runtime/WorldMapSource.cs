using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;

namespace Baryonyx.WorldMap
{
    /// <summary>Where the highway's progress is kept.</summary>
    public interface IWorldMapSource
    {
        Task<WorldMapProgress> LoadAsync(CancellationToken token);

        // 着いたマスを残し、残したあとの進み具合を返す。clear は、そのマスをクリアしたか。
        Task<WorldMapProgress> MoveAsync(string squareId, bool clear, CancellationToken token);
    }

    /// <summary>Keeps the progress on the game server with the shared session.</summary>
    public sealed class WorldMapServerSource : IWorldMapSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly WorldMapApiClient api;

        public WorldMapServerSource(IAccountSessionRunner sessions, WorldMapApiClient api)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<WorldMapProgress> LoadAsync(CancellationToken token) =>
            ToProgress(
                await sessions.WithSessionAsync(session => api.ReadAsync(session, token), token)
            );

        public async Task<WorldMapProgress> MoveAsync(
            string squareId,
            bool clear,
            CancellationToken token
        ) =>
            ToProgress(
                await sessions.WithSessionAsync(
                    session => api.MoveAsync(session, squareId, clear, token),
                    token
                )
            );

        public static WorldMapProgress ToProgress(WorldMapApiClient.State state) =>
            new(state?.at, state?.cleared ?? Array.Empty<string>());
    }

    /// <summary>
    /// The progress without a server (the showcase, or no server URL): the prototype's progress,
    /// kept only while the app runs.
    /// </summary>
    public sealed class WorldMapMemorySource : IWorldMapSource
    {
        private WorldMapProgress kept;

        public WorldMapMemorySource(WorldMapProgress start = null) =>
            kept = start ?? WorldMapProgress.Mock;

        public Task<WorldMapProgress> LoadAsync(CancellationToken token) => Task.FromResult(kept);

        public Task<WorldMapProgress> MoveAsync(
            string squareId,
            bool clear,
            CancellationToken token
        )
        {
            kept = kept.Moved(squareId, clear);
            return Task.FromResult(kept);
        }
    }
}
