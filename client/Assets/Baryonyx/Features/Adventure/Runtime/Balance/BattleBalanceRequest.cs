using System;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// One item a member wears in an automatic battle run: the member (by character id, or else by
    /// party slot), the kind of equipment (an id of <see cref="Equipment.EquipmentCatalog"/>), its
    /// multiplier, and for an accessory the accessory slot (0 to 2).
    /// </summary>
    [Serializable]
    public sealed class BattleBalanceGear
    {
        public string character = "";
        public int slot;
        public string equipment = "";
        public float multiplier = 1f;
        public int accessory;
    }

    /// <summary>
    /// What an automatic battle run measures (doc/features/stage-progression.md, 戦闘の試し): the
    /// destination and the room's battle, the party, how many battles and the seed. Read from JSON
    /// with <see cref="JsonUtility"/>, so a field left out keeps its default here.
    /// </summary>
    [Serializable]
    public sealed class BattleBalanceRequest
    {
        public const string Single = "single";
        public const string Through = "through";

        /// <summary>"single" fights one room's battle again and again; "through" walks the route and carries the HP.</summary>
        public string mode = Single;

        public string destination = AdventureCatalog.ForestRuins;

        /// <summary>The battle of the room in "single": "pack", "elite", "boss" or "mimic".</summary>
        public string room = "elite";

        /// <summary>How strong the enemies on the way are, in percent (85 to 115 on the way; ignored in the deepest room).</summary>
        public int depthPercent = 100;

        /// <summary>The enemy combinations (<see cref="BattleBalanceEncounters"/>); empty fights every one.</summary>
        public int[] variants = Array.Empty<int>();

        /// <summary>How many times each combination is fought in "single".</summary>
        public int repeats = 3;

        /// <summary>How many adventures are walked in "through".</summary>
        public int runs = 5;

        /// <summary>The first seed. Each battle (each adventure in "through") takes the next one.</summary>
        public int seed = 1;

        // パーティ。Lvは編成の枠の順で、0や足りない枠は仮データのLvのまま。
        public int[] levels = Array.Empty<int>();

        /// <summary>The multiplier of every custom skill card (its quality: ×1.00 for the first cards).</summary>
        public float cardMultiplier = 1f;

        /// <summary>The branch every custom skill card awakened into (1 or 2), or 0 for none.</summary>
        public int awakenedBranch;

        /// <summary>The equipment the members wear (none leaves them wearing nothing).</summary>
        public BattleBalanceGear[] equipment = Array.Empty<BattleBalanceGear>();

        /// <summary>The share of the maximum HP each member starts with (in "through", only the first battle).</summary>
        public float startHpShare = 1f;

        /// <summary>Whether the party knows every weakness from the start (the weaknesses revealed before).</summary>
        public bool knowWeaknesses;

        /// <summary>"random" rolls critical hits and statuses; "fixed" has no critical hit and every status land, as the tests do.</summary>
        public string odds = "random";

        /// <summary>How the cards are chosen (<see cref="BattleAutoPolicies"/>).</summary>
        public string policy = BattleAutoPolicies.Default;

        // 道中で怪しい宝箱を開ける（ミミックと戦う）か。開けなければ通り過ぎる。
        public bool openShadyChests;

        /// <summary>A battle still going on after this many of the party's turns ends as a timeout.</summary>
        public int maxTurns = 40;

        /// <summary>Whether each step of the party's turns goes to the log ("[balance] step").</summary>
        public bool verbose;

        /// <summary>How fast the battle's time runs while measuring.</summary>
        public float timeScale = 20f;

        public static BattleBalanceRequest FromJson(string json) =>
            string.IsNullOrWhiteSpace(json)
                ? new BattleBalanceRequest()
                : JsonUtility.FromJson<BattleBalanceRequest>(json) ?? new BattleBalanceRequest();

        public string ToJson() => JsonUtility.ToJson(this, true);

        public bool IsThrough => string.Equals(mode, Through, StringComparison.OrdinalIgnoreCase);

        /// <summary>The kind of room "single" fights in.</summary>
        public AdventureRoomKind RoomKind =>
            (room ?? "").Trim().ToLowerInvariant() switch
            {
                "pack" or "battle" => AdventureRoomKind.Battle,
                "boss" => AdventureRoomKind.Boss,
                "mimic" or "shadychest" => AdventureRoomKind.ShadyChest,
                _ => AdventureRoomKind.Elite,
            };

        /// <summary>The level of the slot asked for, or 0 to keep the party's own.</summary>
        public int LevelOf(int slot) => levels != null && slot < levels.Length ? levels[slot] : 0;
    }
}
