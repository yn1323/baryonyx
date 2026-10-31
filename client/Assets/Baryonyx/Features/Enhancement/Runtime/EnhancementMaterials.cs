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
            int runes,
            ItemQuality? ceiling = null,
            int combineRunes = 0
        )
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            Stars = stars;
            RatePercent = ratePercent;
            Runes = runes;
            Ceiling = ceiling;
            CombineRunes = combineRunes;
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

        // 上げられる上限の品質。その品質の入口で止まる。金と覚醒の欠片は null（種類の上限まで）。
        public ItemQuality? Ceiling { get; }

        // 1つ下の色から合成で1個作るときのルーン。合成で作れない白と覚醒の欠片は0。
        public int CombineRunes { get; }

        public bool IsPolish => RatePercent > 0;
        public bool IsCombined => CombineRunes > 0;
    }

    /// <summary>One combine of a plan: <see cref="Count"/> of <see cref="Made"/>, each from three of the colour below.</summary>
    public readonly struct CombineStep
    {
        public CombineStep(EnhancementMaterial made, int count)
        {
            Made = made;
            Count = count;
        }

        public EnhancementMaterial Made { get; }
        public int Count { get; }
        public EnhancementMaterial From => EnhancementMaterials.CombineFrom(Made);
        public int Uses => EnhancementMaterials.CombineInputs * Count;
        public int Runes => Made.CombineRunes * Count;
    }

    /// <summary>
    /// The enhancement materials (doc/features/enhancement.md), the same as the server
    /// (server/src/features/enhancement/catalog.ts): the polishing stones raise a thing's
    /// multiplier by a share of its range, the colour deciding how much, and the awakening shard
    /// takes a unique skill one stage on. A colour raises a thing only up to its ceiling, and
    /// three of a colour with runes make one of the next. The values are trial ones.
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

        // 合成で上の色を1個作るのに使う、1つ下の色の数。
        public const int CombineInputs = 3;

        // 色ごとの上限：白はBの入口、青はAの入口、紫はSの入口まで、金は種類の上限まで（2026-10-31）。
        public static readonly IReadOnlyList<EnhancementMaterial> Polishes = new[]
        {
            new EnhancementMaterial(White, "白い磨き石", "白", 1, 5, 100, ItemQuality.B),
            new EnhancementMaterial(Blue, "青い磨き石", "青", 2, 10, 200, ItemQuality.A, 100),
            new EnhancementMaterial(Purple, "紫の磨き石", "紫", 3, 25, 500, ItemQuality.S, 300),
            new EnhancementMaterial(Gold, "金の磨き石", "金", 4, 50, 1000, null, 900),
        };

        public static readonly EnhancementMaterial AwakeningShard = new(
            Shard,
            "覚醒の欠片",
            "欠片",
            4,
            0,
            0
        );

        /// <summary>Every material in the server's order: the stones from white to gold, then the shard.</summary>
        public static readonly IReadOnlyList<EnhancementMaterial> All =
            new List<EnhancementMaterial>(Polishes) { AwakeningShard };

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

        /// <summary>
        /// The highest multiplier <paramref name="stone"/> can raise a thing to, in hundredths:
        /// the lowest of its ceiling quality (a border going to the higher quality), never past the
        /// kind's top <paramref name="max"/>.
        /// </summary>
        public static int CeilingPercent(EnhancementMaterial stone, float max)
        {
            int top = Quality.Percent(max);
            return stone?.Ceiling is { } ceiling
                ? Math.Min(top, Quality.Percent(Quality.Range(ceiling, max).Min))
                : top;
        }

        /// <summary>Whether <paramref name="stone"/> can still raise <paramref name="multiplier"/>: it is below the stone's ceiling.</summary>
        public static bool CanRaise(float multiplier, EnhancementMaterial stone, float max) =>
            stone != null
            && stone.IsPolish
            && Quality.Percent(multiplier) < CeilingPercent(stone, max);

        /// <summary>The multiplier after <paramref name="count"/> stones, stopping at the stone's ceiling.</summary>
        public static float Polished(
            float multiplier,
            EnhancementMaterial stone,
            int count,
            float max
        )
        {
            int top = CeilingPercent(stone, max);
            int now = Quality.Percent(multiplier);
            int after = Math.Max(now, Math.Min(top, now + Step(stone, max) * Math.Max(0, count)));
            return after / 100f;
        }

        /// <summary>
        /// The most stones that each still raise the multiplier: past this the last one would
        /// raise nothing, which the server refuses.
        /// </summary>
        public static int UsefulCount(float multiplier, EnhancementMaterial stone, float max)
        {
            int step = Step(stone, max);
            int gap = CeilingPercent(stone, max) - Quality.Percent(multiplier);
            if (step <= 0 || gap <= 0)
                return 0;
            return (gap + step - 1) / step;
        }

        /// <summary>The stone one colour lower that <paramref name="made"/> is combined from, or null (white, the shard).</summary>
        public static EnhancementMaterial CombineFrom(EnhancementMaterial made)
        {
            if (made == null || !made.IsCombined)
                return null;
            for (int i = 1; i < Polishes.Count; i++)
                if (Polishes[i].Id == made.Id)
                    return Polishes[i - 1];
            return null;
        }

        /// <summary>
        /// The combines that make up <paramref name="need"/> of <paramref name="stone"/> from the
        /// stones held (<paramref name="held"/> by id), the lowest colour first: empty when enough
        /// are held, null when even the white stones fall short.
        /// </summary>
        public static IReadOnlyList<CombineStep> CombinePlan(
            EnhancementMaterial stone,
            int need,
            Func<string, int> held
        )
        {
            var needs = new int[Polishes.Count];
            int index = IndexOf(stone);
            if (index < 0)
                return null;
            needs[index] = need;
            return CombinePlan(needs, held);
        }

        /// <summary>
        /// The combines that make up <paramref name="needs"/> (how many of each colour, white to
        /// gold) from the stones held, the lowest colour first: empty when enough are held, null
        /// when even the white stones fall short. A colour used directly is not changed into the
        /// next.
        /// </summary>
        public static IReadOnlyList<CombineStep> CombinePlan(
            IReadOnlyList<int> needs,
            Func<string, int> held
        )
        {
            if (needs == null || held == null)
                return null;
            var made = new int[Polishes.Count];
            int carry = 0;
            // 上の色から下りる：その色の要る数（直接使う数と、上の色を作るのに使う数）が持っている数を超えたら、
            // 足りない分を1つ下の色の3倍から作る。白が足りなければ作れない。
            for (int k = Polishes.Count - 1; k >= 0; k--)
            {
                int demand = (k < needs.Count ? needs[k] : 0) + carry;
                int shortage = demand - held(Polishes[k].Id);
                carry = 0;
                if (shortage <= 0)
                    continue;
                if (k == 0)
                    return null;
                made[k] = shortage;
                carry = CombineInputs * shortage;
            }
            var steps = new List<CombineStep>();
            for (int i = 0; i < made.Length; i++)
                if (made[i] > 0)
                    steps.Add(new CombineStep(Polishes[i], made[i]));
            return steps;
        }

        /// <summary>
        /// The multiplier after the stones <paramref name="uses"/> (how many of each colour, white
        /// to gold), used from the lowest colour, each stopping at its ceiling.
        /// <paramref name="allRaise"/> is false when any stone would raise nothing (the server
        /// refuses those).
        /// </summary>
        public static float PolishedAll(
            float multiplier,
            IReadOnlyList<int> uses,
            float max,
            out bool allRaise
        )
        {
            allRaise = true;
            float now = multiplier;
            for (int k = 0; k < Polishes.Count && uses != null && k < uses.Count; k++)
            for (int n = 0; n < uses[k]; n++)
            {
                float next = Polished(now, Polishes[k], 1, max);
                if (Quality.Percent(next) <= Quality.Percent(now))
                    allRaise = false;
                now = next;
            }
            return now;
        }

        private static int IndexOf(EnhancementMaterial stone)
        {
            for (int i = 0; i < Polishes.Count; i++)
                if (Polishes[i] == stone)
                    return i;
            return -1;
        }

        /// <summary>The runes all the combines of a plan cost.</summary>
        public static int CombineRunesOf(IEnumerable<CombineStep> plan)
        {
            int runes = 0;
            foreach (var step in plan ?? Array.Empty<CombineStep>())
                runes += step.Runes;
            return runes;
        }

        /// <summary>The stones that can be made by combining: blue, purple and gold.</summary>
        public static IEnumerable<EnhancementMaterial> Combined
        {
            get
            {
                foreach (var stone in Polishes)
                    if (stone.IsCombined)
                        yield return stone;
            }
        }

        /// <summary>The runes for taking a unique skill to <paramref name="stage"/>: more for a branch's second step.</summary>
        public static int AwakeningRunes(int stage) =>
            SkillEvolution.UniqueSkillTreeShape.Parent(stage) == 0
                ? FirstStageRunes
                : SecondStageRunes;
    }
}
