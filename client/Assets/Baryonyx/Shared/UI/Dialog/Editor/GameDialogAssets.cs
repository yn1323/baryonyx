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
    /// more choices (Home's list of debugging rooms) asks for more of them, in columns.
    /// </summary>
    public static class GameDialogAssets
    {
        public const int ChoiceCount = 5;

        /// <summary>
        /// Adds the dialog under <paramref name="parent"/>, which should span the whole screen.
        /// Call it inside a <see cref="Baryonyx.Editor.UI.UiBuild"/> session, after the screen's
        /// other parts, so the dialog lies over them.
        /// </summary>
        public static GameDialog Build(
            RectTransform parent,
            string name = "Dialog",
            int choiceCount = ChoiceCount,
            float rowHeight = 112f,
            int columns = 1
        )
        {
            var root = Rect(name, parent);
            Stretch(root);
            var veil = AddImage(root, UiPalette.Veil, true);
            veil.raycastTarget = true;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var dialog = root.gameObject.AddComponent<GameDialog>();
            dialog.Group = group;

            var safe = SafeArea(root);
            // 本文の1文（全角で約28字）が折り返さない、中の窓。
            var (window, title) = GuideFrameAssets.MessageWindow(
                safe,
                GuideFrameAssets.WindowSize.Medium
            );
            var layout = window.GetComponent<VerticalLayoutGroup>();
            dialog.Title = title;

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
                UiType.Lead,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            dialog.Body.textWrappingMode = TextWrappingModes.Normal;
            dialog.Body.lineSpacing = 16f;

            // 列に分けるときは、同じ形のカードを左から右へ、上の行から順に並べる。
            var list = window;
            if (columns > 1)
            {
                list = Rect("Choices", window);
                var grid = list.gameObject.AddComponent<GridLayoutGroup>();
                float width = window.sizeDelta.x - layout.padding.horizontal;
                grid.cellSize = new Vector2(
                    (width - layout.spacing * (columns - 1)) / columns,
                    rowHeight
                );
                grid.spacing = new Vector2(layout.spacing, layout.spacing);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
                grid.childAlignment = TextAnchor.UpperCenter;
            }

            var buttons = new List<Button>();
            var labels = new List<TMP_Text>();
            for (int i = 0; i < choiceCount; i++)
            {
                // 選択肢は同じ形で縦に並べ、どれも窓のボタン（決める方の金の縁なし）にする。
                var (button, label, _) = GuideFrameAssets.WindowButton(
                    list,
                    "Choice" + i,
                    "",
                    0f,
                    rowHeight,
                    primary: false
                );
                GuideMenuAssets.Shrink(label, 28);
                buttons.Add(button);
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
