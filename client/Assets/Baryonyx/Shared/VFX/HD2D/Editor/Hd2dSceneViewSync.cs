using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    /// <summary>
    /// Shows a scene with a 3D stage (HD-2D) in the Scene view as the game shows it: when such a
    /// scene is opened (or from the menu), the Scene view looks through the stage camera (its
    /// position, angle, lens and clipping), draws fog, post-processing and particles, redraws
    /// continuously so flames and stars move, and hides its grid. The UI drawn over the screen
    /// (Screen Space - Overlay) is hidden in the Scene view only: Unity draws it there as a
    /// rectangle 1920 units wide at the origin, which would cover the 56 m stage; it still shows
    /// in the Game view, and the eye icon in the Hierarchy shows it again.
    /// </summary>
    [InitializeOnLoad]
    public static class Hd2dSceneViewSync
    {
        static Hd2dSceneViewSync()
        {
            // Only a scene opened on its own (not one added beside others, as tests do).
            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                if (mode == OpenSceneMode.Single)
                    LookThroughStage(scene);
            };
        }

        [MenuItem("Baryonyx/HD-2D/Scene View Through Stage Camera")]
        public static void LookThroughActiveStage()
        {
            if (!LookThroughStage(SceneManager.GetActiveScene()))
                Debug.LogWarning("No 3D stage camera (Hd2dStageCamera) in the active scene.");
        }

        /// <summary>
        /// Points the last active Scene view through the scene's stage camera. False when the
        /// scene has no stage camera or no Scene view is open.
        /// </summary>
        public static bool LookThroughStage(Scene scene)
        {
            if (
                EditorApplication.isPlayingOrWillChangePlaymode
                || !scene.IsValid()
                || !scene.isLoaded
            )
                return false;
            var roots = scene.GetRootGameObjects();
            var stage = roots
                .SelectMany(root => root.GetComponentsInChildren<Hd2dStageCamera>(true))
                .FirstOrDefault();
            var view = SceneView.lastActiveSceneView;
            if (view == null && SceneView.sceneViews.Count > 0)
                view = (SceneView)SceneView.sceneViews[0];
            if (stage == null || view == null)
                return false;

            var camera = stage.Camera;
            view.in2DMode = false;
            view.orthographic = false;
            var settings = view.cameraSettings;
            settings.fieldOfView = camera.fieldOfView;
            settings.dynamicClip = false;
            settings.nearClip = camera.nearClipPlane;
            settings.farClip = camera.farClipPlane;
            view.cameraSettings = settings;
            view.AlignViewToObject(camera.transform);

            var state = view.sceneViewState;
            state.fxEnabled = true;
            state.showFog = true;
            state.showImageEffects = true;
            state.showParticleSystems = true;
            state.showFlares = true;
            state.alwaysRefresh = true;
            view.showGrid = false;

            // The canvases drawn by the camera (the battlefield's bars and marks, the title's
            // mist) stand in front of it in the scene and stay visible; the overlay is hidden.
            foreach (
                var canvas in roots
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                    .Where(canvas => canvas.isRootCanvas)
            )
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    SceneVisibilityManager.instance.Hide(canvas.gameObject, true);
                else
                    SceneVisibilityManager.instance.Show(canvas.gameObject, true);
            }

            view.Repaint();
            return true;
        }
    }
}
