using System;
using System.Globalization;

namespace Baryonyx.Home
{
    /// <summary>
    /// Display strings and gauge values derived from a snapshot, kept free of Unity objects
    /// so the formatting rules can be tested directly.
    /// </summary>
    public sealed class HomeViewState
    {
        public const int GaugeSegments = 20;

        // 歩数をゲーム共通の単位ACTへ換える率。doc/features/exercise-rewards.md の1歩＝1ACT。
        public const int ActPerStep = 1;

        // ゲージの最大は、厚生労働省が成人に勧める1日8,000歩を換算した8,000ACTに全員そろえる。
        public const int GaugeMaxAct = 8000;

        // 日次のACTボーナスが解放されるACT。doc/game/redesign-2026-09-27.md の候補値で、未確定。
        public static readonly int[] BonusAct = { 1000, 2000, 3000, 5000, 8000 };

        private static readonly string[] Weekdays = { "日", "月", "火", "水", "木", "金", "土" };

        public string DateText { get; private set; }
        public bool ShowSteps { get; private set; }
        public string ActText { get; private set; }
        public int FilledSegments { get; private set; }
        public bool DailyAchieved { get; private set; }
        public string RemainingText { get; private set; }
        public string ClaimText { get; private set; }

        // 同期中以外は、TAP TO STARTと同じように案内を点滅させて押せることを示す。
        public bool ClaimPulses { get; private set; }
        public string RunesText { get; private set; }

        // 右下のカード。冒険の途中なら行き先と階と「再開」、そうでなければ旅の案内所と「出発」。
        public bool AdventureInProgress { get; private set; }
        public string DestinationNameText { get; private set; }
        public string DestinationFloorText { get; private set; }
        public string ResumeText { get; private set; }

        public const string TravelTitle = "冒険に出る";
        public const string TravelName = "旅の案内所";

        public static HomeViewState From(HomeSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            int act = ActFor(snapshot.Steps);
            bool linked = snapshot.StepLink == HomeStepLink.Linked;
            bool known = snapshot.StepsKnown;
            int nextBonus = NextBonusFor(act);
            bool achieved = linked && known && nextBonus == 0;

            return new HomeViewState
            {
                DateText =
                    $"{snapshot.Today.Month}/{snapshot.Today.Day}（{Weekdays[(int)snapshot.Today.DayOfWeek]}）",
                ShowSteps = linked,
                ActText = known ? Number(act) : "--",
                FilledSegments = known ? FilledSegmentsFor(act, GaugeMaxAct) : 0,
                DailyAchieved = achieved,
                RemainingText =
                    !known ? ""
                    : nextBonus == 0 ? "ボーナスをすべて獲得！"
                    : $"あと {Number(nextBonus - act)} ACTで次のボーナス獲得",
                ClaimText =
                    snapshot.StepSyncing ? "Loading..."
                    : linked ? "タップでルーン獲得"
                    : "タップして歩数を連携",
                ClaimPulses = !snapshot.StepSyncing,
                RunesText = snapshot.RunesKnown ? Runes(snapshot.Runes) : "--",
                AdventureInProgress = snapshot.AdventureInProgress,
                DestinationNameText = snapshot.AdventureInProgress
                    ? snapshot.DestinationName ?? ""
                    : TravelTitle,
                DestinationFloorText = snapshot.AdventureInProgress
                    ? snapshot.DestinationFloor ?? ""
                    : TravelName,
                ResumeText = snapshot.AdventureInProgress ? "再開" : "出発",
            };
        }

        public static int ActFor(int steps) =>
            (int)Math.Min(int.MaxValue, (long)Math.Max(0, steps) * ActPerStep);

        public static int FilledSegmentsFor(int act, int max)
        {
            if (max <= 0 || act <= 0)
                return 0;
            if (act >= max)
                return GaugeSegments;
            return (int)((long)act * GaugeSegments / max);
        }

        // まだ解放していない最初のボーナスのACT。すべて解放済みなら0。
        public static int NextBonusFor(int act)
        {
            foreach (int bonus in BonusAct)
                if (act < bonus)
                    return bonus;
            return 0;
        }

        public static string MessageFor(HomeAction action, HomeStepLink link) =>
            action switch
            {
                HomeAction.SyncSteps => link == HomeStepLink.Linked
                    ? "歩数を同期しました（モック）"
                    : "歩数の連携（準備中）",
                HomeAction.Settings => "設定（準備中）",
                HomeAction.Tavern => "編成（準備中）",
                HomeAction.Workshop => "商会（準備中）",
                HomeAction.Temple => "神殿（準備中）",
                HomeAction.TravelOffice => "旅の案内所（準備中）",
                HomeAction.Resume => "再開（準備中）",
                _ => "",
            };

        // 所持ルーンと獲得量の表記。演出で数える途中の値にも使う。
        public static string Runes(long value) =>
            Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);

        private static string Number(int value) =>
            value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
