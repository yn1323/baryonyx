using System.Collections.Generic;
using System.Linq;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Party.Editor
{
    /// <summary>
    /// Bakes the frame the adventurer's tabs share (<see cref="PartySheet"/>) into a framed
    /// panel's layer: the column of people on the left and the tabs right of it, each on a pane
    /// of its own behind the gold double line. The tab's own contents go between
    /// <see cref="ContentLeft"/> and <see cref="ContentRight"/>, the screen's right edge.
    /// Design pixels from the top left of the 1920x1080 layer (<see cref="Layout"/>).
    /// </summary>
    public static class PartySheetAssets
    {
        // 左から人の列（0〜PeopleRight）、タブの列（〜ContentLeft）、各タブの中身（〜ContentRight、画面の右端）。
        // 2026-11-03に、タブの列を右端から人の列の右へ移した（ユーザーの指示）。中身の幅は変えていない。
        public const float PeopleRight = 160f;
        public const float TabWidth = 184f;
        public const float ContentLeft = PeopleRight + TabWidth;
        public const float ContentRight = 1920f;

        // 人の札：ドット絵の上半身（36×32ドット）を3倍で出す。
        private const float BustDot = 3f;
        private const float TileHeight = 128f;
        private const float TileGap = 12f;
        private const float TileInset = 16f;

        // 縦のタブ1つの高さ。
        private const float TabHeight = 128f;

        /// <summary>
        /// Builds the frame over <paramref name="layer"/> for the tab <paramref name="current"/>
        /// of <paramref name="tabs"/>, with a tile for every mock member of
        /// <paramref name="party"/> lined up as its formation, so the prefab reads in the editor.
        /// </summary>
        public static PartySheet Build(
            RectTransform layer,
            GuideMenuView guide,
            PartyMockData party,
            string current,
            IReadOnlyList<(string Key, string Label)> tabs
        )
        {
            Frame.Pane(
                layer,
                "SheetPeoplePane",
                0f,
                PeopleRight,
                Frame.HeaderHeight,
                Frame.PaneLight,
                true,
                false
            );
            Frame.Pane(
                layer,
                "SheetTabPane",
                PeopleRight,
                ContentLeft,
                Frame.HeaderHeight,
                Frame.PaneLight,
                false,
                false
            );
            var root = Layout.Box(layer, "Sheet", 0, 0, Layout.Design.x, Layout.Design.y);
            var sheet = root.gameObject.AddComponent<PartySheet>();
            sheet.Guide = guide;
            sheet.Current = current;
            BuildPeople(root, sheet, party);
            BuildTabs(root, sheet, tabs);
            Frame.Divider(layer, PeopleRight, Frame.HeaderHeight);
            Frame.Divider(layer, ContentLeft, Frame.HeaderHeight);

            var (people, partyCount) = PartyFormation.From(party).TabOrder();
            sheet.Show(people, partyCount, people.FirstOrDefault()?.Id);
            sheet.ShowTabs();
            return sheet;
        }

        // 左の列：全員の札を縦に並べ、パーティの人と控えの間に線を引く。入りきらない分は上下に動かす。
        private static void BuildPeople(RectTransform root, PartySheet sheet, PartyMockData party)
        {
            var list = Layout.Box(
                root,
                "People",
                0,
                Frame.HeaderHeight,
                PeopleRight,
                Layout.Design.y - Frame.HeaderHeight
            );
            var viewport = Layout.Viewport(list);
            var content = Layout.Column(
                viewport,
                "Tiles",
                TileGap,
                new RectOffset(
                    (int)TileInset,
                    (int)TileInset,
                    (int)TileInset,
                    (int)(TileInset + Frame.Margin)
                )
            );
            sheet.People = party.Members.Select(member => BuildPerson(content, member)).ToArray();
            var divider = Layout.ListItem(content, "Divider", 2f);
            // パーティと控えの間。線のない形では線を見せず、間だけを残す（doc/rules/ui-design.md の線のない形）。
            AddImage(divider, UiLook.Lineless ? Color.clear : Frame.LineColor, false);
            sheet.Divider = divider;
            sheet.List = Layout.Scroll(list, viewport, content);
        }

        private static PartySheetPersonWidget BuildPerson(RectTransform content, PartyMember member)
        {
            var rect = Layout.ListItem(content, "Person_" + member.Id, TileHeight);
            var (button, selected, inside) = GlassCardAssets.Choice(rect, pattern: false);
            var size = new Vector2(PartyArt.Bust.width, PartyArt.Bust.height) * BustDot;
            var figure = Rect("Figure", inside);
            Place(figure, Vector2.zero, size);
            var image = figure.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = BustDot;
            PartyArt.PaintBust(image, member);
            return new PartySheetPersonWidget
            {
                Id = member.Id,
                Button = button,
                Selected = selected,
                Figure = image,
            };
        }

        // 人の列の右の列：縦のタブ。選んだタブは明るい金の文字と、左の金の縦線（横のタブの下線と同じ金）。
        private static void BuildTabs(
            RectTransform root,
            PartySheet sheet,
            IReadOnlyList<(string Key, string Label)> tabs
        )
        {
            float width = TabWidth;
            sheet.TabKeys = tabs.Select(tab => tab.Key).ToArray();
            sheet.Tabs = tabs.Select(
                    (tab, i) =>
                        Frame.SideTab(
                            root,
                            "Tab_" + tab.Key,
                            tab.Label,
                            PeopleRight,
                            Frame.HeaderHeight + 24f + i * TabHeight,
                            width,
                            TabHeight
                        )
                )
                .ToArray();
        }
    }
}
