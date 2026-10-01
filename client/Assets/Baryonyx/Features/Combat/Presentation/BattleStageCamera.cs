using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Draws the battlefield's canvas (Screen Space - Camera) with its scene's camera and turns
    /// that camera's post-processing on, so the effects' light goes through Bloom while the
    /// controls stay sharp on the overlay canvas. The screen is a prefab, which cannot point at a
    /// camera of the scene it is put in, so the camera is found as the screen wakes, before
    /// anything turns screen points into the battlefield. A camera already set (the showcase sets
    /// its preview camera) is kept. Until it has a camera, Unity draws such a canvas as an
    /// overlay and reports it as one, so its render mode is not a sign of what it is meant to be.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class BattleStageCamera : MonoBehaviour
    {
        private void Awake() => Bind();

        private void Start() => Bind();

        /// <summary>Finds the camera if none is set and lets it run post-processing.</summary>
        public void Bind()
        {
            var canvas = GetComponent<Canvas>();
            // Nested under another canvas (as the showcase previews it), it draws with that one.
            if (!canvas.isRootCanvas)
                return;
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
