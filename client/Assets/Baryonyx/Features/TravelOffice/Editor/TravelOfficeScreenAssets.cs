using Baryonyx.Adventure;
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
            d.DepartLabel = "出発";
            // 地名・説明・推奨Lvは仮の値。推奨Lvは、パーティの仮データ（Lv8〜12）と育成の上限
            // （Lv30）に合わせた。
            d.Destinations = new[]
            {
                // 冒険に出られるのは、今はミストラ遺跡だけ（server/src/features/adventure/catalog.ts）。
                // 名前は探索・ホームと同じ冒険の定義から取る。出発できる行き先を先頭に置き、
                // ほかは推奨Lvの順に並べる。
                Destination(
                    AdventureCatalog.DestinationName(AdventureCatalog.ForestRuins),
                    "苔の下で、千年前の門番がいまも見張りを続けている",
                    10,
                    id: AdventureCatalog.ForestRuins
                ),
                Destination("港町ポルトリア", "潮と魚と酒の匂い。船乗りの噂話は半分ほど本当", 12),
                Destination("グラシエラ氷窟", "吐く息まで凍る洞窟。奥で何かが寝返りを打つ", 15),
                // 未踏の地は、行では地名を「？？？」に伏せ、説明を出さない。
                Destination(
                    "アルマジャ王墓",
                    "砂に沈んだ王が、いまも財宝の数を数えている",
                    22,
                    locked: true
                ),
                Destination(
                    "ヴォルガン火山",
                    "山が怒るたびに、ふもとの鍛冶屋が忙しくなる",
                    28,
                    locked: true
                ),
            };
        }
    }
}
