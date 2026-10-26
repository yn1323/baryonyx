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
    /// Builds the trading house's "craft" panel (<see cref="WorkshopCraftView"/>), filling the
    /// screen: the kinds' tabs and the things that can be made on the left, and on the right the
    /// chosen one's big art, stars, effect and the quality it will have, the materials' cards,
    /// and the runes with the craft button. The first tab's recipes are baked from the library's
    /// mock records and the mock stock, so the prefab reads without Play Mode.
    /// </summary>
    public static class WorkshopCraftAssets
    {
        // 左の面（作れる物）と右の面（出来上がる物と材料）の境。右に材料を並べるため、右を広く取る。
        public const float Split = 800f;
        private const float RowHeight = 112f;
        private const float RowArt = 80f;
        private const float DetailArt = 224f;
        private const int MaterialColumns = 6;
        private const float MaterialHeight = 168f;
        private const float MaterialArt = 96f;
        private const float MaterialsTop = 440f;

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (craft, layer) = Panel<WorkshopCraftView>(safe, "WorkshopCraft", Split);
            craft.Guide = view;
            craft.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            craft.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            craft.ArtBox = RowArt;
            craft.DetailArtBox = DetailArt;
            craft.MaterialArtBox = MaterialArt;
            craft.Tabs = Tabs(layer, WorkshopStock.TabLabels, Split);

            var (content, scroll) = Column(
                layer,
                "Recipes",
                0f,
                Split,
                BodyTop,
                1080f,
                (int)Edge,
                (int)Inner
            );
            craft.Content = content;
            craft.List = scroll;
            craft.Template = ListRow(content, "Template", RowHeight, RowArt);
            // 読み込む間は、この写しを伏せて一覧を埋める（GlassCardCover）。
            GlassCardAssets.AddBack(craft.Template.Card);
            craft.Template.gameObject.SetActive(false);
            // 仮の記録と仮の持ち物の最初のタブ（武器）で描き、Prefabを開くと停止中でも読めるようにする。
            var stock = WorkshopStock.Mock();
            var recipes = WorkshopCraft
                .Recipes(craft.Data, LibraryMemorySource.Mock())
                .Where(recipe => recipe.Kind == WorkshopStock.Kinds[0])
                .ToArray();
            for (int i = 0; i < recipes.Length; i++)
            {
                var row = ListRow(content, "Recipe" + i, RowHeight, RowArt);
                GlassCardAssets.AddBack(row.Card);
                WorkshopCraftView.Paint(
                    row,
                    recipes[i],
                    WorkshopCraft.CanMake(recipes[i], stock),
                    RowArt
                );
                craft.Rows.Add(row);
            }
            craft.Status = Status(layer, 0f, Split, BodyTop);

            float left = Split + Inner;
            float right = Layout.Design.x - Edge;
            float textLeft = left + DetailArt + 48f;
            craft.DetailArt = ArtStage(layer, "DetailArt", left + 16f, 140f, DetailArt);
            craft.DetailName = Layout.Text(
                layer,
                "DetailName",
                "",
                54,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                textLeft,
                140f,
                right - textLeft,
                72f
            );
            Shrink(craft.DetailName, 32);
            craft.DetailStars = Layout.Text(
                layer,
                "DetailStars",
                "",
                32,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineLeft,
                textLeft,
                212f,
                300f,
                44f
            );
            craft.DetailStars.richText = true;
            craft.DetailQuality = Layout.Text(
                layer,
                "DetailQuality",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight,
                textLeft + 300f,
                212f,
                right - textLeft - 300f,
                44f
            );
            craft.DetailQuality.richText = true;
            craft.DetailEffect = Layout.Text(
                layer,
                "DetailEffect",
                "",
                28,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft,
                textLeft,
                266f,
                right - textLeft,
                110f
            );
            craft.DetailEffect.richText = true;
            craft.DetailEffect.textWrappingMode = TextWrappingModes.Normal;
            craft.DetailEffect.overflowMode = TextOverflowModes.Ellipsis;
            Shrink(craft.DetailEffect, 20);

            // 材料：見出しと選んだ数、その下に材料の札。
            var titleGroup = Rect("MaterialsTitle", layer);
            Stretch(titleGroup);
            GuideFrameAssets.Heading(
                titleGroup,
                "Title",
                "材料",
                left,
                MaterialsTop - 64f,
                300f,
                60f
            );
            craft.MaterialCount = Layout.Text(
                titleGroup,
                "MaterialCount",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight,
                right - 240f,
                MaterialsTop - 64f,
                240f,
                60f
            );
            craft.MaterialsTitle = titleGroup.gameObject;
            titleGroup.gameObject.SetActive(false);

            float materialsBottom = ConfirmTop - 64f;
            var (materials, materialScroll, cell) = Grid(
                layer,
                "Materials",
                Split,
                Layout.Design.x,
                MaterialsTop,
                materialsBottom,
                MaterialColumns,
                MaterialHeight,
                Inner,
                Edge
            );
            craft.MaterialContent = materials;
            craft.MaterialList = materialScroll;
            craft.MaterialTemplate = OwnedCard(materials, "Template", cell, MaterialArt);
            craft.MaterialTemplate.gameObject.SetActive(false);
            for (int i = 0; i < MaterialColumns; i++)
            {
                var row = OwnedCard(materials, "Material" + i, cell, MaterialArt);
                row.gameObject.SetActive(false);
                craft.MaterialRows.Add(row);
            }
            craft.MaterialStatus = Status(layer, Split, Layout.Design.x, MaterialsTop);

            float width = right - left;
            craft.Missing = Layout.Text(
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
            craft.PriceLabel = RunePrice(
                layer,
                "Price",
                left,
                ConfirmTop,
                width - ConfirmWidth - Inner,
                ConfirmHeight,
                56f,
                TextAnchor.MiddleRight
            );
            craft.Price = craft.PriceLabel.transform.parent.gameObject;
            craft.Price.SetActive(false);
            (craft.Confirm, _) = ConfirmButton(layer, "合成");
            return craft.gameObject;
        }
    }
}
