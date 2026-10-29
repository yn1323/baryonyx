using System;
using System.Collections.Generic;
using Baryonyx.Inventory;

namespace Baryonyx.UI
{
    /// <summary>The things of one kind (weapons, items, skills...) a debugging room's drops and chests are made of.</summary>
    [Serializable]
    public sealed class RewardSampleSet
    {
        /// <summary>The kind's name on the room's button ("武器").</summary>
        public string Name = "";

        public RewardItem[] Items = Array.Empty<RewardItem>();
    }

    /// <summary>
    /// What a debugging room's drop or chest holds (doc/features/screens/debug-rooms.md): the
    /// kind chosen (or all kinds mixed), how many, and the quality chosen, which the things
    /// with a quality take. The things of the kind are taken in turn, so the next run shows
    /// others, and are put best first, as the game shows them.
    /// </summary>
    public static class RewardLabSamples
    {
        /// <summary>The counts the rooms offer, in turn.</summary>
        public static readonly int[] Counts = { 1, 2, 3, 4, 6 };

        /// <summary>The name of the choice that mixes every kind, after the kinds.</summary>
        public const string MixedName = "混ぜる";

        /// <summary>The name of the kind at <paramref name="kind"/>: one of <paramref name="sets"/>, or the mix after them.</summary>
        public static string KindName(IReadOnlyList<RewardSampleSet> sets, int kind) =>
            sets != null && kind >= 0 && kind < sets.Count ? sets[kind].Name : MixedName;

        /// <summary>How many choices of kind there are: the kinds and the mix.</summary>
        public static int KindCount(IReadOnlyList<RewardSampleSet> sets) => (sets?.Count ?? 0) + 1;

        /// <summary>
        /// <paramref name="count"/> things of the kind at <paramref name="kind"/> (the mix after
        /// the kinds), the next ones from <paramref name="turn"/>, at <paramref name="quality"/>
        /// where they have a quality; empty when <paramref name="quality"/> is null (nothing
        /// found) or there is nothing of the kind.
        /// </summary>
        public static List<RewardItem> Pick(
            IReadOnlyList<RewardSampleSet> sets,
            int kind,
            int count,
            ItemQuality? quality,
            ref int turn
        )
        {
            var picked = new List<RewardItem>();
            if (quality == null || sets == null || sets.Count == 0)
                return picked;
            bool mixed = kind < 0 || kind >= sets.Count;
            for (int i = 0; i < count; i++)
            {
                int at = turn + i;
                var set = sets[mixed ? at % sets.Count : kind];
                if (set?.Items == null || set.Items.Length == 0)
                    continue;
                var source = set.Items[(mixed ? at / sets.Count : at) % set.Items.Length];
                picked.Add(
                    new RewardItem
                    {
                        Kind = source.Kind,
                        Name = source.Name,
                        Icon = source.Icon,
                        Graded = source.Graded,
                        FoilStyle = source.FoilStyle,
                        Quality = source.Graded ? quality.Value : source.Quality,
                    }
                );
            }
            turn += Math.Max(1, count);
            // The best first, as a real drop is shown; things with no quality after them.
            picked.Sort((a, b) => Rank(b).CompareTo(Rank(a)));
            return picked;
        }

        private static int Rank(RewardItem item) =>
            item.Graded ? Baryonyx.Inventory.Quality.Grade(item.Quality) : -1;
    }
}
