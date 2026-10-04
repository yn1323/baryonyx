using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Temple.Editor
{
    /// <summary>
    /// The temple (神殿): the thousand-year-old fox spirit guides summoning companions and gear.
    /// The summon method, rates and pools are undecided, so the lists are mock data and no
    /// rates are shown.
    /// </summary>
    public static class TempleScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Temple";
        public const string DefinitionPath = Folder + "/Data/TempleGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/TempleScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/TempleGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/TempleBackground.png";

        [MenuItem("Baryonyx/Temple/Create Screen Assets")]
        public static void CreateAssets() =>
            CreateScreen(DefinitionPath, PrefabPath, GuideArtPath, BackgroundPath, Fill);

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "神殿";
            d.Layout = GuideMenuLayout.List;
            d.Items = new[]
            {
                Item(
                    "仲間を召喚",
                    "新しいキャラクターを喚び出す",
                    "召喚する",
                    null,
                    Entry("セラフィナ", "★★★★", "光の弓を操る聖騎士｜貫・雷"),
                    Entry("ガルド", "★★★", "大斧の傭兵｜打・炎"),
                    Entry("ノエル", "★★★", "氷の魔導書を持つ学者｜氷・雷"),
                    Entry("ハク", "★★", "身軽な忍び｜斬・貫"),
                    Entry("ポポ", "★★", "旅の吟遊詩人｜回復・打")
                ),
                Item(
                    "装備を召喚",
                    "武器・防具を喚び出す",
                    "召喚する",
                    null,
                    Entry("星詠みの杖", "★★★★", "属攻の 150%｜雷"),
                    Entry("竜鱗の盾", "★★★", "物防の 145%"),
                    Entry("月影の短剣", "★★★", "速度の 130%｜斬・氷"),
                    Entry("巡礼者の外套", "★★", "速度 +6%｜物防の 110%")
                ),
                Item(
                    "召喚の記録",
                    "これまでに喚び出したもの",
                    "詳しく見る",
                    null,
                    Entry("9/26 仲間を召喚", "★★★", "アリアが仲間になった"),
                    Entry("9/25 装備を召喚", "★★", "鎖かたびらを手に入れた"),
                    Entry("9/24 仲間を召喚", "★★", "ミナが仲間になった")
                ),
            };
        }
    }
}
