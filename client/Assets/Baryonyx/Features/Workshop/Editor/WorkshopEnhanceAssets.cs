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
        private const float RowHeight = 112f;
        private const float ArtBox = 80f;

        // 強化する前と後のカード（70×98ドットを4倍）と、間の矢印。
        private const float CardDot = 4f;
        private const float CardsTop = 232f;
        private const float ArrowWidth = 96f;

        // 足りない石を作る確かめの画面：中央の列の幅、1行の高さ、石の絵（32ドットを2倍）、矢印の幅、行の片側の幅。
        private const float PromptWidth = 760f;
        private const float PromptLine = 64f;
        private const float PromptArt = 64f;
        private const float PromptArrow = 96f;
        private const float PromptSide = 180f;
        private const float GearArt = 128f;

        // 行の右端の品質・費用の幅。
        private const float ValueWidth = 230f;

        // 素材の段：磨き石と欠片の札、使う数の「－」「＋」。その下に、費用と足りない物を1行で書く。
        private const float MaterialsTop = 776f;
        private const float BarHeight = 120f;
        private const float InfoHeight = 48f;
        private const float ChipGap = 12f;
        private const float ChipArt = 64f;

        // 磨き石の札の内側の右上に置く、赤い丸に白い×のボタン：押せる広さ、暗い縁と赤い丸の直径、×の一辺、
        // 札の角からの間。
        private const float CancelHit = 64f;
        private const float CancelRim = 46f;
        private const float CancelDisc = 40f;
        private const float CancelCross = 22f;
        private const float CancelInset = 6f;
        private static readonly Color CancelRed = new(0.86f, 0.2f, 0.2f);
        private static readonly Color CancelRimColor = new(0.18f, 0.05f, 0.05f, 0.9f);

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

        // 選んだ色で届かない所は赤みで覆い、上限には太い壁の線を立てる。
        private static readonly Color GaugeBlocked = new(0.62f, 0.13f, 0.16f, 0.6f);
        private const float GaugeWall = 8f;

        /// <summary>
        /// Builds the polishing panel: skills, equipment and unique skills in the tabs. The skill
        /// awakening is a panel of its own (<see cref="WorkshopAwakenAssets"/>).
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (enhance, layer) = Panel<WorkshopEnhanceView>(safe, "WorkshopEnhance");
            enhance.TabOrder = WorkshopEnhanceView.PolishTabs;
            enhance.Guide = view;
            enhance.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            enhance.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            enhance.Training = AssetDatabase.LoadAssetAtPath<TrainingMockData>(
                TrainingAssets.DataPath
            );
            enhance.ArtBox = ArtBox;
            enhance.GearArtBox = GearArt;
            enhance.CardFrames = DeckCardFrames();
            enhance.ElementIcons = ElementIcons();
            enhance.Tabs = Tabs(
                layer,
                enhance.TabOrder.Select(tab => WorkshopEnhanceView.TabLabels[(int)tab]).ToArray()
            );

            float listLeft = Rail;
            float listTop = ListTop;
            var (content, scroll) = Column(
                layer,
                "Targets",
                listLeft,
                Split,
                listTop,
                1080f,
                (int)Inner,
                (int)Inner
            );
            enhance.Content = content;
            enhance.List = scroll;
            enhance.Template = ListRow(content, "Template", RowHeight, ArtBox, 38f, ValueWidth);
            // 読み込む間は、この写しを伏せて一覧を埋める（GlassCardCover）。
            GlassCardAssets.AddBack(enhance.Template.Card, vertical: true);
            enhance.Template.gameObject.SetActive(false);
            // 仮の編成のスキルで描き、Prefabを開くと停止中でも一覧が読めるようにする。覚醒は実行中に読む。
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
                GlassCardAssets.AddBack(row.Card, vertical: true);
                var entry =
                    enhance.Data != null
                        ? enhance.Data.Entries.FirstOrDefault(e =>
                            e != null && e.Kind == LibraryKind.Skill && e.Id == mock[i].KindId
                        )
                        : null;
                WorkshopEnhanceView.PaintTarget(row, mock[i], entry?.KindLine, entry, ArtBox);
                enhance.Rows.Add(row);
            }
            enhance.Status = Status(layer, listLeft, Split, listTop);

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

            BuildMaterials(layer, enhance, left + 16f, right, MaterialsTop);

            var info = Layout.Text(
                layer,
                "Info",
                "",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                left + 16f,
                MaterialsTop + BarHeight + 4f,
                right - left - 16f,
                InfoHeight
            );
            info.richText = true;
            info.textWrappingMode = TextWrappingModes.NoWrap;
            Shrink(info, 20);
            enhance.Info = info;
            (enhance.Confirm, enhance.ConfirmLabel) = ConfirmButton(
                layer,
                WorkshopEnhanceView.PolishLabel
            );
            enhance.Prompt = BuildCombinePrompt((RectTransform)enhance.transform);
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
            // 選んだ色で届かない所の覆い。位置は表示のときに上限から決める。
            enhance.GaugeBlocked = FillBar(bar, "Blocked", GaugeBlocked);
            enhance.GaugeBlocked.gameObject.SetActive(false);
            enhance.GaugeAfter = FillBar(bar, "After", GaugeGain);
            enhance.GaugeNow = FillBar(bar, "Now", UiPalette.TextMain);
            // 選んだ色の上限に立てる壁の線。棒より上下にはみ出させ、境目の細い線と見分ける。
            var wall = Rect("Wall", bar);
            wall.anchorMin = wall.anchorMax = new Vector2(0.4f, 0.5f);
            wall.pivot = new Vector2(0.5f, 0.5f);
            wall.sizeDelta = new Vector2(GaugeWall, GaugeBar + 32f);
            enhance.GaugeWall = AddImage(wall, Color.white, false);
            wall.gameObject.SetActive(false);
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

        // 足りない石を作る確かめの画面：画面全体を薄暗くし、中央に一文と、変換する石を小さな絵と矢印の行
        // （「白×6 ▶ 青×2」）で下の色から並べ、下に払うルーン（変換と強化の合計）、右下に戦闘と同じ丸いチェック。
        // 合成の演出のような大きな札は使わない（2026-10-31、ユーザーの指示「そんなにガッツリ合成っぽく見せなくていい」）。
        // 行は実行中に使う数だけ出し、中央に寄る。
        private static WorkshopCombinePrompt BuildCombinePrompt(RectTransform root)
        {
            var screen = Rect("CombinePrompt", root);
            Stretch(screen);
            var prompt = screen.gameObject.AddComponent<WorkshopCombinePrompt>();
            prompt.ArtBox = PromptArt;
            var art = ItemArtAssets.Load();
            prompt.StoneArts = EnhancementMaterials
                .Polishes.Select(stone => (Texture)art.TextureOf(stone.Id))
                .ToArray();

            // 背面の「もどる」や強化を押せないよう、Safe Areaの外まで暗くする。暗い所を押すと閉じる。
            var dim = Rect("Dim", screen);
            Stretch(dim);
            dim.offsetMin = new Vector2(-400, -400);
            dim.offsetMax = new Vector2(400, 400);
            prompt.Dim = GuideMenuAssets.AddButton(
                dim,
                AddImage(dim, new Color(0f, 0f, 0.02f, 0.78f), true)
            );

            var layer = Layout.Layer(screen, "Layout");
            var column = Layout.Box(layer, "Steps", 0f, 0f, Layout.Design.x, Layout.Design.y);
            var group = column.gameObject.AddComponent<VerticalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleCenter;
            group.spacing = 12f;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;

            var message = Layout.Text(
                column,
                "Message",
                WorkshopCombinePrompt.Message,
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                0f,
                0f,
                PromptWidth,
                PromptLine
            );
            var messageElement = message.gameObject.AddComponent<LayoutElement>();
            messageElement.preferredWidth = PromptWidth;
            messageElement.preferredHeight = PromptLine + 16f;

            int steps = EnhancementMaterials.Polishes.Count - 1;
            prompt.Steps = new GameObject[steps];
            prompt.From = new WorkshopGoodRow[steps];
            prompt.To = new WorkshopGoodRow[steps];
            float half = PromptWidth / 2f;
            for (int i = 0; i < steps; i++)
            {
                var step = Rect("Step" + i, column);
                var element = step.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = PromptWidth;
                element.preferredHeight = PromptLine;
                prompt.From[i] = PromptStone(step, "From", half - PromptArrow / 2f - PromptSide);
                prompt.To[i] = PromptStone(step, "To", half + PromptArrow / 2f);
                var mark = Layout.Box(
                    step,
                    "Arrow",
                    half - PromptArrow / 4f,
                    PromptLine / 4f,
                    PromptArrow / 2f,
                    PromptLine / 2f
                );
                SpriteImage(mark, ArrowPath, UiPalette.Gold).preserveAspect = true;
                prompt.Steps[i] = step.gameObject;
            }
            var runes = Rect("Runes", column);
            var runesElement = runes.gameObject.AddComponent<LayoutElement>();
            runesElement.preferredWidth = PromptWidth;
            runesElement.preferredHeight = PromptLine + 8f;
            prompt.Runes = RunePrice(
                runes,
                "Price",
                0f,
                8f,
                PromptWidth,
                PromptLine,
                40f,
                TextAnchor.MiddleCenter
            );

            prompt.Confirm = RoundCheck(layer);
            screen.gameObject.SetActive(false);
            return prompt;
        }

        // 確かめの画面の行の片側：石の絵と、その右に数（「×6」）。
        private static WorkshopGoodRow PromptStone(RectTransform step, string name, float x)
        {
            var side = Layout.Box(step, name, x, 0f, PromptSide, PromptLine);
            var row = side.gameObject.AddComponent<WorkshopGoodRow>();
            var box = Layout.Box(side, "Art", 0f, 0f, PromptLine, PromptLine);
            var image = Rect("Image", box);
            Place(image, Vector2.zero, Vector2.one * PromptArt);
            row.Art = image.gameObject.AddComponent<RawImage>();
            row.Art.raycastTarget = false;
            row.Count = Layout.Text(
                side,
                "Count",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                PromptLine + 4f,
                0f,
                PromptSide - PromptLine - 4f,
                PromptLine
            );
            return row;
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
            var materials = EnhancementMaterials.Polishes;
            enhance.Chips = new WorkshopMaterialChip[materials.Count];
            // 4色の札で右端までを等分する。
            float width = (right - left - ChipGap * (materials.Count - 1)) / materials.Count;
            for (int i = 0; i < materials.Count; i++)
            {
                var rect = Layout.Box(
                    layer,
                    "Chip" + i,
                    left + i * (width + ChipGap),
                    top,
                    width,
                    BarHeight
                );
                var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
                var chip = rect.gameObject.AddComponent<WorkshopMaterialChip>();
                chip.Card = card;
                // 絵を上に、持っている数をその下に置く。
                var box = Layout.Box(inside, "Art", (width - ChipArt) / 2f, 12f, ChipArt, ChipArt);
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
                    12f,
                    width,
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
                    12f + ChipArt + 4f,
                    width,
                    BarHeight - ChipArt - 22f
                );
                // 使う数を水色で「3/8」と書くため、色の指定を読む。
                chip.Count.richText = true;
                if (materials[i].IsPolish)
                    chip.Cancel = CancelMark(rect, width);
                enhance.Chips[i] = chip;
                // 欠片は覚醒のタブでだけ出す。
                rect.gameObject.SetActive(materials[i].IsPolish);
            }
        }

        // 使っている色の札の内側の右上に出す、選ぶのをやめるボタン。暗い縁の赤い丸に白い×を描き、
        // 札の上でもボタンと分かる形にする。押せる広さは札の角に収め、指で押せる大きさにする。
        private static Button CancelMark(RectTransform chip, float width)
        {
            var hit = Layout.Box(chip, "Cancel", width - CancelHit, 0f, CancelHit, CancelHit);
            AddImage(hit, Color.clear, true);
            // 丸の中心を札の角から CancelInset だけ内側に寄せる。押せる広さの中心からのずれ。
            float shift = CancelHit / 2f - CancelInset - CancelRim / 2f;
            var center = new Vector2(shift, shift);
            var rim = Rect("Rim", hit);
            Place(rim, center, Vector2.one * CancelRim);
            SpriteImage(rim, UiArt.CirclePath, CancelRimColor);
            var disc = Rect("Disc", hit);
            Place(disc, center, Vector2.one * CancelDisc);
            var button = Baryonyx.Editor.UI.UiBuild.AddButton(
                hit,
                SpriteImage(disc, UiArt.CirclePath, CancelRed)
            );
            var cross = Rect("Cross", hit);
            Place(cross, center, Vector2.one * CancelCross);
            SpriteImage(cross, IconClosePath, Color.white);
            hit.gameObject.SetActive(false);
            return button;
        }
    }
}
