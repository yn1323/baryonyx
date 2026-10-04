using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Marks a part of a screen that belongs to its 2D look only: the painted background and the
    /// lights, mist and embers laid over the painting. While a <see cref="Hd2dStageCamera"/> of the
    /// same scene is on, the 3D stage takes its place; without one (the screen's prefab previewed
    /// on its own) it stays, so the screen still reads in 2D.
    /// In Play Mode the part is turned off. Outside Play Mode it keeps its saved state, so a scene
    /// or prefab is never saved with it turned off: an unsaved canvas group fades it out, its
    /// renderers are skipped when drawn, and the scene shows the 3D stage while it is edited.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class Hd2dFlatOnly : MonoBehaviour
    {
        private static readonly List<Hd2dFlatOnly> All = new();
        private static readonly List<Renderer> Renderers = new();

        // Not saved with the scene or prefab, and never an override of a prefab instance.
        private const HideFlags EditorOnly =
            HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;

        private bool hiddenInEditor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        // Registered on enable rather than on Awake: entering Play Mode without reloading the
        // scene does not wake an object that already woke outside Play Mode.
        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
            Apply();
        }

        private void OnDisable()
        {
            // In Play Mode a part the stage turned off stays listed, to come back without it.
            if (Application.isPlaying)
                return;
            SetHiddenInEditor(false);
            All.Remove(this);
        }

        private void OnDestroy() => All.Remove(this);

        /// <summary>Shows or hides every 2D-only part for the stage camera now on.</summary>
        public static void ApplyAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i] == null)
                    All.RemoveAt(i);
                else
                    All[i].Apply();
            }
        }

        private void Apply()
        {
            var stage = Hd2dStageCamera.Active;
            bool staged = stage != null && stage.gameObject.scene == gameObject.scene;
            if (!Application.isPlaying)
            {
                SetHiddenInEditor(staged && isActiveAndEnabled);
                return;
            }
            SetHiddenInEditor(false);
            if (gameObject.activeSelf == staged)
                gameObject.SetActive(!staged);
        }

        // A canvas group fades out every picture of the part, also those it makes later (the
        // lights, mist and embers build their pictures when the editor updates) and those a mask
        // clips; renderers are skipped by a flag that is not saved either.
        private void SetHiddenInEditor(bool hidden)
        {
            var group = EditorGroup();
            if (hidden && group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
                group.hideFlags = EditorOnly;
            }
            if (group != null)
            {
                // Destroying a component while its object is turned off is not allowed, so
                // outside Play Mode a group no longer needed is left see-through.
                if (!hidden && Application.isPlaying)
                    Destroy(group);
                group.alpha = hidden ? 0f : 1f;
                group.interactable = !hidden;
                group.blocksRaycasts = !hidden;
                group.ignoreParentGroups = false;
            }

            if (hidden || hiddenInEditor)
            {
                GetComponentsInChildren(true, Renderers);
                foreach (var renderer in Renderers)
                    renderer.forceRenderingOff = hidden;
                Renderers.Clear();
            }
            hiddenInEditor = hidden;
        }

        private CanvasGroup EditorGroup()
        {
            foreach (var group in GetComponents<CanvasGroup>())
                if ((group.hideFlags & HideFlags.DontSaveInEditor) != 0)
                    return group;
            return null;
        }
    }
}
