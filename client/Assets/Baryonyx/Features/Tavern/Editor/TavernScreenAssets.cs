using Baryonyx.StepBonus;
using Baryonyx.StepBonus.Editor;
using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Tavern.Editor
{
    /// <summary>
    /// The tavern (酒場): the tavern girl guides party formation, training, card skills and the
    /// UPT bonus slots. The characters, levels and cards are mock values until the party has
    /// data; the bonus item shows the bonus settings panel instead of a list.
    /// </summary>
    public static class TavernScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Tavern";
        public const string DefinitionPath = Folder + "/Data/TavernGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/TavernScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/TavernGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/TavernBackground.png";
        public const string IconFormationPath = Folder + "/UI/Art/IconFormation.aseprite";
        public const string IconTrainingPath = Folder + "/UI/Art/IconTraining.aseprite";
        public const string IconCardSkillPath = Folder + "/UI/Art/IconCardSkill.aseprite";

        [MenuItem("Baryonyx/Tavern/Create Screen Assets")]
        public static void CreateAssets() =>
            CreateScreen(
                DefinitionPath,
                PrefabPath,
                GuideArtPath,
                BackgroundPath,
                Fill,
                itemPanel: (item, safe, view) =>
                    item.Key == StepBonusSession.GuideItemKey
                        ? StepBonusAssets.BuildSettingsPanel(safe, view)
                        : null
            );

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "酒場";
            d.Layout = GuideMenuLayout.List;
            d.Items = new[]
            {
                Item(
                    "編成",
                    "冒険に連れて行く4人を選ぶ",
                    "編成する",
                    Icon(IconFormationPath),
                    Entry("トーマ", "Lv 12", "パーティ｜斬・炎のカードを使える"),
                    Entry("ルカ", "Lv 11", "パーティ｜氷・貫のカードを使える"),
                    Entry("アリア", "Lv 10", "パーティ｜雷・斬のカードを使える"),
                    Entry("ミナ", "Lv 10", "パーティ｜回復と打のカードを使える"),
                    Entry("アンセルム", "Lv 8", "控え｜打・炎のカードを使える"),
                    Entry("グレタ", "Lv 7", "控え｜貫・氷のカードを使える"),
                    Entry("ルッツ", "Lv 5", "控え｜雷・打のカードを使える"),
                    Entry("リタ", "Lv 3", "控え｜斬・貫のカードを使える"),
                    Entry("リツ", "Lv 1", "控え｜氷・雷のカードを使える")
                ),
                Item(
                    "育成",
                    "ルーンを使ってレベルを上げる",
                    "レベルアップ",
                    Icon(IconTrainingPath),
                    Entry(
                        "トーマ",
                        "Lv 12 → 13",
                        "必要ルーン 1,200｜固有スキルの解放まであと3レベル"
                    ),
                    Entry("ルカ", "Lv 11 → 12", "必要ルーン 1,100"),
                    Entry("アリア", "Lv 10 → 11", "必要ルーン 1,000｜Lv 11でパッシブスキルを解放"),
                    Entry("ミナ", "Lv 10 → 11", "必要ルーン 1,000"),
                    Entry("アンセルム", "Lv 8 → 9", "必要ルーン 800"),
                    Entry("グレタ", "Lv 7 → 8", "必要ルーン 700"),
                    Entry("ルッツ", "Lv 5 → 6", "必要ルーン 500"),
                    Entry("リタ", "Lv 3 → 4", "必要ルーン 300"),
                    Entry("リツ", "Lv 1 → 2", "必要ルーン 100")
                ),
                Item(
                    "カードスキル",
                    "キャラごとに3枚のカードを付け替える",
                    "付け替える",
                    Icon(IconCardSkillPath),
                    Entry("斬撃", "コスト 1", "斬｜敵1体に 243 ダメージ"),
                    Entry("ファイア", "コスト 2", "炎｜敵1体に 312 ダメージ"),
                    Entry("アイスランス", "コスト 2", "氷｜敵1体に 280 ダメージ、すばやさを下げる"),
                    Entry("サンダー", "コスト 3", "雷｜敵全体に 190 ダメージ"),
                    Entry("重撃", "コスト 2", "打｜敵1体に 360 ダメージ"),
                    Entry("連突き", "コスト 2", "貫｜敵1体に 120 ダメージを3回"),
                    Entry("ヒール", "コスト 1", "回復｜味方1人のHPを 220 回復"),
                    Entry("ガード", "コスト 1", "防御｜このターンに受けるダメージを減らす")
                ),
                Bonus(),
            };
        }

        // The bonus item opens the bonus settings panel; Home's bonus button opens it directly.
        private static GuideMenuItem Bonus()
        {
            var item = Item(
                "ボーナス",
                "枠にセットして、歩いた日に効かせる",
                "セットする",
                Icon(StepBonusAssets.IconBonusPath)
            );
            item.Key = StepBonusSession.GuideItemKey;
            return item;
        }
    }
}
