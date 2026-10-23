using Baryonyx.Home.Editor;
using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Temple.Editor
{
    /// <summary>
    /// The temple (神殿): the thousand-year-old fox spirit guides the summoning. The menu's
    /// "召喚" opens the summoning over the whole screen (<see cref="TempleSummonAssets"/>), with the
    /// results and rates windows over the screen. The presentations it plays
    /// (<see cref="TempleSummonShowAssets"/>, <see cref="TempleSummonTenShowAssets"/>) are made
    /// first and put in the Temple scene, drawn by its camera with Bloom.
    /// </summary>
    public static class TempleScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Temple";
        public const string DefinitionPath = Folder + "/Data/TempleGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/TempleScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/TempleGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/TempleBackground.png";

        public const string SummonItemKey = "summon";

        [MenuItem("Baryonyx/Temple/Create Screen Assets")]
        public static void CreateAssets()
        {
            TempleSummonShowAssets.CreateAssets();
            TempleSummonTenShowAssets.CreateAssets();
            CreateScreen(
                DefinitionPath,
                PrefabPath,
                GuideArtPath,
                BackgroundPath,
                Fill,
                itemPanel: (_, safe, view) => TempleSummonAssets.BuildPanel(safe, view),
                overlay: TempleSummonAssets.BuildOverlay
            );
        }

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "神殿";
            d.Layout = GuideMenuLayout.List;
            // ホームの神殿のボタンと同じ、円環の中で四方へ光る星（召喚の魔法陣）。
            var summon = Item("召喚", "召喚する", Icon(HomeScreenArt.IconSummonPath));
            summon.Key = SummonItemKey;
            d.Items = new[] { summon };
        }
    }
}
