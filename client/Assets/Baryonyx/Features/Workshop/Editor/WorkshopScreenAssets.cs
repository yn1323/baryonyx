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
        public const string IconBuyPath = Folder + "/UI/Art/IconBuy.aseprite";
        public const string IconSellPath = Folder + "/UI/Art/IconSell.aseprite";
        public const string IconCraftPath = Folder + "/UI/Art/IconCraft.aseprite";

        [MenuItem("Baryonyx/Workshop/Create Screen Assets")]
        public static void CreateAssets() =>
            CreateScreen(DefinitionPath, PrefabPath, GuideArtPath, BackgroundPath, Fill);

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
