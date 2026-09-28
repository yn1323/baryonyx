using Baryonyx.Home;

namespace Baryonyx.App
{
    // Build Settingsに入れたシーンの名前。画面を切り替える処理、シーンの生成、テストが同じ名前を使う。
    public static class SceneNames
    {
        public const string Top = "Top";
        public const string Home = "Home";
        public const string Pub = "Pub";
        public const string Shop = "Shop";
        public const string Temple = "Temple";
        public const string TravelOffice = "TravelOffice";

        // Homeの左下のボタンが開く案内人の画面。ほかの操作はシーンを開かない。
        public static string GuideFor(HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => Pub,
                HomeAction.Workshop => Shop,
                HomeAction.Temple => Temple,
                HomeAction.TravelOffice => TravelOffice,
                _ => null,
            };
    }
}
