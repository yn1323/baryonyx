using Baryonyx.CardLoadout;
using Baryonyx.CardLoadout.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.StepBonus;
using Baryonyx.StepBonus.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Tavern.Editor
{
    /// <summary>
    /// The tavern (酒場): the tavern girl guides party formation, training, card skills and the
    /// UPT bonus slots. The characters, levels and cards are mock values until the party has
    /// data; every item shows its own panel instead of a list.
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
                    item.Key switch
                    {
                        PartySession.GuideItemKey => PartyAssets.BuildFormationPanel(safe, view),
                        TrainingSession.GuideItemKey => TrainingAssets.BuildTrainingPanel(
                            safe,
                            view
                        ),
                        CardLoadoutSession.GuideItemKey => CardLoadoutAssets.BuildPanel(safe, view),
                        StepBonusSession.GuideItemKey => StepBonusAssets.BuildSettingsPanel(
                            safe,
                            view
                        ),
                        _ => null,
                    }
            );

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "酒場";
            d.Layout = GuideMenuLayout.List;
            d.Items = new[] { Formation(), Training(), CardSkill(), Bonus() };
        }

        // The formation item opens the party's formation panel (the four slots and the owned
        // characters) instead of a list.
        private static GuideMenuItem Formation()
        {
            var item = Item(
                "編成",
                "冒険に連れて行く4人を選ぶ",
                "編成する",
                Icon(IconFormationPath)
            );
            item.Key = PartySession.GuideItemKey;
            return item;
        }

        // The training item opens one character's detail, with the level-up over it, instead of
        // a list.
        private static GuideMenuItem Training()
        {
            var item = Item(
                "育成",
                "ルーンを使ってレベルを上げる",
                "レベルアップ",
                Icon(IconTrainingPath)
            );
            item.Key = TrainingSession.GuideItemKey;
            return item;
        }

        // The card skills item opens each companion's four cards and the cards they can set
        // instead of a list.
        private static GuideMenuItem CardSkill()
        {
            var item = Item(
                "カードスキル",
                "仲間ごとに4枚のカードを付け替える",
                "付け替える",
                Icon(IconCardSkillPath)
            );
            item.Key = CardLoadoutSession.GuideItemKey;
            return item;
        }

        // The bonus item opens the bonus settings panel instead of a list.
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
