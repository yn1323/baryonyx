using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Adventure;
using Baryonyx.Inventory;

namespace Baryonyx.WorldMap
{
    /// <summary>Where the highway's progress is kept.</summary>
    public interface IWorldMapSource
    {
        Task<WorldMapProgress> LoadAsync(CancellationToken token);

        // 着いたマスを残し、残したあとの進み具合を返す。clear は、そのマスをクリアしたか。
        // 宝箱のマスをクリアしたときは、進み具合がその中身を持つ。
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
            new(
                state?.at,
                state?.cleared ?? Array.Empty<string>(),
                ToChest(state?.at, state?.chest)
            );

        // 知らない分類や品質、倍率が届いていない物（強化素材を除く）は中身から外す。
        private static AdventureDrop[] ToChest(string squareId, WorldMapApiClient.Drop[] drops) =>
            (drops ?? Array.Empty<WorldMapApiClient.Drop>())
                .Select(drop => ToDrop(squareId, drop))
                .Where(drop => drop != null)
                .ToArray();

        private static AdventureDrop ToDrop(string squareId, WorldMapApiClient.Drop drop)
        {
            if (
                drop == null
                || string.IsNullOrEmpty(drop.id)
                || !AdventureDrops.TryParse(drop.category, out var category)
            )
                return null;
            if (category is AdventureDropCategory.Item or AdventureDropCategory.Material)
                return new AdventureDrop(squareId, category, drop.id);
            if (
                drop.multiplier < Quality.MinMultiplier
                || !Quality.TryParse(drop.quality, out var quality)
            )
                return null;
            return new AdventureDrop(squareId, category, drop.id, drop.multiplier, quality);
        }
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
