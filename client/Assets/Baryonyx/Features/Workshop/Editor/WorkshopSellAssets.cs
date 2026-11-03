using System.Linq;
using Baryonyx.Editor.UI;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using static Baryonyx.Workshop.Editor.WorkshopPanelAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// Builds the trading house's "sell" panel (<see cref="WorkshopSellView"/>), filling the
    /// screen: the kinds' tabs and the held things' cards on the left, one card for each thing,
    /// and the chosen ones, the total and the sell button on the right. The first tab's cards and
    /// three chosen rows are baked from the mock stock, so the prefab reads without Play Mode.
    /// </summary>
    public static class WorkshopSellAssets
    {
        // 札は買うと同じ形（絵・名前・☆・ルーンの値段）に、品質の文字・数・「E」を重ねる。
        private const int Columns = 4;
        private const float CardHeight = 272f;
        private const float CardArt = 128f;

        // 装備は品質の下に効果を書くため、名前・品質・効果の3行が入る高さにする。
        private const float ChosenHeight = 150f;
        private const float ChosenArt = 72f;
        private const float ChosenValue = 180f;

        // 値段は品質・効果の行の高さ、属性のアイコンは名前の行の高さにそろえる（行の下・上から）。

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (sell, panel) = GuidePanelTemplate.GridDetail<WorkshopSellView>(
                safe,
                "WorkshopSell",
                WorkshopStock.TabLabels[0],
                Columns,
                CardHeight,
                WorkshopStock.TabLabels
            );
            var layer = panel.Layer;
            sell.Guide = view;
            sell.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            sell.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            sell.ArtBox = CardArt;
            sell.ChosenArtBox = ChosenArt;
            sell.Tabs = panel.Tabs;
            sell.ListTitle = panel.Title;
            sell.ListCount = panel.Count;
            var content = panel.Content;
            var cell = panel.Cell;
            sell.Content = content;
            sell.List = panel.Scroll;
            sell.Template = GoodCard(content, "Template", cell, CardArt, held: true);
            // 読み込む間は、この写しを伏せて一覧を埋める（GlassCardCover）。
            GlassCardAssets.AddBack(sell.Template.Card);
            sell.Template.gameObject.SetActive(false);
            var mock = WorkshopStock.Mock();
            var first = WorkshopStock.OfKind(mock, WorkshopStock.Kinds[0]);
            for (int i = 0; i < first.Count; i++)
            {
                var row = GoodCard(content, "Owned" + i, cell, CardArt, held: true);
                GlassCardAssets.AddBack(row.Card);
                WorkshopSellView.Paint(row, first[i], EntryOf(sell.Data, first[i]), CardArt);
                sell.Rows.Add(row);
            }
            sell.Status = panel.Status;
            sell.ListCount.text = UiText.Count(first.Count);

            float left = GuidePanelTemplate.DetailLeft;
            float right = GuidePanelTemplate.DetailRight;
            GuideFrameAssets.Heading(
                layer,
                "ChosenTitle",
                "売る物",
                left,
                TabTop,
                right - left,
                TabHeight
            );
            float totalTop = ConfirmTop - 112f;
            var (chosen, _) = Column(
                layer,
                "Chosen",
                Split,
                Layout.Design.x,
                BodyTop,
                totalTop - 12f,
                (int)Inner,
                (int)Edge
            );
            sell.ChosenContent = chosen;
            sell.ChosenTemplate = ChosenRow(chosen, "Template");
            sell.ChosenTemplate.gameObject.SetActive(false);
            var picked = mock.Where(thing =>
                    thing.User == null && thing.Kind == WorkshopStock.Kinds[0]
                )
                .Take(3)
                .ToArray();
            for (int i = 0; i < picked.Length; i++)
            {
                var row = ChosenRow(chosen, "Chosen" + i);
                WorkshopSellView.PaintChosen(
                    row,
                    picked[i],
                    EntryOf(sell.Data, picked[i]),
                    ChosenArt
                );
                row.gameObject.SetActive(false);
                sell.ChosenRows.Add(row);
            }

            Layout.Line(layer, "TotalLine", left, totalTop, right - left, 2f);
            Layout.Text(
                layer,
                "TotalTitle",
                "合計",
                38,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                left,
                totalTop + 8f,
                200f,
                96f
            );
            sell.Total = RunePrice(
                layer,
                "Total",
                left + 200f,
                totalTop + 8f,
                right - left - 200f,
                96f,
                60f,
                TextAnchor.MiddleRight
            );
            sell.Total.text = "+0";

            (sell.Confirm, _) = ConfirmButton(layer, "売る");
            return sell.gameObject;
        }

        // 選んだ物の行（行の標準の形）：左に絵、名前のすぐ右に☆と品質・倍率、2行目に効果、
        // 右上に装備の属性のアイコン、右下にルーンのアイコンつきの値段（doc/rules/ui-design.md の装備の見せ方）。
        private static ItemTile ChosenRow(RectTransform content, string name) =>
            ItemRowAssets.Row(content, name, ChosenHeight, ChosenArt, ChosenValue, runeValue: true);

        private static LibraryEntryData EntryOf(LibraryData data, WorkshopOwned thing) =>
            data != null
                ? data.Entries.FirstOrDefault(entry =>
                    entry != null && entry.Kind == thing.Kind && entry.Id == thing.KindId
                )
                : null;
    }
}
