using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Baryonyx.UI.EditModePreview.Editor
{
    /// <summary>
    /// Shows a state of an <see cref="IEditModePreview"/> screen while the editor is stopped
    /// (doc/rules/ui-design.md#停止中の表示の切り替え). It hides the screen and puts an unsaved
    /// copy beside it, then switches the copy, so the saved screen keeps everything but its
    /// active flag. The screen comes back before a scene or prefab is saved, before its scene
    /// is closed and before Play Mode, so a preview is never saved or left over another scene.
    /// Nothing is recorded for Undo or marked dirty.
    /// </summary>
    [InitializeOnLoad]
    public static class EditModePreviewer
    {
        public const int None = -1;
        public const string CopySuffix = " (停止中の表示)";
        private const HideFlags CopyFlags = HideFlags.DontSave | HideFlags.NotEditable;

        static EditModePreviewer()
        {
            EditorSceneManager.sceneSaving += (scene, _) =>
                RestoreWhere(shown => shown.Source.scene == scene);
            // 保存しない印の付いた物は、シーンを閉じても破棄されず、次に開いたシーンの上に残る。
            EditorSceneManager.sceneClosing += (scene, _) =>
                RestoreWhere(shown => shown.Source.scene == scene);
            EditorSceneManager.sceneOpened += (_, _) => DestroyStrayCopies();
            EditorApplication.delayCall += DestroyStrayCopies;
            PrefabStage.prefabSaving += root =>
                RestoreWhere(shown => shown.Source.transform.IsChildOf(root.transform));
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode)
                    RestoreWhere(_ => true);
            };
        }

        /// <summary>Raised after a state is shown or the screen is put back.</summary>
        public static event Action Changed;

        /// <summary>The state shown for <paramref name="view"/>, or <see cref="None"/>.</summary>
        public static int StateOf(Component view)
        {
            var shown = Find(view);
            return shown != null ? shown.State : None;
        }

        /// <summary>Whether <paramref name="target"/> belongs to a preview's unsaved copy.</summary>
        public static bool IsCopy(Component target) =>
            target != null && (target.gameObject.hideFlags & HideFlags.DontSave) != 0;

        /// <summary>
        /// Whether a state can be shown: stopped, and the screen's parent is visible in its scene.
        /// The screen itself may be hidden (a modal that opens on demand); a screen inside a
        /// closed panel is shown from the screen that opens the panel.
        /// </summary>
        public static bool CanShow(Component view) =>
            view != null
            && view is IEditModePreview
            && !Application.isPlaying
            && !IsCopy(view)
            && (
                view.transform.parent == null || view.transform.parent.gameObject.activeInHierarchy
            );

        /// <summary>Shows the state at <paramref name="state"/> on a copy of the screen.</summary>
        public static void Show(Component view, int state)
        {
            if (!CanShow(view))
                return;
            if (state < 0 || state >= ((IEditModePreview)view).PreviewStates.Count)
                return;
            Restore(view);
            var source = view.gameObject;
            var copy = Copy(source);
            var shown = new Shown
            {
                View = view,
                Source = source,
                Copy = copy,
                State = state,
                SourceActive = source.activeSelf,
            };
            Store.Previews.Add(shown);
            source.SetActive(false);
            copy.SetActive(true);
            try
            {
                ((IEditModePreview)CopyOf(view, copy)).ShowPreviewState(state);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, view);
                Restore(view);
                return;
            }
            Redraw();
        }

        /// <summary>Removes the copy and puts the saved screen back as it was.</summary>
        public static void Restore(Component view) => RestoreWhere(shown => shown.View == view);

        private static void RestoreWhere(Func<Shown, bool> match)
        {
            var store = Store;
            // 閉じたシーンの表示は、戻す先がないので複製ごと捨てる。
            var orphaned = store.Previews.Where(shown => shown.Source == null).ToList();
            foreach (var shown in orphaned)
                if (shown.Copy != null)
                    Object.DestroyImmediate(shown.Copy);
            int dropped = store.Previews.RemoveAll(shown => shown.Source == null);
            var restored = store.Previews.Where(match).ToList();
            foreach (var shown in restored)
            {
                if (shown.Copy != null)
                    Object.DestroyImmediate(shown.Copy);
                shown.Source.SetActive(shown.SourceActive);
                store.Previews.Remove(shown);
            }
            if (restored.Count > 0 || dropped > 0)
                Redraw();
        }

        /// <summary>
        /// Destroys copies no preview keeps track of, such as one left over the next scene
        /// after its own scene was closed.
        /// </summary>
        public static void DestroyStrayCopies()
        {
            RestoreWhere(_ => false);
            var tracked = new HashSet<GameObject>(Store.Previews.Select(shown => shown.Copy));
            var strays = Resources
                .FindObjectsOfTypeAll<GameObject>()
                .Where(gameObject =>
                    gameObject != null
                    && gameObject.transform.parent == null
                    && (gameObject.hideFlags & HideFlags.DontSave) != 0
                    && gameObject.name.EndsWith(CopySuffix)
                    && !EditorUtility.IsPersistent(gameObject)
                    && !tracked.Contains(gameObject)
                )
                .ToList();
            foreach (var stray in strays)
                Object.DestroyImmediate(stray);
            if (strays.Count > 0)
                Redraw();
        }

        private static Shown Find(Component view) =>
            view == null ? null : Store.Previews.FirstOrDefault(shown => shown.View == view);

        // 元のすぐ後ろに置き、描く順と配置をそろえる。保存しない・触らせない印を全体に付ける。
        private static GameObject Copy(GameObject source)
        {
            var parent = source.transform.parent;
            var copy = Object.Instantiate(source, parent, false);
            if (parent == null)
                SceneManager.MoveGameObjectToScene(copy, source.scene);
            copy.name = source.name + CopySuffix;
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            foreach (var transform in copy.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags |= CopyFlags;
                foreach (var component in transform.GetComponents<Component>())
                    if (component != null)
                        component.hideFlags |= CopyFlags;
            }
            return copy;
        }

        // 写しの中の、元と同じ順番の同じ型の部品。
        private static Component CopyOf(Component view, GameObject copy)
        {
            var type = view.GetType();
            int index = Array.IndexOf(view.GetComponents(type), view);
            return copy.GetComponents(type)[index];
        }

        private static void Redraw()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            InternalEditorUtility.RepaintAllViews();
            Changed?.Invoke();
        }

        private static EditModePreviewStore Store => EditModePreviewStore.instance;

        [Serializable]
        internal sealed class Shown
        {
            public Component View;
            public GameObject Source;
            public GameObject Copy;
            public int State;
            public bool SourceActive;
        }
    }

    /// <summary>
    /// The previews being shown. A ScriptableSingleton outlives script reloads, so a preview
    /// shown before a recompile is still put back.
    /// </summary>
    internal sealed class EditModePreviewStore : ScriptableSingleton<EditModePreviewStore>
    {
        public List<EditModePreviewer.Shown> Previews = new();
    }
}
