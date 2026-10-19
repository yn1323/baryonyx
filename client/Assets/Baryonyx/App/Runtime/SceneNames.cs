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

        // 手に入れた物・遭遇した敵・日々の歩数を見返すライブラリ。Homeの左下のボタンが開く。
        public const string Library = "Library";

        // 冒険の探索。Homeの右下の行き先カード（再開）と、旅の案内所の出発が開く。
        public const string Exploration = "Exploration";

        // 冒険の戦闘。探索で戦闘の部屋に入ると開き、終わると探索かHomeへ戻る。
        public const string Battle = "Battle";

        // 戦闘画面のモック。展示室とテストから開く。
        public const string BattleInspect = "BattleInspect";

        // スキルのデバッグルーム。Homeの右上の設定の下のボタンと、展示室から開く。
        public const string CardSkillLab = "CardSkillLab";

        // 品質のデバッグルーム。Homeの右上のスキルのデバッグルームの下のボタンと、展示室から開く。
        public const string QualityLab = "QualityLab";

        // Homeのボタンが開く画面。案内人の画面のほか、スキルと品質のデバッグルームを開く。
        public static string ScreenFor(HomeAction action) =>
            action switch
            {
                HomeAction.SkillLab => CardSkillLab,
                HomeAction.QualityLab => QualityLab,
                _ => GuideFor(action),
            };

        // Homeの左下のボタンと、冒険していないときの右下のカード（旅の案内所）が開く案内人の画面。
        public static string GuideFor(HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => Pub,
                HomeAction.Workshop => Shop,
                HomeAction.Temple => Temple,
                HomeAction.TravelOffice => TravelOffice,
                HomeAction.Library => Library,
                _ => null,
            };
    }
}
