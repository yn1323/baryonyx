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

        private static readonly string[] Weekdays = { "日", "月", "火", "水", "木", "金", "土" };

        public string DateText { get; private set; }
        public bool ShowSteps { get; private set; }
        public string StepsText { get; private set; }
        public string GoalText { get; private set; }
        public int FilledSegments { get; private set; }
        public bool DailyAchieved { get; private set; }
        public string RemainingText { get; private set; }
        public string WeeklyText { get; private set; }
        public string ClaimText { get; private set; }
        public string RunesText { get; private set; }
        public string DestinationNameText { get; private set; }
        public string DestinationFloorText { get; private set; }

        public static HomeViewState From(HomeSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            int steps = Math.Max(0, snapshot.Steps);
            int goal = Math.Max(0, snapshot.DailyGoal);
            bool linked = snapshot.StepLink == HomeStepLink.Linked;
            bool achieved = linked && goal > 0 && steps >= goal;

            return new HomeViewState
            {
                DateText =
                    $"{snapshot.Today.Month}月{snapshot.Today.Day}日（{Weekdays[(int)snapshot.Today.DayOfWeek]}）",
                ShowSteps = linked,
                StepsText = Number(steps),
                GoalText = goal > 0 ? $"今日の目標 {Number(goal)}" : "今日の目標 未設定",
                FilledSegments = FilledSegmentsFor(steps, goal),
                DailyAchieved = achieved,
                RemainingText =
                    goal <= 0 ? ""
                    : achieved ? "今日の目標 達成！"
                    : $"あと {Number(goal - steps)} 歩",
                WeeklyText =
                    snapshot.WeeklyTarget > 0
                        ? $"今週の目標 {Math.Max(0, snapshot.WeeklyDone)} / {snapshot.WeeklyTarget} 回"
                        : "",
                ClaimText = linked ? "タップでルーンを取得" : "タップして歩数を連携",
                RunesText = Number(Math.Max(0, snapshot.Runes)),
                DestinationNameText = snapshot.DestinationName ?? "",
                DestinationFloorText = snapshot.DestinationFloor ?? "",
            };
        }

        public static int FilledSegmentsFor(int steps, int goal)
        {
            if (goal <= 0 || steps <= 0)
                return 0;
            if (steps >= goal)
                return GaugeSegments;
            return (int)((long)steps * GaugeSegments / goal);
        }

        public static string MessageFor(HomeAction action, HomeStepLink link) =>
            action switch
            {
                HomeAction.ClaimRunes => link == HomeStepLink.Linked
                    ? "ルーンを取得しました（モック）"
                    : "歩数の連携（準備中）",
                HomeAction.Settings => "設定（準備中）",
                HomeAction.Party => "パーティ（準備中）",
                HomeAction.Equipment => "装備（準備中）",
                HomeAction.Summon => "召喚（準備中）",
                HomeAction.Goals => "目標（準備中）",
                HomeAction.WorldMap => "ワールドマップ（準備中）",
                HomeAction.Resume => "再開（準備中）",
                _ => "",
            };

        private static string Number(int value) =>
            value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
