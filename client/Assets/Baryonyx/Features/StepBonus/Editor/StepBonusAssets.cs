using System.IO;
using System.Linq;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Inventory;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using GlassCardAssets = Baryonyx.UI.Editor.GlassCardAssets;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.StepBonus.Editor
{
    /// <summary>
    /// The ACT bonus mock data, its icons, and the tavern's bonus settings. The tavern's
    /// generator calls <see cref="BuildSettingsPanel"/> for its bonus item, so the slots and the
    /// owned bonuses are baked into the tavern prefab and read in the editor without Play Mode.
    /// The bonuses and values are mock data until the bonus is saved (doc/features/step-bonus.md).
    /// </summary>
    public static class StepBonusAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/StepBonus";
        public const string DataPath = Folder + "/Data/StepBonusMockData.asset";
        public const string ArtFolder = Folder + "/UI/Art";

        // 一覧で、枠に入れているボーナスに付ける印（コードで描く11×11のドット絵）。
        public const string SetMarkPath = ArtFolder + "/SetMark.png";

        private const float DotScale = 4f;

        // The slot's plate keeps a 4-dot margin around the 24-dot icon, so the icon never
        // overlaps the rim: 24 + 4 + 4 dots.
        private const float SlotSize = 32f * DotScale;
        private const float BonusRowHeight = 112f;

        // 枠つきの表示（GuideFramedPanel）で、左の面に今日の効果（5つの枠）、金の2本線の右の
        // 面に持っているボーナスを置く。設計座標、左上から。編成の冒険者の一覧と同じ間隔。
        private const float Split = 800f;
        private const float Gap = 24f;
        private const float HeadingHeight = 76f;
        private const float CardGap = 16f;
        private const float TabHeight = 76f;
        private const float EffectsLeft = Frame.Margin;
        private const float EffectsWidth = Split - Gap - Frame.Margin;
        private const float BonusesLeft = Split + Gap;
        private const float BonusesWidth = 1920f - Frame.Margin - BonusesLeft;
        private const float CardTop = Frame.HeaderHeight + HeadingHeight + CardGap;

        // 5つの枠を、見出しの下から面の下端まで並べる。
        private const float SlotHeight = (1080f - CardTop - CardGap * 5f) / 5f;

        public static string IconPath(string name) => $"{ArtFolder}/Icon{name}.aseprite";

        [MenuItem("Baryonyx/Step Bonus/Create Mock Data")]
        public static StepBonusMockData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            var data = AssetFolders.LoadOrCreate<StepBonusMockData>(DataPath);
            data.TodayAct = 3240;
            data.Tiers = new[] { 1000, 2000, 3000, 5000, 8000 };
            data.Multipliers = new[] { 1f, 1.2f, 1.4f, 1.7f, 2f };
            data.TotalKinds = 20;
            // 基本の効果量（倍率×1.00のとき）。doc/features/step-bonus.md の仮設定の☆1の値を当てた。
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
                    3
                ),
                Bonus("luck", "幸運", StepBonusCategory.Drop, "ドロップ率 +{0}%", 1, 0, "Luck", 5),
                Bonus(
                    "treasure-sight",
                    "宝箱透視",
                    StepBonusCategory.Explore,
                    "宝箱の中身が見える確率 {0}%",
                    0,
                    100,
                    "TreasureSight",
                    20
                ),
                Bonus(
                    "foresight",
                    "先読み",
                    StepBonusCategory.Explore,
                    "次の部屋の敵が見える確率 {0}%",
                    0,
                    100,
                    "Foresight",
                    20
                ),
                Bonus(
                    "omen",
                    "兆し",
                    StepBonusCategory.Explore,
                    "激化するターンが見える確率 {0}%",
                    0,
                    100,
                    "Omen",
                    20
                ),
                Bonus(
                    "appraisal",
                    "目利き",
                    StepBonusCategory.Drop,
                    "レア度アップ +{0}%",
                    1,
                    0,
                    "Appraisal",
                    2
                ),
                Bonus(
                    "fighting",
                    "闘志",
                    StepBonusCategory.Battle,
                    "攻撃力 +{0}%",
                    1,
                    0,
                    "Fighting",
                    3
                ),
            };
            data.Owned = new[]
            {
                Roll("guard", 1.48f),
                Roll("luck", 1.4f),
                Roll("treasure-sight", 1.28f),
                Roll("appraisal", 1.24f),
                Roll("foresight", 1.15f),
                Roll("fighting", 1.08f),
                Roll("omen", 1f),
            };
            data.Loadout = new[] { "foresight", "luck", "treasure-sight", "fighting", "appraisal" };
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        /// <summary>
        /// Builds the bonus settings over the guide screen's safe area in the guide frame
        /// (<see cref="GuideFramedPanel"/>), like the formation's other screens: the slots
        /// (today's effects) on the left pane, the owned bonuses on the right one, both panes
        /// the scene shows through. Called while the guide screen's prefab is being
        /// built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildSettingsPanel(RectTransform safe, GuideMenuView guide)
        {
            var data = CreateData();
            var loadout = StepBonusLoadout.From(data);
            WriteSetMark();

            var (view, layer) = Frame.FramedPanel<StepBonusSettingsView>(safe, "BonusSettings");
            view.Data = data;
            view.Guide = guide;
            view.TierColors = StepBonusArt.Tiers.ToArray();

            Frame.SplitPanes(layer, Split, "EffectsPane", "BonusesPane");
            Frame.Heading(
                layer,
                "EffectsTitle",
                "今日の効果",
                EffectsLeft,
                Frame.HeaderHeight,
                400f,
                HeadingHeight
            );
            Frame.Heading(
                layer,
                "BonusesTitle",
                "ボーナス",
                BonusesLeft,
                Frame.HeaderHeight,
                400f,
                HeadingHeight
            );
            // 所持数は、右の見出しと同じ高さの右端に置く。
            view.Owned = Layout.Text(
                layer,
                "Owned",
                "",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineRight,
                BonusesLeft,
                Frame.HeaderHeight,
                BonusesWidth,
                HeadingHeight
            );

            BuildEffects(layer, view, loadout);
            BuildBonuses(layer, view, loadout);
            return view.gameObject;
        }

        // --- Left: the slots, which are also today's effects --------------------------

        private static void BuildEffects(
            RectTransform layer,
            StepBonusSettingsView view,
            StepBonusLoadout loadout
        )
        {
            var panel = Layout.Box(
                layer,
                "Effects",
                EffectsLeft,
                CardTop,
                EffectsWidth,
                Layout.Design.y - CardGap - CardTop
            );
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
            var (button, selected, row) = GlassCardAssets.Choice(
                Layout.Box(
                    panel,
                    "Slot" + index,
                    0,
                    index * (SlotHeight + CardGap),
                    EffectsWidth,
                    SlotHeight
                ),
                pattern: false
            );
            var widget = new StepBonusSlotWidget { Button = button, Selected = selected };

            // 縁を段階の色にした小さなすりガラスの板に、24×24のアイコンを4倍で、縁と重ならないように置く。
            var frame = Rect("Frame", row);
            Corner(frame, new Vector2(0, 0.5f), new Vector2(14, 0), Vector2.one * SlotSize);
            var inside = GlassCardAssets.Plaque(frame, pattern: false);
            widget.Frame = GlassCardAssets.RimOf(frame);
            widget.Frame.color = StepBonusArt.Tiers[index];
            var icon = Rect("Icon", inside);
            Place(icon, Vector2.zero, Vector2.one * Guide.IconSize);
            widget.Icon = icon.gameObject.AddComponent<Image>();
            widget.Icon.raycastTarget = false;
            var bonus = loadout.Definition(loadout.Bonus(index));
            widget.Icon.sprite = bonus?.Icon;
            widget.Icon.enabled = bonus?.Icon != null;

            const float textLeft = 14 + SlotSize + 24;
            widget.Tier = Label(
                row,
                "Tier",
                $"{StepBonusLoadout.Act(loadout.Tier(index))} ACT {StepBonusLoadout.Times(loadout.Multiplier(index))}",
                24,
                UiPalette.Gold,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)widget.Tier.transform, top: true, 16, 30, textLeft, 24);
            widget.Name = Label(
                row,
                "Name",
                bonus?.Name ?? "空き",
                38,
                UiPalette.TextMain,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)widget.Name.transform, top: true, 46, 50, textLeft, 24);
            widget.Effect = Label(
                row,
                "Effect",
                bonus != null
                    ? StepBonusLoadout.Effect(bonus, loadout.Effective(bonus.Id, index))
                    : "",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.Left
            );
            Guide.Band(
                (RectTransform)widget.Effect.transform,
                top: false,
                16,
                38,
                textLeft + 2,
                24
            );
            Guide.Shrink(widget.Effect, 20);
            return widget;
        }

        // --- Right: the owned bonuses ------------------------------------------------

        private static void BuildBonuses(
            RectTransform layer,
            StepBonusSettingsView view,
            StepBonusLoadout loadout
        )
        {
            var panel = Layout.Box(
                layer,
                "Bonuses",
                BonusesLeft,
                CardTop,
                BonusesWidth,
                Layout.Design.y - CardGap - CardTop
            );
            // 上に、面の幅いっぱいのカテゴリのタブ（装備の付け替えのタブと同じ形）。
            var labels = StepBonusSettingsPresenter.TabLabels;
            view.Tabs = Frame.Tabs(
                panel,
                "Tabs",
                labels,
                0,
                0,
                BonusesWidth / labels.Length,
                TabHeight,
                BonusesWidth
            );

            var viewport = Layout.Viewport(
                panel,
                offsetMax: new Vector2(0, -(TabHeight + CardGap))
            );
            var content = Layout.Column(viewport, "Rows", 10);

            // One row per known bonus, the mock owner's first; the view shows only the bonuses
            // the player owns and reorders them by quality.
            view.Rows = StepBonusSettingsPresenter
                .Order(loadout)
                .Concat(
                    view.Data.Bonuses.Where(bonus => !loadout.Owns(bonus.Id))
                        .Select(bonus => new StepBonusRoll
                        {
                            Id = bonus.Id,
                            Multiplier = Quality.MinMultiplier,
                        })
                )
                .Select(roll => BuildRow(content, roll, loadout))
                .ToArray();

            view.List = Layout.Scroll(panel, viewport, content);
        }

        private static StepBonusRowWidget BuildRow(
            RectTransform content,
            StepBonusRoll roll,
            StepBonusLoadout loadout
        )
        {
            var bonus = loadout.Definition(roll.Id);
            float value = bonus.Value(roll.Multiplier);
            var quality = bonus.QualityOf(roll.Multiplier);
            var rect = Layout.ListItem(content, "Row_" + roll.Id, BonusRowHeight);
            var (button, _, row) = GlassCardAssets.Choice(rect, pattern: false);
            var widget = new StepBonusRowWidget { Id = roll.Id, Button = button };

            var icon = Rect("Icon", row);
            Corner(icon, new Vector2(0, 0.5f), new Vector2(16, 0), Vector2.one * Guide.IconSize);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = bonus.Icon;
            iconImage.raycastTarget = false;

            var color = StepBonusArt.Quality(quality);
            var badge = Rect("Quality", row);
            Corner(badge, new Vector2(0, 0.5f), new Vector2(128, 0), new Vector2(64, 64));
            var plaque = GlassCardAssets.Plaque(badge, pattern: false);
            widget.QualityFrame = GlassCardAssets.RimOf(badge);
            widget.QualityFrame.color = color;
            widget.Quality = Label(
                plaque,
                "Letter",
                Quality.Letter(quality),
                38,
                color,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)widget.Quality.transform);

            const float textLeft = 216;
            var name = Label(
                row,
                "Name",
                bonus.Name,
                36,
                UiPalette.TextMain,
                TextAlignmentOptions.Left
            );
            Guide.Band((RectTransform)name.transform, top: true, 12, 48, textLeft, 96);
            widget.Effect = Label(
                row,
                "Effect",
                StepBonusLoadout.Effect(bonus, value),
                26,
                UiPalette.TextSub,
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
            Corner(mark, new Vector2(1, 0.5f), new Vector2(-32, 0), new Vector2(11, 11) * DotScale);
            SpriteImage(mark, SetMarkPath, Color.white);
            mark.gameObject.SetActive(loadout.SlotOf(roll.Id) >= 0);
            // 仮データで持っていないボーナスの行は、持ったときだけ出す。
            rect.gameObject.SetActive(loadout.Owns(roll.Id));
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
            float baseValue
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Category = category,
                Effect = effect,
                BaseValue = baseValue,
                Decimals = decimals,
                Cap = cap,
                Icon = Guide.Icon(IconPath(icon)),
            };

        // 持っているボーナスと倍率。品質がそろうよう、サーバーの初期のボーナスと同じ倍率にする。
        private static StepBonusRoll Roll(string id, float multiplier) =>
            new() { Id = id, Multiplier = multiplier };
    }
}
