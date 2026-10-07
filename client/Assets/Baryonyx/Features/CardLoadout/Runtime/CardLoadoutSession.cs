using Baryonyx.Party;
using UnityEngine;

namespace Baryonyx.CardLoadout
{
    /// <summary>
    /// Keeps who the formation's card skills show while the app runs, and the slot to choose
    /// when the adventurer's page opens them. The cards themselves are the party's
    /// (<see cref="PartySession"/>).
    /// </summary>
    public static class CardLoadoutSession
    {
        // 編成の「スキル」の項目のキー。メニューには出さず、冒険者の個別の画面から付け替えの画面として開く。
        public const string GuideItemKey = "card-skill";

        // 最後に見ていたキャラのID。冒険者の一覧・個別・装備の画面と共有する。
        public static string Selected
        {
            get => PartySession.Selected;
            set => PartySession.Selected = value;
        }

        // 次に開いたときに選んでおく枠（0から）。個別の画面で押した枠を渡す。
        public static int OpenSlot { get; set; }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => OpenSlot = 0;
    }
}
