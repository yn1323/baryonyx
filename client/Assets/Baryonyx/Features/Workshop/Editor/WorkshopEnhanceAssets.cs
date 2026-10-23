using System;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Editor.UI;
using Baryonyx.Enhancement;
using Baryonyx.Inventory.Editor;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using static Baryonyx.Workshop.Editor.WorkshopPanelAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// Builds the trading house's "enhance" panel (<see cref="WorkshopEnhanceView"/>), filling the
    /// screen: the tabs and the rows with the art on their left and the quality on their right in
    /// the left pane; in the right pane the chosen thing's art, name, quality now and after, the
    /// effect, the quality gauge, the materials held and how many stones to use, and the enhance
    /// button. The skills tab is baked from the mock party, so the prefab reads without Play
    /// Mode; the view draws what the player holds when it opens.
    /// </summary>
    public static class WorkshopEnhanceAssets
    {
        // 左の面（一覧）と右の面（選んだ物）の境。合成と同じ。
        public const float Split = WorkshopCraftAssets.Split;
        private const float RowHeight = 112f;
        private const float ArtBox = 80f;

        // 強化する前と後のカード（70×98ドットを4倍）と、間の矢印。
        private const float CardDot = 4f;
        private const float CardsTop = 232f;
        private const float ArrowWidth = 120f;
        private const float GearArt = 128f;

        // 行の右端の品質・費用の幅。
        private const float ValueWidth = 230f;

        // 素材の段：磨き石と欠片の札、使う数の「－」「＋」。
        private const float BarHeight = 136f;
        private const float ChipWidth = 120f;
        private const float ChipGap = 12f;
        private const float ChipArt = 64f;
        private const float StepButton = 88f;
        private const float StepCount = 96f;

        // 強化する前と後：スキル・固有スキルのデッキのカードと、装備の札を同じ位置に重ねて作り、選んだ物で出し分ける。
        // 間に金の矢印を置き、それぞれの下に品質と倍率を書く。
        private static void BuildCards(
            RectTransform layer,
            WorkshopEnhanceView enhance,
            float left,
            float right
        )
        {
            var size = DeckCardAssets.Size(CardDot, small: false);
            float x = left + (right - left - size.x * 2f - ArrowWidth) / 2f;
            float afterX = x + size.x + ArrowWidth;
            enhance.BeforeCard = DeckCardAssets.Build(
                layer,
                "BeforeCard",
                x,
                CardsTop,
                CardDot,
                button: false
            );
            enhance.AfterCard = DeckCardAssets.Build(
                layer,
                "AfterCard",
                afterX,
                CardsTop,
                CardDot,
                button: false
            );
            enhance.BeforeGear = GearCard(layer, "BeforeGear", x, size);
            enhance.AfterGear = GearCard(layer, "AfterGear", afterX, size);
            foreach (var card in new[] { enhance.BeforeCard, enhance.AfterCard })
                card.Frame.transform.parent.gameObject.SetActive(false);

            float arrow = 72f;
            var mark = Layout.Box(
                layer,
                "Arrow",
                x + size.x + (ArrowWidth - arrow) / 2f,
                CardsTop + (size.y - arrow) / 2f,
                arrow,
                arrow
            );
            SpriteImage(mark, ArrowPath, UiPalette.Gold).preserveAspect = true;
            mark.gameObject.SetActive(false);
            enhance.Arrow = mark.gameObject;

            float qualityTop = CardsTop + size.y + 8f;
            enhance.BeforeQuality = QualityLabel(
                layer,
                "BeforeQuality",
                x - 40f,
                qualityTop,
                size.x + 80f
            );
            enhance.AfterQuality = QualityLabel(
                layer,
                "AfterQuality",
                afterX - 40f,
                qualityTop,
                size.x + 80f
            );
        }

        private static TMP_Text QualityLabel(
            RectTransform layer,
            string name,
            float x,
            float y,
            float width
        )
        {
            var label = Layout.Text(
                layer,
                name,
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                x,
                y,
                width,
                44f
            );
            label.richText = true;
            Shrink(label, 22);
            return label;
        }

        // 装備の札：すりガラスの板の縁を品質の色にし、上に絵とキラキラ、下に名前・品質・効果を書く。
        private static WorkshopGearCard GearCard(
            RectTransform layer,
            string name,
            float x,
            Vector2 size
        )
        {
            var rect = Layout.Box(layer, name, x, CardsTop, size.x, size.y);
            var inside = GlassCardAssets.Plaque(rect, pattern: false);
            var card = new WorkshopGearCard
            {
                Root = rect.gameObject,
                Rim = GlassCardAssets.RimOf(rect),
            };
            var box = Layout.Box(inside, "ArtBox", (size.x - GearArt) / 2f, 36f, GearArt, GearArt);
            var art = Rect("Art", box);
            Place(art, Vector2.zero, Vector2.one * GearArt);
            card.Art = art.gameObject.AddComponent<RawImage>();
            card.Art.raycastTarget = false;
            card.Foil = CardFoilAssets.Add(card.Art);
            card.Name = Layout.Text(
                inside,
                "Name",
                "",
                36,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                12f,
                188f,
                size.x - 24f,
                48f
            );
            Shrink(card.Name, 24);
            card.Quality = Layout.Text(
                inside,
                "Quality",
                "",
                26,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                12f,
                238f,
                size.x - 24f,
                36f
            );
            card.Quality.richText = true;
            card.Effect = Layout.Text(
                inside,
                "Effect",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.Top,
                16f,
                286f,
                size.x - 32f,
                92f
            );
            card.Effect.richText = true;
            card.Effect.textWrappingMode = TextWrappingModes.Normal;
            Shrink(card.Effect, 18);
            rect.gameObject.SetActive(false);
            return card;
        }

        // 品質のゲージ：境目の文字と棒。
        private const float GaugeTop = 690f;
        private const float GaugeBar = 24f;
        private static readonly Color GaugeBack = new(0.02f, 0.03f, 0.08f, 0.8f);
        private static readonly Color GaugeGain = new(0.5f, 0.89f, 1f, 1f);

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (enhance, layer) = Panel<WorkshopEnhanceView>(safe, "WorkshopEnhance", Split);
            enhance.Guide = view;
            enhance.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            enhance.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            enhance.Training = AssetDatabase.LoadAssetAtPath<TrainingMockData>(
                TrainingAssets.DataPath
            );
            enhance.ArtBox = ArtBox;
            enhance.GearArtBox = GearArt;
            enhance.CardFrames = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(element => (Texture)DeckCardAssets.CardArt($"CardFrame{element}"))
                .ToArray();
            enhance.ElementIcons = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : Icon($"{PartyAssets.AttributeArtFolder}/Element{element}.aseprite")
                )
                .ToArray();
            enhance.Tabs = Tabs(layer, WorkshopEnhanceView.TabLabels, Split);

            var (content, scroll) = Column(
                layer,
                "Targets",
                0f,
                Split,
                BodyTop,
                1080f,
                (int)Edge,
                (int)Inner
            );
            enhance.Content = content;
            enhance.List = scroll;
            enhance.Template = ListRow(content, "Template", RowHeight, ArtBox, 38f, ValueWidth);
            enhance.Template.gameObject.SetActive(false);
            // 仮の編成のスキルで描き、Prefabを開くと停止中でも一覧が読めるようにする。
            var formation = PartyFormation.From(enhance.Party);
            var mock = EnhancementTargets.Skills(
                formation.Roster.SelectMany(member =>
                    member.Cards.Select(
                        (card, i) => new PartySkillCard($"mock:{member.Id}:{i}", card.Skill, 1f)
                    )
                )
            );
            for (int i = 0; i < mock.Count; i++)
            {
                var row = ListRow(content, "Row" + i, RowHeight, ArtBox, 38f, ValueWidth);
                var entry =
                    enhance.Data != null
                        ? enhance.Data.Entries.FirstOrDefault(e =>
                            e != null && e.Kind == LibraryKind.Skill && e.Id == mock[i].KindId
                        )
                        : null;
                WorkshopEnhanceView.PaintTarget(row, mock[i], entry?.KindLine, entry, ArtBox);
                enhance.Rows.Add(row);
            }
            enhance.Status = Status(layer, 0f, Split, BodyTop);

            float left = Split + Inner;
            float right = Layout.Design.x - Edge;
            enhance.DetailName = Layout.Text(
                layer,
                "DetailName",
                "",
                46,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                left + 16f,
                128f,
                right - left - 16f,
                56f
            );
            Shrink(enhance.DetailName, 30);
            enhance.DetailSub = Layout.Text(
                layer,
                "DetailSub",
                "",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                left + 16f,
                184f,
                right - left - 16f,
                36f
            );
            enhance.DetailSub.richText = true;
            Shrink(enhance.DetailSub, 20);
            BuildCards(layer, enhance, left + 16f, right);
            BuildGauge(layer, enhance, left + 16f, right);

            BuildMaterials(layer, enhance, left + 16f, right, 790f);

            var info = Layout.Text(
                layer,
                "Info",
                "",
                30,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                left + 16f,
                ConfirmTop,
                right - left - 16f - ConfirmWidth - Inner,
                ConfirmHeight
            );
            info.richText = true;
            info.textWrappingMode = TextWrappingModes.Normal;
            info.overflowMode = TextOverflowModes.Ellipsis;
            Shrink(info, 20);
            enhance.Info = info;
            (enhance.Confirm, enhance.ConfirmLabel) = ConfirmButton(
                layer,
                WorkshopEnhanceView.PolishLabel
            );
            return enhance.gameObject;
        }

        // 品質のゲージ：上にC・B・A・Sの境目の文字、下に暗い棒。棒には水色（上がった後）と白（今）を重ね、境目に細い線を引く。
        private static void BuildGauge(
            RectTransform layer,
            WorkshopEnhanceView enhance,
            float left,
            float right
        )
        {
            var gauge = Rect("Gauge", layer);
            Stretch(gauge);
            float width = right - left;
            float barTop = GaugeTop + 44f;
            var bar = Layout.Box(gauge, "Bar", left, barTop, width, GaugeBar);
            AddImage(bar, GaugeBack, false);
            enhance.GaugeAfter = FillBar(bar, "After", GaugeGain);
            enhance.GaugeNow = FillBar(bar, "Now", UiPalette.TextMain);
            foreach (
                var (letter, place) in new[] { ("C", 0f), ("B", 0.4f), ("A", 0.7f), ("S", 0.9f) }
            )
            {
                float x = left + width * place;
                if (place > 0f)
                    AddImage(
                        Layout.Box(gauge, "Mark" + letter, x - 1f, barTop - 6f, 2f, GaugeBar + 12f),
                        GuideFrameAssets.DividerColor,
                        false
                    );
                Inventory.Quality.TryParse(letter, out var quality);
                Layout.Text(
                    gauge,
                    "Letter" + letter,
                    letter,
                    30,
                    Inventory.QualityPalette.Of(quality),
                    TextAlignmentOptions.Bottom,
                    x - 30f,
                    GaugeTop,
                    60f,
                    40f
                );
            }
            gauge.gameObject.SetActive(false);
            enhance.Gauge = gauge.gameObject;
        }

        private static RectTransform FillBar(RectTransform bar, string name, Color color)
        {
            var fill = Rect(name, bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            AddImage(fill, color, false);
            return fill;
        }

        // 素材の段：左から磨き石4色と覚醒の欠片の札（覚醒のタブでは欠片だけを出す）、右端に使う数。
        private static void BuildMaterials(
            RectTransform layer,
            WorkshopEnhanceView enhance,
            float left,
            float right,
            float top
        )
        {
            var art = ItemArtAssets.Load();
            var materials = EnhancementMaterials.All;
            enhance.Chips = new WorkshopMaterialChip[materials.Count];
            for (int i = 0; i < materials.Count; i++)
            {
                // 欠片は、覚醒のタブで磨き石の1つ目の位置に出す。
                int slot = materials[i].IsPolish ? i : 0;
                var rect = Layout.Box(
                    layer,
                    "Chip" + i,
                    left + slot * (ChipWidth + ChipGap),
                    top,
                    ChipWidth,
                    BarHeight
                );
                var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
                var chip = rect.gameObject.AddComponent<WorkshopMaterialChip>();
                chip.Card = card;
                // 絵を上に、持っている数をその下に置く。
                var box = Layout.Box(
                    inside,
                    "Art",
                    (ChipWidth - ChipArt) / 2f,
                    16f,
                    ChipArt,
                    ChipArt
                );
                chip.Art = box.gameObject.AddComponent<RawImage>();
                chip.Art.raycastTarget = false;
                chip.Art.texture = art.TextureOf(materials[i].Id);
                chip.Art.enabled = chip.Art.texture != null;
                chip.Name = Layout.Text(
                    inside,
                    "Name",
                    chip.Art.enabled ? "" : materials[i].ShortName,
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center,
                    0f,
                    16f,
                    ChipWidth,
                    ChipArt
                );
                chip.Count = Layout.Text(
                    inside,
                    "Count",
                    "×0",
                    32,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center,
                    0f,
                    16f + ChipArt + 8f,
                    ChipWidth,
                    BarHeight - ChipArt - 32f
                );
                enhance.Chips[i] = chip;
                // 欠片は覚醒のタブでだけ出す。
                rect.gameObject.SetActive(materials[i].IsPolish);
            }

            var stepper = Layout.Box(
                layer,
                "Stepper",
                right - StepButton * 2f - StepCount,
                top,
                StepButton * 2f + StepCount,
                BarHeight
            );
            enhance.Stepper = stepper.gameObject;
            enhance.Fewer = StepperButton(stepper, "Fewer", "－", 0f);
            enhance.CountLabel = Layout.Text(
                stepper,
                "Count",
                "1",
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                StepButton,
                0f,
                StepCount,
                BarHeight
            );
            enhance.More = StepperButton(stepper, "More", "＋", StepButton + StepCount);
        }

        private static Button StepperButton(RectTransform parent, string name, string text, float x)
        {
            var (button, _, content) = GlassCardAssets.Choice(
                Layout.Box(parent, name, x, 8f, StepButton, BarHeight - 16f),
                pattern: false
            );
            var label = Label(
                content,
                "Label",
                text,
                48,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            return button;
        }
    }
}
