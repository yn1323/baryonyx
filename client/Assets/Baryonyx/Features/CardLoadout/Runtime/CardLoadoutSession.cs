using Baryonyx.Party;
using UnityEngine;

namespace Baryonyx.CardLoadout
{
    /// <summary>
    /// Keeps who the adventurer's card skills tab shows while the app runs, and the slot to
    /// choose when it opens. The cards themselves are the party's
    /// (<see cref="PartySession"/>).
    /// </summary>
    public static class CardLoadoutSession
    {
        // 編成の冒険者の「スキル」のタブの項目のキー。メニューには出さず、冒険者のタブから開く。
        public const string GuideItemKey = "card-skill";

        // 最後に見ていたキャラのID。冒険者の一覧とほかのタブと共有する。
        public static string Selected
        {
            get => PartySession.Selected;
            set => PartySession.Selected = value;
        }

        // 次に開いたときに選んでおく枠（0から）。-1なら1つ目のカスタムスキルの枠を選ぶ。
        public static int OpenSlot { get; set; } = -1;

        // 次に開いたときに選んでおく固有スキル（0から）。-1なら OpenSlot に従う。
        public static int OpenUnique { get; set; } = -1;

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            OpenSlot = -1;
            OpenUnique = -1;
        }
    }
}
