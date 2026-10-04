using System;
using UnityEngine;

namespace Baryonyx.Home
{
    /// <summary>
    /// Fixed sample values for the home screen mock. Nothing here reads health data,
    /// rune balances or the server. The running Home scene replaces the steps and runes
    /// with the server's values; the prefab and showcase previews keep these.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Home/Mock Data")]
    public sealed class HomeMockData : ScriptableObject
    {
        [Tooltip("未連携にすると、歩数の代わりに連携の案内を表示します。")]
        public HomeStepLink StepLink = HomeStepLink.Linked;

        [Min(0)]
        [Tooltip("8,000以上にすると、ボーナスをすべて解放した表示になります。")]
        public int Steps = 3820;

        [Min(0)]
        public int Runes = 12480;

        [Tooltip("冒険の途中にすると、右下のカードが行き先の再開になります。")]
        public bool AdventureInProgress;

        public string DestinationName = "森の遺跡";

        public string DestinationFloor = "B3F";

        public HomeSnapshot ToSnapshot(DateTime today) =>
            new()
            {
                StepLink = StepLink,
                Steps = Steps,
                Runes = Runes,
                AdventureInProgress = AdventureInProgress,
                DestinationName = DestinationName,
                DestinationFloor = DestinationFloor,
                Today = today,
            };
    }
}
