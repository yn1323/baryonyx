using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using Baryonyx.SkillEvolution;
using UnityEngine;

namespace Baryonyx.Party
{
    /// <summary>
    /// The progress of the characters' unique skills as the game server keeps it
    /// (doc/features/skill-evolution.md#覚醒の保存): read with the party into
    /// <see cref="UniqueSkillSession"/>, and each change (the stage the formation sets, an
    /// evolution in an adventure's battle) saved behind the other writes in the
    /// <see cref="SaveQueue"/>. The change shows at once; a save that fails is told by the
    /// notice band, and the server's progress comes back the next time the party is read.
    /// Without a server, the progress stays in the app only.
    /// </summary>
    public static class PartyUniqueSkills
    {
        public const string SaveFailedMessage = "固有スキルの進み具合を保存できませんでした";

        private static int pending;

        /// <summary>True while a change waits to be saved.</summary>
        public static bool Saving => pending > 0;

        /// <summary>The last save, for tests to wait on.</summary>
        public static Task Work { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Puts the progress the server read into <see cref="UniqueSkillSession"/>. Skipped while
        /// a change waits to be saved, since the server's is older then.
        /// </summary>
        public static void Use(PartyState state)
        {
            if (state == null || Saving)
                return;
            foreach (var character in state.Owned)
            {
                if (character.Uniques == null)
                    continue;
                for (int index = 0; index < character.Uniques.Count; index++)
                    UniqueSkillSession.Use(character.Id, index, character.Uniques[index]);
            }
        }

        /// <summary>Saves the stage the formation sets of the person's unique skill <paramref name="index"/>.</summary>
        public static void SetStage(string person, int index, int stage) =>
            Send((source, token) => source.SetUniqueAsync(person, index, stage, token));

        /// <summary>Saves one evolution of the person's unique skill <paramref name="index"/> into <paramref name="stage"/>.</summary>
        public static void Evolve(string person, int index, int stage) =>
            Send((source, token) => source.EvolveUniqueAsync(person, index, stage, token));

        private static void Send(
            Func<IPartyUniqueSkillSource, CancellationToken, Task<PartyState>> send
        )
        {
            if (PartySession.Source is not IPartyUniqueSkillSource source)
                return;
            pending++;
            Work = SendAsync(source, send);
        }

        private static async Task SendAsync(
            IPartyUniqueSkillSource source,
            Func<IPartyUniqueSkillSource, CancellationToken, Task<PartyState>> send
        )
        {
            PartyState saved = null;
            try
            {
                saved = await SaveQueue.Enqueue(token => send(source, token));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(SaveFailedMessage + "。" + exception.Message);
                SaveQueue.Report(SaveFailedMessage);
            }
            finally
            {
                pending--;
            }
            // 待っている変更がなくなったら、サーバーが確かめた進み具合にそろえる。
            Use(saved);
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            pending = 0;
            Work = Task.CompletedTask;
        }
    }
}
