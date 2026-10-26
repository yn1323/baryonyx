using Baryonyx.CardLoadout;
using Baryonyx.CardLoadout.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Equipment;
using Baryonyx.Equipment.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using UnityEditor;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Tavern.Editor
{
    /// <summary>
    /// The formation screen (編成; the code keeps the Tavern name): the tavern girl guides the
    /// adventurers and their growth (育成). The growth item opens a card for every character,
    /// and tapping one opens their tabs. The adventurers open the list of every character; from
    /// there one character's tabs open too: the stats (the training item), the equipment, the
    /// card skills and the breakthrough, each an item hidden from the menu.
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

        // The menu's icons are plain shapes without outlines, like the home buttons: a roster for
        // the adventurers and a person beside a rising arrow for their growth (育成).
        // The other guide screens list coloured pixel art with the same white tint, so the text
        // colour is baked in.
        public const string IconFormationPath = Folder + "/UI/Art/IconFormation.png";
        public const string IconGrowthPath = Folder + "/UI/Art/IconGrowth.png";

        // 胸当てを回る2本の矢印。探索の「装備」ボタンがまだこの絵を使うため、書き出しを残す。
        public const string IconChangeGearPath = Folder + "/UI/Art/IconChangeGear.png";

        [MenuItem("Baryonyx/Tavern/Create Screen Assets")]
        public static void CreateAssets()
        {
            // 前のPlayやテストがセッションに残した編成・レベル・選んだ人を、Prefabに焼き込まない。
            PartySession.Reset();
            TrainingSession.Reset();
            TrainingBond.Reset();
            EquipmentSession.Reset();
            CardLoadoutSession.Reset();
            WriteMenuIcons();
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
                        AdventurerPickerView.GuideItemKey => AdventurerPickerAssets.BuildPanel(
                            safe,
                            view
                        ),
                        TrainingSession.GuideItemKey => TrainingAssets.BuildTrainingPanel(
                            safe,
                            view
                        ),
                        TrainingSession.BreakthroughItemKey =>
                            TrainingAssets.BuildBreakthroughPanel(safe, view),
                        EquipmentSession.GuideItemKey => EquipmentAssets.BuildPanel(safe, view),
                        CardLoadoutSession.GuideItemKey => CardLoadoutAssets.BuildPanel(safe, view),
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
                Growth(),
                Hidden("育成", TrainingSession.GuideItemKey),
                Hidden("装備", EquipmentSession.GuideItemKey),
                Hidden("スキル", CardLoadoutSession.GuideItemKey),
                Hidden("突破", TrainingSession.BreakthroughItemKey),
            };
        }

        // The adventurers open the list of every character (the party first) instead of a list:
        // choose one to see them, change the party, or open their page.
        private static GuideMenuItem Adventurers()
        {
            var item = Item("冒険者", "編成する", Icon(IconFormationPath));
            item.Key = PartySession.GuideItemKey;
            return item;
        }

        // 育成は、全員のカードを並べた人選びを開く。1人を押すと、Lv上げ・スキル・装備をまとめた
        // その人の冒険者のタブへ進む（2026-10-26、「装備」から改めた。開く先の中身に名前を合わせる）。
        private static GuideMenuItem Growth()
        {
            var item = Item(GrowthLabel, "選ぶ", Icon(IconGrowthPath));
            item.Key = AdventurerPickerView.GuideItemKey;
            return item;
        }

        /// <summary>The growth item's name, also the breadcrumb of the picker and the pages it opens.</summary>
        public const string GrowthLabel = "育成";

        private static void WriteMenuIcons()
        {
            Roster().Save(IconFormationPath, SmoothIconTextureSize, UiPalette.TextMain);
            GrowthIcon().Save(IconGrowthPath, SmoothIconTextureSize, UiPalette.TextMain);
            ChangeGear().Save(IconChangeGearPath, SmoothIconTextureSize, UiPalette.TextMain);
        }

        // 人の上半身と、その右に伸びる上向きの矢印。矢印の周りを少し空けて人と分ける。
        private static VectorIcon GrowthIcon() =>
            new VectorIcon()
                .Fill(IconShape.Circle(22f, 21f, 9.5f))
                .Fill(IconShape.Path("M3 58 Q3 36 22 36 Q41 36 41 58Z"))
                .Fill(IconShape.Path("M35 28 L48.5 8 L62 28 L53 28 L53 58 L44 58 L44 28Z"), 3f);

        // A sheet with a folded corner, a person at the top left and the lines of the list.
        private static VectorIcon Roster() =>
            new VectorIcon()
                .Fill(IconShape.Path("M12 6 L46 6 L52 12 L52 58 L12 58Z"))
                .Cut(IconShape.Circle(25f, 20f, 5f))
                .Cut(IconShape.Path("M17 34 Q17 27 25 27 Q33 27 33 34Z"))
                .CutStroke(
                    IconShape.Path("M38 18 L45 18 M38 26 L45 26 M18 42 L45 42 M18 50 L40 50"),
                    3f
                );

        // Two arrows turning around a small breastplate.
        private static VectorIcon ChangeGear() =>
            new VectorIcon()
                .Stroke(IconShape.Path("M10 28 Q12 8 32 7 Q45 7 52 16"), 4.5f)
                .Fill(IconShape.Path("M58 8 L58 24 L43 20Z"))
                .Stroke(IconShape.Path("M54 36 Q52 56 32 57 Q19 57 12 48"), 4.5f)
                .Fill(IconShape.Path("M6 56 L6 40 L21 44Z"))
                .Fill(
                    IconShape.Path(
                        "M22 21 L27 21 Q32 25 37 21 L42 21 L46 27 L43 31 L42 44 Q32 50 22 44 L21 31 L18 27Z"
                    )
                );

        // A panel opened from another panel (the adventurer's page and the change screens),
        // without a menu row.
        private static GuideMenuItem Hidden(string label, string key)
        {
            var item = Item(label, "", null);
            item.Key = key;
            item.Hidden = true;
            return item;
        }
    }
}
