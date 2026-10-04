using Baryonyx.CardLoadout;
using Baryonyx.CardLoadout.Editor;
using Baryonyx.Equipment;
using Baryonyx.Equipment.Editor;
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
    /// The formation screen (編成; the code keeps the Tavern name): the tavern girl guides the
    /// party, equipment, training, card skills and the ACT bonus slots. The characters, levels
    /// and cards are mock values until the party has data; every item shows its own panel
    /// instead of a list.
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
                        EquipmentSession.GuideItemKey => EquipmentAssets.BuildPanel(safe, view),
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
            d.Title = "編成";
            d.Layout = GuideMenuLayout.List;
            d.Items = new[] { Formation(), Equipment(), Training(), CardSkill(), Bonus() };
        }

        // The party item opens the party's formation panel (the four slots and the owned
        // characters) instead of a list. The screen itself is called 編成, so the item is パーティ.
        private static GuideMenuItem Formation()
        {
            var item = Item(
                "パーティ",
                "冒険に連れて行く4人を選ぶ",
                "編成する",
                Icon(IconFormationPath)
            );
            item.Key = PartySession.GuideItemKey;
            return item;
        }

        // The equipment item opens each companion's weapon and armour and the owned items
        // instead of a list.
        private static GuideMenuItem Equipment()
        {
            var item = Item(
                "装備",
                "仲間ごとに武器と防具を付け替える",
                "付け替える",
                Icon(EquipmentAssets.IconChangeGearPath)
            );
            item.Key = EquipmentSession.GuideItemKey;
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
                "スキル",
                "仲間ごとに4枚のスキルを付け替える",
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
