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

        // 行商人の品物の見本：スキル3つと装備2つ。2つ目は売り切れ、最後の1つはルーンが足りない値段。
        public static IReadOnlyList<AdventureWare> Wares()
        {
            var random = new Random(7);
            var wares = new List<AdventureWare>();
            var categories = new[]
            {
                AdventureDropCategory.Skill,
                AdventureDropCategory.Skill,
                AdventureDropCategory.Skill,
                AdventureDropCategory.Weapon,
                AdventureDropCategory.Accessory,
            };
            for (int i = 0; i < categories.Length; i++)
            {
                AdventureDrop pick = null;
                for (int attempt = 0; attempt < 12 && pick == null; attempt++)
                {
                    var drop = AdventureDrops.PickWare(
                        AdventureCatalog.ForestRuins,
                        categories[i],
                        AdventureTexts.MaxMultiplierOf,
                        random
                    );
                    if (drop != null && !wares.Exists(ware => ware.Id == drop.Id))
                        pick = drop;
                }
                if (pick == null)
                    continue;
                int price = AdventureWares.PriceOf(
                    AdventureDrops.StarsOf(pick.Category, pick.Id),
                    pick.Quality
                );
                wares.Add(
                    new AdventureWare(
                        i,
                        pick.Category,
                        pick.Id,
                        pick.Multiplier,
                        pick.Quality,
                        price,
                        i == 1
                    )
                );
            }
            return wares;
        }

        // 冒険の結果の見本。クリアは3回目（その回のクリア回数の報酬つき）にする。
        public const int Clears = 3;

        public static AdventureResult Result(AdventureEndStatus status) =>
            new(
                AdventureCatalog.ForestRuins,
                status,
                status == AdventureEndStatus.Cleared ? BestFloor : Floor,
                BestFloor,
                status == AdventureEndStatus.Cleared,
                status == AdventureEndStatus.Cleared ? Clears : Clears - 1,
                0,
                ResultDrops(),
                status == AdventureEndStatus.Cleared
                    ? AdventureClearRewards.At(AdventureCatalog.ForestRuins, Clears)
                    : null
            );

        // 冒険の結果に並ぶ物の見本：品質と☆の違う物、同じ物を2つ以上、2段を超える数。
        public static IReadOnlyList<AdventureDrop> ResultDrops() =>
            new[]
            {
                new AdventureDrop("", AdventureDropCategory.Item, "herb"),
                new AdventureDrop("", AdventureDropCategory.Item, "herb"),
                new AdventureDrop(
                    "",
                    AdventureDropCategory.Weapon,
                    "flame-dagger",
                    1.82f,
                    ItemQuality.S
                ),
                new AdventureDrop(
                    "",
                    AdventureDropCategory.Weapon,
                    "short-bow",
                    1.4f,
                    ItemQuality.B
                ),
                new AdventureDrop(
                    "",
                    AdventureDropCategory.Accessory,
                    "dew-pendant",
                    1.55f,
                    ItemQuality.A
                ),
                new AdventureDrop("", AdventureDropCategory.Item, "acorn"),
                new AdventureDrop("", AdventureDropCategory.Item, "acorn"),
                new AdventureDrop("", AdventureDropCategory.Item, "acorn"),
                new AdventureDrop("", AdventureDropCategory.Item, "dew-vial"),
                new AdventureDrop("", AdventureDropCategory.Item, "moss-wad"),
                new AdventureDrop("", AdventureDropCategory.Item, "shell-shard"),
                new AdventureDrop(
                    "",
                    AdventureDropCategory.Accessory,
                    "moss-charm",
                    1.25f,
                    ItemQuality.C
                ),
                new AdventureDrop(
                    "",
                    AdventureDropCategory.Weapon,
                    "wooden-sword",
                    1.45f,
                    ItemQuality.B
                ),
                new AdventureDrop("", AdventureDropCategory.Item, "dew-vial"),
            };
    }
}
