using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Inventory;

namespace Baryonyx.Adventure
{
    // マスの種類（doc/features/stage-progression.md のマスの種類）。色とアイコンの並びはこの順。
    public enum AdventureRoomKind
    {
        Start,
        Battle,
        Elite,
        Treasure,
        Boss,
        Rest,
        Merchant,
        ShadyChest,
        Trial,
        StepDoor,
        Altar,
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
            || Kind == AdventureRoomKind.Boss
            || Kind == AdventureRoomKind.Trial;

        // 宝箱のように開けて中身を得るマス。怪しい宝箱・歩数の扉・進化の祭壇は、中身を作るまで宝箱と同じに開ける。
        public bool Opens =>
            Kind == AdventureRoomKind.Treasure
            || Kind == AdventureRoomKind.ShadyChest
            || Kind == AdventureRoomKind.StepDoor
            || Kind == AdventureRoomKind.Altar;

        // 着いたら、そのまま終えて先へ進めるマス。休憩は着いたときに回復する。
        public bool PassesThrough => Kind == AdventureRoomKind.Rest;
    }

    /// <summary>
    /// One thing dropped in a room whose event was done (doc/features/drops.md). Items have no
    /// quality: their multiplier is 0 and their quality null.
    /// </summary>
    public sealed class AdventureDrop
    {
        public AdventureDrop(
            string roomId,
            AdventureDropCategory category,
            string id,
            float multiplier = 0f,
            ItemQuality? quality = null
        )
        {
            RoomId = roomId ?? "";
            Category = category;
            Id = id ?? "";
            Multiplier = multiplier;
            Quality = quality;
        }

        public string RoomId { get; }
        public AdventureDropCategory Category { get; }
        public string Id { get; }

        /// <summary>The drop's own multiplier (×1.37 as 1.37) and the quality it gives.</summary>
        public float Multiplier { get; }
        public ItemQuality? Quality { get; }

        /// <summary>The stars of the kind (doc/features/inventory.md, rarity).</summary>
        public int Stars => AdventureDrops.StarsOf(Category, Id);
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
            IReadOnlyList<AdventureDrop> drops,
            int floor = 1,
            AdventureContents contents = null
        )
        {
            Contents = contents ?? AdventureContents.Empty;
            Id = id ?? "";
            DestinationId = destinationId ?? "";
            RoomId = roomId ?? "";
            RoomCleared = roomCleared;
            Route = route ?? Array.Empty<string>();
            Revives = revives;
            ReviveCost = reviveCost;
            Map = map;
            Drops = drops ?? Array.Empty<AdventureDrop>();
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
        public IReadOnlyList<AdventureDrop> Drops { get; }

        // 今いるマスの中身：開けた怪しい宝箱がミミックだったか、試練の条件、行商人の品物。
        public AdventureContents Contents { get; }

        public AdventureRoom Room => Find(RoomId);

        // 今いる階。道にない部屋（以前の冒険の部屋など）でも、サーバーが保存した階を返す。
        public int Floor => Room?.Floor ?? savedFloor;

        // 今いる部屋の入口から進める部屋。出来事を終えるまでは空。
        public IReadOnlyList<AdventureRoom> Exits =>
            !RoomCleared || Room == null
                ? Array.Empty<AdventureRoom>()
                : Room.Next.Select(Find).Where(room => room != null).ToArray();

        // 今いる部屋の戦闘がまだ終わっていない。
        // 開けた怪しい宝箱がミミックなら、戦闘のマスになる。
        public bool InBattle =>
            !RoomCleared
            && Room != null
            && (Room.IsBattle || (Room.Kind == AdventureRoomKind.ShadyChest && Contents.Mimic));

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
            IReadOnlyList<AdventureDrop> drops
        )
        {
            DestinationId = destinationId ?? "";
            Status = status;
            Floor = floor;
            BestFloor = bestFloor;
            NewRecord = newRecord;
            Clears = clears;
            Revives = revives;
            Drops = drops ?? Array.Empty<AdventureDrop>();
        }

        public string DestinationId { get; }
        public AdventureEndStatus Status { get; }

        // この冒険で着いた最も深い階。
        public int Floor { get; }
        public int BestFloor { get; }
        public bool NewRecord { get; }
        public int Clears { get; }
        public int Revives { get; }
        public IReadOnlyList<AdventureDrop> Drops { get; }
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
    /// records and the runes, with the drops, the revive or the result of the request.
    /// </summary>
    public sealed class AdventureState
    {
        public static readonly AdventureState Empty = new(null, null, 0);

        public AdventureState(
            AdventureRun run,
            IReadOnlyList<AdventureRecord> records,
            long runes,
            IReadOnlyList<AdventureDrop> drops = null,
            AdventureResult result = null,
            int? revivedFor = null,
            AdventureStepDoor stepDoor = null
        )
        {
            StepDoor = stepDoor;
            Run = run;
            Records = records ?? Array.Empty<AdventureRecord>();
            Runes = runes;
            Drops = drops;
            Result = result;
            RevivedFor = revivedFor;
        }

        public AdventureRun Run { get; }
        public IReadOnlyList<AdventureRecord> Records { get; }
        public long Runes { get; }

        // 出来事を終えた要求で落ちた物。出来事を終えた要求でなければnull。
        public IReadOnlyList<AdventureDrop> Drops { get; }

        // 冒険を終えた要求の結果。
        public AdventureResult Result { get; }

        // 復活した要求で使ったルーン。
        public int? RevivedFor { get; }

        // 歩数の扉を開けた要求で、サーバーが読んだ今日の歩数と報酬の段。
        public AdventureStepDoor StepDoor { get; }

        public bool InProgress => Run != null;

        public AdventureRecord RecordOf(string destinationId) =>
            Records.FirstOrDefault(record => record.DestinationId == destinationId);
    }
}
