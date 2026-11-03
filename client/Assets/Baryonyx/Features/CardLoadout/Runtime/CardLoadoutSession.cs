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

        // 編成の冒険者の「覚醒」のタブの項目のキー。スキルのタブと同じ6行とツリーで、覚醒させる。
        public const string AwakenItemKey = "party-awaken";

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

        // スキル・覚醒のタブで最後に選んでいた固有スキルとカスタムスキルの枠（選んでいない方は-1）。
        // 強化・覚醒のタブへ移ったときに、同じ物を選んでおく。
        public static int LastUnique { get; set; } = -1;
        public static int LastSlot { get; set; } = -1;

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            OpenSlot = -1;
            OpenUnique = -1;
            LastUnique = -1;
            LastSlot = -1;
        }
    }
}
