using System;
using UnityEngine;

namespace Baryonyx.Home
{
    /// <summary>
    /// Fixed sample values for the home screen mock. Nothing here reads health data,
    /// rune balances or the server.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Home/Mock Data")]
    public sealed class HomeMockData : ScriptableObject
    {
        [Tooltip("未連携にすると、歩数の代わりに連携の案内を表示します。")]
        public HomeStepLink StepLink = HomeStepLink.Linked;

        [Min(0)]
        [Tooltip("今日の目標以上にすると、達成時の表示になります。")]
        public int Steps = 3820;

        [Min(0)]
        public int DailyGoal = 5000;

        [Min(0)]
        public int WeeklyDone = 2;

        [Min(0)]
        public int WeeklyTarget = 3;

        [Min(0)]
        public int Runes = 12480;

        public string DestinationName = "森の遺跡";

        public string DestinationFloor = "B3F";

        public HomeSnapshot ToSnapshot(DateTime today) =>
            new()
            {
                StepLink = StepLink,
                Steps = Steps,
                DailyGoal = DailyGoal,
                WeeklyDone = WeeklyDone,
                WeeklyTarget = WeeklyTarget,
                Runes = Runes,
                DestinationName = DestinationName,
                DestinationFloor = DestinationFloor,
                Today = today,
            };
    }
}
