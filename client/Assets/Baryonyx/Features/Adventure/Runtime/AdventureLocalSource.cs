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

        // 所持ルーン。テストでは、行商人で買えるだけのルーンに変えられる。
        public long Runes
        {
            get => runes;
            set => runes = value;
        }

        // 歩数の扉が読む今日の歩数。サーバーがないため、仮の値にしておく。
        public int TodaySteps { get; set; } = 3240;

        // 怪しい宝箱を開けたときにミミックになるか（テスト用）。null なら半分の確率で決める。
        public bool? NextMimic { get; set; }

        // 次に入る宝箱のマスで、宝箱から覚醒が出るか（テスト用）。null ならサーバーと同じ確率で決める。
        public bool? NextAwakening { get; set; }

        // 宝箱から覚醒が出る確率。サーバーの CHEST_AWAKENING_CHANCE と同じ。
        public const double ChestAwakeningChance = 0.03;

        // 「？」のマスの中身の種類（テスト用）。null なら重みで決める。
        public AdventureRoomKind? MysteryKind { get; set; }

        public Task<AdventureState> LoadAsync(CancellationToken token) => Answer(token, State());

        public Task<AdventureState> StartAsync(string destinationId, CancellationToken token)
        {
            if (AdventureCatalog.Area(destinationId) == null)
                return Fail(token, 404);
            if (run != null)
                return run.DestinationId == destinationId && run.Route.Count == 1
                    ? Answer(token, State())
                    : Fail(token, 409, TravelDeparture.InProgressError);
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
            // 「？」のマスは、入ったときに中身の種類を決める（サーバーの pickMysteryKind と同じ重み）。
            var kind = target.Kind == AdventureRoomKind.Mystery ? PickMystery() : target.Kind;
            run.RoomId = room.Id;
            run.Revealed = target.Kind == AdventureRoomKind.Mystery ? kind : null;
            run.RoomCleared = kind == AdventureRoomKind.Start;
            run.Route.Add(room.Id);
            run.Opened = false;
            run.Mimic = false;
            run.Awakening =
                kind == AdventureRoomKind.Treasure
                && (NextAwakening ?? random.NextDouble() < ChestAwakeningChance);
            if (kind == AdventureRoomKind.Treasure)
                NextAwakening = null;
            run.Trial =
                kind == AdventureRoomKind.Trial
                    ? (AdventureTrial)random.Next(1, 4)
                    : AdventureTrial.None;
            run.Wares =
                kind == AdventureRoomKind.Merchant
                    ? Wares(run.DestinationId)
                    : new List<AdventureWare>();
            return Answer(token, State());
        }

        // 遭遇した敵はサーバーのライブラリにだけ記録するため、ここでは使わない。
        public Task<AdventureState> ClearAsync(
            string roomId,
            CancellationToken token,
            IReadOnlyList<AdventureEnemyTally> enemies = null,
            AdventureClearOptions options = null
        )
        {
            options ??= AdventureClearOptions.None;
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
            // 怪しい宝箱を開けずに通り過ぎる。開ける前だけ。
            if (options.Pass)
            {
                if (room.Kind != AdventureRoomKind.ShadyChest || run.Opened)
                    return Fail(token, 409);
                run.RoomCleared = true;
                return Answer(token, State(drops: Array.Empty<AdventureDrop>()));
            }
            // 怪しい宝箱は、開けてミミックに勝ったときだけここで終える（本物は開けたときに終える）。
            if (room.Kind == AdventureRoomKind.ShadyChest && !run.Mimic)
                return Fail(token, 409);
            AdventureStepDoor door = null;
            var reward = AdventureDrops.RewardOf(room.Kind);
            if (
                room.Kind == AdventureRoomKind.ShadyChest
                || (room.Kind == AdventureRoomKind.Trial && options.TrialMet)
            )
                reward = (AdventureRoomKind.Boss, 0);
            if (room.Kind == AdventureRoomKind.Altar && options.Evolved)
                reward = null;
            if (room.Kind == AdventureRoomKind.StepDoor)
            {
                door = new AdventureStepDoor(TodaySteps, AdventureStepDoor.TierOf(TodaySteps));
                reward = StepDoorReward(door.Tier);
            }
            // 強化素材は、ふつうのドロップのあとに足す（サーバーの rollRoomDrops）。
            var drops = reward is { } row
                ? AdventureDrops
                    .Roll(
                        run.DestinationId,
                        room.Kind,
                        row,
                        roomId,
                        AdventureTexts.MaxMultiplierOf,
                        random
                    )
                    .Concat(
                        AdventureDrops.RollMaterials(run.DestinationId, row.Row, roomId, random)
                    )
                    .ToArray()
                : Array.Empty<AdventureDrop>();
            run.Drops.AddRange(drops);
            run.RoomCleared = true;
            if (room.Kind != AdventureRoomKind.Boss)
                return Answer(token, State(drops: drops, stepDoor: door));
            var result = Finish(AdventureEndStatus.Cleared);
            return Answer(token, State(drops: drops, result: result));
        }

        // 歩数の扉の報酬：3,000歩未満は魔物、3,000〜は強い報酬、5,000〜は強い報酬で1つ多く、8,000〜は最大の報酬。
        public static (AdventureRoomKind Row, int Extra) StepDoorReward(int tier) =>
            tier switch
            {
                >= 3 => (AdventureRoomKind.Boss, 0),
                2 => (AdventureRoomKind.Elite, 1),
                1 => (AdventureRoomKind.Elite, 0),
                _ => (AdventureRoomKind.Battle, 0),
            };

        public Task<AdventureState> OpenAsync(string roomId, CancellationToken token)
        {
            if (run == null)
                return Fail(token, 404);
            var room = run.Find(run.RoomId);
            if (run.RoomId != roomId || room.Kind != AdventureRoomKind.ShadyChest)
                return Fail(token, 409);
            if (run.Opened || run.RoomCleared)
                return run.RoomCleared && !run.Opened
                    ? Fail(token, 409)
                    : Answer(
                        token,
                        State(drops: run.Drops.Where(entry => entry.RoomId == roomId).ToArray())
                    );
            run.Opened = true;
            run.Mimic = NextMimic ?? random.Next(2) == 0;
            NextMimic = null;
            if (run.Mimic)
                return Answer(token, State());
            var drops = AdventureDrops
                .Roll(run.DestinationId, room.Kind, roomId, AdventureTexts.MaxMultiplierOf, random)
                .Concat(
                    AdventureDrops.RewardOf(room.Kind) is { } chest
                        ? AdventureDrops.RollMaterials(run.DestinationId, chest.Row, roomId, random)
                        : Array.Empty<AdventureDrop>()
                )
                .ToArray();
            run.Drops.AddRange(drops);
            run.RoomCleared = true;
            return Answer(token, State(drops: drops));
        }

        public Task<AdventureState> BuyAsync(string roomId, int index, CancellationToken token)
        {
            if (run == null)
                return Fail(token, 404);
            if (run.RoomId != roomId || run.Find(roomId).Kind != AdventureRoomKind.Merchant)
                return Fail(token, 409);
            int at = run.Wares.FindIndex(ware => ware.Index == index);
            if (at < 0 || run.Wares[at].Sold || runes < run.Wares[at].Price)
                return Fail(token, 409);
            var ware = run.Wares[at];
            runes -= ware.Price;
            run.Wares[at] = new AdventureWare(
                ware.Index,
                ware.Category,
                ware.Id,
                ware.Multiplier,
                ware.Quality,
                ware.Price,
                true
            );
            return Answer(token, State());
        }

        // 行商人の品物：スキル3つと装備2つ。同じ物は並べず、その分類の物が足りない行き先ではスキルで埋める。
        // 「？」のマスの中身の種類と重み（サーバーの MYSTERY_KINDS と同じ）。
        public static readonly (AdventureRoomKind Kind, int Weight)[] MysteryKinds =
        {
            (AdventureRoomKind.Battle, 45),
            (AdventureRoomKind.Treasure, 25),
            (AdventureRoomKind.Merchant, 15),
            (AdventureRoomKind.Rest, 15),
        };

        private AdventureRoomKind PickMystery()
        {
            if (MysteryKind is { } chosen)
                return chosen;
            int roll = random.Next(0, MysteryKinds.Sum(entry => entry.Weight));
            foreach (var (kind, weight) in MysteryKinds)
            {
                if (roll < weight)
                    return kind;
                roll -= weight;
            }
            return AdventureRoomKind.Battle;
        }

        private List<AdventureWare> Wares(string destinationId)
        {
            var wares = new List<AdventureWare>();
            var gear = new[]
            {
                AdventureDropCategory.Weapon,
                AdventureDropCategory.Armor,
                AdventureDropCategory.Accessory,
            };
            for (int i = 0; i < AdventureWares.Skills + AdventureWares.Gear; i++)
            {
                AdventureDrop drop = null;
                var kinds =
                    i >= AdventureWares.Skills
                        ? gear.OrderBy(_ => random.Next()).Append(AdventureDropCategory.Skill)
                        : new[] { AdventureDropCategory.Skill };
                foreach (var category in kinds)
                {
                    // 同じ物が出たら、何度か引き直す。
                    for (int attempt = 0; attempt < 12 && drop == null; attempt++)
                    {
                        var pick = AdventureDrops.PickWare(
                            destinationId,
                            category,
                            AdventureTexts.MaxMultiplierOf,
                            random
                        );
                        if (pick == null)
                            break;
                        if (
                            !wares.Any(ware => ware.Category == pick.Category && ware.Id == pick.Id)
                        )
                            drop = pick;
                    }
                    if (drop != null)
                        break;
                }
                if (drop == null)
                    continue;
                wares.Add(
                    new AdventureWare(
                        wares.Count,
                        drop.Category,
                        drop.Id,
                        drop.Multiplier,
                        drop.Quality,
                        AdventureWares.PriceOf(drop.Stars, drop.Quality),
                        false
                    )
                );
            }
            return wares;
        }

        public Task<AdventureState> ReviveAsync(string roomId, CancellationToken token)
        {
            if (run == null)
                return Fail(token, 404);
            var room = run.Find(run.RoomId);
            if (run.RoomId != roomId || !InBattle(room) || run.RoomCleared)
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
                && !(InBattle(run.Find(run.RoomId)) && !run.RoomCleared)
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

        // 戦闘のマス。開けた怪しい宝箱がミミックなら、そのマスも戦闘になる。
        private bool InBattle(AdventureRoom room) =>
            room.IsBattle || (room.Kind == AdventureRoomKind.ShadyChest && run.Mimic);

        private AdventureState State(
            IReadOnlyList<AdventureDrop> drops = null,
            AdventureResult result = null,
            int? revived = null,
            AdventureStepDoor stepDoor = null
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
                        run.Find(run.RoomId).Floor,
                        new AdventureContents(
                            run.Mimic,
                            run.Trial,
                            run.Wares.ToArray(),
                            run.Revealed,
                            run.Awakening
                        )
                    ),
                records.Values.ToArray(),
                runes,
                drops,
                result,
                revived,
                stepDoor
            );

        // アプリの中だけの冒険はすぐに答える。呼び出し側が完了を同期的に待っても止まらない。
        private static Task<AdventureState> Answer(CancellationToken token, AdventureState state) =>
            token.IsCancellationRequested
                ? Task.FromCanceled<AdventureState>(token)
                : Task.FromResult(state);

        private static Task<AdventureState> Fail(
            CancellationToken token,
            long status,
            string error = null
        ) =>
            token.IsCancellationRequested
                ? Task.FromCanceled<AdventureState>(token)
                : Task.FromException<AdventureState>(new ServerApiException(status, error));

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

            // 今いるマスの中身：怪しい宝箱を開けたか、それがミミックか、試練の条件、行商人の品物。
            public bool Opened;
            public bool Mimic;

            // 宝箱のマスで、宝箱から覚醒が出るか。
            public bool Awakening;
            public AdventureTrial Trial;
            public List<AdventureWare> Wares = new();

            // 今いる「？」のマスの中身の種類。
            public AdventureRoomKind? Revealed;

            public AdventureRoom Find(string id) =>
                id == RoomId && Revealed is { } kind ? Map.Reveal(id, kind) : Map.Find(id);
        }
    }
}
