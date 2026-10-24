using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Economy;
using Baryonyx.Networking;
using Baryonyx.SkillEvolution;
using UnityEngine;

namespace Baryonyx.Enhancement
{
    /// <summary>
    /// The trading house's enhancements while the app runs (doc/features/enhancement.md): the
    /// materials held, read from <see cref="Source"/> (the game server, set by the app) or kept
    /// in a mock without one. An enhancement waits for the server's answer before it shows, since
    /// the next one starts from the multiplier or stage it gave (doc/rules/data-storage.md); its
    /// cost is taken off the shown balance while it waits, and the write goes in the app's one
    /// line of saves.
    /// </summary>
    public static class EnhancementSession
    {
        // 商会の画面の「品質強化」の項目のキー。一覧の代わりに品質強化のパネルを開く。
        public const string GuideItemKey = "workshop-enhance";

        // 商会の「スキル覚醒」の項目のキー。覚醒だけのパネルを開く（品質強化は GuideItemKey）。
        public const string AwakeningItemKey = "workshop-awaken";

        private static EnhancementMemorySource local;
        private static Dictionary<string, int> known;

        /// <summary>The server to enhance on. Without it, the mock materials are used.</summary>
        public static IEnhancementSource Source { get; set; }

        public static EnhancementMemorySource Local => local ??= new EnhancementMemorySource();

        public static IEnhancementSource SourceOrLocal => Source ?? Local;

        /// <summary>How many of the material are held, as last read or left by an enhancement (0 when unknown).</summary>
        public static int CountOf(string materialId) =>
            known != null && materialId != null && known.TryGetValue(materialId, out int count)
                ? count
                : 0;

        public static async Task<IReadOnlyDictionary<string, int>> LoadAsync(
            CancellationToken token
        )
        {
            var counts = await SourceOrLocal.LoadAsync(token);
            Use(counts);
            return counts;
        }

        /// <summary>
        /// Raises <paramref name="target"/>'s multiplier with <paramref name="count"/> stones and
        /// waits for the result. A raised unique skill's multiplier goes to its progress at once.
        /// </summary>
        public static Task<EnhancementOutcome> PolishAsync(
            EnhancementTarget target,
            EnhancementMaterial stone,
            int count
        ) =>
            Send(
                (stone?.Runes ?? 0) * count,
                (source, token) => source.PolishAsync(target, stone, count, token),
                outcome =>
                {
                    if (target.Category == EnhancementCategory.Unique)
                        UniqueSkillSession
                            .ProgressOf(target.CharacterId, target.Index, null)
                            .Multiplier = outcome.Multiplier;
                }
            );

        /// <summary>Awakens a unique skill into the option's stage, sets it, and waits for the result.</summary>
        public static Task<EnhancementOutcome> AwakenAsync(AwakeningOption option) =>
            Send(
                option?.Runes ?? 0,
                (source, token) => source.AwakenAsync(option, token),
                outcome =>
                    UniqueSkillSession.Evolve(option.CharacterId, option.Index, outcome.Stage, null)
            );

        private static async Task<EnhancementOutcome> Send(
            int runes,
            Func<IEnhancementSource, CancellationToken, Task<EnhancementOutcome>> send,
            Action<EnhancementOutcome> applied
        )
        {
            var source = Source;
            if (source == null)
            {
                var outcome = await send(Local, CancellationToken.None);
                Done(outcome, applied);
                return outcome;
            }
            RuneBalance.Reserve(runes);
            try
            {
                var outcome = await SaveQueue.Enqueue(token => send(source, token));
                Done(outcome, applied);
                return outcome;
            }
            finally
            {
                RuneBalance.Release(runes);
            }
        }

        private static void Done(EnhancementOutcome outcome, Action<EnhancementOutcome> applied)
        {
            Use(outcome.Materials);
            RuneBalance.Set(outcome.BalanceAfter);
            applied(outcome);
        }

        private static void Use(IReadOnlyDictionary<string, int> counts)
        {
            if (counts != null)
                known = new Dictionary<string, int>(counts);
        }

        // Play Modeに入るたびに、前の接続先と仮の素材を持ち越さない。接続先はアプリが設定し直す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            local = null;
            known = null;
        }
    }
}
