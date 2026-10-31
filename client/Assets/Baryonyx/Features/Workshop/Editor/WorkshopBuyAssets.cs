using System.Linq;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
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
        private const int Columns = 4;
        private const float CardHeight = 272f;
        private const float CardArt = 128f;
        private const float DetailArt = 256f;

        // 右の面の中身の間と、足りないルーンの行の高さ。
        private const float DetailGap = 12f;
        private const float MissingHeight = 44f;

        // 絵・カードの真上の品質の1行の高さ（品質の文字は3倍の120px）。
        private const float QualityHeight = 130f;

        // スキルのデッキのカード（1ドットを4画素、280×392）。名前の行の下端まで使う。
        private const float CardDot = 4f;

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (buy, layer) = Panel<WorkshopBuyView>(safe, "WorkshopBuy");
            buy.Guide = view;
            buy.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            buy.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            buy.ArtBox = CardArt;
            buy.DetailArtBox = DetailArt;
            buy.Tabs = Tabs(layer, WorkshopBuyView.TabLabels);

            var (content, scroll, cell) = Grid(
                layer,
                "Goods",
                Rail,
                Split,
                ListTop,
                1080f,
                Columns,
                CardHeight,
                Inner
            );
            buy.Content = content;
            buy.List = scroll;
            buy.Template = GoodCard(content, "Template", cell, CardArt);
            // 読み込む間は、この写しを伏せて一覧を埋める（GlassCardCover）。
            GlassCardAssets.AddBack(buy.Template.Card);
            buy.Template.gameObject.SetActive(false);
            // 仮の記録の最初のタブ（武器）の品物で描き、Prefabを開くと停止中でも品物が読めるようにする。
            var goods = WorkshopGoods
                .Of(buy.Data, LibraryMemorySource.Mock())
                .Where(good => good.Kind == WorkshopGoods.Kinds[0])
                .ToArray();
            for (int i = 0; i < goods.Length; i++)
            {
                var row = GoodCard(content, "Good" + i, cell, CardArt);
                GlassCardAssets.AddBack(row.Card);
                WorkshopBuyView.Paint(row, goods[i], CardArt);
                buy.Rows.Add(row);
            }
            buy.Status = Status(layer, Rail, Split, ListTop);

            float left = Split + Inner;
            float right = Layout.Design.x - Edge;
            float width = right - left;

            // 右の面の中身（品質、絵かカード、名前、☆、効果、持っている数）を縦に並べ、
            // ヘッダーの下から足りないルーンの行の上までの間で上下中央に置く。
            float detailTop = GuideFrameAssets.HeaderHeight;
            var detail = Layout.Box(
                layer,
                "Detail",
                left,
                detailTop,
                width,
                ConfirmTop - MissingHeight - 12f - detailTop
            );
            var stack = detail.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.spacing = DetailGap;
            stack.childControlWidth = false;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;

            // 品質の1行：絵・カードの真上に、品質の文字を大きく、横に倍率。
            buy.DetailQuality = Layout.Text(
                detail,
                "DetailQuality",
                "",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                0f,
                0f,
                width,
                QualityHeight
            );
            buy.DetailQuality.richText = true;
            Fixed(buy.DetailQuality.transform, QualityHeight);
            buy.DetailArt = ArtStage(detail, "DetailArt", 0f, 0f, DetailArt);
            Fixed(buy.DetailArt.transform.parent, DetailArt + DetailGap * 2f);
            var cardSize = DeckCardAssets.Size(CardDot, small: false);
            buy.DetailCard = DeckCardAssets.Build(
                detail,
                "DetailCard",
                0f,
                0f,
                CardDot,
                button: false
            );
            var cardRoot = buy.DetailCard.Frame.transform.parent;
            Fixed(cardRoot, cardSize.y);
            cardRoot.gameObject.SetActive(false);
            buy.CardFrames = DeckCardFrames();
            buy.ElementIcons = ElementIcons();
            buy.DetailName = Layout.Text(
                detail,
                "DetailName",
                "",
                56,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                0f,
                0f,
                width,
                72f
            );
            Shrink(buy.DetailName, 32);
            Fixed(buy.DetailName.transform, 72f);
            buy.DetailLine = Layout.Text(
                detail,
                "DetailLine",
                "",
                34,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                0f,
                0f,
                width,
                56f
            );
            buy.DetailLine.richText = true;
            Fixed(buy.DetailLine.transform, 56f);
            buy.DetailEffect = Layout.Text(
                detail,
                "DetailEffect",
                "",
                32,
                UiPalette.TextSub,
                TextAlignmentOptions.Top,
                0f,
                0f,
                width,
                100f
            );
            buy.DetailEffect.richText = true;
            buy.DetailEffect.textWrappingMode = TextWrappingModes.Normal;
            buy.DetailOwned = Layout.Text(
                detail,
                "DetailOwned",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                0f,
                0f,
                width,
                48f
            );
            Fixed(buy.DetailOwned.transform, 48f);
            buy.Missing = Layout.Text(
                layer,
                "Missing",
                "",
                28,
                new Color(1f, 0.54f, 0.48f),
                TextAlignmentOptions.Right,
                left,
                ConfirmTop - MissingHeight - 12f,
                width,
                MissingHeight
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

        // 並べる段の高さを決める（並べ方が中身から高さを測らないようにする）。
        private static void Fixed(Transform part, float height) =>
            part.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }
}
