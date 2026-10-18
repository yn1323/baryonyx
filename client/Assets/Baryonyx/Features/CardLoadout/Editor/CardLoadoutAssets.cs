using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.GuideMenu;
using TMPro;
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

        // 枠つきの表示（GuideFramedPanel）で、左の濃い面に人・ステータス・枠、金の2本線の右の透ける面に
        // タブとカードの一覧を置く。装備と同じ配置（PartyChangeScreenAssets）。設計座標、左上から。
        private static readonly Rect Left = ChangeScreen.Left;
        private static readonly Rect Right = ChangeScreen.Right;

        // 左は上から人・ステータス・カスタムスキル・固有スキル。ステータスと線の位置は装備と同じ。
        private const float SlotTop = 318f;
        private const float SlotHeight = 136f;
        private const float SlotStep = 144f;

        // カスタムスキルの下に、固有スキル2つを読むだけの行で出す。
        private const float UniqueHeight = 92f;
        private const float UniqueStep = 100f;
        private const float SlotTextLeft = 156f;
        private const float RowHeight = 140f;
        private const float RowTextLeft = 160f;

        // 右の上のタブの下の、付けられる属性の行。
        private const float UsableTop = 92f;
        private const float ListTop = 152f;
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
            Title(left, "CustomTitle", "カスタムスキル", "スキルかアイテム", SlotTop - 34);
            view.Slots = Enumerable
                .Range(0, CardLoadoutRules.Size)
                .Select(i => BuildSlot(left, i))
                .ToArray();
            float uniqueTop = SlotTop + CardLoadoutRules.Size * SlotStep + 40;
            Title(left, "UniqueTitle", "固有スキル", "外せない", uniqueTop - 34);
            view.Uniques = Enumerable
                .Range(0, 2)
                .Select(i => BuildUnique(left, i, uniqueTop + i * UniqueStep))
                .ToArray();
            BuildCards(right, view);

            // 仮データの最初の人で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            new CardLoadoutPresenter(view, people, new MockStore(party, training)).Dispose();
            return view.gameObject;
        }

        // --- Left: the chosen person's two custom skill slots -------------------------------

        private static CardLoadoutSlotWidget BuildSlot(RectTransform panel, int index)
        {
            var (button, selected, row) = GlassCardAssets.Choice(
                Layout.Box(
                    panel,
                    "Slot" + index,
                    24,
                    SlotTop + index * SlotStep,
                    Left.width - 48,
                    SlotHeight
                ),
                pattern: false
            );
            var widget = new CardLoadoutSlotWidget { Button = button, Selected = selected };
            (widget.Art, widget.Cost, widget.Element) = BuildThumb(row, 12);
            (widget.Name, widget.Kind) = BuildNameLine(row, 8, SlotTextLeft, 24);
            widget.Description = BuildDescription(row, 56, 72, SlotTextLeft, 24);
            return widget;
        }

        // 左の見出し（金）と、その横の小さな注記。
        private static void Title(
            RectTransform panel,
            string name,
            string text,
            string note,
            float y
        )
        {
            var row = Layout.Row(panel, name, 30, y, Left.width - 60, 30, 14, TextAnchor.LowerLeft);
            Label(row, "Title", text, 24, UiPalette.Gold, TextAlignmentOptions.BottomLeft);
            Label(row, "Note", note, 18, UiPalette.TextSub, TextAlignmentOptions.BottomLeft);
            Frame.HeadingRow(row, 24);
        }

        // 固有スキル1つ。挿絵の中央を3倍で出し、名前とエネルギー（未解放なら解放するLv）、効果を並べる。押せない。
        private static CardLoadoutUniqueWidget BuildUnique(RectTransform panel, int index, float y)
        {
            float width = Left.width - 48;
            // 外せないので押せない板にする。
            var row = GlassCardAssets.Plaque(
                Layout.Box(panel, "Unique" + index, 24, y, width, UniqueHeight),
                pattern: false
            );
            var widget = new CardLoadoutUniqueWidget();
            (widget.Icon, widget.Lock) = TrainingAssets.SkillIcon(
                Layout.Box(row, "Icon", 14, (UniqueHeight - 72) / 2f, 72, 72)
            );
            widget.Name = Label(
                row,
                "Name",
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft
            );
            Layout.At(widget.Name, 104, 10, width - 330, 34);
            Guide.Shrink(widget.Name, 20);
            widget.Meta = Label(row, "Meta", "", 20, UiPalette.Gold, TextAlignmentOptions.TopRight);
            Layout.At(widget.Meta, width - 224, 16, 200, 26);
            widget.Description = Label(
                row,
                "Description",
                "",
                22,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            Layout.At(widget.Description, 104, 50, width - 128, 30);
            Guide.Shrink(widget.Description, 16);
            return widget;
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

            public int StatOf(string id, CardStat stat)
            {
                var member = party.Find(id);
                var growth = training.Find(id);
                if (member == null || growth == null)
                    return 0;
                var stats = growth.StatsAt(member.Level);
                return stat switch
                {
                    CardStat.PhysicalAttack => stats.PhysicalAttack,
                    CardStat.MagicAttack => stats.MagicAttack,
                    CardStat.PhysicalDefense => stats.PhysicalDefense,
                    _ => 0,
                };
            }

            public int Act => 0;

            public string Selected
            {
                get => null;
                set { }
            }
        }
    }
}
