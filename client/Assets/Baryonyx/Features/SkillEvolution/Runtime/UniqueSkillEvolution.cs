using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.SkillEvolution
{
    /// <summary>
    /// How a unique skill evolves when its card is used in battle
    /// (doc/features/skill-evolution.md#戦闘中の進化): the stages a stage's card can become, and
    /// the chance that it evolves (the room's base chance, lowered the more the user's level is
    /// over the destination's recommended level, doc/features/skill-evolution.md#確率の仮設定).
    /// </summary>
    public static class UniqueSkillEvolution
    {
        /// <summary>The share of the base chance lost for each level over the recommended one.</summary>
        public const float LevelStep = 0.1f;

        /// <summary>The least share of the base chance kept, however high the level.</summary>
        public const float LeastShare = 0.2f;

        /// <summary>The tree and the stage of a stage's card ("ManaBurst.1" is ManaBurst's 1), or false.</summary>
        public static bool TryFind(string cardId, out UniqueSkillTree tree, out int stage)
        {
            if (!string.IsNullOrEmpty(cardId))
                foreach (var candidate in UniqueSkillTrees.All)
                    for (int i = 0; i < candidate.Stages.Count; i++)
                        if (candidate.Stages[i]?.Card?.Id == cardId)
                        {
                            tree = candidate;
                            stage = i;
                            return true;
                        }
            tree = null;
            stage = -1;
            return false;
        }

        /// <summary>
        /// The stages a stage's card can evolve into, in order: three from a first stage, one
        /// from a branch's first step, none from a last step or a card that is no stage.
        /// </summary>
        public static IReadOnlyList<int> NextStages(string cardId)
        {
            var next = new List<int>();
            if (!TryFind(cardId, out var tree, out int stage))
                return next;
            foreach (int child in UniqueSkillTreeShape.Children(stage))
                if (tree.StageAt(child) != null)
                    next.Add(child);
            return next;
        }

        /// <summary>
        /// The chance (0 to 1) that a card used evolves: <paramref name="basePercent"/> of the
        /// room, lowered by a tenth of it for each of the user's levels over
        /// <paramref name="recommended"/>, down to a fifth of it. A level at or under the
        /// recommended one keeps it all.
        /// </summary>
        public static float Chance(float basePercent, int level, int recommended)
        {
            int over = Mathf.Max(0, level - recommended);
            float share = Mathf.Max(LeastShare, 1f - LevelStep * over);
            return Mathf.Clamp01(basePercent / 100f * share);
        }
    }
}
