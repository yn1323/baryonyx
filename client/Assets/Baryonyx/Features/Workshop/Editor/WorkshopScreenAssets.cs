using Baryonyx.Editor.Art;
using Baryonyx.Enhancement;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// The trading house (商会; the code keeps the Workshop name): the young noble heir of an old
    /// arms shop buys and sells equipment, skills and materials for runes, and makes equipment
    /// and skills from materials. Each item opens its own panel filling the screen without the
    /// guide. "Buy" (<see cref="WorkshopBuyAssets"/>): what the player has found, at the lowest
    /// quality, bought from the server. "Sell" (<see cref="WorkshopSellAssets"/>): the held
    /// things, one by one; selling is not decided yet (doc/features/equipment.md), so the button
    /// only tells it is not ready. "Craft" (<see cref="WorkshopCraftAssets"/>): three things one
    /// star lower make a found thing. "Enhance" (<see cref="WorkshopEnhanceAssets"/>): polishing
    /// stones raise a thing's quality, and awakening shards take a unique skill on a stage.
    /// </summary>
    public static class WorkshopScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Workshop";
        public const string DefinitionPath = Folder + "/Data/WorkshopGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/WorkshopScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/WorkshopGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/WorkshopBackground.png";

        // The menu's icons are plain shapes without outlines, like the formation menu: a price tag
        // for buying, a stack of coins for selling, an anvil and hammer for making things, and a cut
        // gem for enhancing.
        // The home button already shows scales, so the menu avoids them. The text colour is
        // baked in, since the guide screens list pixel art with the same white tint.
        public const string IconBuyPath = Folder + "/UI/Art/IconBuy.png";
        public const string IconSellPath = Folder + "/UI/Art/IconSell.png";
        public const string IconCraftPath = Folder + "/UI/Art/IconCraft.png";
        public const string IconEnhancePath = Folder + "/UI/Art/IconEnhance.png";
        public const string IconAwakenPath = Folder + "/UI/Art/IconAwaken.png";

        // The clear margin that separates a front part from the part behind it (design units).
        private const float PartGap = 2.6f;

        [MenuItem("Baryonyx/Workshop/Create Screen Assets")]
        public static void CreateAssets()
        {
            WriteMenuIcons();
            CreateScreen(
                DefinitionPath,
                PrefabPath,
                GuideArtPath,
                BackgroundPath,
                Fill,
                itemPanel: (item, safe, view) =>
                    item.Key switch
                    {
                        WorkshopSession.BuyItemKey => WorkshopBuyAssets.BuildPanel(safe, view),
                        WorkshopSession.SellItemKey => WorkshopSellAssets.BuildPanel(safe, view),
                        WorkshopSession.CraftItemKey => WorkshopCraftAssets.BuildPanel(safe, view),
                        EnhancementSession.GuideItemKey => WorkshopEnhanceAssets.BuildPanel(
                            safe,
                            view
                        ),
                        EnhancementSession.AwakeningItemKey => WorkshopEnhanceAssets.BuildPanel(
                            safe,
                            view,
                            awakening: true
                        ),
                        _ => null,
                    }
            );
        }

        private static void WriteMenuIcons()
        {
            PriceTag().Save(IconBuyPath, SmoothIconTextureSize, UiPalette.TextMain);
            Coins().Save(IconSellPath, SmoothIconTextureSize, UiPalette.TextMain);
            AnvilAndHammer().Save(IconCraftPath, SmoothIconTextureSize, UiPalette.TextMain);
            CutGem().Save(IconEnhancePath, SmoothIconTextureSize, UiPalette.TextMain);
            RisingStar().Save(IconAwakenPath, SmoothIconTextureSize, UiPalette.TextMain);
        }

        // A cut gem seen from the side: the table on top, the crown's facets, and the point below.
        private static VectorIcon CutGem() =>
            new VectorIcon()
                .Fill(IconShape.Path("M18 9 L46 9 L60 25 L32 59 L4 25Z"))
                .CutStroke(
                    IconShape.Path(
                        "M4 25 L60 25 M18 9 L25 25 L32 59 M46 9 L39 25 L32 59 M25 25 L32 9 L39 25"
                    ),
                    2.6f
                );

        // A tag with a hole for its string and two price lines.
        // スキル覚醒：上へ伸びる光の筋の上に、4本の光を放つ星。
        private static VectorIcon RisingStar() =>
            new VectorIcon()
                .Fill(IconShape.Path("M32 3 L38 21 L56 27 L38 33 L32 51 L26 33 L8 27 L26 21Z"))
                .Fill(IconShape.Rect(14f, 50f, 6f, 11f, 3f), PartGap)
                .Fill(IconShape.Rect(29f, 55f, 6f, 6f, 3f), PartGap)
                .Fill(IconShape.Rect(44f, 50f, 6f, 11f, 3f), PartGap);

        private static VectorIcon PriceTag() =>
            new VectorIcon()
                .Fill(IconShape.Path("M30 5 L57 5 L59 7 L59 34 L32 61 L3 32Z"))
                .Cut(IconShape.Circle(48f, 16f, 4.5f))
                .CutStroke(IconShape.Path("M21 38 L35 24 M27 44 L41 30"), 3.2f);

        // Four coins stacked on their edge, and one coin in front showing its face.
        private static VectorIcon Coins()
        {
            var icon = new VectorIcon();
            foreach (float y in new[] { 49f, 40f, 31f, 22f })
                icon.Fill(IconShape.Rect(6f, y, 28f, 7f, 3.5f));
            return icon.Fill(IconShape.Circle(44f, 42f, 15f), PartGap)
                .CutStroke(IconShape.Circle(44f, 42f, 8.5f), 2.6f);
        }

        // An anvil with a hammer raised over it.
        private static VectorIcon AnvilAndHammer()
        {
            IconShape Hammer(IconShape part) => part.Rotated(-28f, 0f, 0f).Translated(19f, 14f);
            return new VectorIcon()
                .Fill(
                    IconShape.Path(
                        "M3 33 L47 33 Q55 33 61 27 Q62 40 50 42 L42 42 L42 49 L49 59 L15 59 L22 49 L22 42 Q8 42 3 33Z"
                    )
                )
                .Fill(Hammer(IconShape.Rect(-4.6f, -10.5f, 9.2f, 21f)), PartGap)
                .Fill(Hammer(IconShape.Rect(4f, -2.2f, 28f, 4.4f)));
        }

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "商会";
            d.Layout = GuideMenuLayout.List;
            d.Items = new[]
            {
                Buy(),
                Panel("売る", "売る", IconSellPath, WorkshopSession.SellItemKey),
                Panel("合成", "合成", IconCraftPath, WorkshopSession.CraftItemKey),
                Panel("品質強化", "品質強化", IconEnhancePath, EnhancementSession.GuideItemKey),
                Panel(
                    "スキル覚醒",
                    "スキル覚醒",
                    IconAwakenPath,
                    EnhancementSession.AwakeningItemKey
                ),
            };
        }

        // 一覧の代わりに品物のパネルを開く。これまでに手に入れたスキル・武器・防具・アクセサリーを、品質の最低で売る。
        private static GuideMenuItem Buy() =>
            Panel("買う", "買う", IconBuyPath, WorkshopSession.BuyItemKey);

        // 一覧の代わりに、キーの項目のパネルを開く項目。
        private static GuideMenuItem Panel(string label, string confirm, string icon, string key)
        {
            var item = Item(label, confirm, Icon(icon));
            item.Key = key;
            return item;
        }
    }
}
