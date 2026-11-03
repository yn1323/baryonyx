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
    /// more choices (Home's list of debugging rooms) asks for more of them, in columns. Under
    /// them lies a row of buttons side by side for a confirmation, the × at the window's top
    /// right, and over everything a clear board that goes on when tapped anywhere.
    /// </summary>
    public static class GameDialogAssets
    {
        public const int ChoiceCount = 5;

        // 横に並べる選択肢の数と、1つの幅。
        public const int RowCount = 3;
        private const float RowWidth = 520f;

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

            // 確かめる窓のように、窓の下に横に並べる選択肢（左にやめる・つづける、右に決める方）。
            // 見た目は選択肢の役割で替える（doc/rules/ui-design.md#決めるボタン）。
            var row = GuideFrameAssets.ButtonRow(window);
            var rowButtons = new List<Button>();
            var rowLabels = new List<TMP_Text>();
            var rowFaces = new List<ButtonFaces>();
            for (int i = 0; i < RowCount; i++)
            {
                var (button, label, faces) = ActionButtonAssets.Switchable(
                    row,
                    "Choice" + i,
                    RowWidth,
                    rowHeight
                );
                rowButtons.Add(button);
                rowLabels.Add(label);
                rowFaces.Add(faces);
            }
            dialog.Row = row.gameObject;
            dialog.RowButtons = rowButtons.ToArray();
            dialog.RowLabels = rowLabels.ToArray();
            dialog.RowFaces = rowFaces.ToArray();
            row.gameObject.SetActive(false);

            // 右上の×。どの窓とも同じ形と場所にし、窓の縦の並びからは外す。
            var cross = Rect("Close", window);
            cross.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Corner(cross, Vector2.one, new Vector2(-8f, -4f), Vector2.one * UiSpace.Hit);
            dialog.Cross = GuideFrameAssets.CloseMark(cross);
            cross.gameObject.SetActive(false);

            // どこを押しても進む板。窓の上も含めて画面全体で受ける。
            var tap = Rect("TapAnywhere", root);
            Stretch(tap);
            dialog.TapAnywhere = AddTintButton(tap, AddImage(tap, Color.clear, true));
            dialog.TapAnywhere.transition = Selectable.Transition.None;
            tap.gameObject.SetActive(false);

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return dialog;
        }
    }
}
