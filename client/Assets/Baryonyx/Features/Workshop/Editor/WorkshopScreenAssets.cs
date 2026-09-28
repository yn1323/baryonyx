using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// The workshop (工房): the young noble heir of an old arms workshop guides changing gear
    /// and browsing weapons and armour. The items and values are mock data until equipment
    /// has data.
    /// </summary>
    public static class WorkshopScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Workshop";
        public const string DefinitionPath = Folder + "/Data/WorkshopGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/WorkshopScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/WorkshopGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/WorkshopBackground.png";
        public const string IconChangeGearPath = Folder + "/UI/Art/IconChangeGear.aseprite";
        public const string IconWeaponPath = Folder + "/UI/Art/IconWeapon.aseprite";
        public const string IconArmorPath = Folder + "/UI/Art/IconArmor.aseprite";

        [MenuItem("Baryonyx/Workshop/Create Screen Assets")]
        public static void CreateAssets() =>
            CreateScreen(DefinitionPath, PrefabPath, GuideArtPath, BackgroundPath, Fill);

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "工房";
            d.Layout = GuideMenuLayout.List;
            d.Items = new[]
            {
                Item(
                    "付け替え",
                    "キャラごとの武器・防具を変える",
                    "付け替える",
                    Icon(IconChangeGearPath),
                    Entry("トーマ", "Lv 12", "鉄の剣｜革の鎧"),
                    Entry("ルカ", "Lv 11", "氷晶のワンド｜魔法のローブ"),
                    Entry("アリア", "Lv 10", "雷鳴の槍｜鎖かたびら"),
                    Entry("ミナ", "Lv 10", "樫の杖｜旅人のマント")
                ),
                Item(
                    "武器",
                    "持っている武器を見る",
                    "装備する",
                    Icon(IconWeaponPath),
                    Entry("炎のダガー", "★★★★", "すばやさの 125%｜斬・炎｜2回攻撃"),
                    Entry("雷鳴の槍", "★★★", "力の 140%｜貫・雷"),
                    Entry("氷晶のワンド", "★★★", "魔力の 135%｜氷｜会心 +5%"),
                    Entry("戦鎚", "★★", "力の 150%｜打"),
                    Entry("鉄の剣", "★★", "力の 120%｜斬"),
                    Entry("短弓", "★", "すばやさの 100%｜貫"),
                    Entry("樫の杖", "★", "魔力の 110%｜炎"),
                    Entry("木の剣", "★", "力の 100%｜斬")
                ),
                Item(
                    "防具",
                    "持っている防具を見る",
                    "装備する",
                    Icon(IconArmorPath),
                    Entry("旅人のマント", "★★★", "すばやさ +8%｜防御の 110%"),
                    Entry("鎖かたびら", "★★", "防御の 130%"),
                    Entry("魔法のローブ", "★★", "魔力の 115%｜防御の 105%"),
                    Entry("鉄の兜", "★★", "防御の 115%｜HP +40"),
                    Entry("革の鎧", "★", "防御の 110%"),
                    Entry("木の丸盾", "★", "防御の 120%")
                ),
            };
        }
    }
}
