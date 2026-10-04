using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.StepBonus.Editor
{
    /// <summary>
    /// The UPT bonus mock data, its icons, and the tavern's bonus settings panel. The tavern's
    /// generator calls <see cref="BuildSettingsPanel"/> for its bonus item, so the slots and the
    /// owned bonuses are baked into the tavern prefab and read in the editor without Play Mode.
    /// The bonuses and values are mock data until the bonus is saved (doc/features/step-bonus.md).
    /// </summary>
    public static class StepBonusAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/StepBonus";
        public const string DataPath = Folder + "/Data/StepBonusMockData.asset";
        public const string ArtFolder = Folder + "/UI/Art";

        // 24×24のドット絵。ホームのボタンと酒場のメニューに置くボーナスのアイコン。
        public const string IconBonusPath = ArtFolder + "/IconBonus.aseprite";

        // Coordinates inside the right panel (1040×908 in the 1920×1080 design).
        private const float SlotSize = 112f;
        private const float CellWidth = 176f;
        private const float CellHeight = 160f;
        private const float RowHeight = 104f;

        public static string IconPath(string name) => $"{ArtFolder}/Icon{name}.aseprite";

        [MenuItem("Baryonyx/Step Bonus/Create Mock Data")]
        public static StepBonusMockData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            var data = AssetDatabase.LoadAssetAtPath<StepBonusMockData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<StepBonusMockData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.TodayUpt = 3240;
            data.Tiers = new[] { 1000, 2000, 3000, 5000, 8000 };
            data.Multipliers = new[] { 1f, 1.2f, 1.4f, 1.7f, 2f };
            data.TotalKinds = 20;
            data.Bonuses = new[]
            {
                Bonus(
                    "guard",
                    "守り",
                    StepBonusCategory.Battle,
                    "受けるダメージ -{0}%",
                    3,
                    8,
                    1,
                    0,
                    "Guard"
                ),
                Bonus(
                    "luck",
                    "幸運",
                    StepBonusCategory.Drop,
                    "ドロップ率 +{0}%",
                    5,
                    15,
                    1,
                    0,
                    "Luck"
                ),
                Bonus(
                    "treasure-sight",
                    "宝箱透視",
                    StepBonusCategory.Explore,
                    "宝箱の中身が見える確率 {0}%",
                    20,
                    60,
                    0,
                    100,
                    "TreasureSight"
                ),
                Bonus(
                    "foresight",
                    "先読み",
                    StepBonusCategory.Explore,
                    "次の部屋の敵が見える確率 {0}%",
                    20,
                    50,
                    0,
                    100,
                    "Foresight"
                ),
                Bonus(
                    "omen",
                    "兆し",
                    StepBonusCategory.Explore,
                    "激化するターンが見える確率 {0}%",
                    30,
                    80,
                    0,
                    100,
                    "Omen"
                ),
                Bonus(
                    "appraisal",
                    "目利き",
                    StepBonusCategory.Drop,
                    "レア度アップ +{0}%",
                    2,
                    5,
                    1,
                    0,
                    "Appraisal"
                ),
                Bonus(
                    "fighting",
                    "闘志",
                    StepBonusCategory.Battle,
                    "攻撃力 +{0}%",
                    3,
                    8,
                    1,
                    0,
                    "Fighting"
                ),
                Bonus(
                    "healing",
                    "癒し",
                    StepBonusCategory.Battle,
                    "戦闘後にHPを {0}% 回復",
                    3,
                    10,
                    1,
                    0,
                    "Healing"
                ),
            };
            data.Owned = new[]
            {
                Roll("guard", 7.6f, updated: true),
                Roll("luck", 11.6f),
                Roll("treasure-sight", 46f),
                Roll("foresight", 36f),
                Roll("omen", 55f),
                Roll("appraisal", 3.4f),
                Roll("fighting", 4.1f),
                Roll("healing", 5f),
            };
            data.Loadout = new[] { "foresight", "luck", "treasure-sight", "fighting", "appraisal" };
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        /// <summary>
        /// Builds the bonus settings in the right panel of a guide screen. Called while the
        /// guide screen's prefab is being built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildSettingsPanel(RectTransform safe, GuideMenuView guide)
        {
            var data = CreateData();
            var loadout = StepBonusLoadout.From(data);
            var panel = Guide.RightPanel(safe, "BonusSettings");
            Guide.Frame(panel, Guide.FramePath, Color.white).raycastTarget = true;
            var view = panel.gameObject.AddComponent<StepBonusSettingsView>();
            view.Data = data;
            view.Guide = guide;
            view.TierColors = StepBonusArt.Tiers.ToArray();

            var title = Label(
                panel,
                "Title",
                "ボーナス",
                48,
                StepBonusArt.Gold,
                TextAlignmentOptions.TopLeft
            );
            Guide.Fill((RectTransform)title.transform, new Vector2(48, 0), new Vector2(-48, -32));
            view.Header = Label(
                panel,
                "Header",
                "",
                26,
                StepBonusArt.TextSub,
                TextAlignmentOptions.TopRight
            );
            Guide.Fill(
                (RectTransform)view.Header.transform,
                new Vector2(48, 0),
                new Vector2(-48, -48)
            );

            view.Slots = Enumerable
                .Range(0, loadout.SlotCount)
                .Select(i => BuildSlot(panel, i, loadout))
                .ToArray();
            view.Tabs = BuildTabs(panel);
            view.Owned = Label(
                panel,
                "Owned",
                "",
                26,
                StepBonusArt.TextSub,
                TextAlignmentOptions.Right
            );
            Corner(
                (RectTransform)view.Owned.transform,
                Vector2.one,
                new Vector2(-48, -280),
                new Vector2(300, 64)
            );
            BuildList(panel, view, loadout);
            BuildFooter(panel, view);
            return panel.gameObject;
        }

        // --- Slots --------------------------------------------------------------------

        private static StepBonusSlotWidget BuildSlot(
            RectTransform panel,
            int index,
            StepBonusLoadout loadout
        )
        {
            var cell = Rect("Slot" + index, panel);
            float left =
                (1040f - CellWidth * loadout.SlotCount - 16f * (loadout.SlotCount - 1)) / 2f;
            Corner(
                cell,
                new Vector2(0, 1),
                new Vector2(left + index * (CellWidth + 16f), -100),
                new Vector2(CellWidth, CellHeight)
            );
            var widget = new StepBonusSlotWidget();
            widget.Button = Guide.AddButton(cell, AddImage(cell, Color.clear, true));

            // The gold ring for the chosen slot and the teal one for the slot it swaps with.
            widget.Selected = Ring(cell, "Selected", Guide.FrameSelectedPath, Color.white);
            widget.Partner = Ring(cell, "Partner", Guide.FramePath, StepBonusArt.Teal);

            var frame = Rect("Frame", cell);
            Corner(
                frame,
                new Vector2(0.5f, 1),
                new Vector2(0, -8),
                new Vector2(SlotSize, SlotSize)
            );
            widget.Frame = Guide.Frame(frame, Guide.FramePath, StepBonusArt.Tiers[index]);
            widget.Frame.raycastTarget = false;
            var icon = Rect("Icon", frame);
            Place(icon, Vector2.zero, Vector2.one * Guide.IconSize);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            var bonus = loadout.Definition(loadout.Bonus(index));
            widget.Icon.sprite = bonus?.Icon;
            widget.Icon.enabled = bonus?.Icon != null;

            widget.Label = Label(
                cell,
                "Label",
                $"{StepBonusLoadout.Upt(loadout.Tier(index))} {StepBonusLoadout.Times(loadout.Multiplier(index))}",
                26,
                StepBonusArt.TextSub,
                TextAlignmentOptions.Center
            );
            Corner(
                (RectTransform)widget.Label.transform,
                new Vector2(0.5f, 0),
                new Vector2(0, 2),
                new Vector2(CellWidth, 32)
            );
            return widget;
        }

        private static GameObject Ring(RectTransform cell, string name, string path, Color color)
        {
            var ring = Rect(name, cell);
            Corner(ring, new Vector2(0.5f, 1), new Vector2(0, 4), Vector2.one * (SlotSize + 24));
            Guide.Frame(ring, path, color).raycastTarget = false;
            ring.gameObject.SetActive(false);
            return ring.gameObject;
        }

        // --- Tabs and list ------------------------------------------------------------

        private static Button[] BuildTabs(RectTransform panel)
        {
            var tabs = new List<Button>();
            for (int i = 0; i < StepBonusSettingsPresenter.TabLabels.Length; i++)
            {
                var tab = Rect("Tab" + i, panel);
                Corner(
                    tab,
                    new Vector2(0, 1),
                    new Vector2(48 + i * 180, -276),
                    new Vector2(168, 72)
                );
                tabs.Add(Guide.AddButton(tab, Guide.Frame(tab, Guide.FramePath, Color.white)));
                var selected = Rect("Selected", tab);
                Stretch(selected);
                Guide.Frame(selected, Guide.FrameSelectedPath, Color.white).raycastTarget = false;
                selected.gameObject.SetActive(i == 0);
                var label = Label(
                    tab,
                    "Label",
                    StepBonusSettingsPresenter.TabLabels[i],
                    30,
                    StepBonusArt.TextMain,
                    TextAlignmentOptions.Center
                );
                Stretch((RectTransform)label.transform);
            }
            return tabs.ToArray();
        }

        private static void BuildList(
            RectTransform panel,
            StepBonusSettingsView view,
            StepBonusLoadout loadout
        )
        {
            var viewport = Rect("Viewport", panel);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(28, 132);
            viewport.offsetMax = new Vector2(-28, -360);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);

            var content = Rect("Rows", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var group = content.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 10;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            view.Rows = StepBonusSettingsPresenter
                .Order(loadout)
                .Select(roll => BuildRow(content, roll, loadout))
                .ToArray();

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            view.List = scroll;
        }

        private static StepBonusRowWidget BuildRow(
            RectTransform content,
            StepBonusRoll roll,
            StepBonusLoadout loadout
        )
        {
            var bonus = loadout.Definition(roll.Id);
            var row = Rect("Row_" + roll.Id, content);
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = RowHeight;
            var widget = new StepBonusRowWidget
            {
                Id = roll.Id,
                Button = Guide.AddButton(
                    row,
                    Guide.Frame(row, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
                ),
            };
            var selected = Rect("Selected", row);
            Stretch(selected);
            Guide.Frame(selected, Guide.FrameSelectedPath, Color.white).raycastTarget = false;
            selected.gameObject.SetActive(false);
            widget.Selected = selected.gameObject;

            var icon = Rect("Icon", row);
            Corner(icon, new Vector2(0, 0.5f), new Vector2(8, 0), Vector2.one * Guide.IconSize);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = bonus.Icon;
            iconImage.raycastTarget = false;

            // The rank shows where the rolled value fell in the bonus's range.
            var rank = StepBonusLoadout.RankOf(bonus, roll.Value);
            var badge = Rect("Rank", row);
            Corner(badge, new Vector2(0, 0.5f), new Vector2(116, 0), new Vector2(52, 52));
            Guide.Frame(badge, Guide.FramePath, StepBonusArt.Rank(rank)).raycastTarget = false;
            var letter = Label(
                badge,
                "Letter",
                rank.ToString(),
                34,
                StepBonusArt.Rank(rank),
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)letter.transform);

            var name = Label(
                row,
                "Name",
                bonus.Name,
                38,
                StepBonusArt.TextMain,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)name.transform, top: true, 10, 48, 188, 230);
            var effect = Label(
                row,
                "Effect",
                StepBonusLoadout.Effect(bonus, roll.Value),
                28,
                StepBonusArt.TextSub,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)effect.transform, top: false, 10, 38, 190, 230);
            Guide.Shrink(effect, 20);

            widget.Note = Label(
                row,
                "Note",
                "",
                24,
                StepBonusArt.TextFaint,
                TextAlignmentOptions.Right
            );
            Guide.Band((RectTransform)widget.Note.transform, top: true, 12, 32, 420, 230);

            // The bar shows the same position as the rank, with the range under it.
            float position = StepBonusLoadout.Position(bonus, roll.Value);
            var bar = Rect("Range", row);
            Corner(bar, new Vector2(1, 0.5f), new Vector2(-36, 14), new Vector2(168, 12));
            AddImage(bar, new Color(0.17f, 0.18f, 0.32f), false);
            var fill = Rect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(position, 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            AddImage(fill, StepBonusArt.Rank(rank) * new Color(0.75f, 0.75f, 0.75f, 1f), false);
            var marker = Rect("Marker", bar);
            marker.anchorMin = marker.anchorMax = new Vector2(position, 0.5f);
            marker.sizeDelta = new Vector2(4, 24);
            AddImage(marker, StepBonusArt.TextMain, false);
            var range = Label(
                row,
                "RangeText",
                StepBonusLoadout.Range(bonus),
                22,
                StepBonusArt.TextFaint,
                TextAlignmentOptions.Right
            );
            Corner(
                (RectTransform)range.transform,
                new Vector2(1, 0.5f),
                new Vector2(-36, -20),
                new Vector2(200, 30)
            );
            return widget;
        }

        // --- Footer -------------------------------------------------------------------

        private static void BuildFooter(RectTransform panel, StepBonusSettingsView view)
        {
            view.FooterTitle = Label(
                panel,
                "FooterTitle",
                "",
                32,
                StepBonusArt.TextMain,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)view.FooterTitle.transform, top: false, 64, 44, 48, 392);
            Guide.Shrink(view.FooterTitle, 20);
            view.FooterDetail = Label(
                panel,
                "FooterDetail",
                "",
                26,
                StepBonusArt.TextSub,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)view.FooterDetail.transform, top: false, 24, 36, 50, 392);
            Guide.Shrink(view.FooterDetail, 18);

            var confirm = Rect("Confirm", panel);
            Corner(confirm, new Vector2(1, 0), new Vector2(-32, 24), new Vector2(320, 96));
            view.Confirm = Guide.AddButton(
                confirm,
                Guide.Frame(confirm, Guide.FrameSelectedPath, Color.white)
            );
            view.ConfirmLabel = Label(
                confirm,
                "Label",
                "セットする",
                42,
                StepBonusArt.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)view.ConfirmLabel.transform);
            view.Confirm.interactable = false;
        }

        // --- Mock content -------------------------------------------------------------

        private static StepBonusDefinition Bonus(
            string id,
            string name,
            StepBonusCategory category,
            string effect,
            float min,
            float max,
            int decimals,
            float cap,
            string icon
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Category = category,
                Effect = effect,
                Min = min,
                Max = max,
                Decimals = decimals,
                Cap = cap,
                Icon = Guide.Icon(IconPath(icon)),
            };

        private static StepBonusRoll Roll(string id, float value, bool updated = false) =>
            new()
            {
                Id = id,
                Value = value,
                Updated = updated,
            };
    }
}
