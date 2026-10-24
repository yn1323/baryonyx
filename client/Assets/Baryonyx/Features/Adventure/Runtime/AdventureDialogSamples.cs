using System;
using System.Collections.Generic;
using Baryonyx.Inventory;

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
                Array.Empty<AdventureDrop>(),
                Floor
            );

        // ミストラ遺跡で落ちる物の見本。アイテム・武器・アクセサリー。
        public static IReadOnlyList<AdventureDrop> Drops() =>
            new[]
            {
                new AdventureDrop("", AdventureDropCategory.Item, "herb"),
                new AdventureDrop(
                    "",
                    AdventureDropCategory.Weapon,
                    "wooden-sword",
                    1.45f,
                    ItemQuality.B
                ),
                new AdventureDrop(
                    "",
                    AdventureDropCategory.Accessory,
                    "moss-charm",
                    1.25f,
                    ItemQuality.B
                ),
            };

        public static AdventureResult Result(AdventureEndStatus status) =>
            new(
                AdventureCatalog.ForestRuins,
                status,
                status == AdventureEndStatus.Cleared ? BestFloor : Floor,
                BestFloor,
                status == AdventureEndStatus.Cleared,
                status == AdventureEndStatus.Cleared ? 1 : 0,
                0,
                Drops()
            );
    }
}
