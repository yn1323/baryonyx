using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Adventure
{
    public enum ExplorationChest
    {
        None,
        Closed,
        Open,
    }

    /// <summary>One way on from the room: the room on the next floor the road leads to.</summary>
    public sealed class ExplorationExit
    {
        public ExplorationExit(AdventureRoom room)
        {
            Room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public AdventureRoom Room { get; }
        public string Id => Room.Id;
        public AdventureRoomKind Kind => Room.Kind;
    }

    /// <summary>
    /// What the exploration map shows of the room the party is in
    /// (doc/features/stage-progression.md): where it is, the rooms on the next floor the roads
    /// lead to once the room's event is done, the chest of a treasure room, and whether a battle
    /// waits.
    /// </summary>
    public sealed class ExplorationRoom
    {
        private static string OpenPrompt(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.ShadyChest => ShadyChestPrompt,
                AdventureRoomKind.StepDoor => StepDoorPrompt,
                AdventureRoomKind.Altar => AltarPrompt,
                _ => ChestPrompt,
            };

        public const string ChoosePrompt = "進む道を選んでください";
        public const string ChestPrompt = "宝箱がある。タップして開けよう";
        public const string ShadyChestPrompt = "怪しい宝箱がある。タップして開けよう";
        public const string StepDoorPrompt = "歩数の扉がある。タップして開けよう";
        public const string AltarPrompt = "進化の祭壇がある。タップして調べよう";
        public const string MerchantPrompt = "行商人が店を広げている";
        public const string BattlePrompt = "魔物が現れた！";

        private ExplorationRoom() { }

        public AdventureRun Run { get; private set; }
        public string Location { get; private set; } = "";
        public IReadOnlyList<ExplorationExit> Exits { get; private set; } =
            Array.Empty<ExplorationExit>();
        public ExplorationChest Chest { get; private set; }

        // 着いたマスの戦闘がまだ終わっていない。探索から戦闘へ移る。
        public bool StartsBattle { get; private set; }

        // 着いたマスを、そのまま終えて先へ進む（休憩）。
        public bool PassesBy { get; private set; }

        // 着いたら行商人の店を開く。立ち去るまで先へ進めない。
        public bool Shops { get; private set; }
        public string Prompt { get; private set; } = "";

        public static ExplorationRoom From(AdventureRun run)
        {
            if (run == null)
                return new ExplorationRoom();
            var room = run.Room;
            bool treasure = room != null && room.Opens;
            var exits = run.Exits.Select(next => new ExplorationExit(next)).ToArray();
            return new ExplorationRoom
            {
                Run = run,
                Location = AdventureCatalog.Location(run),
                Exits = exits,
                Chest =
                    !treasure ? ExplorationChest.None
                    : run.RoomCleared ? ExplorationChest.Open
                    : ExplorationChest.Closed,
                StartsBattle = run.InBattle,
                PassesBy = room != null && room.PassesThrough && !run.RoomCleared,
                Shops = room != null && room.Kind == AdventureRoomKind.Merchant && !run.RoomCleared,
                Prompt =
                    run.InBattle ? BattlePrompt
                    : treasure && !run.RoomCleared ? OpenPrompt(room.Kind)
                    : room != null && room.Kind == AdventureRoomKind.Merchant && !run.RoomCleared
                        ? MerchantPrompt
                    : exits.Length > 0 ? ChoosePrompt
                    : "",
            };
        }
    }
}
