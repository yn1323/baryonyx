using System.Globalization;
using Baryonyx.Editor.Art;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Temple.Editor
{
    /// <summary>
    /// The temple (神殿): the thousand-year-old fox spirit guides summoning companions and items.
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

        // The menu's icons are plain shapes without outlines, like the other guides' menus: what
        // is summoned (a person, a treasure chest) beside the four-pointed star of the home
        // temple button, which marks summoning. The text colour is baked in, like the trading
        // house's icons.
        public const string IconSummonFriendPath = Folder + "/UI/Art/IconSummonFriend.png";
        public const string IconSummonItemPath = Folder + "/UI/Art/IconSummonItem.png";

        // The clear margin that separates a front part from the part behind it (design units).
        private const float PartGap = 2.6f;

        [MenuItem("Baryonyx/Temple/Create Screen Assets")]
        public static void CreateAssets()
        {
            WriteMenuIcons();
            CreateScreen(DefinitionPath, PrefabPath, GuideArtPath, BackgroundPath, Fill);
        }

        private static void WriteMenuIcons()
        {
            Friend().Save(IconSummonFriendPath, SmoothIconTextureSize, UiPalette.TextMain);
            Chest().Save(IconSummonItemPath, SmoothIconTextureSize, UiPalette.TextMain);
        }

        // A person's head and shoulders, with the star at the top right.
        private static VectorIcon Friend() =>
            new VectorIcon()
                .Fill(IconShape.Circle(25f, 22f, 9.5f))
                .Fill(IconShape.Path("M6 61 Q6 38 25 38 Q44 38 44 61Z"))
                .Fill(Star(49f, 15f, 13f), PartGap);

        // A treasure chest with a rounded lid, two bands and a lock, with the star at the top right.
        private static VectorIcon Chest() =>
            new VectorIcon()
                .Fill(IconShape.Path("M5 33 Q5 18 28 18 Q51 18 51 33Z"))
                .Fill(IconShape.Rect(5f, 35f, 46f, 24f))
                .CutStroke(IconShape.Path("M15 19.5 L15 59 M41 19.5 L41 59"), 2.6f)
                .Cut(IconShape.Rect(23.5f, 36f, 9f, 11f))
                .Fill(Star(52f, 13f, 12f), PartGap);

        // The home temple button's four-pointed star: arms of length radius, pinched toward the
        // centre by curves.
        private static IconShape Star(float cx, float cy, float radius)
        {
            float pinch = radius * 0.12f;
            string Point(float x, float y) =>
                x.ToString(CultureInfo.InvariantCulture)
                + " "
                + y.ToString(CultureInfo.InvariantCulture);
            return IconShape.Path(
                $"M{Point(cx, cy - radius)} "
                    + $"Q{Point(cx + pinch, cy - pinch)} {Point(cx + radius, cy)} "
                    + $"Q{Point(cx + pinch, cy + pinch)} {Point(cx, cy + radius)} "
                    + $"Q{Point(cx - pinch, cy + pinch)} {Point(cx - radius, cy)} "
                    + $"Q{Point(cx - pinch, cy - pinch)} {Point(cx, cy - radius)}Z"
            );
        }

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
                    Icon(IconSummonFriendPath),
                    Entry("セラフィナ", "★★★★", "光の弓を操る聖騎士｜貫・雷"),
                    Entry("ガルド", "★★★", "大斧の傭兵｜打・炎"),
                    Entry("ノエル", "★★★", "氷の魔導書を持つ学者｜氷・雷"),
                    Entry("ハク", "★★", "身軽な忍び｜斬・貫"),
                    Entry("ポポ", "★★", "旅の吟遊詩人｜回復・打")
                ),
                Item(
                    "アイテムを召喚",
                    "道具や素材を喚び出す",
                    "召喚する",
                    Icon(IconSummonItemPath),
                    Entry("星屑の小瓶", "★★★★", "道具｜味方全体のHPを回復"),
                    Entry("雷の羽根", "★★★", "素材｜雷の装備とスキルの合成に使う"),
                    Entry("火の魔石", "★★", "素材｜炎の装備とスキルの合成に使う"),
                    Entry("回復の薬", "★★", "道具｜味方単体のHPを回復"),
                    Entry("狼の牙", "★", "素材｜武器と防具の合成に使う")
                ),
            };
        }
    }
}
