using System.Collections.Generic;
using System.Linq;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Party.Editor
{
    /// <summary>
    /// Bakes the frame the adventurer's tabs share (<see cref="PartySheet"/>) into a framed
    /// panel's layer: the column of people on the left and the tabs down the right, each on a
    /// pane of its own behind the gold double line. The tab's own contents go between
    /// <see cref="ContentLeft"/> and <see cref="ContentRight"/>. Design pixels from the top left
    /// of the 1920x1080 layer (<see cref="Layout"/>).
    /// </summary>
    public static class PartySheetAssets
    {
        // 左の人の列と、右のタブの列の境目。その間に各タブの中身を置く。
        public const float ContentLeft = 160f;
        public const float ContentRight = 1736f;

        // 人の札：ドット絵の上半身（36×32ドット）を3倍で出す。
        private const float BustDot = 3f;
        private const float TileHeight = 128f;
        private const float TileGap = 12f;
        private const float TileInset = 16f;

        // 縦のタブ1つの高さと、選んだときに左に引く金の縦線の太さ。
        private const float TabHeight = 128f;
        private const float TabBar = 8f;

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
                ContentLeft,
                Frame.HeaderHeight,
                Frame.PaneLight,
                true,
                false
            );
            Frame.Pane(
                layer,
                "SheetTabPane",
                ContentRight,
                Layout.Design.x,
                Frame.HeaderHeight,
                Frame.PaneLight,
                false,
                true
            );
            var root = Layout.Box(layer, "Sheet", 0, 0, Layout.Design.x, Layout.Design.y);
            var sheet = root.gameObject.AddComponent<PartySheet>();
            sheet.Guide = guide;
            sheet.Current = current;
            BuildPeople(root, sheet, party);
            BuildTabs(root, sheet, tabs);
            Frame.Divider(layer, ContentLeft, Frame.HeaderHeight);
            Frame.Divider(layer, ContentRight, Frame.HeaderHeight);

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
                ContentLeft,
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
            AddImage(divider, Frame.LineColor, false);
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

        // 右の列：縦のタブ。選んだタブは明るい金の文字と、左の金の縦線（横のタブの下線と同じ金）。
        private static void BuildTabs(
            RectTransform root,
            PartySheet sheet,
            IReadOnlyList<(string Key, string Label)> tabs
        )
        {
            float width = Layout.Design.x - ContentRight;
            sheet.TabKeys = tabs.Select(tab => tab.Key).ToArray();
            sheet.Tabs = tabs.Select(
                    (tab, i) =>
                    {
                        var rect = Layout.Box(
                            root,
                            "Tab_" + tab.Key,
                            ContentRight,
                            Frame.HeaderHeight + 24f + i * TabHeight,
                            width,
                            TabHeight
                        );
                        var guideTab = rect.gameObject.AddComponent<GuideTab>();
                        guideTab.Button = Guide.AddButton(rect, AddImage(rect, Color.clear, true));
                        var bar = Layout.Box(rect, "Selected", 6f, 20f, TabBar, TabHeight - 40f);
                        AddImage(bar, UiPalette.Gold, false);
                        bar.gameObject.SetActive(false);
                        guideTab.Selected = bar.gameObject;
                        guideTab.Label = Layout.Text(
                            rect,
                            "Label",
                            tab.Label,
                            34,
                            UiPalette.TextSub,
                            TextAlignmentOptions.Center,
                            TabBar + 12f,
                            0f,
                            width - TabBar - 24f,
                            TabHeight
                        );
                        Guide.Shrink(guideTab.Label, 24);
                        Layout.Line(rect, "Line", 20f, TabHeight - 2f, width - 40f, 2f);
                        return guideTab;
                    }
                )
                .ToArray();
        }
    }
}
