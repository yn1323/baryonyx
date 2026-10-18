using System.Linq;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Equipment;
using Baryonyx.Equipment.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Card = Baryonyx.Tavern.Editor.AdventurerCardAssets;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Tavern.Editor
{
    /// <summary>
    /// The formation's equipment item: a card for every mock character, three to a row, baked
    /// into the formation prefab by its generator (<see cref="BuildPanel"/>) so they read in the
    /// editor without Play Mode. The cards are the adventurers' frosted glass
    /// (<see cref="GlassCardAssets"/>) inside the guide screen's frame
    /// (<see cref="GuideFramedPanel"/>), on one pane the scene shows through. Positions are design
    /// pixels from the top left of a 1920x1080 layer (<see cref="GuidePanelLayout"/>).
    /// </summary>
    public static class AdventurerPickerAssets
    {
        public const string Heading = "冒険者を選ぶ";

        private const int Columns = 3;
        private const float HeadingHeight = 76f;
        private const float CardGap = Card.CardGap;
        private const float ListTop = Frame.HeaderHeight + HeadingHeight;
        private const float CardWidth =
            (1920f - Frame.Margin * 2f - CardGap * (Columns - 1)) / Columns;

        // 9人が3行でスクロールせずに収まる高さ。
        private const float CardHeight =
            (1080f - ListTop - CardGap - Frame.Margin - CardGap * 2f) / 3f;

        // カードの中の文字の左端と幅。
        private const float TextLeft = Card.TextLeft;
        private const float TextWidth = CardWidth - TextLeft - 24f;

        // 武器・防具のアイコン（24×24を2倍）。
        private const float GearIcon = 24f * 2f;

        /// <summary>
        /// Builds the picker over the guide screen's safe area. Called while the guide screen's
        /// prefab is being built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var weapon = Guide.Icon(EquipmentAssets.IconWeaponPath);
            var armor = Guide.Icon(EquipmentAssets.IconArmorPath);
            GlassCardAssets.EnsureArt();

            var (view, layer) = Frame.FramedPanel<AdventurerPickerView>(safe, "GearPicker");
            view.Party = party;
            view.Guide = guide;

            Frame.Pane(layer, "Pane", 0f, 1920f, Frame.HeaderHeight, Frame.PaneLight, true, true);
            Frame.Heading(
                layer,
                "Title",
                Heading,
                Frame.Margin,
                Frame.HeaderHeight,
                600f,
                HeadingHeight
            );

            var (list, viewport, content) = Card.Grid(
                layer,
                0f,
                ListTop,
                new Vector2(CardWidth, CardHeight),
                new RectOffset(
                    (int)Frame.Margin,
                    (int)Frame.Margin,
                    (int)CardGap,
                    (int)(CardGap + Frame.Margin)
                ),
                Columns
            );

            view.Cards = party
                .Members.Select(member => BuildCard(content, member, shadow, weapon, armor))
                .ToArray();

            view.List = Layout.Scroll(list, viewport, content);

            view.Status = Layout.Text(
                layer,
                "Status",
                "",
                36,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                Frame.Margin,
                ListTop + CardGap,
                1920f - Frame.Margin * 2f,
                CardHeight
            );

            // 仮データの編成と装備で描き、Prefabを開くと停止中でも読めるようにする。
            view.Render(PartyFormation.From(party), EquipmentLocalSource.Starter());
            return view.gameObject;
        }

        private static AdventurerPickWidget BuildCard(
            RectTransform content,
            PartyMember member,
            Sprite shadow,
            Sprite weapon,
            Sprite armor
        )
        {
            var rect = Rect("Member_" + member.Id, content);
            var (card, inside) = GlassCardAssets.Build(rect);
            var widget = new AdventurerPickWidget { Id = member.Id, Card = card };
            Card.Figure(inside, member, shadow, CardHeight);
            Card.Name(inside, member.Name, 20f, TextWidth);

            var line = Layout.Row(inside, "LevelLine", TextLeft, 84f, TextWidth, 48f, 16f);
            widget.Level = Card.Level(line, member.Level.ToString());
            var tag = Label(
                line,
                "PartyTag",
                "パーティ",
                26,
                UiPalette.GoldLight,
                TextAlignmentOptions.Bottom
            );
            Layout.Fit(tag, 0);
            widget.PartyTag = tag.gameObject;

            (widget.WeaponIcon, widget.Weapon) = BuildGear(inside, "Weapon", 148f, weapon);
            (widget.ArmorIcon, widget.Armor) = BuildGear(inside, "Armor", 200f, armor);
            return widget;
        }

        // 種類のアイコンと、付けている装備の名前の1行。
        private static (Image Icon, TMP_Text Name) BuildGear(
            RectTransform card,
            string name,
            float y,
            Sprite sprite
        )
        {
            var row = Layout.Row(card, name, TextLeft, y, TextWidth, GearIcon, 10f);
            var icon = Layout.RowIcon(row, "Icon", GearIcon);
            icon.sprite = sprite;
            var label = Label(
                row,
                "Name",
                AdventurerPickerView.EmptyText,
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(label, TextWidth - GearIcon - 10f);
            Guide.Shrink(label, 22);
            return (icon, label);
        }
    }
}
