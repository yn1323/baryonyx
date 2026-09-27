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
        [Tooltip("8,000以上にすると、ボーナスをすべて解放した表示になります。")]
        public int Steps = 3820;

        [Min(0)]
        public int Runes = 12480;

        [Tooltip(
            "有効にすると、所持ルーンに上の値を表示し、ワットパネルを押すたびに下の仮の獲得量を足します。サーバーのルーンは増えません。"
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
                Runes = Runes,
                DestinationName = DestinationName,
                DestinationFloor = DestinationFloor,
                Today = today,
            };
    }
}
