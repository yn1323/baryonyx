using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.SkillEvolution
{
    /// <summary>
    /// Keeps each character's unique skill progress while the app runs: what the mock data gives
    /// at first, and the stage the formation sets. The server does not keep it yet
    /// (doc/features/skill-evolution.md), so it resets when the app starts.
    /// </summary>
    public static class UniqueSkillSession
    {
        private static readonly Dictionary<
            (string Person, int Index),
            UniqueSkillProgress
        > Progress = new();

        /// <summary>
        /// The progress of the person's unique skill <paramref name="index"/>, starting from
        /// <paramref name="mock"/> (the first stage only when null). The mock is never changed.
        /// </summary>
        public static UniqueSkillProgress ProgressOf(
            string person,
            int index,
            UniqueSkillProgress mock
        )
        {
            if (string.IsNullOrEmpty(person))
                return new UniqueSkillProgress();
            if (!Progress.TryGetValue((person, index), out var progress))
            {
                progress = mock?.Copy() ?? new UniqueSkillProgress();
                Progress[(person, index)] = progress;
            }
            return progress;
        }

        /// <summary>Sets an acquired stage of the person's unique skill <paramref name="index"/>.</summary>
        public static bool SetStage(string person, int index, int stage, UniqueSkillProgress mock)
        {
            var progress = ProgressOf(person, index, mock);
            if (!progress.Has(stage) || progress.SetStage == stage)
                return false;
            progress.Set = stage;
            return true;
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => Progress.Clear();
    }
}
