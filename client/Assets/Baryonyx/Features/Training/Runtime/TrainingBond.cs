using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using Baryonyx.Party;
using UnityEngine;

namespace Baryonyx.Training
{
    /// <summary>
    /// The character's breakthrough (限界突破; the code and the server call it the bond): a copy of a
    /// character the player already has becomes a token of that character, and each token raises
    /// it one stage, up to <see cref="Max"/>
    /// (doc/features/monetization.md). Each stage raises every stat by
    /// <see cref="PercentPerStage"/> percent, the mock effect until each character has its own.
    /// With a server, the stages and tokens are the party's (<see cref="PartySession.State"/>) and
    /// deepening is saved there (<see cref="PartySession.DeepenBondAsync"/>). Without it they start
    /// from the mock data and are kept while the app runs.
    /// </summary>
    public static class TrainingBond
    {
        public const int Max = 6;
        public const int PercentPerStage = 2;

        // 1段突破するのに使うしるし。
        public const int TokensPerStage = 1;

        private static readonly Dictionary<string, int> stages = new();
        private static readonly Dictionary<string, int> tokens = new();

        /// <summary>How many percent every stat rises at <paramref name="stage"/>.</summary>
        public static int PercentAt(int stage) => Clamp(stage) * PercentPerStage;

        public static double RateOf(int stage) => 1.0 + PercentAt(stage) / 100.0;

        /// <summary>What reaching <paramref name="stage"/> adds, as the breakthrough tab writes it.</summary>
        public static string EffectOf(int stage) =>
            stage >= 1 && stage <= Max ? $"全ステータス +{PercentPerStage}%" : "";

        public static int StageOf(TrainingCharacter character) =>
            character == null ? 0
            : PartySession.State is { } state ? Clamp(state.Find(character.Id)?.Bond ?? 0)
            : stages.TryGetValue(character.Id, out int stage) ? stage
            : Clamp(character.Bond);

        public static int TokensOf(TrainingCharacter character) =>
            character == null ? 0
            : PartySession.State is { } state ? state.Find(character.Id)?.Tokens ?? 0
            : tokens.TryGetValue(character.Id, out int count) ? count
            : Math.Max(0, character.BondTokens);

        /// <summary>The character's stats at <paramref name="level"/> with the bond's raise.</summary>
        public static CharacterStats StatsOf(TrainingCharacter character, int level) =>
            character != null ? character.StatsAt(level, StageOf(character)) : default;

        /// <summary>
        /// Spends a token to deepen the bond one stage while the app has no server; false at the
        /// cap, without a token, or with a server (which deepens it instead).
        /// </summary>
        public static bool Deepen(TrainingCharacter character)
        {
            int stage = StageOf(character);
            int count = TokensOf(character);
            if (
                character == null
                || PartySession.State != null
                || stage >= Max
                || count < TokensPerStage
            )
                return false;
            stages[character.Id] = stage + 1;
            tokens[character.Id] = count - TokensPerStage;
            return true;
        }

        // Play Modeに入るたびに仮データの段へ戻す（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            stages.Clear();
            tokens.Clear();
        }

        private static int Clamp(int stage) => Math.Clamp(stage, 0, Max);
    }
}
