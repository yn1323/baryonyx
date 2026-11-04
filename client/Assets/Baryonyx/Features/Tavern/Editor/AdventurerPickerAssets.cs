using System.Linq;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
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
    /// The formation's list item (一覧): a card for every mock character, three to a row, baked
    /// into the formation prefab by its generator (<see cref="BuildPanel"/>) so they read in the
    /// editor without Play Mode. The cards are the adventurers' frosted glass
    /// (<see cref="GlassCardAssets"/>) inside the guide screen's frame
    /// (<see cref="GuideFramedPanel"/>), on one pane the scene shows through. Positions are design
    /// pixels from the top left of a 1920x1080 layer (<see cref="GuidePanelLayout"/>).
    /// </summary>
    public static class AdventurerPickerAssets
    {
        private const int Columns = 3;
        private const float CardGap = Card.CardGap;

        // 見出しは付けない。何の画面かは、上の帯のパンくず（編成 › 一覧）でわかる。
        private const float ListTop = Frame.HeaderHeight;
        private const float CardWidth =
            (1920f - Frame.Margin * 2f - CardGap * (Columns - 1)) / Columns;

        // 9人が3行でスクロールせずに収まる高さ。
        private const float CardHeight =
            (1080f - ListTop - CardGap - Frame.Margin - CardGap * 2f) / 3f;

        // 右上の「パーティー」の札の余白。
        private const float TagInset = 20f;

        /// <summary>
        /// Builds the picker over the guide screen's safe area. Called while the guide screen's
        /// prefab is being built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            GlassCardAssets.EnsureArt();

            var (view, layer) = Frame.FramedPanel<AdventurerPickerView>(safe, "GearPicker");
            view.Party = party;
            view.Guide = guide;

            Frame.Pane(layer, "Pane", 0f, 1920f, Frame.HeaderHeight, Frame.PaneLight, true, true);

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
                .Members.Select(member => BuildCard(content, member, shadow))
                .ToArray();

            view.List = Layout.Scroll(list, viewport, content);

            view.Status = Frame.ListStatus(layer, Frame.Margin, 1920f - Frame.Margin, ListTop);

            // 仮データの編成で描き、Prefabを開くと停止中でも読めるようにする。
            view.Render(PartyFormation.From(party));
            return view.gameObject;
        }

        // パーティーの冒険者のカードと同じ、立ち姿・名前・Lv・属性。パーティーの人だけ右上に金の札を出す。
        private static AdventurerPickWidget BuildCard(
            RectTransform content,
            PartyMember member,
            Sprite shadow
        )
        {
            var rect = Rect("Member_" + member.Id, content);
            var (card, inside) = GlassCardAssets.Build(rect);
            GlassCardAssets.AddBack(card);
            var widget = new AdventurerPickWidget { Id = member.Id, Card = card };
            Card.Figure(inside, member, shadow, CardHeight);
            var (name, _, level, elements) = Card.NameLines(
                inside,
                CardWidth,
                CardHeight,
                Card.NameLeft
            );
            name.text = member.Name;
            widget.Level = level;
            widget.Elements = elements;

            var tag = Layout.Text(
                inside,
                "PartyTag",
                AdventurerPickerView.PartyLabel,
                26,
                UiPalette.GoldLight,
                TextAlignmentOptions.TopRight,
                CardWidth - TagInset - 200f,
                TagInset,
                200f,
                40f
            );
            widget.PartyTag = tag.gameObject;
            return widget;
        }
    }
}
