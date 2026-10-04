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

        // 冒険の探索。Homeの右下の行き先カード（再開）と、旅の案内所の出発が開く。
        public const string Exploration = "Exploration";

        // 冒険の戦闘。探索で戦闘の部屋に入ると開き、終わると探索かHomeへ戻る。
        public const string Battle = "Battle";

        // 戦闘画面のモック。展示室とテストから開く。
        public const string BattleInspect = "BattleInspect";

        // Homeの左下のボタンと、冒険していないときの右下のカード（旅の案内所）が開く案内人の画面。
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
