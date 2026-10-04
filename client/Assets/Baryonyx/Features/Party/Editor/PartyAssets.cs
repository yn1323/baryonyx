using System;
using System.IO;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Party.Editor
{
    /// <summary>
    /// The party mock data and the tavern's formation. The tavern's generator calls
    /// <see cref="BuildFormationPanel"/> for its formation item, so the slots and the owned
    /// characters are baked into the tavern prefab and read in the editor without Play Mode.
    /// The characters, levels and cards are mock data until characters have data
    /// (doc/features/party.md).
    /// </summary>
    public static class PartyAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Party";
        public const string DataPath = Folder + "/Data/PartyMockData.asset";
        public const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";
        public const string AttributeArtFolder = "Assets/Baryonyx/Shared/Art/Attributes";

        private const float DotScale = 4f;

        // One large frame below the back button, split by a line into the four slots on the
        // left (a fixed width) and the owned characters on the right (the rest of the width).
        private const float LeftWidth = 780f;
        private const float DividerWidth = DotScale;
        private const float SlotGap = 16f;

        // In a slot, from the bottom: the card icons, the name and level, then the feet.
        private const float CardsBottom = 20f;
        private const float CardIconSize = 12f * DotScale;
        private const float CardGap = 12f;
        private const float NameBottom = 76f;
        private const float FeetY = 132f;

        // A tile shows the whole figure (the trimmed sprite at 4x) on its shadow, then the name
        // and level; four tiles fit across at the 16:9 design width.
        private static readonly Vector2 TileSize = new(232, 312);
        private const float TileGap = 12f;
        private const float TileFigureTop = 12f;
        private const float TileNameTop = 230f;
        private const float TileLevelTop = 266f;
        private static readonly Color Faint = new(0.55f, 0.55f, 0.6f, 1f);

        [MenuItem("Baryonyx/Party/Create Mock Data")]
        public static PartyMockData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            var data = AssetDatabase.LoadAssetAtPath<PartyMockData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<PartyMockData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            // カードは戦闘画面のモックと同じスキル。
            data.Members = new[]
            {
                Member("toma", "トーマ", 12, "Toma", "Fire", "Meteor", "Ice", "Blizzard"),
                Member(
                    "luka",
                    "ルカ",
                    11,
                    "Luka",
                    "VitalThrust",
                    "ArrowRain",
                    "Thunder",
                    "LightningBolt"
                ),
                Member("aria", "アリア", 10, "Aria", "Slash", "Iai", "ShieldBash", "EarthSplitter"),
                Member("mina", "ミナ", 10, "Mina", "Heal", "Protect", "HolyHammer", "HolyLight"),
                // 絵のないキャラは、4人の絵に色を掛けて左右を反転した仮の絵で見分ける。
                StandIn(
                    Member(
                        "anselm",
                        "アンセルム",
                        8,
                        "Aria",
                        "EarthSplitter",
                        "HolyHammer",
                        "Fire",
                        "Embers"
                    ),
                    new Color(0.8f, 0.9f, 1f),
                    flip: true
                ),
                StandIn(
                    Member(
                        "greta",
                        "グレタ",
                        7,
                        "Luka",
                        "VitalThrust",
                        "PoisonNeedle",
                        "Ice",
                        "Icicles"
                    ),
                    new Color(1f, 0.82f, 0.9f),
                    flip: false
                ),
                StandIn(
                    Member(
                        "lutz",
                        "ルッツ",
                        5,
                        "Toma",
                        "Thunder",
                        "Thundercloud",
                        "ShieldBash",
                        "EarthSplitter"
                    ),
                    new Color(0.86f, 1f, 0.8f),
                    flip: true
                ),
                StandIn(
                    Member(
                        "rita",
                        "リタ",
                        3,
                        "Mina",
                        "Slash",
                        "Whirlwind",
                        "VitalThrust",
                        "ShadowStitch"
                    ),
                    new Color(1f, 0.88f, 0.76f),
                    flip: true
                ),
                StandIn(
                    Member(
                        "ritsu",
                        "リツ",
                        1,
                        "Luka",
                        "Ice",
                        "Blizzard",
                        "Thunder",
                        "Thundercloud"
                    ),
                    new Color(0.86f, 0.84f, 1f),
                    flip: true
                ),
            };
            data.Formation = new[] { "toma", "luka", "aria", "mina" };
            data.ElementIcons = ((CardElement[])Enum.GetValues(typeof(CardElement)))
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : Guide.Icon($"{AttributeArtFolder}/Element{element}.aseprite")
                )
                .ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        /// <summary>
        /// Builds the formation over the guide screen's safe area: one large frame below the
        /// back button, split into the four slots on the left and the owned characters on the
        /// right. Called while the guide screen's prefab is being built, so the shared frames
        /// and font are ready.
        /// </summary>
        public static GameObject BuildFormationPanel(RectTransform safe, GuideMenuView guide)
        {
            var data = CreateData();
            var formation = PartyFormation.From(data);
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);

            var root = Rect("Formation", safe);
            Stretch(root);
            var view = root.gameObject.AddComponent<PartyFormationView>();
            view.Data = data;
            view.Guide = guide;

            var panel = Rect("Panel", root);
            Stretch(panel);
            panel.offsetMin = new Vector2(32, 24);
            panel.offsetMax = new Vector2(-32, -208);
            Guide.Frame(panel, Guide.FramePath, Color.white).raycastTarget = true;

            var left = Rect("Party", panel);
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
            var right = Rect("Members", panel);
            Stretch(right);
            right.offsetMin = new Vector2(LeftWidth + DividerWidth, 0);

            BuildSlots(left, view, data, formation, shadow);
            BuildMembers(right, view, data, formation, shadow);
            return root.gameObject;
        }

        // --- Left: the four slots in two rows of two -------------------------------------

        private static void BuildSlots(
            RectTransform panel,
            PartyFormationView view,
            PartyMockData data,
            PartyFormation formation,
            Sprite shadow
        )
        {
            var title = Label(
                panel,
                "Title",
                "編成中",
                44,
                Guide.Gold,
                TextAlignmentOptions.TopLeft
            );
            Guide.Fill((RectTransform)title.transform, new Vector2(40, 0), new Vector2(-40, -24));

            var slots = Rect("Slots", panel);
            Stretch(slots);
            slots.offsetMin = new Vector2(24, 24);
            slots.offsetMax = new Vector2(-24, -84);
            view.Slots = Enumerable
                .Range(0, PartyFormation.Size)
                .Select(i => BuildSlot(slots, i, data, data.Find(formation.Member(i)), shadow))
                .ToArray();
        }

        private static PartySlotWidget BuildSlot(
            RectTransform slots,
            int index,
            PartyMockData data,
            PartyMember member,
            Sprite shadow
        )
        {
            var art = data.Members[0].Art;
            int column = index % 2;
            int row = index / 2;
            var cell = Rect("Slot" + index, slots);
            cell.anchorMin = new Vector2(column * 0.5f, 0.5f - row * 0.5f);
            cell.anchorMax = new Vector2(column * 0.5f + 0.5f, 1f - row * 0.5f);
            cell.offsetMin = new Vector2(column * SlotGap / 2f, (1 - row) * SlotGap / 2f);
            cell.offsetMax = new Vector2(-(1 - column) * SlotGap / 2f, -row * SlotGap / 2f);
            var widget = new PartySlotWidget
            {
                Button = Guide.AddButton(
                    cell,
                    Guide.Frame(cell, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
                ),
            };
            // Under the character, so the gold frame's fill does not cover them.
            var selected = Rect("Selected", cell);
            Stretch(selected);
            Guide.Frame(selected, Guide.FrameSelectedPath, Color.white).raycastTarget = false;
            widget.Selected = selected.gameObject;

            var foot = Picture(cell, "Shadow", shadow, Vector2.zero, new Vector2(150, 24), 0.5f);
            Bottom((RectTransform)foot.transform, FeetY + 2);
            widget.Shadow = foot.gameObject;
            // An empty slot still holds a 64x64 sprite, hidden, so its size never changes.
            if (member != null && member.Art != null)
                art = member.Art;
            widget.Sprite = PixelActor(cell, "Sprite", art, Vector2.zero, DotScale);
            Bottom(widget.Sprite.rectTransform, FeetY);

            var line = Rect("NameLine", cell);
            Guide.Band(line, top: false, NameBottom, 48, 16, 16);
            var group = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.LowerCenter;
            group.spacing = 14;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            widget.Name = Label(line, "Name", "", 34, Guide.TextMain, TextAlignmentOptions.Bottom);
            widget.Level = Label(line, "Level", "", 26, Guide.Gold, TextAlignmentOptions.Bottom);

            var cards = Rect("Cards", cell);
            float width = PartyFormation.Size * CardIconSize + (PartyFormation.Size - 1) * CardGap;
            cards.anchorMin = cards.anchorMax = cards.pivot = new Vector2(0.5f, 0f);
            cards.anchoredPosition = new Vector2(0, CardsBottom);
            cards.sizeDelta = new Vector2(width, CardIconSize);
            widget.Cards = Enumerable
                .Range(0, PartyFormation.Size)
                .Select(i => BuildCard(cards, i))
                .ToArray();

            widget.Empty = Label(
                cell,
                "Empty",
                "空き",
                40,
                Guide.TextSub,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)widget.Empty.transform);

            // The mock data's level and cards: the session may still hold the last Play's.
            PartyFormationView.ShowSlot(
                widget,
                data,
                member,
                member?.Level ?? 1,
                member?.Cards.Select(card => card.Skill).ToArray(),
                index == 0
            );
            return widget;
        }

        // One card's attribute icon (12x12 at 4x); a card without an attribute shows "無".
        private static PartyCardWidget BuildCard(RectTransform cards, int index)
        {
            var card = Rect("Card" + index, cards);
            Corner(
                card,
                new Vector2(0, 0.5f),
                new Vector2(index * (CardIconSize + CardGap), 0),
                Vector2.one * CardIconSize
            );
            var icon = card.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            var none = Rect("None", card);
            Stretch(none);
            Guide.Frame(none, Guide.FramePath, Faint).raycastTarget = false;
            var label = Label(none, "Label", "無", 26, Guide.TextSub, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            return new PartyCardWidget { Icon = icon, None = none.gameObject };
        }

        // --- Right: the owned characters not in the party, then "外す" -------------------

        private static void BuildMembers(
            RectTransform panel,
            PartyFormationView view,
            PartyMockData data,
            PartyFormation formation,
            Sprite shadow
        )
        {
            var title = Label(panel, "Title", "仲間", 44, Guide.Gold, TextAlignmentOptions.TopLeft);
            Guide.Fill((RectTransform)title.transform, new Vector2(40, 0), new Vector2(-40, -24));
            view.Owned = Label(
                panel,
                "Owned",
                $"所持 {formation.Roster.Count}人",
                26,
                Guide.TextSub,
                TextAlignmentOptions.TopRight
            );
            Guide.Fill(
                (RectTransform)view.Owned.transform,
                new Vector2(40, 0),
                new Vector2(-40, -36)
            );

            var viewport = Rect("Viewport", panel);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(24, 24);
            viewport.offsetMax = new Vector2(-24, -100);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);

            var content = Rect("Tiles", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = TileSize;
            grid.spacing = Vector2.one * TileGap;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            // One tile per owned character, in the roster's order; the view shows only those
            // not in the party.
            view.Members = data
                .Members.Select(member => BuildTile(content, member, formation, shadow))
                .ToArray();
            BuildLeave(content, view);

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            view.List = scroll;
        }

        private static PartyMemberWidget BuildTile(
            RectTransform content,
            PartyMember member,
            PartyFormation formation,
            Sprite shadow
        )
        {
            var tile = Rect("Member_" + member.Id, content);
            var widget = new PartyMemberWidget
            {
                Id = member.Id,
                Button = Guide.AddButton(
                    tile,
                    Guide.Frame(tile, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
                ),
            };

            var size =
                new Vector2(PartyFormationView.Figure.width, PartyFormationView.Figure.height)
                * DotScale;
            var foot = Picture(tile, "Shadow", shadow, Vector2.zero, new Vector2(120, 20), 0.5f);
            Top((RectTransform)foot.transform, TileFigureTop + size.y - 4);
            var figure = Rect("Figure", tile);
            Corner(figure, new Vector2(0.5f, 1f), new Vector2(0, -TileFigureTop), size);
            var image = figure.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            PartyFormationView.Paint(image, member, trim: true);
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = DotScale;

            var name = Label(
                tile,
                "Name",
                member.Name,
                28,
                Guide.TextMain,
                TextAlignmentOptions.Center
            );
            Guide.Band((RectTransform)name.transform, top: true, TileNameTop, 36, 8, 8);
            Guide.Shrink(name, 20);
            widget.Level = Label(
                tile,
                "Level",
                PartyFormationView.LevelText(member.Level),
                22,
                Guide.Gold,
                TextAlignmentOptions.Center
            );
            Guide.Band((RectTransform)widget.Level.transform, top: true, TileLevelTop, 28, 8, 8);

            tile.gameObject.SetActive(formation.SlotOf(member.Id) < 0);
            return widget;
        }

        // The last tile takes the chosen slot's member out of the party.
        private static void BuildLeave(RectTransform content, PartyFormationView view)
        {
            var tile = Rect("Leave", content);
            view.Leave = Guide.AddButton(
                tile,
                Guide.Frame(tile, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
            );
            view.LeaveLabel = Label(
                tile,
                "Label",
                "外す",
                40,
                Guide.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)view.LeaveLabel.transform);
        }

        // --- Helpers -------------------------------------------------------------------

        // Keeps the rect's centre where it is horizontally and puts it a height above the
        // bottom edge of its parent.
        private static void Bottom(RectTransform rect, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, y);
        }

        // The same, measured down from the top edge.
        private static void Top(RectTransform rect, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -y);
        }

        private static PartyMember Member(
            string id,
            string name,
            int level,
            string art,
            params string[] cards
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Level = level,
                Art = ArtAssets.ImportTexture(
                    $"{CharacterArtFolder}/Battle{art}.aseprite",
                    FilterMode.Point
                ),
                Cards = cards.Select(Card).ToArray(),
            };

        private static PartyMember StandIn(PartyMember member, Color tint, bool flip)
        {
            member.Tint = tint;
            member.Flip = flip;
            return member;
        }

        private static PartyCard Card(string skill)
        {
            if (CardSkills.Find(skill) == null)
                throw new InvalidOperationException("Unknown card skill: " + skill);
            return new PartyCard { Skill = skill };
        }
    }
}
