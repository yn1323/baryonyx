using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.TravelOffice.Editor
{
    /// <summary>
    /// The travel office (旅の案内所): the old owl cartographer's world map of destinations.
    /// Place names are provisional mock values, not settled world settings.
    /// </summary>
    public static class TravelOfficeScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/TravelOffice";
        public const string DefinitionPath = Folder + "/Data/TravelOfficeGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/TravelOfficeScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/TravelOfficeGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/TravelOfficeBackground.png";
        public const string MapArtPath = Folder + "/UI/Art/WorldMap.png";

        [MenuItem("Baryonyx/Travel Office/Create Screen Assets")]
        public static void CreateAssets() =>
            CreateScreen(
                DefinitionPath,
                PrefabPath,
                GuideArtPath,
                BackgroundPath,
                Fill,
                MapArtPath
            );

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "旅の案内所";
            d.Layout = GuideMenuLayout.Map;
            d.DepartLabel = "出発";
            // Positions are measured on WorldMap.png (0-1 from the bottom left).
            d.MapPoints = new[]
            {
                Point("城下町", 0.495f, 0.59f, "冒険の拠点。宿と店がそろう"),
                Point("森の遺跡", 0.19f, 0.375f, "探索中｜B3Fまで到達"),
                Point("雪山の洞窟", 0.505f, 0.79f, "おすすめ Lv 15"),
                Point("港町", 0.495f, 0.277f, "船で島々へ渡れる"),
                Point("火山", 0.853f, 0.775f, "未踏の地", locked: true),
                Point("砂漠の王墓", 0.8f, 0.385f, "未踏の地", locked: true),
            };
        }
    }
}
