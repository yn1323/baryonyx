using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Inventory;

namespace Baryonyx.Adventure
{
    /// <summary>The condition of a trial room (doc/features/stage-progression.md, マスの中身).</summary>
    public enum AdventureTrial
    {
        None,

        // 6ターン以内に勝つ。
        WithinSixTurns,

        // 誰も倒れずに勝つ。
        NoOneFalls,

        // 全員がHPを半分以上残して勝つ。
        HalfHp,
    }

    /// <summary>The trials' words and how a battle won meets them.</summary>
    public static class AdventureTrials
    {
        public const int TurnLimit = 6;

        public static string NameOf(AdventureTrial trial) =>
            trial switch
            {
                AdventureTrial.WithinSixTurns => $"{TurnLimit}ターン以内に勝つ",
                AdventureTrial.NoOneFalls => "誰も倒れずに勝つ",
                AdventureTrial.HalfHp => "全員がHPを半分以上残して勝つ",
                _ => "",
            };

        public static string KeyOf(AdventureTrial trial) =>
            trial switch
            {
                AdventureTrial.WithinSixTurns => "within-6-turns",
                AdventureTrial.NoOneFalls => "no-one-falls",
                AdventureTrial.HalfHp => "half-hp",
                _ => "",
            };

        public static AdventureTrial Parse(string key) =>
            key switch
            {
                "within-6-turns" => AdventureTrial.WithinSixTurns,
                "no-one-falls" => AdventureTrial.NoOneFalls,
                "half-hp" => AdventureTrial.HalfHp,
                _ => AdventureTrial.None,
            };

        /// <summary>
        /// Whether a battle won meets the trial: in how many turns it was won, whether anyone
        /// fell in it, and the share of maximum HP each member present ended with.
        /// </summary>
        public static bool Met(
            AdventureTrial trial,
            int turns,
            bool anyoneFell,
            IEnumerable<float> shares
        ) =>
            trial switch
            {
                AdventureTrial.WithinSixTurns => turns <= TurnLimit,
                AdventureTrial.NoOneFalls => !anyoneFell,
                AdventureTrial.HalfHp => !anyoneFell
                    && (shares ?? Array.Empty<float>()).All(share => share >= 0.5f),
                _ => false,
            };
    }

    /// <summary>One thing a merchant sells, for runes (doc/features/stage-progression.md, マスの中身).</summary>
    public sealed class AdventureWare
    {
        public AdventureWare(
            int index,
            AdventureDropCategory category,
            string id,
            float multiplier,
            ItemQuality? quality,
            int price,
            bool sold
        )
        {
            Index = index;
            Category = category;
            Id = id ?? "";
            Multiplier = multiplier;
            Quality = quality;
            Price = price;
            Sold = sold;
        }

        public int Index { get; }
        public AdventureDropCategory Category { get; }
        public string Id { get; }
        public float Multiplier { get; }
        public ItemQuality? Quality { get; }
        public int Price { get; }
        public bool Sold { get; }
        public int Stars => AdventureDrops.StarsOf(Category, Id);

        /// <summary>The ware as a drop, to show it with the drops' plates and words.</summary>
        public AdventureDrop AsDrop(string roomId) =>
            new(roomId, Category, Id, Multiplier, Quality);
    }

    /// <summary>The merchants' prices and wares, as the server's (server/src/features/adventure).</summary>
    public static class AdventureWares
    {
        // 値段（ルーン）：☆ごとの元値に品質の倍率を掛け、10の位で丸める（仮）。
        private static readonly int[] StarPrices = { 300, 600, 1200, 2400, 4800 };

        // 品揃え：スキル3つと装備2つ。
        public const int Skills = 3;
        public const int Gear = 2;

        public static int PriceOf(int stars, ItemQuality? quality)
        {
            int basePrice = StarPrices[Math.Clamp(stars, 1, StarPrices.Length) - 1];
            float rate = quality switch
            {
                ItemQuality.S => 2f,
                ItemQuality.A => 1.5f,
                ItemQuality.B => 1.25f,
                _ => 1f,
            };
            return (int)Math.Round(basePrice * rate / 10.0) * 10;
        }
    }

    /// <summary>
    /// What the room the party is in holds besides its kind: whether a shady chest opened was a
    /// mimic, a trial's condition, and a merchant's wares.
    /// </summary>
    public sealed class AdventureContents
    {
        public static readonly AdventureContents Empty = new(false, AdventureTrial.None, null);

        public AdventureContents(
            bool mimic,
            AdventureTrial trial,
            IReadOnlyList<AdventureWare> wares
        )
        {
            Mimic = mimic;
            Trial = trial;
            Wares = wares ?? Array.Empty<AdventureWare>();
        }

        public bool Mimic { get; }
        public AdventureTrial Trial { get; }
        public IReadOnlyList<AdventureWare> Wares { get; }
    }

    /// <summary>What today's steps opened a step door to (doc/features/stage-progression.md).</summary>
    public sealed class AdventureStepDoor
    {
        public AdventureStepDoor(int steps, int tier)
        {
            Steps = steps;
            Tier = tier;
        }

        public int Steps { get; }

        // 0：3,000歩未満、1：3,000〜、2：5,000〜、3：8,000歩以上。
        public int Tier { get; }

        public static int TierOf(int steps) =>
            steps >= 8000 ? 3
            : steps >= 5000 ? 2
            : steps >= 3000 ? 1
            : 0;
    }

    /// <summary>How a room is finished: passed by (a shady chest), or a trial met.</summary>
    public sealed class AdventureClearOptions
    {
        public static readonly AdventureClearOptions None = new();

        public bool Pass { get; set; }
        public bool TrialMet { get; set; }

        // 進化の祭壇で固有スキルを進化させた。進化させたら、祭壇は物を落とさない。
        public bool Evolved { get; set; }
    }
}
