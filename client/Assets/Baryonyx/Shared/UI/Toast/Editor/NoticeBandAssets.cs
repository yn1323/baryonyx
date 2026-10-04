using TMPro;
using UnityEditor;
using UnityEngine;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the shared notice band into a screen: the translucent text panel with the dark
    /// backdrop and white text of the battle's skill names, at the top middle of the screen
    /// between the back button and the title.
    /// Every notice that asks nothing of the player uses it (doc/rules/ui-design.md).
    /// </summary>
    public static class NoticeBandAssets
    {
        // 画面の上端から104pxの位置（戻るボタンや画面の題名と同じ高さの、左右の間）に帯の中心を置く。
        // 画面の中身の上に重ならず、どの画面でも同じ場所に出る。
        public const float Top = 104f;

        // 戦闘画面の技名と同じ暗い帯。
        public static readonly Color BackdropColor = new(0.02f, 0.025f, 0.04f);
        public const float BackdropAlpha = 0.88f;
        public const float FontSize = 44f;

        /// <summary>
        /// Adds the band under <paramref name="parent"/>, which should span the whole screen.
        /// Call it inside a <see cref="Baryonyx.Editor.UI.UiBuild"/> session.
        /// </summary>
        public static NoticeBand Build(RectTransform parent, string name = "Notice")
        {
            TranslucentTextPanelAssets.EnsurePrefab();
            var instance = InstantiatePrefab(TranslucentTextPanelAssets.PrefabPath, name, parent);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, -Top);
            rect.sizeDelta = new Vector2(1100, 80);

            var panel = instance.GetComponent<TranslucentTextPanel>();
            panel.Backdrop.color = BackdropColor;
            panel.SetBackdropSize(new Vector2(1240, 112));
            panel.SetBackdropAlpha(BackdropAlpha);
            panel.SetFontSize(FontSize);
            panel.SetText("");
            panel.Label.color = Color.white;
            panel.Label.alignment = TextAlignmentOptions.Center;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Backdrop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.BackdropCanvas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Label);

            var group = instance.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var band = instance.AddComponent<NoticeBand>();
            band.Group = group;
            band.Label = panel.Label;
            band.Panel = panel;
            return band;
        }
    }
}
