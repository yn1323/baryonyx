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
    /// adventurers and the ACT bonus slots. The adventurers open the list of every character;
    /// from there one character's page (the training item) opens, and from the page the
    /// equipment and card skills change screens. Those three items are hidden from the menu.
    /// The characters, levels and cards are mock values until the party has data; every item
    /// shows its own panel instead of a list.
    /// </summary>
    public static class TavernScreenAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Tavern";
        public const string DefinitionPath = Folder + "/Data/TavernGuideMenu.asset";
        public const string PrefabPath = Folder + "/UI/TavernScreen.prefab";
        public const string GuideArtPath = Folder + "/UI/Art/TavernGuide.aseprite";
        public const string BackgroundPath = Folder + "/UI/Art/TavernBackground.png";
        public const string IconFormationPath = Folder + "/UI/Art/IconFormation.aseprite";

        [MenuItem("Baryonyx/Tavern/Create Screen Assets")]
        public static void CreateAssets()
        {
            // 前のPlayやテストがセッションに残した編成・レベル・選んだ人を、Prefabに焼き込まない。
            PartySession.Reset();
            TrainingSession.Reset();
            EquipmentSession.Reset();
            CardLoadoutSession.Reset();
            CreateScreen(
                DefinitionPath,
                PrefabPath,
                GuideArtPath,
                BackgroundPath,
                Fill,
                itemPanel: (item, safe, view) =>
                    item.Key switch
                    {
                        PartySession.GuideItemKey => AdventurerRosterAssets.BuildPanel(safe, view),
                        TrainingSession.GuideItemKey => TrainingAssets.BuildTrainingPanel(
                            safe,
                            view
                        ),
                        EquipmentSession.GuideItemKey => EquipmentAssets.BuildPanel(safe, view),
                        CardLoadoutSession.GuideItemKey => CardLoadoutAssets.BuildPanel(safe, view),
                        StepBonusSession.GuideItemKey => StepBonusAssets.BuildSettingsPanel(
                            safe,
                            view
                        ),
                        _ => null,
                    }
            );
        }

        private static void Fill(GuideMenuDefinition d)
        {
            d.Title = "編成";
            d.Layout = GuideMenuLayout.List;
            d.Items = new[]
            {
                Adventurers(),
                Bonus(),
                Hidden("育成", TrainingSession.GuideItemKey),
                Hidden("装備", EquipmentSession.GuideItemKey),
                Hidden("スキル", CardLoadoutSession.GuideItemKey),
            };
        }

        // The adventurers open the list of every character (the party first) instead of a list:
        // choose one to see them, change the party, or open their page.
        private static GuideMenuItem Adventurers()
        {
            var item = Item(
                "冒険者",
                "パーティを組み、装備・スキル・育成をする",
                "編成する",
                Icon(IconFormationPath)
            );
            item.Key = PartySession.GuideItemKey;
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

        // A panel opened from another panel (the adventurer's page and the change screens),
        // without a menu row.
        private static GuideMenuItem Hidden(string label, string key)
        {
            var item = Item(label, "", "", null);
            item.Key = key;
            item.Hidden = true;
            return item;
        }
    }
}
