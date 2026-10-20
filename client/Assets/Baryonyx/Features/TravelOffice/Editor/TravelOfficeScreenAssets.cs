using System.Linq;
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
            // 説明は仮の値。推奨Lvは、行き先の敵のLv（AdventureCatalog.Areas）と同じにする。
            // 行き先は近い順に並べ、どれも出発できる（server/src/features/adventure/catalog.ts）。
            // 名前は探索・ホームと同じ冒険の定義から取る。最後の未踏の地は、行では地名を「？？？」に伏せる。
            var details = new System.Collections.Generic.Dictionary<string, string>
            {
                [AdventureCatalog.ForestRuins] = "苔の下で、千年前の門番がいまも見張りを続けている",
                ["port-portoria"] = "潮と魚と酒の匂い。船乗りの噂話は半分ほど本当",
                ["glaciera-cave"] = "吐く息まで凍る洞窟。奥で何かが寝返りを打つ",
                ["hush-mire"] = "鳥も虫も鳴かない沼。泡の音だけが返事をする",
                ["almaja-tomb"] = "砂に沈んだ王が、いまも財宝の数を数えている",
                ["moonshade-graveyard"] = "月の夜だけ、墓守の灯りがひとりでに灯る",
                ["volgan-volcano"] = "山が怒るたびに、ふもとの鍛冶屋が忙しくなる",
                ["neve-peaks"] = "白い峰の上を、霧をまとった影が渡っていく",
            };
            d.Destinations = AdventureCatalog
                .Areas.Select(area =>
                    Destination(
                        area.Name,
                        details.TryGetValue(area.Id, out var detail) ? detail : "",
                        area.Level,
                        id: area.Id
                    )
                )
                .Append(
                    Destination(
                        "天空の迷い路",
                        "雲の上に、だれも帰ってこない道があるという",
                        50,
                        locked: true
                    )
                )
                .ToArray();
        }
    }
}
