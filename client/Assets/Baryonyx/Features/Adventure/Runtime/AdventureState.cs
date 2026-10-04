using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.StepBonus;

namespace Baryonyx.Adventure
{
    public enum AdventureRoomKind
    {
        Start,
        Battle,
        Elite,
        Treasure,
        Boss,
    }

    public enum AdventureRewardOutcome
    {
        // 持っていないボーナスを持ち物に加えた。
        Added,

        // 持っているボーナスのランクを上げた。
        Updated,

        // 持っているほうがランクが高いため、手に入れたほうを手放した。
        Discarded,
    }

    public enum AdventureEndStatus
    {
        // 最奥のボスを倒した。
        Cleared,

        // 負けて帰還した。
        Defeated,

        // 自分から冒険をやめた。
        Retreated,
    }

    public enum AdventureEndReason
    {
        Defeat,
        Retreat,
    }

    /// <summary>One room of an adventure's branching route, made from the adventure's seed (<see cref="AdventureRouteMap"/>).</summary>
    public sealed class AdventureRoom
    {
        public AdventureRoom(
            string id,
            int floor,
            AdventureRoomKind kind,
            string encounter,
            IReadOnlyList<string> next
        )
        {
            Id = id ?? "";
            Floor = floor;
            Kind = kind;
            Encounter = encounter ?? "";
            Next = next ?? Array.Empty<string>();
        }

        public string Id { get; }
        public int Floor { get; }
        public AdventureRoomKind Kind { get; }

        // 戦う敵の組み合わせ。戦わない部屋は空。
        public string Encounter { get; }

        // 次に進める部屋。最奥のボスの部屋は空。
        public IReadOnlyList<string> Next { get; }

        public bool IsBattle =>
            Kind == AdventureRoomKind.Battle
            || Kind == AdventureRoomKind.Elite
            || Kind == AdventureRoomKind.Boss;
    }

    /// <summary>A ACT bonus found in a room whose event was done.</summary>
    public sealed class AdventureReward
    {
        public AdventureReward(
            string roomId,
            string bonusId,
            StepBonusRank rank,
            AdventureRewardOutcome outcome
        )
        {
            RoomId = roomId ?? "";
            BonusId = bonusId ?? "";
            Rank = rank;
            Outcome = outcome;
        }

        public string RoomId { get; }
        public string BonusId { get; }
        public StepBonusRank Rank { get; }
        public AdventureRewardOutcome Outcome { get; }
    }

    /// <summary>
    /// The adventure in progress: the route made from its seed, where the party is and the
    /// rooms behind it.
    /// </summary>
    public sealed class AdventureRun
    {
        public AdventureRun(
            string id,
            string destinationId,
            string roomId,
            bool roomCleared,
            IReadOnlyList<string> route,
            int revives,
            int reviveCost,
            AdventureRouteMap map,
            IReadOnlyList<AdventureReward> rewards,
            int floor = 1
        )
        {
            Id = id ?? "";
            DestinationId = destinationId ?? "";
            RoomId = roomId ?? "";
            RoomCleared = roomCleared;
            Route = route ?? Array.Empty<string>();
            Revives = revives;
            ReviveCost = reviveCost;
            Map = map;
            Rewards = rewards ?? Array.Empty<AdventureReward>();
            savedFloor = Math.Max(1, floor);
        }

        private readonly int savedFloor;

        public string Id { get; }
        public string DestinationId { get; }
        public string RoomId { get; }

        // 今いる部屋の出来事（戦闘・宝箱）を終えたか。終えるまで次の部屋へ進めない。
        public bool RoomCleared { get; }
        public IReadOnlyList<string> Route { get; }
        public int Revives { get; }

        // 次に復活するときに要るルーン。
        public int ReviveCost { get; }

        // 冒険の種から作った道。
        public AdventureRouteMap Map { get; }
        public IReadOnlyList<AdventureRoom> Rooms => Map?.Rooms ?? Array.Empty<AdventureRoom>();
        public IReadOnlyList<AdventureReward> Rewards { get; }

        public AdventureRoom Room => Find(RoomId);

        // 今いる階。道にない部屋（以前の冒険の部屋など）でも、サーバーが保存した階を返す。
        public int Floor => Room?.Floor ?? savedFloor;

        // 今いる部屋の入口から進める部屋。出来事を終えるまでは空。
        public IReadOnlyList<AdventureRoom> Exits =>
            !RoomCleared || Room == null
                ? Array.Empty<AdventureRoom>()
                : Room.Next.Select(Find).Where(room => room != null).ToArray();

        // 今いる部屋の戦闘がまだ終わっていない。
        public bool InBattle => !RoomCleared && Room != null && Room.IsBattle;

        public int DeepestFloor => Map?.BossFloor ?? 1;

        public AdventureRoom Find(string id) => Map?.Find(id);
    }

    /// <summary>How an adventure ended, and what it brought back.</summary>
    public sealed class AdventureResult
    {
        public AdventureResult(
            string destinationId,
            AdventureEndStatus status,
            int floor,
            int bestFloor,
            bool newRecord,
            int clears,
            int revives,
            IReadOnlyList<AdventureReward> rewards
        )
        {
            DestinationId = destinationId ?? "";
            Status = status;
            Floor = floor;
            BestFloor = bestFloor;
            NewRecord = newRecord;
            Clears = clears;
            Revives = revives;
            Rewards = rewards ?? Array.Empty<AdventureReward>();
        }

        public string DestinationId { get; }
        public AdventureEndStatus Status { get; }

        // この冒険で着いた最も深い階。
        public int Floor { get; }
        public int BestFloor { get; }
        public bool NewRecord { get; }
        public int Clears { get; }
        public int Revives { get; }
        public IReadOnlyList<AdventureReward> Rewards { get; }
    }

    /// <summary>A destination's record: the deepest floor reached and the times it was cleared.</summary>
    public sealed class AdventureRecord
    {
        public AdventureRecord(string destinationId, int bestFloor, int clears)
        {
            DestinationId = destinationId ?? "";
            BestFloor = bestFloor;
            Clears = clears;
        }

        public string DestinationId { get; }
        public int BestFloor { get; }
        public int Clears { get; }
    }

    /// <summary>
    /// What the server answered: the adventure in progress (null when there is none), the
    /// records and the runes, with the reward, the revive or the result of the request.
    /// </summary>
    public sealed class AdventureState
    {
        public static readonly AdventureState Empty = new(null, null, 0);

        public AdventureState(
            AdventureRun run,
            IReadOnlyList<AdventureRecord> records,
            long runes,
            AdventureReward reward = null,
            AdventureResult result = null,
            int? revivedFor = null
        )
        {
            Run = run;
            Records = records ?? Array.Empty<AdventureRecord>();
            Runes = runes;
            Reward = reward;
            Result = result;
            RevivedFor = revivedFor;
        }

        public AdventureRun Run { get; }
        public IReadOnlyList<AdventureRecord> Records { get; }
        public long Runes { get; }

        // 出来事を終えた要求で手に入れた物。
        public AdventureReward Reward { get; }

        // 冒険を終えた要求の結果。
        public AdventureResult Result { get; }

        // 復活した要求で使ったルーン。
        public int? RevivedFor { get; }

        public bool InProgress => Run != null;

        public AdventureRecord RecordOf(string destinationId) =>
            Records.FirstOrDefault(record => record.DestinationId == destinationId);
    }
}
