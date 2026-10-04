using System;
using UnityEngine;

namespace Baryonyx.CardLoadout
{
    /// <summary>
    /// Keeps who the tavern's card skills show while the app runs, and how another screen opens
    /// them for one character (the training's "スキルを付け替える"). The cards themselves are
    /// the party's (<see cref="Baryonyx.Party.PartySession"/>).
    /// </summary>
    public static class CardLoadoutSession
    {
        // 酒場のメニューの「スキル」の項目のキー。この項目はリストの代わりにスキルの画面を開く。
        public const string GuideItemKey = "card-skill";

        // 最後に見ていたキャラのID。開き直すとそのキャラから見せる。
        public static string Selected { get; set; }

        // ほかの画面から開いたときの戻り先。「もどる」で酒場のメニューの代わりにこれを呼ぶ。
        private static Action back;

        /// <summary>
        /// Shows <paramref name="characterId"/> the next time the card skills open. With
        /// <paramref name="onBack"/>, back (the button and the device key) calls it instead of
        /// going back to the tavern's menu, once. The caller then opens the card skills item.
        /// </summary>
        public static void Open(string characterId, Action onBack)
        {
            Selected = characterId;
            back = onBack;
        }

        public static bool HasBack => back != null;

        /// <summary>Calls and forgets the caller's back; false when the menu was opened as usual.</summary>
        public static bool TakeBack()
        {
            var action = back;
            back = null;
            if (action == null)
                return false;
            action();
            return true;
        }

        // 閉じたら戻り先を忘れる。次に酒場のメニューから開いたときは、酒場のメニューへ戻る。
        public static void ForgetBack() => back = null;

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Selected = null;
            back = null;
        }
    }
}
