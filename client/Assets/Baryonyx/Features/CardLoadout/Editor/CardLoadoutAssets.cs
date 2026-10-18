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
    /// (EquipmentAssets): the person, the stats and the slots on the left, the tabs and the list
    /// on the right. The formation's generator calls <see cref="BuildPanel"/> for its card
    /// skills item, so the person, the two slots, the stats and a row for every card are baked
    /// into the formation prefab and read in the editor without Play Mode. The cards are the combat's card skills; the people, levels and
    /// stats are the party's and the training's mock data (doc/features/party.md).
    /// </summary>
    public static class CardLoadoutAssets
    {
        // カードの挿絵（64×58）は2倍で出す。
        private const float ArtDot = 2f;

        // 枠つきの表示（GuideFramedPanel）で、左の面に人・ステータス・枠、金の2本線の右の面に
        // タブとカードの一覧を置く。装備と同じ配置（PartyChangeScreenAssets）。設計座標、左上から。
        private static readonly Rect Left = ChangeScreen.Left;
        private static readonly Rect Right = ChangeScreen.Right;

        // 左は上から人・ステータス・固有スキル・カスタムスキル。ステータスと線の位置は装備と同じ。
        // 固有スキルもカスタムスキルと同じ形のカードにする。
        private const float SlotTop = 318f;
        private const float SlotHeight = 136f;
        private const float SlotStep = 144f;

        // 固有スキル2つの下で、見出しの前に空ける間。
        private const float GroupGap = 40f;
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
            Title(left, "UniqueTitle", "固有スキル", SlotTop - 34);
            view.Uniques = Enumerable
                .Range(0, CardLoadoutPresenter.UniqueCount)
                .Select(i => BuildUnique(left, i, SlotTop + i * SlotStep))
                .ToArray();
            float customTop = SlotTop + CardLoadoutPresenter.UniqueCount * SlotStep + GroupGap;
            Title(left, "CustomTitle", "カスタムスキル", customTop - 34);
            view.Slots = Enumerable
                .Range(0, CardLoadoutRules.Size)
                .Select(i => BuildSlot(left, "Slot" + i, customTop + i * SlotStep))
                .ToArray();
            BuildCards(right, view);
            view.Tree = BuildTree(right);

            // 仮データの最初の人で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            // 先に1つ目の固有スキルの進化ツリーを作り込み（停止中の表示の切り替えで見せる）、
            // そのあと開いたときの表示（1枚目のカスタムスキルを選んだ状態）に戻す。
            var store = new MockStore(party, training);
            new CardLoadoutPresenter(view, people, store, unique: 0).Dispose();
            new CardLoadoutPresenter(view, people, store).Dispose();
            return view.gameObject;
        }

        // --- Left: the chosen person's two custom skill slots -------------------------------

        // カスタムスキルの枠1つ（固有スキルも同じ形）。挿絵・コスト・属性、名前と「攻撃・敵単体」、説明。
        private static CardLoadoutSlotWidget BuildSlot(RectTransform panel, string name, float y) =>
            BuildCard(Layout.Box(panel, name, 24, y, Left.width - 48, SlotHeight)).Widget;

        private static (CardLoadoutSlotWidget Widget, RectTransform Content) BuildCard(
            RectTransform rect
        )
        {
            var (button, selected, row) = GlassCardAssets.Choice(rect, pattern: false);
            var widget = new CardLoadoutSlotWidget { Button = button, Selected = selected };
            (widget.Art, widget.Cost, widget.Element) = BuildThumb(row, 12);
            (widget.Name, widget.Kind) = BuildNameLine(row, 8, SlotTextLeft, 24);
            widget.Description = BuildDescription(row, 56, 72, SlotTextLeft, 24);
            return (widget, row);
        }

        // 左の見出し（金のひし形と明るい金の文字）。
        private static void Title(RectTransform panel, string name, string text, float y)
        {
            var row = Layout.Row(panel, name, 30, y, Left.width - 60, 30, 14, TextAnchor.LowerLeft);
            Label(row, "Title", text, 24, UiPalette.Gold, TextAlignmentOptions.BottomLeft);
            Frame.HeadingRow(row, 24);
        }

        // 固有スキル1つ。カスタムスキルと同じ形のカードに、未解放のときの錠前と「Lv 20で解放」を足す。
        private static CardLoadoutUniqueWidget BuildUnique(RectTransform panel, int index, float y)
        {
            var (card, row) = BuildCard(
                Layout.Box(panel, "Unique" + index, 24, y, Left.width - 48, SlotHeight)
            );
            var widget = new CardLoadoutUniqueWidget { Card = card };
            widget.Lock = Frame.IconLock((RectTransform)card.Art.transform);
            widget.Lock.SetActive(false);
            widget.Mark = BuildMark(row);
            return widget;
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

            // タブの下に、その人が付けられる属性。属性のないカードは誰でも付けられる。
            var usable = Layout.Box(panel, "Usable", 36, UsableTop, Right.width - 72, 48);
            view.UsableLine = usable.gameObject;
            var group = usable.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 8;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            Label(
                usable,
                "Title",
                "付けられる属性",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft
            );
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
            Label(
                usable,
                "None",
                "＋属性なし",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft
            );

            var (viewport, content) = ChangeScreen.List(panel, ListTop);

            // One row per card in the catalog's order; the view shows only the cards the person
            // can set, by cost.
            view.Rows = CardSkills.All.Select(card => BuildRow(content, card, view)).ToArray();

            view.List = Layout.Scroll(panel, viewport, content);

            // 読み込み中と読めなかったとき、アイテムのタブを選んでいるときだけ出す文字。
            view.Status = ChangeScreen.Status(panel);
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

            var (name, kind) = BuildNameLine(row, 12, RowTextLeft, 150);
            name.text = card.Name;
            kind.text = CardText.KindLine(card);
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
