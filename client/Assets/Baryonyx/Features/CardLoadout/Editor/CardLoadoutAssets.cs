using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using ActionButtonAssets = Baryonyx.UI.Editor.ActionButtonAssets;
using ChangeScreen = Baryonyx.Party.Editor.PartyChangeScreenAssets;
using ElementIconRowAssets = Baryonyx.UI.Editor.ElementIconRowAssets;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using GlassCardAssets = Baryonyx.UI.Editor.GlassCardAssets;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.CardLoadout.Editor
{
    /// <summary>
    /// The adventurer's card skills tab, laid out like the equipment's (EquipmentAssets): the
    /// six cards as rows with their effects on the left, the tabs and the list (or the unique
    /// skill's evolution tree) on the right, between the people's column and the tabs. The
    /// formation's generator calls <see cref="BuildPanel"/> for its card skills item, so the six
    /// rows and a row for every card are baked into the formation prefab and read in the editor
    /// without Play Mode. The cards are the combat's card skills; the people, levels and
    /// stats are the party's and the training's mock data (doc/features/party.md).
    /// </summary>
    public static class CardLoadoutAssets
    {
        // カードの挿絵（64×58）は2倍で出す。
        private const float ArtDot = CardLoadoutView.ArtDot;

        // 枠つきの表示（GuideFramedPanel）で、人の列とタブの間の左の面に6枚の行、金の2本線の右の面に
        // タブとカードの一覧を置く（PartyChangeScreenAssets）。左の面は装備より広い（左の行に効果の文を
        // 2行で出していたため。2026-11-03に効果の文を外したあとも、長い名前と右上の印が並ぶので広いままにした）。
        // 設計座標、左上から。
        private const float Split = UiSpace.SheetSplitWide;
        private static readonly Rect Left = ChangeScreen.LeftOf(Split);
        private static readonly Rect Right = ChangeScreen.RightOf(Split);

        // 左の6行：見出し「固有スキル」と3行、見出し「カスタムスキル」と3行。行は効果の文まで読めるよう
        // 右の一覧の1.5倍の高さにし、6行が面に収まらない分は縦にスクロールする（2026-11-03、ユーザーの指示）。
        private const float DeckRowHeight = 192f;
        private const float DeckRowGap = 8f;
        private const float DeckHeadingHeight = 48f;
        private const float DeckGroupGap = 16f;
        private const float CustomTop =
            DeckHeadingHeight + 3 * DeckRowHeight + 2 * DeckRowGap + DeckGroupGap;
        private const float DeckHeight =
            CustomTop + DeckHeadingHeight + 3 * DeckRowHeight + 2 * DeckRowGap;

        private const float SlotHeight = 136f;
        private const float SlotTextLeft = 156f;

        // 左の6行は、上から名前、種類（「攻撃・敵単体」）、効果の文（3行まで）を積む。
        private const float NameHeight = 46f;
        private const float DeckNameTop = 14f;
        private const float DeckKindTop = DeckNameTop + NameHeight + 2f;
        private const float DeckKindHeight = 32f;
        private const float DeckTextTop = DeckKindTop + DeckKindHeight + 2f;
        private const float DeckTextHeight = 84f;
        private const float DeckArtDot = 3f;

        // 行の右上（名前の行の高さ）の、印・属性・コストの札。挿絵に重ねると絵の模様に溶けて読めないため、
        // 絵の外に出した（2026-11-03、ユーザーの指示）。印は装備の行の属性と同じ3倍のドット絵。
        private const float CornerDot = ElementIconRowAssets.Size / 12f;
        private const float CornerWidth = 260f;
        private const float CornerMark = 28f;
        private static readonly float CornerIcons =
            ElementIconRowAssets.Size + ElementIconRowAssets.Gap + 11f * CornerDot;

        // 右上の印からの名前の行の右端：左の6行（属性とコスト）、ツリーの段階（「E」と属性とコスト）。
        private const float SlotCornerRight = 20f;
        private static readonly float DeckNameRight = SlotCornerRight + CornerIcons + 12f;
        private const float StageCornerRight = 24f;
        private static readonly float StageNameRight = StageCornerRight + CornerIcons + 36f;

        // 右の進化ツリー：上に見出し、その下に7つの段階を1行ずつ縦に並べ、はみ出す分はスクロールする。
        // 最初の段階、続けて枝ごとに1段目（1段字下げ）、2段目（2段字下げ）の順。
        // 字下げの手前に縦の線を引き、その段階のカードへ横に線を出す。
        private const float TreeTitleTop = 24f;
        private const float TreeListTop = 84f;
        private const float StageIndent = 48f;
        private const float RowSpacing = 10f;
        private const float LineWidth = 4f;
        private static readonly Color LineColor = new(0.86f, 0.71f, 0.38f, 0.7f);

        // 未取得の段階の封印と鎖の絵。ドット絵ではない生成画像で、滑らかに縮めて出す。
        private const string ArtFolder = "Assets/Baryonyx/Features/CardLoadout/UI/Art";
        private const float SealSize = 112f;
        private const float ChainWidth = 420f;

        /// <summary>
        /// Builds the card skills over the guide screen's safe area: the person, their two
        /// slots and stats in the left frame, the cards they can set in the right frame. Called
        /// while the guide screen's prefab is being built, so the shared frames and font are
        /// ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var training = TrainingAssets.CreateData();
            var people = CardLoadoutPresenter.People(PartyFormation.From(party));

            var (view, layer) = Frame.FramedPanel<CardLoadoutView>(safe, "CardLoadout");
            view.Party = party;
            view.Training = training;
            view.ItemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();
            view.Guide = guide;
            view.ElementIcons = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(ElementIcon)
                .ToArray();

            view.Swipe = BuildDeck(layer, view);
            view.Sheet = PartySheetAssets.Build(
                layer,
                guide,
                party,
                CardLoadoutSession.GuideItemKey,
                TrainingAssets.SheetTabs
            );
            BuildPicker((RectTransform)view.transform, view);

            // 仮データの最初の人で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            // 選ぶ画面には、1つ目の固有スキルの覚醒ツリーと、1つ目のカスタムスキルの枠の候補を作り込む
            // （停止中の表示の切り替えで見せる）。最後に開いたときの表示（1つ目のカスタムスキルの枠）に戻す。
            var store = new MockStore(party, training);
            var tree = new CardLoadoutPresenter(view, people, store, unique: 0);
            view.BakePicker(tree.State, true);
            tree.Dispose();
            var choices = new CardLoadoutPresenter(view, people, store);
            view.BakePicker(choices.State, false);
            choices.Dispose();
            return view.gameObject;
        }

        // --- The skill tab: six battle cards, and the cards to choose from ------------------

        // スキルのタブ（2026-11-03、ユーザーが案Dを選んだ）：人の列とタブの列の間を1枚の透ける面にし、
        // 見出し「◆固有スキル」「◆カスタムスキル」と右へ伸びる金の線の下に、戦闘のカードを3枚ずつ並べる。
        // カードは2段が面の高さに収まる大きさ（戦闘の手札の約1.3倍）にする。設計座標、左上から。
        private static readonly Vector2 BattleCardSize = new(210f, 294f);
        private const float GridTop = Frame.HeaderHeight + 6f;
        private const float GridHeight = ChangeScreen.Height;
        private const float GridPad = 10f;
        private const float GridHeading = 40f;
        private const float GridHeadingGap = 8f;
        private const float GridRowGap = 24f;
        private const float GridCardGap = 40f;
        private const float GridHeadingSize = 26f;
        private const float GridLineGap = 18f;
        private const float GridCardHeight =
            (GridHeight - 2 * GridPad - 2 * (GridHeading + GridHeadingGap) - GridRowGap) / 2f;
        private static readonly float GridScale = GridCardHeight / BattleCardSize.y;

        // 左の縦の欄のステータス8つ（項目名を左、数字を右に1列で。2026-11-03、ユーザーが4案から選んだ）。
        // 色は項目名も数字も白（目立たせる項目は作らない、同日のユーザーの指示）。
        private const float StatsWidth = 260f;
        private const float StatsGap = 28f;
        private const float StatsInset = 22f;
        private const float StatLabelSize = 28f;
        private const float StatValueSize = 34f;
        private static readonly Color StatsBackdrop = new(0f, 0f, 0f, 0.28f);

        // 右下の印（「E」「所持 3」）の札：カードの中の大きさ（戦闘のカードの設計座標）。
        // 右上の属性の下に置いていたときは、挿絵に溶けて見えにくかった（2026-11-04、ユーザーの指摘）。
        // 効果の欄の暗い所に置き、少し大きくした。
        private static readonly Vector2 MarkSize = new(72f, 30f);
        private const float MarkFont = 21f;

        // 空いた枠の点線のカードの板（暗いすりガラス）と「＋」の大きさ（×印の絵を45度回す）。
        // 白い板では明るい灰色になって目立ったため、暗くして「＋」を小さくした（2026-11-03）。
        private static readonly Color EmptyFill = new(0.02f, 0.03f, 0.08f, 0.35f);
        private const float EmptyPlus = 0.55f;

        // 未取得の段階の封印（カードの中の大きさ）。
        private const float CardSealSize = 96f;
        private const string SealPath = ArtFolder + "/StageSeal.png";

        /// <summary>
        /// The pane between the people's column and the tabs, the two headings with their gold
        /// lines and the six cards under them; flicking on it walks the people.
        /// </summary>
        private static GuideSwipe BuildDeck(RectTransform layer, CardLoadoutView view)
        {
            SmoothTexture(SealPath);
            Frame.Pane(
                layer,
                "DeckPane",
                PartySheetAssets.ContentLeft,
                PartySheetAssets.ContentRight,
                Frame.HeaderHeight,
                Frame.PaneLight,
                false,
                true
            );
            float left = PartySheetAssets.ContentLeft + 24f;
            float width = PartySheetAssets.ContentRight - 24f - left;
            var deck = Layout.Box(layer, "Deck", left, GridTop, width, GridHeight);
            // 面の上の左右のフリックで人を替える。カードを押したところから動かしても届くよう、面全体で受ける。
            AddImage(deck, Color.clear, true);
            var swipe = deck.gameObject.AddComponent<GuideSwipe>();

            view.Stats = BuildStats(deck);

            // カードの段は、ステータスの欄の右の残りの真ん中に置く。
            var size = BattleCardSize * GridScale;
            float rowWidth = 3 * size.x + 2 * GridCardGap;
            float rest = width - StatsWidth - StatsGap;
            float x0 = StatsWidth + StatsGap + (rest - rowWidth) / 2f;
            float y = GridPad;
            view.UniqueCards = DeckRow(deck, "Unique", "固有スキル", x0, ref y, size, rowWidth);
            y += GridRowGap;
            view.SlotCards = DeckRow(deck, "Slot", "カスタムスキル", x0, ref y, size, rowWidth);

            // 読み込めなかったときの文字。カードは伏せたままにする。
            view.Status = Layout.Text(
                deck,
                "Status",
                "",
                UiType.Lead,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                0f,
                0f,
                width,
                GridHeight
            );
            return swipe;
        }

        // 左の縦の欄：縁のない薄い黒の下地に、ステータス8つを1行ずつ（項目名を左、数字を右）並べ、行の間に細い線を
        // 引く。札（すりガラスの板と縁）にはしない（2026-11-03、ユーザーの指示）。
        private static TMP_Text[] BuildStats(RectTransform deck)
        {
            var inside = Layout.Box(
                deck,
                "Stats",
                0f,
                GridPad,
                StatsWidth,
                GridHeight - 2 * GridPad
            );
            AddImage(inside, StatsBackdrop, false);
            float rowWidth = StatsWidth - 2 * StatsInset;
            float row = (GridHeight - 2 * GridPad - 2 * StatsInset) / CharacterStats.Count;
            var values = new TMP_Text[CharacterStats.Count];
            for (int i = 0; i < CharacterStats.Count; i++)
            {
                float y = StatsInset + i * row;
                Layout.Text(
                    inside,
                    "Label" + i,
                    CharacterStats.Labels[i],
                    StatLabelSize,
                    UiPalette.TextMain,
                    TextAlignmentOptions.MidlineLeft,
                    StatsInset,
                    y,
                    rowWidth,
                    row
                );
                values[i] = Layout.Text(
                    inside,
                    "Value" + i,
                    "",
                    StatValueSize,
                    UiPalette.TextMain,
                    TextAlignmentOptions.MidlineRight,
                    StatsInset,
                    y,
                    rowWidth,
                    row
                );
                if (i > 0)
                    Layout.Line(inside, "Line" + i, StatsInset, y - 1f, rowWidth, 2f);
            }
            return values;
        }

        // 見出しの段1つと、その下の3枚。
        private static CardLoadoutCard[] DeckRow(
            RectTransform deck,
            string name,
            string title,
            float x,
            ref float y,
            Vector2 size,
            float rowWidth
        )
        {
            DeckHeading(deck, name + "Title", title, x, y, rowWidth);
            y += GridHeading + GridHeadingGap;
            float top = y;
            var cards = Enumerable
                .Range(0, 3)
                .Select(i =>
                    BuildCard(
                        Layout.Box(
                            deck,
                            name + i,
                            x + i * (size.x + GridCardGap),
                            top,
                            size.x,
                            size.y
                        ),
                        GridScale
                    )
                )
                .ToArray();
            y += size.y;
            return cards;
        }

        // 見出し：金のひし形と明るい金の文字、その右からカードの右端まで伸びる金の細い線。
        private static void DeckHeading(
            RectTransform deck,
            string name,
            string text,
            float x,
            float y,
            float width
        )
        {
            var row = Layout.Row(deck, name, x, y, width, GridHeading, 12, TextAnchor.MiddleLeft);
            Label(row, "Title", text, GridHeadingSize, UiPalette.Gold, TextAlignmentOptions.Left);
            Frame.HeadingRow(row, GridHeadingSize);
            var cell = Rect("Line", row);
            cell.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var line = Rect("Gold", cell);
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(1f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.offsetMin = new Vector2(GridLineGap - 12f, -1f);
            line.offsetMax = new Vector2(0f, 1f);
            AddImage(line, UiPalette.LineDivider, false);
        }

        /// <summary>
        /// One battle card that takes a tap: the battle's card prefab scaled by
        /// <paramref name="scale"/> into <paramref name="rect"/>, as the body of a
        /// <see cref="GlassCard"/> (it sinks when pressed and turns over when its slot changes),
        /// with the gold rim of the card chosen, the mark at the bottom right, the text in place of
        /// the effect and the seal of a stage not acquired.
        /// </summary>
        private static CardLoadoutCard BuildCard(RectTransform rect, float scale)
        {
            GlassCardAssets.EnsureArt();
            AddImage(rect, Color.clear, true);
            var glass = rect.gameObject.AddComponent<GlassCard>();
            var card = rect.gameObject.AddComponent<CardLoadoutCard>();
            card.Glass = glass;

            // 回す処理（GlassCardTurn）は板の拡大率を書き換えるので、拡大しない板に戦闘のカードを入れる。
            var body = Rect("Body", rect);
            Stretch(body);
            glass.Body = body;
            var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Face", body);
            var input =
                instance.GetComponent<Baryonyx.Combat.Presentation.BattleInspectCardInput>();
            if (input != null)
                UnityEngine.Object.DestroyImmediate(input);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            var face = (RectTransform)instance.transform;
            face.anchorMin = face.anchorMax = face.pivot = new Vector2(0.5f, 0.5f);
            face.anchoredPosition = Vector2.zero;
            face.localScale = Vector3.one * scale;
            card.Face = instance.GetComponent<Baryonyx.Combat.Presentation.BattleInspectCardView>();

            // 効果の欄の真ん中の文字（「空き」「Lv 30で解放」）。効果の文と同じ所に置く。
            var note = UnityEngine.Object.Instantiate(
                card.Face.Description.gameObject,
                card.Face.Description.transform.parent
            );
            note.name = "Note";
            card.Note = note.GetComponent<TMP_Text>();
            card.Note.text = "";
            card.Note.alignment = TextAlignmentOptions.Center;
            card.Note.color = UiPalette.GoldLight;
            note.SetActive(false);

            // 右下の印：効果の欄の右下の角に、濃い札と金の太い文字。
            var plate = Rect("Mark", face);
            plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(1f, 0f);
            plate.anchoredPosition = new Vector2(-6f, 6f);
            plate.sizeDelta = MarkSize;
            AddImage(plate, new Color(0f, 0f, 0f, 0.65f), false);
            card.Mark = Label(
                plate,
                "Text",
                "",
                MarkFont,
                UiPalette.Gold,
                TextAlignmentOptions.Center
            );
            card.Mark.fontStyle = FontStyles.Bold;
            Stretch((RectTransform)card.Mark.transform);
            card.MarkPlate = plate.gameObject;
            plate.gameObject.SetActive(false);

            // 取得していない段階：伏せたカードの上の「？」の封印。
            var seal = Rect("Seal", face);
            Place(seal, Vector2.zero, Vector2.one * CardSealSize);
            var sealImage = seal.gameObject.AddComponent<RawImage>();
            sealImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SealPath);
            sealImage.raycastTarget = false;
            card.Seal = seal.gameObject;
            seal.gameObject.SetActive(false);

            // 空いた枠：戦闘のカードの代わりに、同じ大きさの点線のカード（うすい板、点線の縁、真ん中に「＋」、
            // その下に「空き」）。ほかの画面の空き（点線の札）と同じ見せ方（2026-11-03、ユーザーが5案から選んだ）。
            var empty = Rect("Empty", body);
            Stretch(empty);
            var fill = SpriteImage(empty, GlassCardAssets.FillPath, EmptyFill);
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = false;
            var rim = Rect("Rim", empty);
            Stretch(rim);
            var dashed = SpriteImage(
                rim,
                GlassCardAssets.DashedPath,
                new Color(1f, 0.93f, 0.78f, 0.45f)
            );
            dashed.type = Image.Type.Tiled;
            dashed.raycastTarget = false;
            var plus = Frame.CrossIcon(
                empty,
                new Vector2(0f, 20f * scale),
                Guide.IconSize * scale * EmptyPlus
            );
            plus.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            plus.color = new Color(
                UiPalette.TextSub.r,
                UiPalette.TextSub.g,
                UiPalette.TextSub.b,
                0.8f
            );
            plus.raycastTarget = false;
            var emptyLabel = Label(
                empty,
                "Label",
                CardLoadoutView.EmptyText,
                UiType.Body,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Place(
                (RectTransform)emptyLabel.transform,
                new Vector2(0f, -40f * scale),
                new Vector2(rect.rect.width, 48f)
            );
            card.Empty = empty.gameObject;
            empty.gameObject.SetActive(false);

            // 選んでいるカード（今の枠のカード、セットしている段階）の金の縁と光。
            var selection = Rect("Selection", rect);
            Stretch(selection);
            selection.offsetMin = new Vector2(-10f, -10f);
            selection.offsetMax = new Vector2(10f, 10f);
            var glow = SpriteImage(selection, GlassCardAssets.GlowPath, GlassCardAssets.GoldColor);
            glow.type = Image.Type.Sliced;
            glow.raycastTarget = false;
            glass.Selection = selection.gameObject;
            selection.gameObject.SetActive(false);
            return card;
        }

        // 選ぶ画面：候補は戦闘の手札の約1.45倍で1行5枚（2026-11-04、ユーザーの指示。それまでは約1.8倍で1行4枚）。
        // ツリーは約1.8倍のまま、1段目の真ん中に最初の段階、その下の段に枝1〜3の1段目、さらに下の段に
        // 2段目を並べる（右へ伸ばすと見づらいため、下へ伸ばす。2026-11-03、ユーザーの指示）。どちらも縦にスクロールする。
        private const float ChoiceScale = 1.45f;
        private const float PickerScale = 1.8f;
        private const float TreePad = 48f;

        // 案内人の画面（0）より手前、画面の切り替えの幕（1000）より奥。
        private const int PickerSortingOrder = 10;
        private const float StageColumnGap = 64f;
        private const float StageRowGap = 96f;

        /// <summary>
        /// The picker over the whole panel, header too: the dark veil (a tap on it closes), the
        /// list of choices that scrolls, and the unique skill's tree with its gold lines. Hidden
        /// until a card is pressed.
        /// </summary>
        private static void BuildPicker(RectTransform panel, CardLoadoutView view)
        {
            var elements = (CardElement[])Enum.GetValues(typeof(CardElement));
            view.CardFrames = elements
                .Select(element => (Texture)BattleInspectAssets.Art("CardFrame" + element))
                .ToArray();
            view.ElementArts = elements
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : (Texture)BattleInspectAssets.Art("Element" + element)
                )
                .ToArray();

            var root = Rect("Picker", panel);
            Stretch(root);
            // ヘッダーの金の線（画面より後に描く）も覆うよう、自分のCanvasで手前に出す。
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = PickerSortingOrder;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var picker = root.gameObject.AddComponent<CardLoadoutPicker>();
            picker.Group = root.gameObject.AddComponent<CanvasGroup>();
            var veil = AddImage(root, UiPalette.VeilDeep, true);
            var closers = new List<Button> { Closer(veil) };
            var layer = Layout.Layer(root, "Layer");

            var choices = Rect("Choices", layer);
            Stretch(choices);
            picker.ChoicesRoot = choices.gameObject;
            var viewport = Layout.Viewport(choices);
            viewport.GetComponent<RectMask2D>().softness = new Vector2Int(0, ChangeScreen.ListFade);
            // 候補の間の余白を押しても閉じる。
            closers.Add(Closer(viewport.GetComponent<Image>()));
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            picker.Content = content;
            picker.Scroll = Layout.Scroll(choices, viewport, content);
            var size = BattleCardSize * ChoiceScale;
            var first = BuildCard(
                Layout.Box(content, "Choice0", 0f, 0f, size.x, size.y),
                ChoiceScale
            );
            picker.Cards = new[] { first };
            var line = Layout.Box(content, "Line0", 0f, 0f, 100f, 2f);
            AddImage(line, UiPalette.LineSeparator, false);
            line.gameObject.SetActive(false);
            picker.Lines = new[] { line };
            picker.Remove = BuildPickerRemove(content, size);

            picker.TreeRoot = BuildStages(layer, picker, closers).gameObject;
            picker.Closers = closers.ToArray();

            // 右下の丸いチェック（戦闘のターン終了と同じ形。商会の合成の選ぶ画面と同じ所）。
            var checkSize = ActionButtonAssets.RoundCheckSize;
            var check = Layout.Box(
                layer,
                "Check",
                Layout.Design.x - 48f - checkSize.x,
                Layout.Design.y - 40f - checkSize.y,
                checkSize.x,
                checkSize.y
            );
            picker.Confirm = ActionButtonAssets.RoundCheck(check);
            view.Picker = picker;
            root.gameObject.SetActive(false);
        }

        private static Button Closer(Image target)
        {
            var button = target.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            return button;
        }

        // 一覧の付けられるスキルの最後の「スキルを外す」：点線のカードの真ん中に×、その下に文字。
        private static GlassCard BuildPickerRemove(RectTransform content, Vector2 size)
        {
            var rect = Layout.Box(content, "Remove", 0f, 0f, size.x, size.y);
            var (card, inside) = GlassCardAssets.Build(rect, dashed: true, pattern: false);
            Frame.CrossIcon(inside, new Vector2(0f, 24f), Guide.IconSize);
            var label = Label(
                inside,
                "Label",
                CardLoadoutPresenter.RemoveLabel,
                UiType.Body,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Place((RectTransform)label.transform, new Vector2(0f, -48f), new Vector2(size.x, 40f));
            rect.gameObject.SetActive(false);
            return card;
        }

        /// <summary>
        /// The seven stages laid out as the tree, top down: the first stage in the middle of the
        /// first row, the first step of branches 1 to 3 in the row under it and their second
        /// steps in the row under that, joined by gold lines; the rows scroll.
        /// </summary>
        private static RectTransform BuildStages(
            RectTransform layer,
            CardLoadoutPicker picker,
            List<Button> closers
        )
        {
            var size = BattleCardSize * PickerScale;
            int rows = 3;
            float height = rows * size.y + (rows - 1) * StageRowGap;
            float ColumnLeft(int column) =>
                Layout.Design.x / 2f + (column - 1) * (size.x + StageColumnGap) - size.x / 2f;
            float RowTop(int row) => TreePad + row * (size.y + StageRowGap);

            var tree = Rect("Tree", layer);
            Stretch(tree);
            var viewport = Layout.Viewport(tree);
            viewport.GetComponent<RectMask2D>().softness = new Vector2Int(0, ChangeScreen.ListFade);
            closers.Add(Closer(viewport.GetComponent<Image>()));
            var root = Rect("Content", viewport);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, height + 2 * TreePad);
            picker.TreeScroll = Layout.Scroll(tree, viewport, root);

            // 線を先に作り、カードの下に描く。最初の段階の下から幹を下ろして横に分け、枝の1段目へ下ろす。
            // 枝の1段目の下からは、同じ枝の2段目へまっすぐ下ろす。
            float half = LineWidth / 2f;
            float middle = Layout.Design.x / 2f;
            float split = RowTop(1) - StageRowGap / 2f;
            float Center(int column) => ColumnLeft(column) + size.x / 2f;
            Bar(root, "Root", middle - half, RowTop(0) + size.y, split - RowTop(0) - size.y);
            Bar(
                root,
                "Split",
                Center(0) - half,
                split - half,
                Center(2) - Center(0) + LineWidth,
                horizontal: true
            );
            for (int branch = 0; branch < UniqueSkillTreeShape.Branches; branch++)
            {
                Bar(root, "Branch" + branch, Center(branch) - half, split, RowTop(1) - split);
                Bar(root, "Step" + branch, Center(branch) - half, RowTop(1) + size.y, StageRowGap);
            }

            var stages = new CardLoadoutCard[UniqueSkillTreeShape.Count];
            stages[0] = BuildCard(
                Layout.Box(root, "Stage0", ColumnLeft(1), RowTop(0), size.x, size.y),
                PickerScale
            );
            for (int branch = 0; branch < UniqueSkillTreeShape.Branches; branch++)
            for (int step = 1; step <= 2; step++)
            {
                int stage = UniqueSkillTreeShape.StageAt(branch, step);
                stages[stage] = BuildCard(
                    Layout.Box(
                        root,
                        "Stage" + stage,
                        ColumnLeft(branch),
                        RowTop(step),
                        size.x,
                        size.y
                    ),
                    PickerScale
                );
            }
            picker.Stages = stages;
            tree.gameObject.SetActive(false);
            return tree;
        }

        // --- The awakening tab ----------------------------------------------------------------

        // 覚醒のタブ：右の面の下に、払う物の札（左）と「覚醒」（右）を置き、その上までツリーを並べる。
        private const float AwakenBar = 128f;
        private const float CostWidth = 400f;
        private const float ShardArt = 32f * 2f;

        // 覚醒の演出：画面を覆う幕と、上の中央に浮かぶ今のカード（戦闘のカードを縮めて。商会のスキル覚醒と同じ）。
        private const float OriginTop = 22f;
        private const float OriginScale = 0.55f;

        // スキルの覚醒の形：覚醒する前の1枚と、そこから分かれる2つの枝。
        private static readonly (int Step, bool Last)[] BranchShape =
        {
            (0, false),
            (1, false),
            (1, true),
        };

        /// <summary>
        /// The adventurer's 「覚醒」 tab (doc/features/screens/loadout.md#覚醒): the card skills
        /// tab's six rows on the left; on the right the unique skill's tree, or the custom skill's
        /// card and its two branches, above the shard and runes it takes and 「覚醒」; over all,
        /// hidden until it awakens, the trading house's awakening effect. The first mock person's
        /// first unique skill is baked, so the prefab reads in the editor.
        /// </summary>
        public static GameObject BuildAwakenPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var training = TrainingAssets.CreateData();
            var people = CardLoadoutPresenter.People(PartyFormation.From(party));

            var (view, layer) = Frame.FramedPanel<CardLoadoutView>(safe, "CardAwaken");
            view.Party = party;
            view.Training = training;
            view.ItemArt = Baryonyx.Inventory.Editor.ItemArtAssets.Load();
            view.Guide = guide;
            view.ElementIcons = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(ElementIcon)
                .ToArray();

            RectTransform left,
                right;
            (left, right, view.Swipe) = ChangeScreen.Build(layer, Split, "AwakenPane", "Awaken");
            BuildDeckRows(left, view);
            var awakening = view.gameObject.AddComponent<CardLoadoutAwakening>();
            awakening.View = view;
            view.Awakening = awakening;
            view.Tree = BuildTree(right);
            view.Tree.Scroll.viewport.offsetMin = new Vector2(
                view.Tree.Scroll.viewport.offsetMin.x,
                AwakenBar
            );
            awakening.Branches = BuildTree(right, "Branches", AwakenBar, BranchShape);
            BuildCost(layer, awakening);
            (awakening.Confirm, _) = Guide.ConfirmButton(
                layer,
                CardLoadoutAwakening.AwakenLabel,
                PartySheetAssets.ContentRight - 8f
            );
            view.Sheet = PartySheetAssets.Build(
                layer,
                guide,
                party,
                CardLoadoutSession.AwakenItemKey,
                TrainingAssets.SheetTabs
            );
            BuildScene((RectTransform)view.transform, awakening);

            // 仮データの最初の人の1つ目の固有スキルのツリーで描き、Prefabを開くと停止中でも読めるようにする。
            new CardLoadoutPresenter(
                view,
                people,
                new MockStore(party, training),
                unique: 0
            ).Dispose();
            return view.gameObject;
        }

        // 右の面の左下の払う物の札：覚醒の欠片の絵と「使う数/持っている数」、ルーンのアイコンと払う額
        // （商会のスキル覚醒と同じ形）。高さと下の端は「覚醒」にそろえる。
        private static void BuildCost(RectTransform layer, CardLoadoutAwakening awakening)
        {
            var plaque = Layout.Box(
                layer,
                "Cost",
                Split + 24f,
                Layout.Design.y - Guide.ConfirmBottom - Guide.ConfirmHeight,
                CostWidth,
                Guide.ConfirmHeight
            );
            awakening.CostCard = plaque.gameObject;
            var inside = GlassCardAssets.Plaque(plaque, pattern: false);
            var shard = Rect("Shard", inside);
            Corner(shard, new Vector2(0f, 0.5f), new Vector2(20f, 0f), Vector2.one * ShardArt);
            var image = shard.gameObject.AddComponent<RawImage>();
            image.texture = Baryonyx
                .Inventory.Editor.ItemArtAssets.Load()
                .TextureOf(Enhancement.EnhancementMaterials.Shard);
            image.raycastTarget = false;
            shard.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = 2f;
            awakening.ShardCount = Label(
                inside,
                "ShardCount",
                "1/0",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Corner(
                awakening.ShardCount.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(20f + ShardArt + 10f, 0f),
                new Vector2(120f, 60f)
            );
            awakening.RuneCost = Frame.RuneAmount(
                inside,
                "RuneCost",
                0f,
                0f,
                200f,
                60f,
                UiType.Lead,
                TextAnchor.MiddleLeft,
                48f,
                UiPalette.TextMain
            );
            awakening.RuneCost.text = "0";
            Corner(
                (RectTransform)awakening.RuneCost.transform.parent,
                new Vector2(0f, 0.5f),
                new Vector2(210f, 0f),
                new Vector2(200f, 60f)
            );
            plaque.gameObject.SetActive(false);
        }

        // 覚醒の演出：ヘッダーごと画面を暗く覆う幕、戦闘と同じ覚醒の演出の層、浮かぶカード（商会のスキル覚醒と同じ）。
        private static void BuildScene(RectTransform panel, CardLoadoutAwakening awakening)
        {
            var scene = Rect("AwakenScene", panel);
            Stretch(scene);
            // 演出の間は、後ろの行・タブ・ヘッダーを押せないよう、幕でタップを受ける。
            AddImage(scene, UiPalette.VeilDeep, true);
            awakening.Scene = scene.gameObject;

            var stage = Rect("Stage", scene);
            Stretch(stage);
            var dim = BattleSkillVfxAssets.Dim(stage);
            var back = BattleSkillVfxAssets.Layer("VfxBack", stage);
            var front = BattleSkillVfxAssets.Layer("VfxFront", stage);

            var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Origin", scene);
            var origin = (RectTransform)instance.transform;
            origin.anchorMin = origin.anchorMax = origin.pivot = new Vector2(0.5f, 1f);
            origin.anchoredPosition = new Vector2(0f, -OriginTop);
            origin.localScale = Vector3.one * OriginScale;
            var input =
                instance.GetComponent<Baryonyx.Combat.Presentation.BattleInspectCardInput>();
            if (input != null)
                UnityEngine.Object.DestroyImmediate(input);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            awakening.Origin =
                instance.GetComponent<Baryonyx.Combat.Presentation.BattleInspectCardView>();
            var elements = (CardElement[])Enum.GetValues(typeof(CardElement));
            awakening.CardFrames = elements
                .Select(element => (Texture)BattleInspectAssets.Art("CardFrame" + element))
                .ToArray();
            awakening.ElementArts = elements
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : (Texture)BattleInspectAssets.Art("Element" + element)
                )
                .ToArray();
            awakening.Vfx = BattleSkillVfxAssets.Attach(scene, stage, dim, back, front);
            scene.gameObject.SetActive(false);
        }

        // --- Left: the six cards as rows ---------------------------------------------------

        // 見出し「固有スキル」の下に固有スキル3行、見出し「カスタムスキル」の下にカスタムスキルの枠3行。
        private static void BuildDeckRows(RectTransform left, CardLoadoutView view)
        {
            // 縦のスクロールは、人を替える左右のフリック（GuideSwipe）と同じ左の面で受ける。
            // 同じ物に付けると、どちらにもドラッグが届く。縁は右の一覧と同じく薄く消す。
            var viewport = Layout.Viewport(left);
            viewport.GetComponent<RectMask2D>().softness = new Vector2Int(0, ChangeScreen.ListFade);
            var rows = Rect("Rows", viewport);
            rows.anchorMin = new Vector2(0, 1);
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(0.5f, 1);
            rows.sizeDelta = new Vector2(0, DeckHeight + ChangeScreen.ListFade);
            view.DeckScroll = Layout.Scroll(left, viewport, rows);
            left = rows;

            Title(left, "UniqueTitle", "固有スキル", 0);
            view.UniqueRows = Enumerable
                .Range(0, CardLoadoutPresenter.UniqueCount)
                .Select(i => BuildDeckRow(left, "Unique" + i, DeckHeadingHeight + RowTop(i)))
                .ToArray();
            Title(left, "CustomTitle", "カスタムスキル", CustomTop);
            view.SlotRows = Enumerable
                .Range(0, CardLoadoutRules.Size)
                .Select(i =>
                    BuildDeckRow(left, "Slot" + i, CustomTop + DeckHeadingHeight + RowTop(i))
                )
                .ToArray();
        }

        private static float RowTop(int index) => index * (DeckRowHeight + DeckRowGap);

        /// <summary>
        /// One row of the six: the art, the name with the cost and element at the top right, the
        /// kind and the effect over up to three lines under it, 「空き」 for an empty slot, and a
        /// lock with the level (in place of the effect) for a unique skill not unlocked yet.
        /// Pressing it chooses the slot.
        /// </summary>
        private static CardLoadoutDeckRowWidget BuildDeckRow(
            RectTransform left,
            string name,
            float y
        )
        {
            var (button, selected, row) = GlassCardAssets.Choice(
                Layout.Box(left, name, 0, y, Left.width, DeckRowHeight),
                pattern: false
            );
            var widget = new CardLoadoutDeckRowWidget();
            var card = widget.Card;
            card.Button = button;
            card.Selected = selected;
            GlassCardAssets.AddBack(button.GetComponent<GlassCard>(), vertical: true);

            var content = Rect("Content", row);
            Stretch(content);
            widget.Content = content.gameObject;
            // 挿絵は3倍で行の上下いっぱいに出し、幅は右の一覧と同じにそろえて左右を切る（2026-11-03、ユーザーの指示）。
            card.Art = BuildThumb(content, 12, DeckArtDot, 64 * ArtDot);
            card.Foil = Baryonyx.Editor.UI.CardFoilAssets.Add(card.Art);
            (card.Cost, card.CostDigit, card.Element, _) = BuildCorner(
                content,
                DeckNameTop,
                SlotCornerRight
            );
            card.Name = Label(
                content,
                "Name",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            Guide.Band(
                (RectTransform)card.Name.transform,
                true,
                DeckNameTop,
                NameHeight,
                SlotTextLeft,
                DeckNameRight
            );
            Guide.Shrink(card.Name, 24);
            card.Kind = Label(
                content,
                "Kind",
                "",
                22,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft
            );
            card.Kind.richText = true;
            Guide.Band(
                (RectTransform)card.Kind.transform,
                true,
                DeckKindTop,
                DeckKindHeight,
                SlotTextLeft,
                SlotCornerRight
            );
            card.Description = BuildDescription(
                content,
                DeckTextTop,
                DeckTextHeight,
                SlotTextLeft,
                SlotCornerRight
            );
            Guide.Shrink(card.Description, 18);
            widget.Lock = Frame.IconLock((RectTransform)card.Art.transform);
            widget.Lock.SetActive(false);
            widget.When = Label(
                content,
                "When",
                "",
                26,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            Guide.Band(
                (RectTransform)widget.When.transform,
                true,
                DeckTextTop,
                DeckKindHeight + 4f,
                SlotTextLeft,
                SlotCornerRight
            );

            var empty = Label(
                row,
                "Empty",
                TrainingPresenter.EmptyName,
                32,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)empty.transform);
            widget.Empty = empty.gameObject;
            empty.gameObject.SetActive(false);
            return widget;
        }

        // 左の見出し（金のひし形と明るい金の文字）。
        private static void Title(RectTransform panel, string name, string text, float y)
        {
            var row = Layout.Row(panel, name, 6, y, Left.width - 12, 36, 14, TextAnchor.LowerLeft);
            Label(row, "Title", text, 26, UiPalette.Gold, TextAlignmentOptions.BottomLeft);
            Frame.HeadingRow(row, 26);
        }

        // --- Right: the evolution tree of the unique skill chosen on the left ---------------

        /// <summary>
        /// The tree in the right frame, hidden until a unique skill is chosen: the heading, then
        /// the seven stages one to a row in a list that scrolls — the first stage, then for each
        /// branch its first step indented once and its second step indented twice. A line runs
        /// down before each indent and turns into the stage's card.
        /// </summary>
        private static CardLoadoutTreeWidget BuildTree(RectTransform panel) =>
            BuildTree(
                panel,
                "Tree",
                0f,
                Enumerable
                    .Range(0, UniqueSkillTreeShape.Count)
                    .Select(stage =>
                        (
                            UniqueSkillTreeShape.Step(stage),
                            UniqueSkillTreeShape.Branch(stage) == UniqueSkillTreeShape.Branches - 1
                        )
                    )
                    .ToArray()
            );

        // 段階を shape の順に1行ずつ並べたツリー。Step は字下げの段（0が最初の段階）、Last は最後の枝か。
        // bottom は、面の下に空けておく高さ（覚醒のタブの払う物の札と「覚醒」）。
        private static CardLoadoutTreeWidget BuildTree(
            RectTransform panel,
            string name,
            float bottom,
            (int Step, bool Last)[] shape
        )
        {
            var root = Layout.Box(panel, name, 0, 0, Right.width, Right.height);
            var tree = new CardLoadoutTreeWidget { Root = root.gameObject };
            tree.Title = Frame.Heading(
                root,
                "TreeTitle",
                "",
                24,
                TreeTitleTop,
                Right.width - 48,
                44,
                30
            );

            // 右の面のスクロールは一覧が使うので、ツリーは自分の面でスクロールする。
            var (viewport, content) = ChangeScreen.List(root, TreeListTop);
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, bottom);
            content.GetComponent<VerticalLayoutGroup>().spacing = RowSpacing;
            tree.Scroll = Layout.Scroll(root, viewport, content);

            tree.Stages = shape
                .Select((stage, i) => BuildStageRow(content, "Stage" + i, stage.Step, stage.Last))
                .ToArray();
            root.gameObject.SetActive(false);
            return tree;
        }

        // 段階1行。線を先に作り、カードの下に描く。
        private static CardLoadoutStageWidget BuildStageRow(
            RectTransform content,
            string name,
            int step,
            bool lastBranch
        )
        {
            var row = Layout.ListItem(content, name, SlotHeight);
            float middle = (SlotHeight - LineWidth) / 2f;
            if (step == 1)
            {
                // 最初の段階から下ろす幹。最後の枝ならここで止める。
                Bar(
                    row,
                    "Trunk",
                    Line(0),
                    -RowSpacing,
                    lastBranch ? middle + RowSpacing : SlotHeight + RowSpacing
                );
                Bar(
                    row,
                    "Branch",
                    Line(0),
                    middle,
                    StageIndent - Line(0),
                    LineWidth,
                    horizontal: true
                );
            }
            else if (step == 2)
            {
                // 最後でない枝の2段目の横も、幹を次の枝まで通す。
                if (!lastBranch)
                    Bar(row, "Trunk", Line(0), -RowSpacing, SlotHeight + RowSpacing);
                Bar(row, "Step", Line(1), -RowSpacing, middle + RowSpacing);
                Bar(
                    row,
                    "Branch",
                    Line(1),
                    middle,
                    StageIndent * 2 - Line(1),
                    LineWidth,
                    horizontal: true
                );
            }
            float x = step * StageIndent;
            var rect = Layout.Box(row, "Card", x, 0, Right.width - 48 - x, SlotHeight);
            return BuildStage(rect);
        }

        // 字下げ depth 段目の手前の、縦の線の左端。
        private static float Line(int depth) =>
            depth * StageIndent + (StageIndent - LineWidth) / 2f;

        // 縦の線（x, y から高さ length）。horizontal なら横の線（x, y から幅 length、太さ height）。
        private static void Bar(
            RectTransform parent,
            string name,
            float x,
            float y,
            float length,
            float height = LineWidth,
            bool horizontal = false
        ) =>
            AddImage(
                horizontal
                    ? Layout.Box(parent, name, x, y, length, height)
                    : Layout.Box(parent, name, x, y, LineWidth, length),
                LineColor,
                false
            );

        /// <summary>
        /// One stage: a card like the list's row, its shadow when not acquired, a lock for a
        /// locked unique skill, and 「E」 (the stage set) or the level to unlock at the top right,
        /// left of the element and the cost.
        /// </summary>
        private static CardLoadoutStageWidget BuildStage(RectTransform rect)
        {
            var (button, selected, row) = GlassCardAssets.Choice(rect, pattern: false);
            var widget = new CardLoadoutStageWidget();
            var card = widget.Card;
            card.Button = button;
            card.Selected = selected;

            var content = Rect("Content", row);
            Stretch(content);
            widget.Content = content.gameObject;
            card.Art = BuildThumb(content, 12);
            card.Foil = Baryonyx.Editor.UI.CardFoilAssets.Add(card.Art);
            (card.Cost, card.CostDigit, card.Element, widget.Mark) = BuildCorner(
                content,
                8,
                StageCornerRight,
                mark: true
            );
            (card.Name, card.Kind) = BuildNameLine(content, 8, SlotTextLeft, StageNameRight);
            card.Description = BuildDescription(content, 56, 72, SlotTextLeft, 24);
            widget.Lock = Frame.IconLock((RectTransform)card.Art.transform);
            widget.Lock.SetActive(false);

            // 取得していない段階は、カードの中央に「？」の封印を置き、左右へ鎖を渡した封じた札にする。
            var shadow = Rect("Shadow", row);
            Stretch(shadow);
            widget.Shadow = shadow.gameObject;
            var chain = SmoothTexture($"{ArtFolder}/StageSealChain.png");
            var chainSize = new Vector2(ChainWidth, ChainWidth * chain.height / chain.width);
            // 鎖の内側の端を封印の縁の下へ少し差し込む。右の鎖は左右を反転して、外側の端を薄くする。
            float chainX = (SealSize - 16) / 2f + chainSize.x / 2f;
            Seal(shadow, "ChainLeft", chain, new Vector2(-chainX, 0), chainSize, 1);
            Seal(shadow, "ChainRight", chain, new Vector2(chainX, 0), chainSize, -1);
            Seal(
                shadow,
                "Seal",
                SmoothTexture($"{ArtFolder}/StageSeal.png"),
                Vector2.zero,
                Vector2.one * SealSize,
                1
            );
            shadow.gameObject.SetActive(false);
            return widget;
        }

        // 封印の絵1枚。カードの中央から offset の位置に置く。
        private static void Seal(
            RectTransform parent,
            string name,
            Texture texture,
            Vector2 offset,
            Vector2 size,
            float flip
        )
        {
            var rect = Rect(name, parent);
            Place(rect, offset, size);
            rect.localScale = new Vector3(flip, 1, 1);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }

        // ドット絵ではない生成画像を、透明を保って滑らかに縮める設定で読み込む。
        private static Texture2D SmoothTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // --- Pieces shared by the slots and the rows ----------------------------------------

        // The card's illustration (64×58 dots) at dot times, 2x by default. With a width, only that
        // much of its middle shows, so a larger dot fills a taller row without widening it. Its cost
        // and element go at the row's top right (BuildCorner).
        internal static RawImage BuildThumb(
            RectTransform parent,
            float left,
            float dot = ArtDot,
            float width = 0f
        )
        {
            var size = new Vector2(64, 58) * dot;
            float shown = width > 0f ? Mathf.Min(width / size.x, 1f) : 1f;
            var thumb = Rect("Art", parent);
            Corner(
                thumb,
                new Vector2(0, 0.5f),
                new Vector2(left, 0),
                new Vector2(size.x * shown, size.y)
            );
            var art = thumb.gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            art.uvRect = new Rect((1f - shown) / 2f, 0f, shown, 1f);
            thumb.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = dot;
            return art;
        }

        /// <summary>
        /// The marks at a row's top right, centred on the name line <paramref name="top"/> down
        /// and <paramref name="right"/> in from the edge: from the corner leftwards the cost plate
        /// (the battle card's 11×11 dots, unless <paramref name="cost"/> is false), the element
        /// (12×12 dots), both at 3x like the equipment's attributes, then with
        /// <paramref name="mark"/> the mark (「E」「所持 3」「Lv 20で解放」). Hidden ones close up.
        /// </summary>
        internal static (GameObject Cost, RawImage Digit, Image Element, TMP_Text Mark) BuildCorner(
            RectTransform parent,
            float top,
            float right,
            bool cost = true,
            bool mark = false
        )
        {
            var corner = Rect("Corner", parent);
            Corner(
                corner,
                Vector2.one,
                new Vector2(-right, -top),
                new Vector2(CornerWidth, NameHeight)
            );
            var group = corner.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleRight;
            group.spacing = ElementIconRowAssets.Gap;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;

            TMP_Text text = null;
            if (mark)
            {
                text = Label(
                    corner,
                    "Mark",
                    "",
                    CornerMark,
                    UiPalette.Gold,
                    TextAlignmentOptions.MidlineRight
                );
                text.gameObject.SetActive(false);
            }
            var element = Fixed(corner, "Element", Vector2.one * ElementIconRowAssets.Size)
                .gameObject.AddComponent<Image>();
            element.preserveAspect = true;
            element.raycastTarget = false;
            if (!cost)
                return (null, null, element, text);

            var frame = DeckCardAssets.CardArt("CardCostFrame");
            var digits = DeckCardAssets.CardArt("CardCostDigits");
            var plate = Fixed(corner, "Cost", new Vector2(frame.width, frame.height) * CornerDot);
            var back = plate.gameObject.AddComponent<RawImage>();
            back.texture = frame;
            back.raycastTarget = false;
            var digit = Rect("Digit", plate);
            Place(digit, Vector2.zero, new Vector2(digits.width / 10f, digits.height) * CornerDot);
            var number = digit.gameObject.AddComponent<RawImage>();
            number.texture = digits;
            number.raycastTarget = false;
            return (plate.gameObject, number, element, text);
        }

        // 右上の印の並び（レイアウトグループ）の中で、決まった大きさを保つ1つ。
        private static RectTransform Fixed(RectTransform parent, string name, Vector2 size)
        {
            var rect = Rect(name, parent);
            rect.sizeDelta = size;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = size.x;
            layout.minHeight = layout.preferredHeight = size.y;
            return rect;
        }

        // The name, then the kind line ("攻撃・敵単体") in its colours, on one baseline.
        internal static (TMP_Text Name, TMP_Text Kind) BuildNameLine(
            RectTransform parent,
            float inset,
            float left,
            float right
        )
        {
            var line = Rect("NameLine", parent);
            Guide.Band(line, top: true, inset, 46, left, right);
            var group = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.LowerLeft;
            group.spacing = 16;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            var name = Label(
                line,
                "Name",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            var kind = Label(
                line,
                "Kind",
                "",
                22,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            kind.richText = true;
            return (name, kind);
        }

        // The description, with the numbers in colour, over up to two lines.
        internal static TMP_Text BuildDescription(
            RectTransform parent,
            float inset,
            float height,
            float left,
            float right
        )
        {
            var description = Label(
                parent,
                "Description",
                "",
                24,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            description.richText = true;
            description.textWrappingMode = TextWrappingModes.Normal;
            Guide.Band((RectTransform)description.transform, top: true, inset, height, left, right);
            return description;
        }

        private static Sprite ElementIcon(CardElement element) =>
            element == CardElement.None
                ? null
                : Guide.Icon($"{PartyAssets.AttributeArtFolder}/Element{element}.aseprite");

        /// <summary>
        /// The mock data only, so the prefab never bakes what Play Mode or a test left in the
        /// sessions: the mock cards, the stats at the mock level, no ACT, the first person.
        /// </summary>
        private sealed class MockStore : ICardLoadoutStore
        {
            private readonly PartyMockData party;
            private readonly TrainingMockData training;

            public MockStore(PartyMockData party, TrainingMockData training)
            {
                this.party = party;
                this.training = training;
            }

            // 仮データのカードは、スキルIDのまま枠に入れる（持ち物は読まない）。
            public IReadOnlyList<string> CardsOf(string id) =>
                party.Find(id)?.Cards.Select(card => card.Skill).ToArray() ?? Array.Empty<string>();

            public void SetCards(string id, IReadOnlyList<string> skills) { }

            public string Selected
            {
                get => null;
                set { }
            }

            // 仮データの進み具合だけを使い、アプリを動かしている間の進み具合は読まない。
            public IReadOnlyList<CardLoadoutUnique> UniquesOf(string id) =>
                CardLoadoutSessionStore.Uniques(
                    training,
                    id,
                    party.Find(id)?.Level ?? 1,
                    (_, _, mock) => mock ?? new UniqueSkillProgress()
                );

            public bool SetUniqueStage(string id, int index, int stage) => false;
        }
    }
}
