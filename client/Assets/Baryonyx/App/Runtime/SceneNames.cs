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

        // 以下のデバッグルームは、Homeの右上のデバッグルームの一覧（DebugRooms）と、展示室から開く。

        // 戦闘画面のモック（BattleLab）。テストからも開く。
        public const string BattleLab = "BattleLab";

        // スキルのデバッグルーム。
        public const string CardSkillLab = "CardSkillLab";

        // 品質のデバッグルーム。
        public const string QualityLab = "QualityLab";

        // 敵の挙動デバッグルーム。
        public const string EnemyLab = "EnemyLab";

        // 戦闘の演出デバッグルーム。
        public const string BattleMomentLab = "BattleMomentLab";

        // 探索の演出デバッグルーム。
        public const string ExplorationMomentLab = "ExplorationMomentLab";

        // 召喚の演出デバッグルーム。
        public const string TempleSummonLab = "TempleSummonLab";

        /// <summary>
        /// The debugging rooms Home's list opens, in its order: the name shown, the scene,
        /// whether the room has its own button back to Home, and for the others where the one
        /// put over them goes (a corner their controls leave free).
        /// </summary>
        public static readonly (
            string Name,
            string Scene,
            bool OwnHome,
            DebugRoomCorner Corner
        )[] DebugRooms =
        {
            ("スキルのデバッグルーム", CardSkillLab, true, DebugRoomCorner.TopRight),
            ("品質のデバッグルーム", QualityLab, true, DebugRoomCorner.TopRight),
            ("戦闘画面のモック", BattleLab, false, DebugRoomCorner.TopRight),
            ("敵の挙動デバッグルーム", EnemyLab, false, DebugRoomCorner.TopRight),
            ("戦闘の演出デバッグルーム", BattleMomentLab, false, DebugRoomCorner.TopRight),
            ("探索の演出デバッグルーム", ExplorationMomentLab, false, DebugRoomCorner.BottomLeft),
            ("召喚の演出デバッグルーム", TempleSummonLab, false, DebugRoomCorner.TopRight),
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
