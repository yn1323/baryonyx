using System;
using System.Collections.Generic;
using Baryonyx.Enhancement;
using Baryonyx.Inventory;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// One clear's reward (doc/features/stage-progression.md, ダンジョンのクリア回数の報酬): the thing
    /// (a guaranteed quality for a skill or equipment, no multiplier in the table), how many, and
    /// the runes that come with it.
    /// </summary>
    public sealed class AdventureClearReward
    {
        public AdventureClearReward(int clears, AdventureDrop drop, int count, int runes)
        {
            Clears = clears;
            Drop = drop;
            Count = count;
            Runes = runes;
        }

        /// <summary>The clear it is for (1 to <see cref="AdventureClearRewards.Count"/>).</summary>
        public int Clears { get; }
        public AdventureDrop Drop { get; }
        public int Count { get; }
        public int Runes { get; }
    }

    /// <summary>
    /// The rewards of the first five clears of each destination, the same as the server's
    /// CLEAR_REWARDS. One pattern for every destination, filled from its own list (2026-11-01):
    /// the first clear the starriest equipment that is not the boss's (quality A), the second
    /// polishing stones (ten for the near four, twenty for the far four), the third the starriest
    /// skill (quality A), the fourth two awakening shards, the fifth the boss's ☆5 accessory
    /// (quality S), and each the same 300 runes whatever the destination.
    /// </summary>
    public static class AdventureClearRewards
    {
        public const int Count = 5;

        // どの回にも付けるルーン。行き先によらず同じ（2026-11-01、ユーザー）。
        public const int Runes = 300;

        private static readonly Dictionary<string, AdventureClearReward[]> Table = new()
        {
            [AdventureCatalog.ForestRuins] = Of(AdventureDropCategory.Accessory, "dew-pendant", 10, "Hone", "guardian-circlet"),
            ["port-portoria"] = Of(AdventureDropCategory.Weapon, "flame-dagger", 10, "ManaPrayer", "kraken-pearl"),
            ["glaciera-cave"] = Of(AdventureDropCategory.Armor, "crystal-shield", 10, "IceMirror", "troll-heartstone"),
            ["hush-mire"] = Of(AdventureDropCategory.Accessory, "frog-crown", 10, "QuickCast", "croc-fang"),
            ["almaja-tomb"] = Of(AdventureDropCategory.Armor, "ancient-plate", 20, "Revelation", "golden-mask"),
            ["moonshade-graveyard"] = Of(AdventureDropCategory.Weapon, "moonshade-staff", 20, "Resurrection", "lich-signet"),
            ["volgan-volcano"] = Of(AdventureDropCategory.Armor, "obsidian-shield", 20, "Meteor", "obsidian-crown"),
            ["neve-peaks"] = Of(AdventureDropCategory.Weapon, "glacier-greatsword", 20, "AbsoluteZero", "mistdragon-eye"),
        };

        private static AdventureClearReward[] Of(
            AdventureDropCategory gearCategory,
            string gear,
            int polishes,
            string skill,
            string bossAccessory
        )
        {
            int runes = Runes;
            return new[]
            {
                new AdventureClearReward(1, new AdventureDrop("", gearCategory, gear, 0f, ItemQuality.A), 1, runes),
                new AdventureClearReward(2, new AdventureDrop("", AdventureDropCategory.Material, EnhancementMaterials.White), polishes, runes),
                new AdventureClearReward(3, new AdventureDrop("", AdventureDropCategory.Skill, skill, 0f, ItemQuality.A), 1, runes),
                new AdventureClearReward(4, new AdventureDrop("", AdventureDropCategory.Material, EnhancementMaterials.Shard), 2, runes),
                new AdventureClearReward(5, new AdventureDrop("", AdventureDropCategory.Accessory, bossAccessory, 0f, ItemQuality.S), 1, runes),
            };
        }

        /// <summary>The five rewards of the destination, empty for one without them.</summary>
        public static IReadOnlyList<AdventureClearReward> Of(string destinationId) =>
            destinationId != null && Table.TryGetValue(destinationId, out var rewards)
                ? rewards
                : Array.Empty<AdventureClearReward>();

        /// <summary>The reward of the <paramref name="clears"/>th clear, or null (none after the fifth).</summary>
        public static AdventureClearReward At(string destinationId, int clears)
        {
            var rewards = Of(destinationId);
            return clears >= 1 && clears <= rewards.Count ? rewards[clears - 1] : null;
        }
    }
}
