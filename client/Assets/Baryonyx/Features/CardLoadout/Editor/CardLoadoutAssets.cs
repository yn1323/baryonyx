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
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.CardLoadout.Editor
{
    /// <summary>
    /// The tavern's card skills. The tavern's generator calls <see cref="BuildPanel"/> for its
    /// card skills item, so the people's tabs, the four slots and a row for every card are
    /// baked into the tavern prefab and read in the editor without Play Mode. The cards are the
    /// combat's card skills; the people, levels and stats are the party's and the training's
    /// mock data (doc/features/party.md).
    /// </summary>
    public static class CardLoadoutAssets
    {
        // カードの挿絵（64×58）は2倍で出す。
        private const float ArtDot = 2f;

        private const float LeftWidth = 780f;
        private const float DividerWidth = 4f;
        private const float TabTop = 24f;
        private const float SlotTop = 168f;
        private const float SlotHeight = 152f;
        private const float SlotGap = 12f;
        private const float SlotTextLeft = 172f;
        private const float RowHeight = 140f;
        private const float RowTextLeft = 160f;
        private static readonly Color DividerColor = new(0.52f, 0.59f, 0.68f, 0.7f);
        private static readonly Color BadgeColor = new(0.02f, 0.03f, 0.06f, 0.85f);

        /// <summary>
        /// Builds the card skills over the guide screen's safe area: one large frame below the
        /// back button, split into the people and their four slots on the left and the cards on
        /// the right. Called while the guide screen's prefab is being built, so the shared frames
        /// and font are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var training = TrainingAssets.CreateData();
            var (people, partyCount) = CardLoadoutPresenter.People(PartyFormation.From(party));

            var root = Rect("CardLoadout", safe);
            Stretch(root);
            var view = root.gameObject.AddComponent<CardLoadoutView>();
            view.Party = party;
            view.Training = training;
            view.Guide = guide;
            view.ElementIcons = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(ElementIcon)
                .ToArray();

            var panel = Rect("Panel", root);
            Stretch(panel);
            panel.offsetMin = new Vector2(32, 24);
            panel.offsetMax = new Vector2(-32, -208);
            Guide.Frame(panel, Guide.FramePath, Color.white).raycastTarget = true;

            var left = Rect("Person", panel);
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
            AddImage(divider, DividerColor, false);
            var right = Rect("Cards", panel);
            Stretch(right);
            right.offsetMin = new Vector2(LeftWidth + DividerWidth, 0);

            view.People = PartyTabAssets.Build(left, people, partyCount, TabTop);
            BuildSlots(left, view);
            BuildCards(right, view);

            // 仮データの最初の人で描き、Prefabを開くと停止中でも文字と絵が読めるようにする。
            new CardLoadoutPresenter(
                view,
                people,
                partyCount,
                new MockStore(party, training)
            ).Dispose();
            return root.gameObject;
        }

        // --- Left: the chosen person's four slots ------------------------------------------

        private static void BuildSlots(RectTransform panel, CardLoadoutView view)
        {
            view.Slots = Enumerable
                .Range(0, CardLoadoutRules.Size)
                .Select(i => BuildSlot(panel, i))
                .ToArray();
        }

        private static CardLoadoutSlotWidget BuildSlot(RectTransform panel, int index)
        {
            var row = Rect("Slot" + index, panel);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 1);
            float top = SlotTop + index * (SlotHeight + SlotGap);
            row.offsetMin = new Vector2(24, -(top + SlotHeight));
            row.offsetMax = new Vector2(-24, -top);
            var widget = new CardLoadoutSlotWidget
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

            (widget.Art, widget.Cost, widget.Element) = BuildThumb(row, 20);
            (widget.Name, widget.Kind) = BuildNameLine(row, 16, SlotTextLeft, 24);
            widget.Description = BuildDescription(row, 70, 72, SlotTextLeft, 24);
            return widget;
        }

        // --- Right: every card, shown when the person can set it ---------------------------

        private static void BuildCards(RectTransform panel, CardLoadoutView view)
        {
            var title = Label(
                panel,
                "Title",
                "スキル",
                44,
                Guide.Gold,
                TextAlignmentOptions.TopLeft
            );
            Guide.Fill((RectTransform)title.transform, new Vector2(40, 0), new Vector2(-40, -24));

            // 見出しの横に、その人が付けられる属性。属性のないカードは誰でも付けられる。
            var usable = Rect("Usable", panel);
            Corner(usable, new Vector2(0, 1), new Vector2(196, -28), new Vector2(420, 48));
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
            Label(
                usable,
                "None",
                "＋属性なし",
                26,
                Guide.TextSub,
                TextAlignmentOptions.MidlineLeft
            );

            view.Count = Label(
                panel,
                "Count",
                "",
                26,
                Guide.TextSub,
                TextAlignmentOptions.TopRight
            );
            Guide.Fill(
                (RectTransform)view.Count.transform,
                new Vector2(40, 0),
                new Vector2(-40, -36)
            );

            var viewport = Rect("Viewport", panel);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(24, 24);
            viewport.offsetMax = new Vector2(-24, -100);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);

            var content = Rect("Rows", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var rows = content.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.spacing = 10;
            rows.childControlWidth = rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            // One row per card in the catalog's order; the view shows only the cards the person
            // can set, by cost.
            view.Rows = CardSkills.All.Select(card => BuildRow(content, card, view)).ToArray();

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            view.List = scroll;
        }

        private static CardLoadoutRowWidget BuildRow(
            RectTransform content,
            CardSkill card,
            CardLoadoutView view
        )
        {
            var row = Rect("Card_" + card.Id, content);
            var size = row.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = RowHeight;
            var widget = new CardLoadoutRowWidget
            {
                Id = card.Id,
                Button = Guide.AddButton(
                    row,
                    Guide.Frame(row, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
                ),
            };
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
            widget.Mark = Label(row, "Mark", "", 24, Guide.Gold, TextAlignmentOptions.TopRight);
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
            var cost = Label(badge, "Value", "", 28, Guide.Gold, TextAlignmentOptions.Center);
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
            var name = Label(line, "Name", "", 34, Guide.TextMain, TextAlignmentOptions.BottomLeft);
            var kind = Label(line, "Kind", "", 22, Guide.TextMain, TextAlignmentOptions.BottomLeft);
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
                Guide.TextSub,
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
