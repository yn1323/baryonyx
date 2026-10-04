using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baryonyx.Editor.Art;
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
    /// The UPT bonus mock data, its icons, and the tavern's bonus settings. The tavern's
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

        // 一覧で、枠に入れているボーナスに付ける印（コードで描く11×11のドット絵）。
        public const string SetMarkPath = ArtFolder + "/SetMark.png";

        private const float DotScale = 4f;

        // The slot frame keeps its 4-dot edge outside the 24-dot icon, so the icon never
        // overlaps the frame: 24 + 4 + 4 dots.
        private const float SlotSize = 32f * DotScale;
        private const float SlotRowHeight = 136f;
        private const float BonusRowHeight = 112f;

        // One large frame below the back button, split by a line into today's effects on the
        // left (a fixed width) and the owned bonuses on the right (the rest of the width).
        private const float LeftWidth = 780f;
        private const float DividerWidth = DotScale;

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
            // ランクごとの効果量はE・D・C・B・A・Sの順。doc/features/step-bonus.md の仮設定と同じ値。
            data.Bonuses = new[]
            {
                Bonus(
                    "guard",
                    "守り",
                    StepBonusCategory.Battle,
                    "受けるダメージ -{0}%",
                    1,
                    0,
                    "Guard",
                    2,
                    3,
                    4,
                    5,
                    6,
                    8
                ),
                Bonus(
                    "luck",
                    "幸運",
                    StepBonusCategory.Drop,
                    "ドロップ率 +{0}%",
                    1,
                    0,
                    "Luck",
                    3,
                    5,
                    7,
                    9,
                    12,
                    15
                ),
                Bonus(
                    "treasure-sight",
                    "宝箱透視",
                    StepBonusCategory.Explore,
                    "宝箱の中身が見える確率 {0}%",
                    0,
                    100,
                    "TreasureSight",
                    15,
                    20,
                    25,
                    30,
                    40,
                    50
                ),
                Bonus(
                    "foresight",
                    "先読み",
                    StepBonusCategory.Explore,
                    "次の部屋の敵が見える確率 {0}%",
                    0,
                    100,
                    "Foresight",
                    15,
                    20,
                    25,
                    30,
                    40,
                    50
                ),
                Bonus(
                    "omen",
                    "兆し",
                    StepBonusCategory.Explore,
                    "激化するターンが見える確率 {0}%",
                    0,
                    100,
                    "Omen",
                    15,
                    20,
                    25,
                    30,
                    40,
                    50
                ),
                Bonus(
                    "appraisal",
                    "目利き",
                    StepBonusCategory.Drop,
                    "レア度アップ +{0}%",
                    1,
                    0,
                    "Appraisal",
                    1,
                    2,
                    3,
                    4,
                    5,
                    6
                ),
                Bonus(
                    "fighting",
                    "闘志",
                    StepBonusCategory.Battle,
                    "攻撃力 +{0}%",
                    1,
                    0,
                    "Fighting",
                    2,
                    3,
                    4,
                    5,
                    6,
                    8
                ),
            };
            data.Owned = new[]
            {
                Roll("guard", StepBonusRank.S),
                Roll("luck", StepBonusRank.A),
                Roll("treasure-sight", StepBonusRank.B),
                Roll("appraisal", StepBonusRank.B),
                Roll("foresight", StepBonusRank.C),
                Roll("fighting", StepBonusRank.D),
                Roll("omen", StepBonusRank.E),
            };
            data.Loadout = new[] { "foresight", "luck", "treasure-sight", "fighting", "appraisal" };
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        /// <summary>
        /// Builds the bonus settings over the guide screen's safe area: one large frame below
        /// the back button, split into the slots (today's effects) on the left and the owned
        /// bonuses on the right. Called while the guide screen's prefab is being built, so the
        /// shared frames and font are ready.
        /// </summary>
        public static GameObject BuildSettingsPanel(RectTransform safe, GuideMenuView guide)
        {
            var data = CreateData();
            var loadout = StepBonusLoadout.From(data);
            WriteSetMark();

            var root = Rect("BonusSettings", safe);
            Stretch(root);
            var view = root.gameObject.AddComponent<StepBonusSettingsView>();
            view.Data = data;
            view.Guide = guide;
            view.TierColors = StepBonusArt.Tiers.ToArray();

            var panel = Rect("Panel", root);
            Stretch(panel);
            panel.offsetMin = new Vector2(32, 24);
            panel.offsetMax = new Vector2(-32, -208);
            Guide.Frame(panel, Guide.FramePath, Color.white).raycastTarget = true;

            var left = Rect("Effects", panel);
            left.anchorMin = Vector2.zero;
            left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(LeftWidth, 0);
            var divider = Rect("Divider", panel);
            divider.anchorMin = Vector2.zero;
            divider.anchorMax = new Vector2(0, 1);
            divider.pivot = new Vector2(0, 0.5f);
            divider.offsetMin = new Vector2(LeftWidth, 28);
            divider.offsetMax = new Vector2(LeftWidth + DividerWidth, -28);
            AddImage(divider, new Color(0.52f, 0.59f, 0.68f, 0.7f), false);
            var right = Rect("Bonuses", panel);
            Stretch(right);
            right.offsetMin = new Vector2(LeftWidth + DividerWidth, 0);

            BuildEffects(left, view, loadout);
            BuildBonuses(right, view, loadout);
            return root.gameObject;
        }

        // --- Left: the slots, which are also today's effects --------------------------

        private static void BuildEffects(
            RectTransform panel,
            StepBonusSettingsView view,
            StepBonusLoadout loadout
        )
        {
            var title = Label(
                panel,
                "Title",
                "今日の効果",
                44,
                StepBonusArt.Gold,
                TextAlignmentOptions.TopLeft
            );
            Guide.Fill((RectTransform)title.transform, new Vector2(40, 0), new Vector2(-40, -24));

            view.Slots = Enumerable
                .Range(0, loadout.SlotCount)
                .Select(i => BuildSlot(panel, i, loadout))
                .ToArray();
        }

        private static StepBonusSlotWidget BuildSlot(
            RectTransform panel,
            int index,
            StepBonusLoadout loadout
        )
        {
            var row = Rect("Slot" + index, panel);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = new Vector2(24, -(84 + index * (SlotRowHeight + 8) + SlotRowHeight));
            row.offsetMax = new Vector2(-24, -(84 + index * (SlotRowHeight + 8)));
            var widget = new StepBonusSlotWidget
            {
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

            var frame = Rect("Frame", row);
            Corner(frame, new Vector2(0, 0.5f), new Vector2(4, 0), Vector2.one * SlotSize);
            widget.Frame = Guide.Frame(frame, Guide.FramePath, StepBonusArt.Tiers[index]);
            widget.Frame.raycastTarget = false;
            var icon = Rect("Icon", frame);
            Place(icon, Vector2.zero, Vector2.one * Guide.IconSize);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            var bonus = loadout.Definition(loadout.Bonus(index));
            widget.Icon.sprite = bonus?.Icon;
            widget.Icon.enabled = bonus?.Icon != null;

            const float textLeft = 4 + SlotSize + 24;
            widget.Tier = Label(
                row,
                "Tier",
                $"{StepBonusLoadout.Upt(loadout.Tier(index))} UPT {StepBonusLoadout.Times(loadout.Multiplier(index))}",
                24,
                StepBonusArt.TextFaint,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)widget.Tier.transform, top: true, 12, 30, textLeft, 24);
            widget.Name = Label(
                row,
                "Name",
                bonus?.Name ?? "空き",
                38,
                StepBonusArt.TextMain,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)widget.Name.transform, top: true, 42, 48, textLeft, 24);
            widget.Effect = Label(
                row,
                "Effect",
                bonus != null
                    ? StepBonusLoadout.Effect(bonus, loadout.Effective(bonus.Id, index))
                    : "",
                28,
                StepBonusArt.TextSub,
                TextAlignmentOptions.Left
            );
            Guide.Band(
                (RectTransform)widget.Effect.transform,
                top: false,
                10,
                38,
                textLeft + 2,
                24
            );
            Guide.Shrink(widget.Effect, 20);
            return widget;
        }

        // --- Right: the owned bonuses ------------------------------------------------

        private static void BuildBonuses(
            RectTransform panel,
            StepBonusSettingsView view,
            StepBonusLoadout loadout
        )
        {
            var title = Label(
                panel,
                "Title",
                "ボーナス",
                44,
                StepBonusArt.Gold,
                TextAlignmentOptions.TopLeft
            );
            Guide.Fill((RectTransform)title.transform, new Vector2(40, 0), new Vector2(-40, -24));
            view.Owned = Label(
                panel,
                "Owned",
                "",
                26,
                StepBonusArt.TextSub,
                TextAlignmentOptions.TopRight
            );
            Guide.Fill(
                (RectTransform)view.Owned.transform,
                new Vector2(40, 0),
                new Vector2(-40, -36)
            );
            view.Tabs = BuildTabs(panel);

            var viewport = Rect("Viewport", panel);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(24, 24);
            viewport.offsetMax = new Vector2(-24, -176);
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

            // One row per known bonus, the mock owner's first; the view shows only the bonuses
            // the player owns and reorders them by rank.
            view.Rows = StepBonusSettingsPresenter
                .Order(loadout)
                .Concat(
                    view.Data.Bonuses.Where(bonus => !loadout.Owns(bonus.Id))
                        .Select(bonus => new StepBonusRoll
                        {
                            Id = bonus.Id,
                            Rank = StepBonusRank.E,
                        })
                )
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

        private static Button[] BuildTabs(RectTransform panel)
        {
            var tabs = new List<Button>();
            for (int i = 0; i < StepBonusSettingsPresenter.TabLabels.Length; i++)
            {
                var tab = Rect("Tab" + i, panel);
                Corner(
                    tab,
                    new Vector2(0, 1),
                    new Vector2(36 + i * 180, -88),
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

        private static StepBonusRowWidget BuildRow(
            RectTransform content,
            StepBonusRoll roll,
            StepBonusLoadout loadout
        )
        {
            var bonus = loadout.Definition(roll.Id);
            float value = bonus.Value(roll.Rank);
            var row = Rect("Row_" + roll.Id, content);
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = BonusRowHeight;
            var widget = new StepBonusRowWidget
            {
                Id = roll.Id,
                Button = Guide.AddButton(
                    row,
                    Guide.Frame(row, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
                ),
            };

            var icon = Rect("Icon", row);
            Corner(icon, new Vector2(0, 0.5f), new Vector2(12, 0), Vector2.one * Guide.IconSize);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = bonus.Icon;
            iconImage.raycastTarget = false;

            var color = StepBonusArt.Rank(roll.Rank);
            var badge = Rect("Rank", row);
            Corner(badge, new Vector2(0, 0.5f), new Vector2(124, 0), new Vector2(64, 64));
            widget.RankFrame = Guide.Frame(badge, Guide.FramePath, color);
            widget.RankFrame.raycastTarget = false;
            widget.Rank = Label(
                badge,
                "Letter",
                roll.Rank.ToString(),
                38,
                color,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)widget.Rank.transform);

            const float textLeft = 212;
            var name = Label(
                row,
                "Name",
                bonus.Name,
                38,
                StepBonusArt.TextMain,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)name.transform, top: true, 12, 48, textLeft, 96);
            widget.Effect = Label(
                row,
                "Effect",
                StepBonusLoadout.Effect(bonus, value),
                28,
                StepBonusArt.TextSub,
                TextAlignmentOptions.Left
            );
            Guide.Band(
                (RectTransform)widget.Effect.transform,
                top: false,
                12,
                38,
                textLeft + 2,
                96
            );
            Guide.Shrink(widget.Effect, 20);

            var mark = Rect("SetMark", row);
            Corner(mark, new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(11, 11) * DotScale);
            SpriteImage(mark, SetMarkPath, Color.white);
            mark.gameObject.SetActive(loadout.SlotOf(roll.Id) >= 0);
            // 仮データで持っていないボーナスの行は、持ったときだけ出す。
            row.gameObject.SetActive(loadout.Owns(roll.Id));
            widget.SetMark = mark.gameObject;
            return widget;
        }

        // A white tick on a teal tile, lit from the top left.
        private static void WriteSetMark()
        {
            ArtAssets.WritePattern(
                SetMarkPath,
                new[]
                {
                    ".ooooooooo.",
                    "oLLLLLLLLTo",
                    "oLTTTTTTwDo",
                    "oLTTTTTwwDo",
                    "oLTTTTwwTDo",
                    "oLwTTwwTTDo",
                    "oLwwwwTTTDo",
                    "oLTwwTTTTDo",
                    "oLTTTTTTTDo",
                    "oTDDDDDDDDo",
                    ".ooooooooo.",
                },
                dot =>
                    dot switch
                    {
                        'o' => new Color(0.06f, 0.16f, 0.18f),
                        'L' => new Color(0.55f, 0.9f, 0.84f),
                        'T' => new Color(0.24f, 0.66f, 0.62f),
                        'D' => new Color(0.13f, 0.42f, 0.44f),
                        'w' => Color.white,
                        _ => Color.clear,
                    }
            );
            ArtAssets.ImportSprite(SetMarkPath, Vector4.zero, FilterMode.Point);
        }

        // --- Mock content -------------------------------------------------------------

        private static StepBonusDefinition Bonus(
            string id,
            string name,
            StepBonusCategory category,
            string effect,
            int decimals,
            float cap,
            string icon,
            params float[] values
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Category = category,
                Effect = effect,
                Values = values,
                Decimals = decimals,
                Cap = cap,
                Icon = Guide.Icon(IconPath(icon)),
            };

        private static StepBonusRoll Roll(string id, StepBonusRank rank) =>
            new() { Id = id, Rank = rank };
    }
}
