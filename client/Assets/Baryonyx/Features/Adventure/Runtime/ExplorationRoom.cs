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

    /// <summary>One way on out of the room: the room it leads to and the hint at it.</summary>
    public sealed class ExplorationExit
    {
        public ExplorationExit(AdventureRoom room)
        {
            Room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public AdventureRoom Room { get; }
        public string Id => Room.Id;
        public AdventureRoomKind Kind => Room.Kind;
        public string Hint => AdventureCatalog.Hint(Room.Kind);
    }

    /// <summary>
    /// What the exploration screen shows of the room the party is in
    /// (doc/features/stage-progression.md): where it is, the doors on to the next rooms once
    /// the room's event is done, the chest of a treasure room, and whether a battle waits.
    /// </summary>
    public sealed class ExplorationRoom
    {
        public const string ChoosePrompt = "進む入口を選んでください";
        public const string ChestPrompt = "宝箱がある。タップして開けよう";
        public const string BattlePrompt = "魔物が現れた！";

        private ExplorationRoom() { }

        public string Location { get; private set; } = "";
        public IReadOnlyList<ExplorationExit> Exits { get; private set; } =
            Array.Empty<ExplorationExit>();
        public ExplorationChest Chest { get; private set; }

        // 着いた部屋の戦闘がまだ終わっていない。探索から戦闘へ移る。
        public bool StartsBattle { get; private set; }
        public string Prompt { get; private set; } = "";

        public static ExplorationRoom From(AdventureRun run)
        {
            if (run == null)
                return new ExplorationRoom();
            var room = run.Room;
            bool treasure = room != null && room.Kind == AdventureRoomKind.Treasure;
            var exits = run.Exits.Select(next => new ExplorationExit(next)).ToArray();
            return new ExplorationRoom
            {
                Location = AdventureCatalog.Location(run),
                Exits = exits,
                Chest =
                    !treasure ? ExplorationChest.None
                    : run.RoomCleared ? ExplorationChest.Open
                    : ExplorationChest.Closed,
                StartsBattle = run.InBattle,
                Prompt =
                    run.InBattle ? BattlePrompt
                    : treasure && !run.RoomCleared ? ChestPrompt
                    : exits.Length > 0 ? ChoosePrompt
                    : "",
            };
        }
    }
}
