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

        // 足りないルーンの行の高さ。
        private const float MissingHeight = 44f;

        // スキルのデッキのカード（1ドットを4画素、280×392）。名前の行の下端まで使う。
        private const float CardDot = 4f;

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (buy, panel) = GuidePanelTemplate.GridDetail<WorkshopBuyView>(
                safe,
                "WorkshopBuy",
                WorkshopBuyView.TabLabels[0],
                Columns,
                CardHeight,
                WorkshopBuyView.TabLabels
            );
            var layer = panel.Layer;
            buy.Guide = view;
            buy.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            buy.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            buy.ArtBox = CardArt;
            buy.DetailArtBox = DetailArt;
            buy.Tabs = panel.Tabs;
            buy.ListTitle = panel.Title;
            buy.ListCount = panel.Count;
            var content = panel.Content;
            var cell = panel.Cell;
            buy.Content = content;
            buy.List = panel.Scroll;
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
            buy.Status = panel.Status;
            buy.ListCount.text = UiText.Count(goods.Length);

            float left = GuidePanelTemplate.DetailLeft;
            float right = GuidePanelTemplate.DetailRight;
            float width = right - left;

            // 右の面は詳しい面（品質と倍率・絵・名前・☆・効果・属性。doc/rules/ui-design.md の装備の見せ方）。
            // スキルは絵・名前・効果の代わりにデッキのカードを絵の位置に出す。その下に持っている数。
            // ヘッダーの下から足りないルーンの行の上までの間で上下中央に置く。
            float detailTop = GuideFrameAssets.HeaderHeight;
            var detail = ItemDetailAssets.Column(
                layer,
                "Detail",
                left,
                detailTop,
                width,
                ConfirmTop - MissingHeight - 12f - detailTop
            );
            var face = ItemDetailAssets.Face(detail, width, DetailArt);
            buy.DetailQuality = face.Quality;
            buy.DetailArt = face.Art;
            buy.DetailName = face.Name;
            buy.DetailLine = face.Stars;
            buy.DetailEffect = face.Effect;
            buy.DetailElements = face.Elements;
            face.ArtStage.SetActive(false);
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
            cardRoot.SetSiblingIndex(face.ArtStage.transform.GetSiblingIndex() + 1);
            cardRoot.gameObject.SetActive(false);
            buy.CardFrames = DeckCardFrames();
            buy.ElementIcons = ElementIcons();
            buy.DetailOwned = Layout.Text(
                detail,
                "DetailOwned",
                "",
                UiType.Small,
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
            // 買ったときの演出：合成の演出（光の渦）の部品を、渦を使わずに終わりだけ流す。
            buy.Reveal = WorkshopCraftAssets.BuildSwirl((RectTransform)buy.transform, "BuyReveal");
            return buy.gameObject;
        }

        // 並べる段の高さを決める（並べ方が中身から高さを測らないようにする）。
        private static void Fixed(Transform part, float height) =>
            part.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }
}
