using System.Linq;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Enhancement;
using Baryonyx.Inventory.Editor;
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
        private const float RowHeight = 112f;
        private const float RowArt = 80f;
        private const float DetailArt = 192f;
        private const float MaterialsTop = 440f;

        // 材料の札：一辺160の正方形を4つ横に並べ、下に名前。レシピの材料は多くて4種類。
        private const int MaterialSlots = 4;
        private const float MaterialTile = 160f;
        private const float MaterialArt = 96f;

        // 右の面の☆の欄の幅（☆5つ）。残りに出来上がる品質を書く。
        private const float StarsWidth = 180f;

        // 使う物を選ぶ画面：札を4列で、画面の上から下まで並べる。
        private const int PieceColumns = 4;
        private static readonly Vector2 PieceCell = new(222f, 236f);
        private const float PieceGap = 18f;
        private const float PieceArt = 96f;

        // 合成の演出の渦の線のシェーダーとマテリアル。
        private const string SwirlShaderPath =
            WorkshopScreenAssets.Folder + "/Vfx/Shaders/WorkshopCraftSwirl.shader";
        private const string SwirlMaterialFolder = WorkshopScreenAssets.Folder + "/Vfx/Materials";

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var (craft, layer) = Panel<WorkshopCraftView>(safe, "WorkshopCraft");
            craft.Guide = view;
            craft.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            craft.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            craft.ArtBox = RowArt;
            craft.DetailArtBox = DetailArt;
            craft.MaterialArtBox = MaterialArt;
            craft.Tabs = Tabs(layer, WorkshopCraft.TabLabels);
            // 磨き石のタブの石の絵。強化の画面と同じ ItemArt から写す。
            var itemArt = ItemArtAssets.Load();
            craft.StoneArts = EnhancementMaterials
                .Polishes.Select(stone => (Texture)itemArt.TextureOf(stone.Id))
                .ToArray();

            var (content, scroll) = Column(
                layer,
                "Recipes",
                Rail,
                Split,
                ListTop,
                1080f,
                (int)Inner,
                (int)Inner
            );
            craft.Content = content;
            craft.List = scroll;
            craft.Template = ListRow(content, "Template", RowHeight, RowArt);
            // 読み込む間は、この写しを伏せて一覧を埋める（GlassCardCover）。
            GlassCardAssets.AddBack(craft.Template.Card, vertical: true);
            craft.Template.gameObject.SetActive(false);
            // 仮の記録と仮の持ち物の最初のタブ（武器）で描き、Prefabを開くと停止中でも読めるようにする。
            var stock = WorkshopStock.Mock();
            var recipes = WorkshopCraft
                .Recipes(craft.Data, LibraryMemorySource.Mock())
                .Where(recipe => recipe.Kind == WorkshopCraft.Kinds[0])
                .ToArray();
            for (int i = 0; i < recipes.Length; i++)
            {
                var row = ListRow(content, "Recipe" + i, RowHeight, RowArt);
                GlassCardAssets.AddBack(row.Card, vertical: true);
                WorkshopCraftView.Paint(
                    row,
                    recipes[i],
                    WorkshopCraft.CanMake(recipes[i], stock),
                    RowArt
                );
                craft.Rows.Add(row);
            }
            craft.Status = Status(layer, Rail, Split, ListTop);

            float left = Split + Inner;
            float right = Layout.Design.x - Edge;
            float textLeft = left + DetailArt + 40f;
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
                StarsWidth,
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
                textLeft + StarsWidth,
                200f,
                right - textLeft - StarsWidth,
                64f
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

            // 材料：見出しと、レシピの材料の札を横に並べる。
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
            craft.MaterialsTitle = titleGroup.gameObject;
            titleGroup.gameObject.SetActive(false);
            float gap = (right - left - MaterialTile * MaterialSlots) / (MaterialSlots - 1);
            for (int i = 0; i < MaterialSlots; i++)
            {
                var tile = MaterialTile(
                    layer,
                    "Material" + i,
                    left + i * (MaterialTile + gap),
                    MaterialsTop,
                    MaterialTile,
                    MaterialArt
                );
                tile.gameObject.SetActive(false);
                craft.MaterialRows.Add(tile);
            }

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
            craft.Picker = BuildPicker((RectTransform)craft.transform);
            craft.Swirl = BuildSwirl((RectTransform)craft.transform);
            return craft.gameObject;
        }

        // 合成の演出（光の渦）：画面全体の暗転と、計算で描く光の板（doc/features/synthesis.md 合成の演出）。
        // 渦の線は専用のシェーダー、ほかの光は戦闘の形のマテリアルを使う。押すまで隠しておく。
        private static WorkshopCraftSwirl BuildSwirl(RectTransform root)
        {
            var screen = Rect("CraftSwirl", root);
            Stretch(screen);
            var swirl = screen.gameObject.AddComponent<WorkshopCraftSwirl>();

            // Safe Areaの外まで暗くし、押すと演出を飛ばす。
            var dim = Rect("Dim", screen);
            Stretch(dim);
            dim.offsetMin = new Vector2(-400, -400);
            dim.offsetMax = new Vector2(400, 400);
            swirl.Dim = AddImage(dim, new Color(0.02f, 0.03f, 0.08f, 0f), true);
            swirl.Skip = GuideMenuAssets.AddButton(dim, swirl.Dim);
            swirl.Skip.transition = UnityEngine.UI.Selectable.Transition.None;

            // 渦の見た目（通常・金・虹）ごとのマテリアル。腕の数と淡い虹の強さを変える。
            swirl.HaloLooks = new[]
            {
                SwirlMaterial("CraftSwirlHalo", 0f, 6f, 0f),
                SwirlMaterial("CraftSwirlHaloGold", 0f, 8f, 0f),
                SwirlMaterial("CraftSwirlHaloRainbow", 0f, 8f, 1f),
            };
            swirl.LineLooks = new[]
            {
                SwirlMaterial("CraftSwirlLine", 1f, 6f, 0f),
                SwirlMaterial("CraftSwirlLineGold", 1f, 8f, 0f),
                SwirlMaterial("CraftSwirlLineRainbow", 1f, 8f, 1f),
            };
            swirl.SwirlHalo = Board(screen, "SwirlHalo", swirl.HaloLooks[0]);
            swirl.SwirlLine = Board(screen, "SwirlLine", swirl.LineLooks[0]);
            swirl.Glints = Boards(screen, "Glint", 8, Shape("Sparkle"));
            swirl.Converge = Boards(screen, "Converge", 20, Shape("Streak"));
            swirl.OrbHalo = Board(screen, "OrbHalo", Shape("Glow"));
            const int pieces = MaterialSlots;
            swirl.PieceTrails = Boards(screen, "PieceTrail", pieces, Shape("Streak"));
            swirl.PieceGlows = Boards(screen, "PieceGlow", pieces, Shape("Glow"));
            swirl.Pieces = new UnityEngine.UI.RawImage[pieces];
            for (int i = 0; i < pieces; i++)
            {
                var piece = Rect("Piece" + i, screen);
                Center(piece);
                var art = piece.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                art.raycastTarget = false;
                piece.gameObject.SetActive(false);
                swirl.Pieces[i] = art;
            }
            swirl.Sparks = Boards(screen, "Spark", 12, Shape("Spark"));
            swirl.OrbCore = Board(screen, "OrbCore", Shape("Glow"));
            swirl.ItemRings = Boards(screen, "ItemRing", 2, Shape("Ring"));
            swirl.Ring = Board(screen, "Ring", Shape("Ring"));
            swirl.MoreRings = Boards(screen, "MoreRing", 3, Shape("Ring"));
            swirl.Rays = Board(screen, "Rays", Shape("Star"));
            swirl.MoreRays = Board(screen, "MoreRays", Shape("Star"));
            swirl.Flakes = Boards(screen, "Flake", 16, Shape("Shard"));
            swirl.Prisms = Boards(screen, "Prism", 18, Shape("Sparkle"));
            swirl.ItemGlow = Board(screen, "ItemGlow", Shape("Glow"));
            var item = Rect("Item", screen);
            Center(item);
            swirl.Item = item.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            swirl.Item.raycastTarget = false;
            item.gameObject.SetActive(false);
            swirl.Sheen = Board(screen, "Sheen", Shape("Sparkle"));
            swirl.Twinkles = Boards(screen, "Twinkle", 6, Shape("Sparkle"));

            BattleVfxImage.EnableShapeChannel(screen);
            screen.gameObject.SetActive(false);
            return swirl;
        }

        private static BattleVfxImage[] Boards(
            RectTransform parent,
            string name,
            int count,
            Material material
        )
        {
            var boards = new BattleVfxImage[count];
            for (int i = 0; i < count; i++)
                boards[i] = Board(parent, name + i, material);
            return boards;
        }

        private static BattleVfxImage Board(RectTransform parent, string name, Material material)
        {
            var rect = Rect(name, parent);
            Center(rect);
            var board = rect.gameObject.AddComponent<BattleVfxImage>();
            board.material = material;
            board.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return board;
        }

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static Material Shape(string name)
        {
            var path = $"{BattleSkillVfxAssets.MaterialFolder}/VfxShape{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                throw new System.InvalidOperationException("Missing material: " + path);
            return material;
        }

        private static Material SwirlMaterial(string name, float layer, float arms, float rainbow)
        {
            AssetFolders.Ensure(SwirlMaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SwirlShaderPath);
            if (shader == null)
                throw new System.InvalidOperationException("Missing shader: " + SwirlShaderPath);
            var path = $"{SwirlMaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_Layer", layer);
            material.SetFloat("_Arms", arms);
            material.SetFloat("_Rainbow", rainbow);
            material.SetFloat("_Intensity", layer < 0.5f ? 1f : 1.3f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 使う物を選ぶ画面：画面全体を薄暗くし、手前に札を4列で並べて上下に動かし、右下に戦闘と同じ丸いチェック。
        // 枠・背景の板・見出しは置かない（doc/features/screens/shop.md の使う物を選ぶ画面）。
        private static WorkshopMaterialPicker BuildPicker(RectTransform root)
        {
            var screen = Rect("MaterialPicker", root);
            Stretch(screen);
            var picker = screen.gameObject.AddComponent<WorkshopMaterialPicker>();
            picker.ArtBox = PieceArt;

            // 背面の「もどる」や合成を押せないよう、Safe Areaの外まで暗くする。札の外を押すと閉じる。
            var dim = Rect("Dim", screen);
            Stretch(dim);
            dim.offsetMin = new Vector2(-400, -400);
            dim.offsetMax = new Vector2(400, 400);
            picker.Dim = GuideMenuAssets.AddButton(
                dim,
                AddImage(dim, new Color(0f, 0f, 0.02f, 0.72f), true)
            );

            var layer = Layout.Layer(screen, "Layout");
            float gridWidth = PieceColumns * PieceCell.x + (PieceColumns - 1) * PieceGap;
            float x = (Layout.Design.x - gridWidth) / 2f;
            var viewport = Layout.Box(
                layer,
                "PiecesViewport",
                x - 16f,
                0f,
                gridWidth + 32f,
                Layout.Design.y
            );
            Layout.Mask(viewport);
            var content = Layout.Grid(
                viewport,
                "Pieces",
                PieceCell,
                PieceGap,
                new RectOffset(16, 16, 40, 40),
                PieceColumns
            );
            picker.Content = content;
            picker.List = Layout.Scroll(viewport, viewport, content);
            picker.Template = PieceCard(content, "Template", PieceCell, PieceArt);
            picker.Template.gameObject.SetActive(false);

            // 右下の丸いチェック（戦闘のターン終了と同じ絵）。
            picker.Confirm = RoundCheck(layer);
            screen.gameObject.SetActive(false);
            return picker;
        }
    }
}
