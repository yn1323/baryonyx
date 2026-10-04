using UnityEngine;

namespace Baryonyx.StepBonus
{
    /// <summary>
    /// Keeps the bonus slots while the app runs, so a change made in the tavern is still there
    /// when the player comes back. Where the slots are saved is not decided yet
    /// (doc/features/step-bonus.md), so they start again from the mock data on every launch.
    /// Home hands over today's UPT so the tavern shows the same open slots.
    /// </summary>
    public static class StepBonusSession
    {
        // 酒場のメニューの「ボーナス」の項目のキー。ホームのボタンはこの項目を直接開く。
        public const string GuideItemKey = "bonus";

        private static StepBonusLoadout loadout;

        // ホームで取得した今日のUPT。取得していなければnull。
        public static int? TodayUpt { get; set; }

        public static StepBonusLoadout Loadout(StepBonusMockData data) =>
            loadout ??= StepBonusLoadout.From(data);

        public static int UptOr(StepBonusMockData data) =>
            TodayUpt ?? (data != null ? data.TodayUpt : 0);

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            loadout = null;
            TodayUpt = null;
        }
    }
}
