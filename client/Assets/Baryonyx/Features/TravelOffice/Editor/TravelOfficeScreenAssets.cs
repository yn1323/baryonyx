using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.TravelOffice.Editor
{
    /// <summary>
    /// The travel office (旅の案内所): the old owl cartographer's list of destinations.
    /// Place names are provisional mock values, not settled world settings.
    /// </summary>
    public static class TravelOfficeScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/TravelOffice";
        public const string DefinitionPath = Folder + "/Data/TravelOfficeGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/TravelOfficeScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/TravelOfficeGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/TravelOfficeBackground.png";

        [MenuItem("Baryonyx/Travel Office/Create Screen Assets")]
        public static void CreateAssets() =>
            CreateScreen(DefinitionPath, PrefabPath, GuideArtPath, BackgroundPath, Fill);

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "旅の案内所";
            d.Layout = GuideMenuLayout.Destinations;
            d.DestinationsLabel = "行き先";
            d.DepartLabel = "出発";
            d.Destinations = new[]
            {
                // 冒険に出られるのは、今は森の遺跡だけ（server/src/features/adventure/catalog.ts）。
                // 出発できる行き先を先頭に置く。
                Destination(
                    "森の遺跡",
                    "苔むした古代の遺跡。最奥に守り手が眠る",
                    id: "forest-ruins"
                ),
                Destination("雪山の洞窟", "凍てつく洞窟。腕に覚えのある者向け", "おすすめ Lv 15"),
                Destination("港町", "船で島々へ渡れる"),
                // 未踏の地の名前は、行では「？？？」に伏せる。
                Destination("火山", "まだ道が見つかっていない", "未踏", locked: true),
                Destination("砂漠の王墓", "まだ道が見つかっていない", "未踏", locked: true),
            };
        }
    }
}
