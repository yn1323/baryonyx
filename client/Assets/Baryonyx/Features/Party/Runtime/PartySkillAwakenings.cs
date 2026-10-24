using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Party
{
    /// <summary>
    /// Owned skill cards awakening in an adventure's battle (doc/features/skill-awakening.md#保存):
    /// each awakening kept when its room ends shows on the card at once (<see cref="PartySession"/>)
    /// and is saved behind the other writes; a save that fails is told by the notice band and the
    /// card goes back as the server keeps it. Without a server, a card is not owned one by one and
    /// nothing awakens.
    /// </summary>
    public static class PartySkillAwakenings
    {
        public const string SaveFailedMessage = "スキルの覚醒を保存できませんでした";

        /// <summary>The last save, for tests to wait on.</summary>
        public static Task Work { get; private set; } = Task.CompletedTask;

        /// <summary>Awakens the owned card <paramref name="cardId"/> into <paramref name="branch"/> (1 or 2) and saves it.</summary>
        public static void Awaken(string cardId, int branch)
        {
            if (string.IsNullOrEmpty(cardId) || branch is not (1 or 2))
                return;
            if (PartySession.Source is not IPartySkillCardSource || PartySession.State == null)
                return;
            Work = AwakenAsync(cardId, branch);
        }

        private static async Task AwakenAsync(string cardId, int branch)
        {
            try
            {
                await PartySession.AwakenCardAsync(cardId, branch, SaveFailedMessage);
            }
            catch (Exception exception)
            {
                // 失敗は保存の仕組みが通知の帯で知らせ、カードをサーバーの状態へ戻す。
                Debug.LogWarning(SaveFailedMessage + "。" + exception.Message);
            }
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => Work = Task.CompletedTask;
    }
}
