using UnityEngine;

namespace Baryonyx.StepBonus
{
    /// <summary>
    /// Keeps the bonus slots while the app runs and where they are saved. The app sets
    /// <see cref="Source"/> to the game server, which keeps each player's bonuses and slots;
    /// without it (the showcase, or no server URL) the slots start from the mock data and stay
    /// only while the app runs. Home hands over today's UPT so the tavern shows the same open
    /// slots.
    /// </summary>
    public static class StepBonusSession
    {
        // 酒場のメニューの「ボーナス」の項目のキー。この項目はリストの代わりにボーナス設定を開く。
        public const string GuideItemKey = "bonus";

        private static StepBonusLoadout loadout;

        // 持ち物と枠を読み書きするサーバー。なければ仮データを使う。
        public static IStepBonusSource Source { get; set; }

        // ホームで取得した今日のUPT。取得していなければnull。
        public static int? TodayUpt { get; set; }

        public static StepBonusLoadout Loadout(StepBonusMockData data) =>
            loadout ??= StepBonusLoadout.From(data);

        // サーバーから読んだ、または保存した結果に置き換える。
        public static void Use(StepBonusLoadout value) => loadout = value;

        public static int UptOr(StepBonusMockData data) =>
            TodayUpt ?? (data != null ? data.TodayUpt : 0);

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            loadout = null;
            TodayUpt = null;
            Source = null;
        }
    }
}
