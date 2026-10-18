using Baryonyx.Editor.Art;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// The trading house (商会; the code keeps the Workshop name): the young noble heir of an old
    /// arms shop buys and sells equipment, skills and materials for runes, and makes equipment
    /// and skills from materials. The lists and prices are mock data until trading has data
    /// (doc/features/equipment.md); choosing a row only tells it is not ready.
    /// </summary>
    public static class WorkshopScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Workshop";
        public const string DefinitionPath = Folder + "/Data/WorkshopGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/WorkshopScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/WorkshopGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/WorkshopBackground.png";

        // The menu's icons are plain shapes without outlines, like the formation menu: a price tag
        // for buying, a stack of coins for selling, and an anvil and hammer for making things.
        // The home button already shows scales, so the menu avoids them. The text colour is
        // baked in, since the guide screens list pixel art with the same white tint.
        public const string IconBuyPath = Folder + "/UI/Art/IconBuy.png";
        public const string IconSellPath = Folder + "/UI/Art/IconSell.png";
        public const string IconCraftPath = Folder + "/UI/Art/IconCraft.png";

        // The clear margin that separates a front part from the part behind it (design units).
        private const float PartGap = 2.6f;

        [MenuItem("Baryonyx/Workshop/Create Screen Assets")]
        public static void CreateAssets()
        {
            WriteMenuIcons();
            CreateScreen(DefinitionPath, PrefabPath, GuideArtPath, BackgroundPath, Fill);
        }

        private static void WriteMenuIcons()
        {
            PriceTag().Save(IconBuyPath, SmoothIconTextureSize, UiPalette.TextMain);
            Coins().Save(IconSellPath, SmoothIconTextureSize, UiPalette.TextMain);
            AnvilAndHammer().Save(IconCraftPath, SmoothIconTextureSize, UiPalette.TextMain);
        }

        // A tag with a hole for its string and two price lines.
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
                Item(
                    "買う",
                    "装備・スキル・素材をルーンで買う",
                    "買う",
                    Icon(IconBuyPath),
                    Entry("鉄の剣", "400 ルーン", "武器｜物攻の 120%｜斬"),
                    Entry("鎖かたびら", "450 ルーン", "防具｜物防の 130%"),
                    Entry("ファイア", "300 ルーン", "スキル｜炎の魔法で敵単体を攻撃"),
                    Entry("ヒール", "300 ルーン", "スキル｜味方単体のHPを回復"),
                    Entry("鉄くず", "30 ルーン", "素材｜武器と防具の合成に使う"),
                    Entry("火の魔石", "80 ルーン", "素材｜炎の装備とスキルの合成に使う"),
                    Entry("氷の結晶", "80 ルーン", "素材｜氷の装備とスキルの合成に使う")
                ),
                Item(
                    "売る",
                    "持っている装備・スキル・素材を売る",
                    "売る",
                    Icon(IconSellPath),
                    Entry("木の剣", "50 ルーン", "武器｜物攻の 100%｜斬"),
                    Entry("木の丸盾", "60 ルーン", "防具｜物防の 120%"),
                    Entry("斬り払い", "40 ルーン", "スキル｜所持 2"),
                    Entry("獣の牙", "10 ルーン", "素材｜所持 12"),
                    Entry("鉄くず", "15 ルーン", "素材｜所持 8")
                ),
                Item(
                    "合成",
                    "素材を合わせて装備やスキルを作る",
                    "合成する",
                    Icon(IconCraftPath),
                    Entry("炎のダガー", "★★★★", "火の魔石×3・鉄くず×2・獣の牙×4"),
                    Entry("氷晶のワンド", "★★★", "氷の結晶×3・鉄くず×1"),
                    Entry("雷鳴の槍", "★★★", "雷の羽根×3・鉄くず×3"),
                    Entry("ブリザード", "スキル", "氷の結晶×5・魔石のかけら×2"),
                    Entry("ライトニングボルト", "スキル", "雷の羽根×4・魔石のかけら×2")
                ),
            };
        }
    }
}
