using System;
using System.Collections.Generic;
using Baryonyx.Inventory;

namespace Baryonyx.Enhancement
{
    /// <summary>One kind of enhancement material: the polishing stone, or the awakening shard.</summary>
    public sealed class EnhancementMaterial
    {
        public EnhancementMaterial(string id, string name, string shortName, int stars, int runes)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            Stars = stars;
            Runes = runes;
        }

        public string Id { get; }
        public string Name { get; }

        // 絵がまだないときに札へ出す、短い名前。
        public string ShortName { get; }
        public int Stars { get; }

        // 磨き石1つあたりのルーン。覚醒の欠片は段階で決まるため0。
        public int Runes { get; }

        public bool IsPolish => Id == EnhancementMaterials.White;
    }

    /// <summary>
    /// The enhancement materials (doc/features/enhancement.md), the same as the server
    /// (server/src/features/enhancement/catalog.ts): one kind of polishing stone raises a
    /// thing's multiplier a step at a time, each step a share of its range and costing more
    /// stones the higher the quality, and the awakening shard takes a unique skill one stage on.
    /// The values are trial ones.
    /// </summary>
    public static class EnhancementMaterials
    {
        // 磨き石は1種類（2026-11-02、ユーザーの指示「白だけで大丈夫」）。IDと絵は白い磨き石のものを使う。
        public const string White = "polish-white";
        public const string Shard = "awakening-shard";

        // 覚醒で1段進めるのに使う欠片の数と、払うルーン（1段目へ・2段目へ）。
        public const int ShardsPerStage = 1;
        public const int FirstStageRunes = 1000;
        public const int SecondStageRunes = 2000;

        // 1段階で上がる量（倍率の幅に対する百分率）。
        public const int StepRatePercent = 5;

        public static readonly EnhancementMaterial Stone = new(White, "磨き石", "石", 1, 100);

        public static readonly EnhancementMaterial AwakeningShard = new(Shard, "覚醒の欠片", "欠片", 4, 0);

        /// <summary>Every material in the server's order: the stone, then the shard.</summary>
        public static readonly IReadOnlyList<EnhancementMaterial> All = new[] { Stone, AwakeningShard };

        public static EnhancementMaterial Find(string id)
        {
            foreach (var material in All)
                if (material.Id == id)
                    return material;
            return null;
        }

        /// <summary>How much one step raises a multiplier, in hundredths, for a kind whose top is <paramref name="max"/>.</summary>
        public static int StepPercent(float max) =>
            (int)
                Math.Round(
                    (Quality.Percent(max) - Quality.Percent(Quality.MinMultiplier))
                        * StepRatePercent
                        / 100.0,
                    MidpointRounding.AwayFromZero
                );

        /// <summary>The stones one step takes from a multiplier of <paramref name="quality"/>: more the higher it is.</summary>
        public static int StonesPerStep(ItemQuality quality) =>
            quality switch
            {
                ItemQuality.C => 1,
                ItemQuality.B => 2,
                ItemQuality.A => 3,
                _ => 6,
            };

        /// <summary>The multiplier after <paramref name="steps"/> steps, stopping at the kind's top.</summary>
        public static float Polished(float multiplier, int steps, float max)
        {
            int top = Quality.Percent(max);
            int now = Quality.Percent(multiplier);
            int after = Math.Max(now, Math.Min(top, now + StepPercent(max) * Math.Max(0, steps)));
            return after / 100f;
        }

        /// <summary>
        /// The stones <paramref name="steps"/> steps take, each costing what the quality it starts
        /// from asks; a step past the top costs nothing.
        /// </summary>
        public static int StonesFor(float multiplier, int steps, float max)
        {
            int top = Quality.Percent(max);
            int step = StepPercent(max);
            int now = Quality.Percent(multiplier);
            int stones = 0;
            for (int i = 0; i < steps && now < top && step > 0; i++)
            {
                stones += StonesPerStep(Quality.Of(now / 100f, max));
                now = Math.Min(top, now + step);
            }
            return stones;
        }

        /// <summary>The most steps that each still raise the multiplier: past this the last one would raise nothing, which the server refuses.</summary>
        public static int UsefulSteps(float multiplier, float max)
        {
            int step = StepPercent(max);
            int gap = Quality.Percent(max) - Quality.Percent(multiplier);
            if (step <= 0 || gap <= 0)
                return 0;
            return (gap + step - 1) / step;
        }

        /// <summary>The runes <paramref name="stones"/> stones cost.</summary>
        public static int RunesFor(int stones) => Stone.Runes * Math.Max(0, stones);

        /// <summary>The runes for taking a unique skill to <paramref name="stage"/>: more for a branch's second step.</summary>
        public static int AwakeningRunes(int stage) =>
            SkillEvolution.UniqueSkillTreeShape.Parent(stage) == 0
                ? FirstStageRunes
                : SecondStageRunes;
    }
}
