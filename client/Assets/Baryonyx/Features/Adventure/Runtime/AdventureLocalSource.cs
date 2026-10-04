using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using Baryonyx.StepBonus;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure kept only while the app runs, for the editor without a server and the
    /// showcase. It follows the server's rules (server/src/features/adventure) on a route made
    /// from a seed of its own, like the server's, so the loop can be played through without a
    /// server. Rewards are told but not added to the bonuses, and the runes are a fixed mock balance.
    /// </summary>
    public sealed class AdventureLocalSource : IAdventureSource
    {
        public const string ForestRuins = "forest-ruins";
        public const int ReviveCostBase = 100;

        private static readonly string[] BonusIds =
        {
            "guard",
            "luck",
            "treasure-sight",
            "foresight",
            "omen",
            "appraisal",
            "fighting",
        };

        private readonly Random random;
        private readonly Dictionary<string, AdventureRecord> records = new();
        private Run run;
        private long runes;

        // seed を決めると、報酬と道の種も毎回同じになる（テスト用）。
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
            if (destinationId != ForestRuins)
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

        public Task<AdventureState> ClearAsync(string roomId, CancellationToken token)
        {
            if (run == null)
                return Fail(token, 404);
            var room = run.Find(run.RoomId);
            if (run.RoomId != roomId || room.Kind == AdventureRoomKind.Start)
                return Fail(token, 409);
            if (run.RoomCleared)
                return Answer(
                    token,
                    State(reward: run.Rewards.FirstOrDefault(entry => entry.RoomId == roomId))
                );
            var reward = new AdventureReward(
                roomId,
                BonusIds[random.Next(BonusIds.Length)],
                RankFor(room.Kind),
                AdventureRewardOutcome.Added
            );
            run.Rewards.Add(reward);
            run.RoomCleared = true;
            if (room.Kind != AdventureRoomKind.Boss)
                return Answer(token, State(reward: reward));
            var result = Finish(AdventureEndStatus.Cleared);
            return Answer(token, State(reward: reward, result: result));
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

        public Task<AdventureState> EndAsync(AdventureEndReason reason, CancellationToken token)
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
                run.Rewards.ToArray()
            );
            run = null;
            return result;
        }

        // サーバーの REWARD_RANK_WEIGHTS と同じ重み。
        private StepBonusRank RankFor(AdventureRoomKind kind)
        {
            var weights = kind switch
            {
                AdventureRoomKind.Battle => new[]
                {
                    (StepBonusRank.E, 40),
                    (StepBonusRank.D, 35),
                    (StepBonusRank.C, 20),
                    (StepBonusRank.B, 5),
                },
                AdventureRoomKind.Boss => new[]
                {
                    (StepBonusRank.B, 40),
                    (StepBonusRank.A, 45),
                    (StepBonusRank.S, 15),
                },
                _ => new[]
                {
                    (StepBonusRank.D, 30),
                    (StepBonusRank.C, 40),
                    (StepBonusRank.B, 25),
                    (StepBonusRank.A, 5),
                },
            };
            int roll = random.Next(weights.Sum(entry => entry.Item2));
            foreach (var (rank, weight) in weights)
            {
                if (roll < weight)
                    return rank;
                roll -= weight;
            }
            return weights[^1].Item1;
        }

        private AdventureState State(
            AdventureReward reward = null,
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
                        run.Rewards.ToArray(),
                        run.Find(run.RoomId).Floor
                    ),
                records.Values.ToArray(),
                runes,
                reward,
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
            public List<AdventureReward> Rewards { get; } = new();

            public AdventureRoom Find(string id) => Map.Find(id);
        }
    }
}
