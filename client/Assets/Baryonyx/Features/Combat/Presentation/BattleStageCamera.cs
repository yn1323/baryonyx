using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Draws the battlefield's canvas (Screen Space - Camera) with its scene's camera and turns
    /// that camera's post-processing on, so the effects' light goes through Bloom while the
    /// controls stay sharp on the overlay canvas. The screen is a prefab, which cannot point at a
    /// camera of the scene it is put in, so the camera is found as the screen wakes, before
    /// anything turns screen points into the battlefield. A camera already set is kept. Until it
    /// has a camera, Unity draws such a canvas as an overlay and reports it as one, so its render
    /// mode is not a sign of what it is meant to be. Put under another canvas (the showcase
    /// previews the screen on its own camera canvas), it draws with that one: the camera's scale
    /// and placement it took as a root are undone so it fills its parent again.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class BattleStageCamera : MonoBehaviour
    {
        private void Awake() => Bind();

        private void Start() => Bind();

        private void OnTransformParentChanged() => Bind();

        /// <summary>
        /// Finds the camera if none is set and lets it run post-processing; or, under another
        /// canvas, fills the parent.
        /// </summary>
        public void Bind()
        {
            var canvas = GetComponent<Canvas>();
            if (!canvas.isRootCanvas)
            {
                FillParent((RectTransform)transform);
                return;
            }
            if (canvas.worldCamera == null)
                canvas.worldCamera = SceneCamera();
            var camera = canvas.worldCamera;
            if (camera == null)
                return;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing)
                data.renderPostProcessing = true;
        }

        /// <summary>Stretches the rect over its parent at its own size, as a nested canvas is.</summary>
        public static void FillParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            var position = rect.localPosition;
            rect.localPosition = new Vector3(position.x, position.y, 0f);
        }

        /// <summary>The camera of this canvas's scene that draws to the screen, or the main camera.</summary>
        private Camera SceneCamera()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (
                    camera.isActiveAndEnabled
                    && camera.gameObject.scene == gameObject.scene
                    && camera.targetTexture == null
                )
                    return camera;
            return Camera.main;
        }
    }
}
