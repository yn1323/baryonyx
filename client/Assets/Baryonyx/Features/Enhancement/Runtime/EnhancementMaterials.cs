using System;
using System.Collections.Generic;
using Baryonyx.Inventory;

namespace Baryonyx.Enhancement
{
    /// <summary>One kind of enhancement material: a polishing stone of one colour, or the awakening shard.</summary>
    public sealed class EnhancementMaterial
    {
        public EnhancementMaterial(
            string id,
            string name,
            string shortName,
            int stars,
            int ratePercent,
            int runes
        )
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            Stars = stars;
            RatePercent = ratePercent;
            Runes = runes;
        }

        public string Id { get; }
        public string Name { get; }

        // 絵がまだないときに札へ出す、短い名前。
        public string ShortName { get; }
        public int Stars { get; }

        // 磨き石1つで上がる量（倍率の幅に対する百分率）。覚醒の欠片は0。
        public int RatePercent { get; }

        // 磨き石1つあたりのルーン。覚醒の欠片は段階で決まるため0。
        public int Runes { get; }

        public bool IsPolish => RatePercent > 0;
    }

    /// <summary>
    /// The enhancement materials (doc/features/enhancement.md), the same as the server
    /// (server/src/features/enhancement/catalog.ts): the polishing stones raise a thing's
    /// multiplier by a share of its range, the colour deciding how much, and the awakening shard
    /// takes a unique skill one stage on. The values are trial ones.
    /// </summary>
    public static class EnhancementMaterials
    {
        public const string White = "polish-white";
        public const string Blue = "polish-blue";
        public const string Purple = "polish-purple";
        public const string Gold = "polish-gold";
        public const string Shard = "awakening-shard";

        // 覚醒で1段進めるのに使う欠片の数と、払うルーン（1段目へ・2段目へ）。
        public const int ShardsPerStage = 1;
        public const int FirstStageRunes = 1000;
        public const int SecondStageRunes = 2000;

        public static readonly IReadOnlyList<EnhancementMaterial> Polishes = new[]
        {
            new EnhancementMaterial(White, "白い磨き石", "白", 1, 5, 100),
            new EnhancementMaterial(Blue, "青い磨き石", "青", 2, 10, 200),
            new EnhancementMaterial(Purple, "紫の磨き石", "紫", 3, 25, 500),
            new EnhancementMaterial(Gold, "金の磨き石", "金", 4, 50, 1000),
        };

        public static readonly EnhancementMaterial AwakeningShard = new(Shard, "覚醒の欠片", "欠片", 4, 0, 0);

        /// <summary>Every material in the server's order: the stones from white to gold, then the shard.</summary>
        public static readonly IReadOnlyList<EnhancementMaterial> All = new List<EnhancementMaterial>(
            Polishes
        )
        {
            AwakeningShard,
        };

        public static EnhancementMaterial Find(string id)
        {
            foreach (var material in All)
                if (material.Id == id)
                    return material;
            return null;
        }

        /// <summary>How much one stone raises a multiplier, in hundredths, for a kind whose top is <paramref name="max"/>.</summary>
        public static int Step(EnhancementMaterial stone, float max) =>
            stone == null || !stone.IsPolish
                ? 0
                : (int)
                    Math.Round(
                        (Quality.Percent(max) - Quality.Percent(Quality.MinMultiplier))
                            * stone.RatePercent
                            / 100.0,
                        MidpointRounding.AwayFromZero
                    );

        /// <summary>The multiplier after <paramref name="count"/> stones, stopping at the top.</summary>
        public static float Polished(float multiplier, EnhancementMaterial stone, int count, float max)
        {
            int top = Quality.Percent(max);
            int after = Math.Min(top, Quality.Percent(multiplier) + Step(stone, max) * Math.Max(0, count));
            return after / 100f;
        }

        /// <summary>
        /// The most stones that each still raise the multiplier: past this the last one would
        /// raise nothing, which the server refuses.
        /// </summary>
        public static int UsefulCount(float multiplier, EnhancementMaterial stone, float max)
        {
            int step = Step(stone, max);
            int gap = Quality.Percent(max) - Quality.Percent(multiplier);
            if (step <= 0 || gap <= 0)
                return 0;
            return (gap + step - 1) / step;
        }

        /// <summary>The runes for taking a unique skill to <paramref name="stage"/>: more for a branch's second step.</summary>
        public static int AwakeningRunes(int stage) =>
            SkillEvolution.UniqueSkillTreeShape.Parent(stage) == 0 ? FirstStageRunes : SecondStageRunes;
    }
}
