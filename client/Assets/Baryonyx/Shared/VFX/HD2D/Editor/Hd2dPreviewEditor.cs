using UnityEditor;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    // 停止中にも演出を表示するHD-2Dの演出のInspector。Inspectorの変更とUndoのたびに、
    // 次のEditorの更新で1回だけ表示を作り直す。再生中は作り直さない。
    public abstract class Hd2dPreviewEditor<T> : UnityEditor.Editor
        where T : MonoBehaviour
    {
        private bool previewUpdateQueued;

        // 停止中の表示を、今の設定から作り直す。
        protected abstract void Rebuild(T target);

        protected virtual void OnEnable()
        {
            Undo.undoRedoPerformed += QueuePreviewUpdate;
            QueuePreviewUpdate();
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= QueuePreviewUpdate;
            EditorApplication.delayCall -= RefreshPreview;
        }

        protected void QueuePreviewUpdate()
        {
            if (previewUpdateQueued)
                return;

            previewUpdateQueued = true;
            EditorApplication.delayCall += RefreshPreview;
        }

        private void RefreshPreview()
        {
            previewUpdateQueued = false;
            if (this == null)
                return;

            foreach (var editedTarget in targets)
            {
                if (editedTarget is T effect && !Application.IsPlaying(effect.gameObject))
                    Rebuild(effect);
            }
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
