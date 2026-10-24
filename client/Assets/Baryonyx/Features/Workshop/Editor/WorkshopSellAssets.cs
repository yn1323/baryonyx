using System.Linq;
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
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using static Baryonyx.Workshop.Editor.WorkshopPanelAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// Builds the trading house's "sell" panel (<see cref="WorkshopSellView"/>), filling the
    /// screen: the kinds' tabs and the held things' cards on the left, one card for each thing,
    /// and the chosen ones, the total and the buttons on the right. The first tab's cards and
    /// three chosen rows are baked from the mock stock, so the prefab reads without Play Mode.
    /// </summary>
    public static class WorkshopSellAssets
    {
        public const float Split = WorkshopBuyAssets.Split;
        private const int Columns = 5;
        private const float CardHeight = 200f;
        private const float CardArt = 112f;

        // 装備は品質の下に効果を書くため、名前・品質・効果の3行が入る高さにする。
        private const float ChosenHeight = 150f;
        private const float ChosenArt = 72f;
        private const float ClearWidth = 300f;

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (sell, layer) = Panel<WorkshopSellView>(safe, "WorkshopSell", Split);
            sell.Guide = view;
            sell.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            sell.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            sell.ArtBox = CardArt;
            sell.ChosenArtBox = ChosenArt;
            sell.Tabs = Tabs(layer, WorkshopStock.TabLabels, Split);

            var (content, scroll, cell) = Grid(
                layer,
                "Owned",
                0f,
                Split,
                BodyTop,
                1080f,
                Columns,
                CardHeight
            );
            sell.Content = content;
            sell.List = scroll;
            sell.Template = OwnedCard(content, "Template", cell, CardArt);
            sell.Template.gameObject.SetActive(false);
            var mock = WorkshopStock.Mock();
            var first = WorkshopStock.OfKind(mock, WorkshopStock.Kinds[0]);
            for (int i = 0; i < first.Count; i++)
            {
                var row = OwnedCard(content, "Owned" + i, cell, CardArt);
                WorkshopSellView.Paint(row, first[i], EntryOf(sell.Data, first[i]), CardArt);
                sell.Rows.Add(row);
            }
            sell.Status = Status(layer, 0f, Split, BodyTop);

            float left = Split + Inner;
            float right = Layout.Design.x - Edge;
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
            sell.ChosenTemplate = ListRow(chosen, "Template", ChosenHeight, ChosenArt, 34f, 180f);
            sell.ChosenTemplate.gameObject.SetActive(false);
            var picked = mock.Where(thing =>
                    thing.User == null && thing.Kind == WorkshopStock.Kinds[0]
                )
                .Take(3)
                .ToArray();
            for (int i = 0; i < picked.Length; i++)
            {
                var row = ListRow(chosen, "Chosen" + i, ChosenHeight, ChosenArt, 34f, 180f);
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

            var (clear, _, inside) = GlassCardAssets.Choice(
                Layout.Box(
                    layer,
                    "Clear",
                    right - ConfirmWidth - Inner - ClearWidth,
                    ConfirmTop,
                    ClearWidth,
                    ConfirmHeight
                ),
                pattern: false
            );
            var label = Label(
                inside,
                "Label",
                "選びなおす",
                36,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            clear.interactable = false;
            sell.Clear = clear;
            (sell.Confirm, _) = ConfirmButton(layer, "売る");
            return sell.gameObject;
        }

        private static LibraryEntryData EntryOf(LibraryData data, WorkshopOwned thing) =>
            data != null
                ? data.Entries.FirstOrDefault(entry =>
                    entry != null && entry.Kind == thing.Kind && entry.Id == thing.KindId
                )
                : null;
    }
}
