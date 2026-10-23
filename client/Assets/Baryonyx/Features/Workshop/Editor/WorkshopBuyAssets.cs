using System.Linq;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.UI;
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
    /// Builds the trading house's "buy" panel (<see cref="WorkshopBuyView"/>), filling the screen
    /// without the guide: the kinds' tabs and the goods' grid on the left, the chosen good's big
    /// art, quality, effect and price with the buy button on the right. The first tab's cards
    /// are baked from the library's mock records, so the prefab reads without Play Mode; the
    /// view draws what the player found when it opens.
    /// </summary>
    public static class WorkshopBuyAssets
    {
        // 左の面（一覧）と右の面（選んだ品物）の境。一覧を広く取る。
        public const float Split = 1160f;
        private const int Columns = 4;
        private const float CardHeight = 272f;
        private const float CardArt = 128f;
        private const float DetailArt = 256f;

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (buy, layer) = Panel<WorkshopBuyView>(safe, "WorkshopBuy", Split);
            buy.Guide = view;
            buy.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            buy.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            buy.ArtBox = CardArt;
            buy.DetailArtBox = DetailArt;
            buy.Tabs = Tabs(layer, WorkshopBuyView.TabLabels, Split);

            var (content, scroll, cell) = Grid(
                layer,
                "Goods",
                0f,
                Split,
                BodyTop,
                1080f,
                Columns,
                CardHeight
            );
            buy.Content = content;
            buy.List = scroll;
            buy.Template = GoodCard(content, "Template", cell, CardArt);
            buy.Template.gameObject.SetActive(false);
            // 仮の記録の最初のタブ（武器）の品物で描き、Prefabを開くと停止中でも品物が読めるようにする。
            var goods = WorkshopGoods
                .Of(buy.Data, LibraryMemorySource.Mock())
                .Where(good => good.Kind == WorkshopGoods.Kinds[0])
                .ToArray();
            for (int i = 0; i < goods.Length; i++)
            {
                var row = GoodCard(content, "Good" + i, cell, CardArt);
                WorkshopBuyView.Paint(row, goods[i], CardArt);
                buy.Rows.Add(row);
            }
            buy.Status = Status(layer, 0f, Split, BodyTop);

            float left = Split + Inner;
            float right = Layout.Design.x - Edge;
            float width = right - left;
            buy.DetailArt = ArtStage(
                layer,
                "DetailArt",
                left + (width - DetailArt) / 2f,
                150f,
                DetailArt
            );
            buy.DetailName = Layout.Text(
                layer,
                "DetailName",
                "",
                56,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                left,
                440f,
                width,
                72f
            );
            Shrink(buy.DetailName, 32);
            buy.DetailLine = Layout.Text(
                layer,
                "DetailLine",
                "",
                32,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                left,
                516f,
                width,
                48f
            );
            buy.DetailLine.richText = true;
            buy.DetailEffect = Layout.Text(
                layer,
                "DetailEffect",
                "",
                30,
                UiPalette.TextSub,
                TextAlignmentOptions.Top,
                left,
                572f,
                width,
                100f
            );
            buy.DetailEffect.richText = true;
            buy.DetailEffect.textWrappingMode = TextWrappingModes.Normal;
            buy.DetailEffect.overflowMode = TextOverflowModes.Ellipsis;
            Shrink(buy.DetailEffect, 22);
            buy.DetailOwned = Layout.Text(
                layer,
                "DetailOwned",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                left,
                692f,
                width,
                48f
            );
            buy.Missing = Layout.Text(
                layer,
                "Missing",
                "",
                28,
                new Color(1f, 0.54f, 0.48f),
                TextAlignmentOptions.Right,
                left,
                ConfirmTop - 56f,
                width,
                44f
            );

            buy.PriceLabel = RunePrice(
                layer,
                "Price",
                left,
                ConfirmTop,
                width - ConfirmWidth - Inner,
                ConfirmHeight,
                60f,
                TextAnchor.MiddleLeft
            );
            buy.Price = buy.PriceLabel.transform.parent.gameObject;
            buy.Price.SetActive(false);
            (buy.Confirm, _) = ConfirmButton(layer, "買う");
            return buy.gameObject;
        }
    }
}
