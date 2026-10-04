using Baryonyx.Home;
using Baryonyx.StepBonus;

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

        // Homeの右下の行き先カード（再開）が開く戦闘画面のモック。まだHomeへ戻る操作はない。
        public const string BattleInspect = "BattleInspect";

        // Homeの左下のボタンが開く案内人の画面。ほかの操作はシーンを開かない。
        public static string GuideFor(HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => Pub,
                HomeAction.Workshop => Shop,
                HomeAction.Temple => Temple,
                HomeAction.TravelOffice => TravelOffice,
                HomeAction.Bonus => Pub,
                _ => null,
            };

        // 案内人の画面で、メニューを経ずに直接開く項目（GuideMenuItem.Key）。ほかの操作はメニューから始める。
        public static string GuideItemFor(HomeAction action) =>
            action == HomeAction.Bonus ? StepBonusSession.GuideItemKey : null;
    }
}
