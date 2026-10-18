using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baryonyx.UI.EditModePreview.Editor
{
    /// <summary>
    /// The inspector of an <see cref="IEditModePreview"/> screen: a button per state that shows
    /// it while the editor is stopped (<see cref="EditModePreviewer"/>), above the usual fields.
    /// A screen gets it with an empty subclass:
    /// <c>[CustomEditor(typeof(FooView))] sealed class FooViewEditor : EditModePreviewEditor { }</c>.
    /// </summary>
    public abstract class EditModePreviewEditor : UnityEditor.Editor
    {
        public const string Title = "停止中の表示の切り替え";
        public const string BackLabel = "元の表示";

        private VisualElement preview;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var foldout = new Foldout { text = Title, value = true };
            preview = new VisualElement();
            foldout.Add(preview);
            root.Add(foldout);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EditModePreviewer.Changed += Rebuild;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                EditorApplication.hierarchyChanged += Rebuild;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                EditModePreviewer.Changed -= Rebuild;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorApplication.hierarchyChanged -= Rebuild;
            });
            Rebuild();
            return root;
        }

        private void OnPlayModeChanged(PlayModeStateChange _) => Rebuild();

        private void Rebuild()
        {
            if (preview == null)
                return;
            preview.Clear();
            var view = target as Component;
            if (view == null || view is not IEditModePreview screen)
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                preview.Add(Help("再生中は、画面のボタンで切り替えてください。"));
                return;
            }
            if (EditModePreviewer.IsCopy(view))
            {
                preview.Add(
                    Help(
                        "停止中の表示の複製です。切り替えと編集は、元のオブジェクトで行ってください。"
                    )
                );
                return;
            }
            var states = screen.PreviewStates;
            if (states == null || states.Count == 0)
            {
                preview.Add(Help("切り替える表示がありません。"));
                return;
            }
            if (!EditModePreviewer.CanShow(view))
            {
                preview.Add(
                    Help(
                        "この画面は閉じたパネルの中にあります。パネルを開く画面（親）のInspectorで切り替えてください。"
                    )
                );
                return;
            }

            preview.Add(
                new HelpBox(
                    "停止中に、Play中に切り替わる表示を仮データで見られます。"
                        + "シーンやPrefabの保存と再生の前に、自動で元の表示へ戻します。",
                    HelpBoxMessageType.Info
                )
            );
            int shown = EditModePreviewer.StateOf(view);
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap },
            };
            row.Add(
                Choice(
                    BackLabel,
                    shown == EditModePreviewer.None,
                    () => EditModePreviewer.Restore(view)
                )
            );
            for (int i = 0; i < states.Count; i++)
            {
                int index = i;
                row.Add(
                    Choice(states[i], shown == index, () => EditModePreviewer.Show(view, index))
                );
            }
            preview.Add(row);
        }

        private static Button Choice(string label, bool current, Action pressed)
        {
            var button = new Button(pressed) { text = label };
            button.SetEnabled(!current);
            button.style.minWidth = 72;
            return button;
        }

        private static HelpBox Help(string message) => new(message, HelpBoxMessageType.None);
    }
}
