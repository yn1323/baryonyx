using System;
using UnityEngine;

namespace Baryonyx.Home
{
    /// <summary>
    /// Fixed sample values for the home screen mock. Nothing here reads health data,
    /// rune balances or the server. With <see cref="MockRuneGain"/> on, the rune balance
    /// and each tap's gain also come from here instead of the server.
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

        [Tooltip(
            "有効にすると、所持ルーンに上の値を表示し、歩数パネルを押すたびに下の仮の獲得量を足します。サーバーのルーンは増えません。"
        )]
        public bool MockRuneGain = true;

        [Min(0)]
        [Tooltip("押すたびに獲得する仮のルーン。0にすると「獲得ルーンはありません」を表示します。")]
        public int MockGrantedRunes = 1340;

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
