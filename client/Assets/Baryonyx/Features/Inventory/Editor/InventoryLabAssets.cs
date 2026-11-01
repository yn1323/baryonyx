using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Inventory.Editor
{
    /// <summary>
    /// The inventory lab's screen (InventoryLabScreen.prefab), a debugging room that adds things
    /// to the saved inventory (<see cref="InventoryLab"/>): over a dark backdrop, the way back to
    /// home at the top left, the quality and how many at the top right, the category tabs, the
    /// kinds of the category in a grid, and the result of the last addition along the bottom.
    /// The weapons' rows are baked so the stopped screen shows the list.
    /// </summary>
    public static class InventoryLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Inventory/UI/InventoryLabScreen.prefab";

        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);
        private static readonly Color Backdrop = new(0.03f, 0.04f, 0.08f, 1f);

        // 16:9（幅1920）の画面にも、Editorの狭いGameビューにも収まる幅。
        private const float FrameWidth = 1700f;
        private const float LineWidth = 1660f;

        // Buttons are as tall as a finger needs (doc/rules/ui-design.md).
        private const float ButtonHeight = 128f;
        private const float TabHeight = 72f;

        private const int Columns = 4;
        private const float Gap = 16f;
        private static readonly Vector2 Cell = new(
            (LineWidth - Gap * (Columns - 1)) / Columns,
            ButtonHeight
        );

        [MenuItem("Baryonyx/Inventory/Create Inventory Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();

            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            using (UiBuild.Begin(font, shadowText))
            {
                var root = CanvasRoot("InventoryLabScreen");
                var lab = root.gameObject.AddComponent<InventoryLab>();
                AddImage(root, Backdrop, false);
                var safe = Rect("Frame", SafeArea(root));
                safe.anchorMin = safe.anchorMax = safe.pivot = new Vector2(0.5f, 1f);
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = new Vector2(FrameWidth, 1080f);

                BuildHeader(safe, lab);
                BuildList(safe, lab);
                lab.Status = Label(
                    safe,
                    "Status",
                    "",
                    32,
                    UiPalette.TextMain,
                    TextAlignmentOptions.MidlineLeft
                );
                Layout.At(lab.Status, 20f, 996f, LineWidth, 60f);

                lab.Art = ItemArtAssets.Load();
                lab.BuildRows(InventoryLabEntry.Of(InventoryLabTab.Weapons));
                lab.Tabs[0].Chosen = true;
                lab.QualityTabs[0].Chosen = true;
                lab.AmountTabs[0].Chosen = true;

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        // 左上のホームへ戻るボタンと画面の名前、右上の品質と数、その下の分類のタブ。
        private static void BuildHeader(RectTransform safe, InventoryLab lab)
        {
            var home = Layout.Box(safe, "Home", 20f, 8f, 200f, ButtonHeight);
            var (button, _, card) = GlassCardAssets.Choice(
                home,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            Stretch(
                Label(
                    card,
                    "Label",
                    "ホームへ",
                    32,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                ).rectTransform
            );
            lab.HomeButton = button;

            var title = Label(
                safe,
                "Heading",
                "持ち物のデバッグルーム",
                38,
                Gold,
                TextAlignmentOptions.Left
            );
            Layout.At(title, 260f, 46f, 600f, 52f);

            const float qualityWidth = 130f;
            var qualityRow = Layout.Box(safe, "QualityRow", 880f, 30f, qualityWidth * 4, TabHeight);
            lab.QualityRow = qualityRow.gameObject;
            lab.QualityTabs = GuideFrameAssets.Tabs(
                qualityRow,
                "Qualities",
                new[] { "品質S", "品質A", "品質B", "品質C" },
                0f,
                0f,
                qualityWidth,
                TabHeight,
                qualityWidth * 4
            );
            const float amountWidth = 120f;
            lab.AmountTabs = GuideFrameAssets.Tabs(
                safe,
                "Amounts",
                new[] { "1個", "10個" },
                20f + LineWidth - amountWidth * 2,
                30f,
                amountWidth,
                TabHeight,
                amountWidth * 2
            );

            lab.Tabs = GuideFrameAssets.Tabs(
                safe,
                "Tabs",
                new[] { "武器", "防具", "アクセサリー", "スキル", "アイテム", "強化素材" },
                20f,
                148f,
                LineWidth / 6f,
                TabHeight,
                LineWidth
            );
        }

        // 分類の種類の格子。行は分類に合わせてPlayの開始時に作り直すため、見本を置き、武器の行を焼き込む。
        private static void BuildList(RectTransform safe, InventoryLab lab)
        {
            var panel = Layout.Box(safe, "List", 20f, 236f, LineWidth, 744f);
            var viewport = Layout.Viewport(panel);
            var content = Layout.Grid(
                viewport,
                "Rows",
                Cell,
                Gap,
                new RectOffset(0, 0, 8, 24),
                Columns
            );
            lab.ListContent = content;
            lab.ListScroll = Layout.Scroll(panel, viewport, content);

            var row = Rect("RowTemplate", panel);
            row.sizeDelta = Cell;
            var (rowButton, _, rowCard) = GlassCardAssets.Choice(
                row,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );

            const float iconSize = 96f;
            var iconRect = Rect(InventoryLab.IconName, rowCard);
            Corner(
                iconRect,
                new Vector2(0f, 0.5f),
                new Vector2(16f + iconSize / 2f, 0f),
                Vector2.one * iconSize
            );
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            float textLeft = 16f + iconSize + 16f;
            var name = Label(
                rowCard,
                InventoryLab.LabelName,
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            // 長い名前（ギガントインパクトなど）は、札に収まるまで縮める。
            name.enableAutoSizing = true;
            name.fontSizeMin = 22f;
            name.fontSizeMax = 30f;
            name.overflowMode = TextOverflowModes.Truncate;
            Band(name.rectTransform, textLeft, 0.5f, 1f, 8f);
            var stars = Label(
                rowCard,
                InventoryLab.StarsName,
                "",
                24,
                Gold,
                TextAlignmentOptions.TopLeft
            );
            Band(stars.rectTransform, textLeft, 0f, 0.5f, 6f);
            row.gameObject.SetActive(false);
            lab.RowTemplate = rowButton;
        }

        // 札の横の、上下の割合で区切った帯。左は絵の右から、右は札の端の手前まで。
        private static void Band(RectTransform rect, float left, float bottom, float top, float gap)
        {
            rect.anchorMin = new Vector2(0f, bottom);
            rect.anchorMax = new Vector2(1f, top);
            rect.offsetMin = new Vector2(left, bottom > 0f ? gap : 0f);
            rect.offsetMax = new Vector2(-16f, top < 1f ? -gap : 0f);
        }
    }
}
