using System;
using Baryonyx.StepBonus;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Made-up adventure data for the dialogs shown while the editor is stopped, where there is
    /// no server and no session (doc/rules/ui-design.md#停止中の表示の切り替え).
    /// </summary>
    public static class AdventureDialogSamples
    {
        public const int Floor = 3;
        public const int BestFloor = 5;
        public const int ReviveCost = 300;
        public const long Runes = 1250;

        public static AdventureRun Run =>
            new(
                "preview",
                AdventureCatalog.ForestRuins,
                "",
                false,
                Array.Empty<string>(),
                0,
                ReviveCost,
                null,
                Array.Empty<AdventureReward>(),
                Floor
            );

        // 仮データの最初のボーナス。
        public static AdventureReward Reward(StepBonusMockData bonuses) =>
            new(
                "",
                bonuses != null && bonuses.Bonuses.Length > 0 ? bonuses.Bonuses[0].Id : "",
                StepBonusRank.B,
                AdventureRewardOutcome.Added
            );

        public static AdventureResult Result(
            StepBonusMockData bonuses,
            AdventureEndStatus status
        ) =>
            new(
                AdventureCatalog.ForestRuins,
                status,
                status == AdventureEndStatus.Cleared ? BestFloor : Floor,
                BestFloor,
                status == AdventureEndStatus.Cleared,
                status == AdventureEndStatus.Cleared ? 1 : 0,
                0,
                new[] { Reward(bonuses) }
            );
    }
}
