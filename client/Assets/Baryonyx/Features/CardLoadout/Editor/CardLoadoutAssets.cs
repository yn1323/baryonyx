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
using ChangeScreen = Baryonyx.Party.Editor.PartyChangeScreenAssets;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using GlassCardAssets = Baryonyx.UI.Editor.GlassCardAssets;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.CardLoadout.Editor
{
    /// <summary>
    /// The formation's card skills change screen, laid out like the equipment's
    /// (EquipmentAssets): the person, the stats and the six cards (or, in the selection mode,
    /// six tiles over the chosen card shown large) on the left, the tabs and the list on the
    /// right, and the deck's six cards over the whole screen. The formation's generator calls
    /// <see cref="BuildPanel"/> for its card skills item, so the person, the cards, the stats and
    /// a row for every card are baked into the formation prefab and read in the editor without
    /// Play Mode. The cards are the combat's card skills; the people, levels and
    /// stats are the party's and the training's mock data (doc/features/party.md).
    /// </summary>
    public static class CardLoadoutAssets
    {
        // カードの挿絵（64×58）は2倍で出す。
        private const float ArtDot = CardLoadoutView.ArtDot;

        // 枠つきの表示（GuideFramedPanel）で、左の面に人・ステータス・枠、金の2本線の右の面に
        // タブとカードの一覧を置く。装備と同じ配置（PartyChangeScreenAssets）。設計座標、左上から。
        private static readonly Rect Left = ChangeScreen.Left;
        private static readonly Rect Right = ChangeScreen.Right;

        // 左は上から人・ステータス、その下に6枚の一覧か選択モード。ステータスと線の位置は装備と同じ。
        // 6枚の一覧：固有スキル3枚とカスタムスキル3枚を、戦闘のカードの小さなカード（3倍）で並べる。
        private const float SmallDot = 3f;
        private const float UniqueTop = 324f;
        private const float CustomTop = 608f;
        private const float CardGap = 16f;

        // 選択モード：左に6枚の札を2列（固有スキル・カスタムスキル）×3段、右に選んでいる枠のカード（戦闘と同じ5倍）。
        // 札は押せる範囲の決まり（128四方）の大きさにし、挿絵の中央56×56ドットを2倍で出す。
        private const float SelectionTop = 290f;
        private const float TileTop = 368f;
        private const float TileSize = 128f;
        private const float TileGap = 8f;
        private const float TileArtDots = 56f;
        private const float ChosenGap = 24f;
        private const float ChosenDot = 5f;

        // 今のデッキの全画面：上の帯の下に「〇〇のデッキ」と×、戦闘のカード（4倍）を固有スキルと
        // カスタムスキルの2段に並べ、段の見出しはカードの左に置く。上の帯は全画面の上に残る。
        private const float DeckDot = 4f;
        private const float DeckGap = 32f;
        private const float DeckTop = Frame.HeaderHeight + 14f;
        private const float DeckFirstTop = DeckTop + 72f;
        private const float DeckRowGap = 22f;
        private const float DeckLabelWidth = 240f;
        private static readonly Color DeckDim = new(0.01f, 0.015f, 0.04f, 0.93f);

        private const float SlotHeight = 136f;
        private const float SlotTextLeft = 156f;
        private const float RowHeight = 140f;
        private const float RowTextLeft = 160f;

        // 右の上のタブの下の、付けられる属性の行。
        private const float UsableTop = 92f;
        private const float ListTop = 152f;

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
        private static readonly Color BadgeColor = new(0.02f, 0.03f, 0.06f, 0.85f);

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

            RectTransform left,
                right;
            (left, right, view.Swipe, view.Person) = ChangeScreen.Build(
                layer,
                "CardsPane",
                "Cards",
                people.FirstOrDefault()
            );
            // ステータスは人のすぐ下に置き、その下に線を引いて枠と分ける（装備と同じ）。
            view.Stats = ChangeScreen.Stats(left);
            view.CardFrames = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(element => (Texture)DeckCardAssets.CardArt($"CardFrame{element}"))
                .ToArray();
            BuildOverview(left, view);
            BuildSelection(left, view);
            BuildCards(right, view);
            view.Tree = BuildTree(right);
            BuildDeck((RectTransform)view.transform, view);
            // 全画面は、画面のほかの部品より手前に描く。
            view.Deck.transform.SetAsLastSibling();

            // 仮データの最初の人で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            // 先に1つ目の固有スキルの選択モードと進化ツリーを作り込み（停止中の表示の切り替えで見せる）、
            // そのあと開いたときの表示（6枚の一覧）に戻す。
            var store = new MockStore(party, training);
            new CardLoadoutPresenter(view, people, store, unique: 0).Dispose();
            new CardLoadoutPresenter(view, people, store).Dispose();
            return view.gameObject;
        }

        // --- Left: the six cards, or the selection mode ------------------------------------

        // 開いたときの6枚の一覧。固有スキルの見出しの右端に「今のデッキ」。
        private static void BuildOverview(RectTransform left, CardLoadoutView view)
        {
            var root = Layout.Box(left, "Overview", 0, 0, Left.width, Left.height);
            view.Overview = root.gameObject;
            Title(root, "UniqueTitle", "固有スキル", UniqueTop - 40);
            view.DeckButton = SmallButton(
                root,
                "DeckButton",
                "今のデッキ",
                Left.width - 24 - 200,
                UniqueTop - 52,
                200
            );
            view.UniqueCards = Enumerable
                .Range(0, CardLoadoutPresenter.UniqueCount)
                .Select(i => SmallCard(root, "Unique" + i, i, UniqueTop))
                .ToArray();
            Title(root, "CustomTitle", "カスタムスキル", CustomTop - 40);
            view.SlotCards = Enumerable
                .Range(0, CardLoadoutRules.Size)
                .Select(i => SmallCard(root, "Slot" + i, i, CustomTop))
                .ToArray();
        }

        private static TrainingCardWidget SmallCard(
            RectTransform root,
            string name,
            int index,
            float y
        )
        {
            var size = DeckCardAssets.Size(SmallDot, small: true);
            float x = 24 + index * (size.x + CardGap);
            return DeckCardAssets.Build(root, name, x, y, SmallDot, button: true, small: true);
        }

        // 選択モード：右上に「戻る」、左に6枚の札（左の列が固有スキル、右の列がカスタムスキル）、
        // 右に選んでいる枠のカード。見出しや列の名前は付けず、並びで伝える。
        private static void BuildSelection(RectTransform left, CardLoadoutView view)
        {
            var root = Layout.Box(left, "Selection", 0, 0, Left.width, Left.height);
            view.Selection = root.gameObject;
            view.Back = SmallButton(
                root,
                "Back",
                "戻る",
                Left.width - 24 - 160,
                SelectionTop - 12,
                160,
                arrow: true
            );
            const float x = 24f;
            int uniques = CardLoadoutPresenter.UniqueCount;
            view.Tiles = Enumerable
                .Range(0, uniques + CardLoadoutRules.Size)
                .Select(i =>
                {
                    int column = i < uniques ? 0 : 1;
                    int row = i < uniques ? i : i - uniques;
                    return BuildTile(
                        root,
                        i,
                        x + column * (TileSize + TileGap),
                        TileTop + row * (TileSize + TileGap)
                    );
                })
                .ToArray();
            view.Chosen = DeckCardAssets.Build(
                root,
                "Chosen",
                x + TileSize * 2 + TileGap + ChosenGap,
                TileTop - 34,
                ChosenDot,
                button: false
            );
            root.gameObject.SetActive(false);
        }

        // 札1枚：すりガラスの札に、挿絵の中央を2倍で出す。選ぶと金の枠で光る。
        private static CardLoadoutTileWidget BuildTile(
            RectTransform root,
            int index,
            float x,
            float y
        )
        {
            var (button, selected, inside) = GlassCardAssets.Choice(
                Layout.Box(root, "Tile" + index, x, y, TileSize, TileSize),
                pattern: false
            );
            var widget = new CardLoadoutTileWidget { Button = button, Selected = selected };
            var art = Rect("Art", inside);
            Place(art, Vector2.zero, Vector2.one * TileArtDots * ArtDot);
            widget.Art = art.gameObject.AddComponent<RawImage>();
            widget.Art.raycastTarget = false;
            widget.Art.uvRect = new Rect(
                (64 - TileArtDots) / 2f / 64f,
                (58 - TileArtDots) / 2f / 58f,
                TileArtDots / 64f,
                TileArtDots / 58f
            );
            widget.Lock = Frame.IconLock(art);
            widget.Lock.SetActive(false);
            var empty = Label(
                inside,
                "Empty",
                "＋",
                40,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)empty.transform);
            widget.Empty = empty.gameObject;
            empty.gameObject.SetActive(false);
            return widget;
        }

        // 文字の小さなボタン（「今のデッキ」「戻る」）。模様のないすりガラスの札にする。
        // arrow なら、文字の左に左向きの金の矢印を置く。
        private static Button SmallButton(
            RectTransform parent,
            string name,
            string text,
            float x,
            float y,
            float width,
            bool arrow = false
        )
        {
            var (button, _, inside) = GlassCardAssets.Choice(
                Layout.Box(parent, name, x, y, width, 52),
                pattern: false
            );
            var row = Layout.Row(inside, "Row", 0, 0, width, 52, 10, TextAnchor.MiddleCenter);
            if (arrow)
            {
                var cell = Rect("Arrow", row);
                Layout.Fit(cell, 5 * 3);
                var image = Rect("Image", cell);
                Place(image, Vector2.zero, new Vector2(5, 9) * 3);
                image.localScale = new Vector3(-1, 1, 1);
                SpriteImage(image, Guide.ArrowPath, UiPalette.Gold);
            }
            Layout.Fit(
                Label(row, "Label", text, 24, UiPalette.TextMain, TextAlignmentOptions.Midline),
                0
            );
            return button;
        }

        // --- The deck over the whole screen ------------------------------------------------

        /// <summary>
        /// The deck's six cards over everything, hidden until 「今のデッキ」 is pressed: a dim
        /// that takes every tap, 「〇〇のデッキ」 with × at the top, then the three unique skills
        /// and below them the three custom skills, as large as the battle's cards nearly.
        /// </summary>
        private static void BuildDeck(RectTransform root, CardLoadoutView view)
        {
            var deck = Rect("Deck", root);
            Stretch(deck);
            view.Deck = deck.gameObject;
            // 後ろの画面を押せないよう、Safe Areaの外まで暗くする。
            var dim = Rect("Dim", deck);
            Stretch(dim);
            dim.offsetMin = new Vector2(-400, -400);
            dim.offsetMax = new Vector2(400, 400);
            AddImage(dim, DeckDim, true);

            var layer = Layout.Layer(deck, "Layout");
            var size = DeckCardAssets.Size(DeckDot, small: false);
            int count = CardLoadoutRules.Size;
            float cardsWidth = count * size.x + (count - 1) * DeckGap;
            float left = (1920 - DeckLabelWidth - cardsWidth) / 2f;
            float x = left + DeckLabelWidth;
            float secondTop = DeckFirstTop + size.y + DeckRowGap;
            view.DeckTitle = Frame.Heading(layer, "Title", "", left - 4, DeckTop, 900, 56, 40);

            var close = Layout.Box(layer, "Close", x + cardsWidth - 72, DeckTop - 8, 72, 72);
            var hit = AddImage(close, Color.clear, true);
            var icon = Rect("Icon", close);
            Place(icon, Vector2.zero, Vector2.one * 48);
            SpriteImage(icon, Guide.IconClosePath, Color.white);
            view.DeckClose = Guide.AddButton(close, hit);

            float label = DeckLabelWidth - 16;
            Frame.Heading(
                layer,
                "UniqueTitle",
                "固有スキル",
                left - 4,
                DeckFirstTop,
                label,
                36,
                26
            );
            Frame.Heading(
                layer,
                "CustomTitle",
                "カスタムスキル",
                left - 4,
                secondTop,
                label,
                36,
                26
            );
            view.DeckUniques = Enumerable
                .Range(0, CardLoadoutPresenter.UniqueCount)
                .Select(i =>
                    DeckCard(layer, "Unique" + i, x + i * (size.x + DeckGap), DeckFirstTop)
                )
                .ToArray();
            view.DeckSlots = Enumerable
                .Range(0, count)
                .Select(i => DeckCard(layer, "Slot" + i, x + i * (size.x + DeckGap), secondTop))
                .ToArray();
            deck.gameObject.SetActive(false);
        }

        private static TrainingCardWidget DeckCard(
            RectTransform layer,
            string name,
            float x,
            float y
        ) => DeckCardAssets.Build(layer, name, x, y, DeckDot, button: false);

        // 左の見出し（金のひし形と明るい金の文字）。
        private static void Title(RectTransform panel, string name, string text, float y)
        {
            var row = Layout.Row(panel, name, 30, y, Left.width - 60, 30, 14, TextAnchor.LowerLeft);
            Label(row, "Title", text, 24, UiPalette.Gold, TextAlignmentOptions.BottomLeft);
            Frame.HeadingRow(row, 24);
        }

        // カードの右上の金の小さな文字（「Lv 20で解放」「セット中」）。
        private static TMP_Text BuildMark(RectTransform row)
        {
            var mark = Label(row, "Mark", "", 22, UiPalette.Gold, TextAlignmentOptions.TopRight);
            Guide.Band((RectTransform)mark.transform, true, 12, 30, SlotTextLeft, 20);
            return mark;
        }

        // --- Right: the evolution tree of the unique skill chosen on the left ---------------

        /// <summary>
        /// The tree in the right frame, hidden until a unique skill is chosen: the heading, then
        /// the seven stages one to a row in a list that scrolls — the first stage, then for each
        /// branch its first step indented once and its second step indented twice. A line runs
        /// down before each indent and turns into the stage's card.
        /// </summary>
        private static CardLoadoutTreeWidget BuildTree(RectTransform panel)
        {
            var root = Layout.Box(panel, "Tree", 0, 0, Right.width, Right.height);
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
            content.GetComponent<VerticalLayoutGroup>().spacing = RowSpacing;
            tree.Scroll = Layout.Scroll(root, viewport, content);

            tree.Stages = Enumerable
                .Range(0, UniqueSkillTreeShape.Count)
                .Select(stage => BuildStageRow(content, stage))
                .ToArray();
            root.gameObject.SetActive(false);
            return tree;
        }

        // 段階1行。線を先に作り、カードの下に描く。
        private static CardLoadoutStageWidget BuildStageRow(RectTransform content, int stage)
        {
            var row = Layout.ListItem(content, "Stage" + stage, SlotHeight);
            int step = UniqueSkillTreeShape.Step(stage);
            float middle = (SlotHeight - LineWidth) / 2f;
            bool lastBranch =
                UniqueSkillTreeShape.Branch(stage) == UniqueSkillTreeShape.Branches - 1;
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
        /// One stage: a card like a slot's, its shadow when not acquired, a lock for a locked
        /// unique skill, and 「セット中」 at the top right.
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
            (card.Art, card.Cost, card.Element) = BuildThumb(content, 12);
            card.Foil = Baryonyx.Editor.UI.CardFoilAssets.Add(card.Art);
            (card.Name, card.Kind) = BuildNameLine(content, 8, SlotTextLeft, 24);
            card.Description = BuildDescription(content, 56, 72, SlotTextLeft, 24);
            widget.Lock = Frame.IconLock((RectTransform)card.Art.transform);
            widget.Lock.SetActive(false);
            widget.Mark = BuildMark(content);

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

        // --- Right: the skill and item tabs, then every card the person can set -------------

        private static void BuildCards(RectTransform panel, CardLoadoutView view)
        {
            // 上に、面の幅いっぱいのスキル・アイテムのタブ（装備のタブと同じ形）。
            view.Tabs = ChangeScreen.Tabs(panel, "スキル", "アイテム");

            // タブの下に、その人が付けられる属性のアイコンだけを並べる。属性のないカードは誰でも
            // 付けられる。見出しや「属性なし」の文字は付けず、下の一覧の属性のアイコンと見比べさせる。
            var usable = Layout.Box(panel, "Usable", 36, UsableTop, Right.width - 72, 48);
            view.UsableLine = usable.gameObject;
            var group = usable.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 8;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            view.Usable = Enumerable
                .Range(0, 2)
                .Select(i =>
                {
                    var icon = Rect("Element" + i, usable);
                    var size = icon.gameObject.AddComponent<LayoutElement>();
                    size.minWidth = size.preferredWidth = 12 * Guide.DotScale;
                    size.minHeight = size.preferredHeight = 12 * Guide.DotScale;
                    var image = icon.gameObject.AddComponent<Image>();
                    image.raycastTarget = false;
                    return image;
                })
                .ToArray();

            var (viewport, content) = ChangeScreen.List(panel, ListTop);

            // One row per card in the catalog's order; the view shows only the cards the person
            // can set, by cost.
            view.Rows = CardSkills.All.Select(card => BuildRow(content, card, view)).ToArray();
            view.Remove = BuildRemove(content);

            view.List = Layout.Scroll(panel, viewport, content);

            // 読み込み中と読めなかったとき、アイテムのタブを選んでいるときだけ出す文字。
            view.Status = ChangeScreen.Status(panel);
        }

        // 一覧の最後の「スキルを外す」。点線のカードに×と文字（パーティの「パーティから外す」と同じ形）。
        private static Button BuildRemove(RectTransform content)
        {
            var row = Layout.ListItem(content, "Remove", RowHeight);
            var (button, _, inside) = GlassCardAssets.Choice(row, pattern: false, dashed: true);
            var icon = Layout.Box(
                inside,
                "Icon",
                (RowTextLeft - Guide.IconSize) / 2f,
                (RowHeight - Guide.IconSize) / 2f,
                Guide.IconSize,
                Guide.IconSize
            );
            SpriteImage(icon, Guide.IconClosePath, Color.white);
            Layout.Text(
                inside,
                "Label",
                CardLoadoutPresenter.RemoveLabel,
                40,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                RowTextLeft,
                0f,
                400f,
                RowHeight
            );
            row.gameObject.SetActive(false);
            return button;
        }

        private static CardLoadoutRowWidget BuildRow(
            RectTransform content,
            CardSkill card,
            CardLoadoutView view
        )
        {
            var row = Layout.ListItem(content, "Card_" + card.Id, RowHeight);
            var widget = new CardLoadoutRowWidget { Id = card.Id };
            var (button, _, inside) = GlassCardAssets.Choice(row, pattern: false);
            widget.Button = button;
            row = inside;
            var (art, cost, element) = BuildThumb(row, 12);
            art.texture = ArtAssets.LoadTexture(
                $"{BattleInspectAssets.ArtFolder}/Card{card.Id}.aseprite"
            );
            cost.text = card.Cost.ToString();
            element.sprite = view.IconOf(card.Element);
            element.enabled = element.sprite != null;
            widget.Art = art;
            widget.Cost = cost;
            widget.Element = element;
            widget.Foil = Baryonyx.Editor.UI.CardFoilAssets.Add(art, card.Quality);

            var (name, kind) = BuildNameLine(row, 12, RowTextLeft, 150);
            name.text = card.Name;
            kind.text = CardText.KindLine(card);
            widget.Name = name;
            widget.Kind = kind;
            widget.Description = BuildDescription(row, 64, 64, RowTextLeft, 24);
            widget.Mark = Label(row, "Mark", "", 24, UiPalette.Gold, TextAlignmentOptions.TopRight);
            Guide.Band((RectTransform)widget.Mark.transform, top: true, 20, 32, RowTextLeft, 28);
            widget.Mark.gameObject.SetActive(false);
            return widget;
        }

        // --- Pieces shared by the slots and the rows ----------------------------------------

        // The card's illustration at 2x, with its cost and element over the top corners.
        private static (RawImage Art, TMP_Text Cost, Image Element) BuildThumb(
            RectTransform parent,
            float left
        )
        {
            var thumb = Rect("Art", parent);
            Corner(thumb, new Vector2(0, 0.5f), new Vector2(left, 0), new Vector2(64, 58) * ArtDot);
            var art = thumb.gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            thumb.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = ArtDot;

            var badge = Rect("Cost", thumb);
            Corner(badge, new Vector2(0, 1), new Vector2(2, -2), new Vector2(34, 34));
            AddImage(badge, BadgeColor, false);
            var cost = Label(badge, "Value", "", 28, UiPalette.Gold, TextAlignmentOptions.Center);
            Stretch((RectTransform)cost.transform);

            var icon = Rect("Element", thumb);
            Corner(icon, new Vector2(1, 1), new Vector2(-2, -2), Vector2.one * 12 * ArtDot);
            var element = icon.gameObject.AddComponent<Image>();
            element.raycastTarget = false;
            return (art, cost, element);
        }

        // The name, then the kind line ("攻撃・敵単体") in its colours, on one baseline.
        private static (TMP_Text Name, TMP_Text Kind) BuildNameLine(
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
        private static TMP_Text BuildDescription(
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
