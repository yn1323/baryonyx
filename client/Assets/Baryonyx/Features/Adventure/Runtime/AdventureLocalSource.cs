using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure kept only while the app runs, for the editor without a server and the
    /// showcase. It follows the server's rules (server/src/features/adventure) on a route made
    /// from a seed of its own, like the server's, so the loop can be played through without a
    /// server. Drops are told but not added to the belongings, and the runes are a fixed mock balance.
    /// </summary>
    public sealed class AdventureLocalSource : IAdventureSource
    {
        public const string ForestRuins = "forest-ruins";
        public const int ReviveCostBase = 100;

        private readonly Random random;
        private readonly Dictionary<string, AdventureRecord> records = new();
        private Run run;
        private long runes;

        // seed を決めると、ドロップと道の種も毎回同じになる（テスト用）。
        public AdventureLocalSource(long runes = 1000, int seed = 0, int roomCount = 0)
        {
            this.runes = runes;
            this.roomCount = roomCount;
            random = seed == 0 ? new Random() : new Random(seed);
        }

        private readonly int roomCount;

        public long Runes => runes;

        public Task<AdventureState> LoadAsync(CancellationToken token) => Answer(token, State());

        public Task<AdventureState> StartAsync(string destinationId, CancellationToken token)
        {
            if (AdventureCatalog.Area(destinationId) == null)
                return Fail(token, 404);
            if (run != null)
                return run.DestinationId == destinationId && run.Route.Count == 1
                    ? Answer(token, State())
                    : Fail(token, 409);
            run = new Run(
                destinationId,
                AdventureCatalog.Route(
                    destinationId,
                    random.Next(1, int.MaxValue),
                    roomCount > 0 ? roomCount : AdventureCatalog.RoomCount(destinationId)
                )
            );
            return Answer(token, State());
        }

        public Task<AdventureState> MoveAsync(AdventureRoom room, CancellationToken token)
        {
            if (run == null)
                return Fail(token, 404);
            if (room == null)
                return Fail(token, 409);
            if (run.RoomId == room.Id)
                return Answer(token, State());
            var current = run.Find(run.RoomId);
            var target = run.Find(room.Id);
            if (target == null || !current.Next.Contains(room.Id) || !run.RoomCleared)
                return Fail(token, 409);
            run.RoomId = room.Id;
            run.RoomCleared = target.Kind == AdventureRoomKind.Start;
            run.Route.Add(room.Id);
            return Answer(token, State());
        }

        // 遭遇した敵はサーバーのライブラリにだけ記録するため、ここでは使わない。
        public Task<AdventureState> ClearAsync(
            string roomId,
            CancellationToken token,
            IReadOnlyList<AdventureEnemyTally> enemies = null
        )
        {
            if (run == null)
                return Fail(token, 404);
            var room = run.Find(run.RoomId);
            if (run.RoomId != roomId || room.Kind == AdventureRoomKind.Start)
                return Fail(token, 409);
            if (run.RoomCleared)
                return Answer(
                    token,
                    State(drops: run.Drops.Where(entry => entry.RoomId == roomId).ToArray())
                );
            var drops = AdventureDrops.Roll(
                run.DestinationId,
                room.Kind,
                roomId,
                AdventureTexts.MaxMultiplierOf,
                random
            );
            run.Drops.AddRange(drops);
            run.RoomCleared = true;
            if (room.Kind != AdventureRoomKind.Boss)
                return Answer(token, State(drops: drops));
            var result = Finish(AdventureEndStatus.Cleared);
            return Answer(token, State(drops: drops, result: result));
        }

        public Task<AdventureState> ReviveAsync(string roomId, CancellationToken token)
        {
            if (run == null)
                return Fail(token, 404);
            var room = run.Find(run.RoomId);
            if (run.RoomId != roomId || !room.IsBattle || run.RoomCleared)
                return Fail(token, 409);
            int cost = ReviveCostBase * (run.Revives + 1);
            if (runes < cost)
                return Fail(token, 409);
            runes -= cost;
            run.Revives++;
            return Answer(token, State(revived: cost));
        }

        public Task<AdventureState> EndAsync(
            AdventureEndReason reason,
            CancellationToken token,
            IReadOnlyList<AdventureEnemyTally> enemies = null
        )
        {
            if (run == null)
                return Fail(token, 404);
            if (
                reason == AdventureEndReason.Defeat
                && !(run.Find(run.RoomId).IsBattle && !run.RoomCleared)
            )
                return Fail(token, 409);
            var result = Finish(
                reason == AdventureEndReason.Defeat
                    ? AdventureEndStatus.Defeated
                    : AdventureEndStatus.Retreated
            );
            return Answer(token, State(result: result));
        }

        private AdventureResult Finish(AdventureEndStatus status)
        {
            int floor = run.Route.Max(id => run.Find(id).Floor);
            records.TryGetValue(run.DestinationId, out var record);
            int previous = record?.BestFloor ?? 0;
            int clears = (record?.Clears ?? 0) + (status == AdventureEndStatus.Cleared ? 1 : 0);
            var updated = new AdventureRecord(run.DestinationId, Math.Max(previous, floor), clears);
            records[run.DestinationId] = updated;
            var result = new AdventureResult(
                run.DestinationId,
                status,
                floor,
                updated.BestFloor,
                floor > previous,
                clears,
                run.Revives,
                run.Drops.ToArray()
            );
            run = null;
            return result;
        }

        private AdventureState State(
            IReadOnlyList<AdventureDrop> drops = null,
            AdventureResult result = null,
            int? revived = null
        ) =>
            new(
                run == null
                    ? null
                    : new AdventureRun(
                        "local",
                        run.DestinationId,
                        run.RoomId,
                        run.RoomCleared,
                        run.Route.ToArray(),
                        run.Revives,
                        ReviveCostBase * (run.Revives + 1),
                        run.Map,
                        run.Drops.ToArray(),
                        run.Find(run.RoomId).Floor
                    ),
                records.Values.ToArray(),
                runes,
                drops,
                result,
                revived
            );

        // アプリの中だけの冒険はすぐに答える。呼び出し側が完了を同期的に待っても止まらない。
        private static Task<AdventureState> Answer(CancellationToken token, AdventureState state) =>
            token.IsCancellationRequested
                ? Task.FromCanceled<AdventureState>(token)
                : Task.FromResult(state);

        private static Task<AdventureState> Fail(CancellationToken token, long status) =>
            token.IsCancellationRequested
                ? Task.FromCanceled<AdventureState>(token)
                : Task.FromException<AdventureState>(new ServerApiException(status));

        private sealed class Run
        {
            public Run(string destinationId, AdventureRouteMap map)
            {
                DestinationId = destinationId;
                Map = map;
                RoomId = AdventureRouteMap.EntranceId;
                RoomCleared = true;
                Route = new List<string> { RoomId };
            }

            public string DestinationId { get; }
            public AdventureRouteMap Map { get; }
            public string RoomId;
            public bool RoomCleared;
            public List<string> Route { get; }
            public int Revives;
            public List<AdventureDrop> Drops { get; } = new();

            public AdventureRoom Find(string id) => Map.Find(id);
        }
    }
}
