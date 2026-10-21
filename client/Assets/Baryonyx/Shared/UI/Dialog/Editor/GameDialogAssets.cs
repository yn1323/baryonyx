using System.Collections.Generic;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the shared <see cref="GameDialog"/> into a screen: the veil over the whole screen,
    /// and in the safe area the guide frame's window (<see cref="GuideFrameAssets.WindowFrame"/>)
    /// with the title, an optional picture, the body and five rows of choices as frosted glass
    /// cards (<see cref="GlassCardAssets"/>), like the formation's level-up. A screen that needs
    /// more choices (Home's list of debugging rooms) asks for more, lower rows.
    /// </summary>
    public static class GameDialogAssets
    {
        public const int ChoiceCount = 5;

        private static readonly Color Veil = new(0.012f, 0.02f, 0.04f, 0.72f);

        /// <summary>
        /// Adds the dialog under <paramref name="parent"/>, which should span the whole screen.
        /// Call it inside a <see cref="Baryonyx.Editor.UI.UiBuild"/> session, after the screen's
        /// other parts, so the dialog lies over them.
        /// </summary>
        public static GameDialog Build(
            RectTransform parent,
            string name = "Dialog",
            int choiceCount = ChoiceCount,
            float rowHeight = 112f
        )
        {
            var root = Rect(name, parent);
            Stretch(root);
            var veil = AddImage(root, Veil, true);
            veil.raycastTarget = true;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var dialog = root.gameObject.AddComponent<GameDialog>();
            dialog.Group = group;

            var safe = SafeArea(root);
            var window = Rect("Window", safe);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            // 本文の1文（全角で約28字）が折り返さない幅。
            window.sizeDelta = new Vector2(1360, 0);
            GuideFrameAssets.WindowFrame(window);
            var fitter = window.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(72, 72, 56, 64);
            layout.spacing = 28;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            dialog.Title = Label(
                window,
                "Title",
                "",
                56,
                UiPalette.GoldLight,
                TextAlignmentOptions.Center
            );

            var picture = Rect("Picture", window);
            var pictureSize = picture.gameObject.AddComponent<LayoutElement>();
            pictureSize.preferredHeight = pictureSize.minHeight = GuideMenuAssets.IconSize;
            var image = picture.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            dialog.Picture = image;

            dialog.Body = Label(
                window,
                "Body",
                "",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            dialog.Body.textWrappingMode = TextWrappingModes.Normal;
            dialog.Body.lineSpacing = 16f;

            var buttons = new List<Button>();
            var labels = new List<TMP_Text>();
            for (int i = 0; i < choiceCount; i++)
            {
                var row = Rect("Choice" + i, window);
                var size = row.gameObject.AddComponent<LayoutElement>();
                size.preferredHeight = size.minHeight = rowHeight;
                // 選択肢は同じ形で縦に並べ、どれも模様のないすりガラスのカードにする。
                var (button, _, card) = GlassCardAssets.Choice(row, pattern: false);
                buttons.Add(button);
                var label = Label(
                    card,
                    "Label",
                    "",
                    44,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                );
                Stretch((RectTransform)label.transform);
                GuideMenuAssets.Shrink(label, 28);
                labels.Add(label);
            }
            dialog.Buttons = buttons.ToArray();
            dialog.Labels = labels.ToArray();

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return dialog;
        }
    }
}
