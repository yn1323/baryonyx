using System;
using Baryonyx.Stages.Editor;
using Baryonyx.Vfx.Hd2d;
using Baryonyx.Vfx.Hd2d.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Puts a 3D stage (HD-2D) in a screen's scene: the stage prefab, the scene's camera turned
    /// into a perspective <see cref="Hd2dStageCamera"/> looking at it from the screen's view, and
    /// the stage's ambient light and fog. The pixel-art characters of the screen's UI then stand
    /// on it (<see cref="Hd2dUiBillboard"/>). The stage itself is built by
    /// <see cref="StageSetAssets"/>.
    /// </summary>
    public static class Hd2dStageSceneSetup
    {
        public const string StageName = "Stage3D";

        public static Hd2dStageCamera Apply(
            Scene scene,
            Camera camera,
            string stagePrefabPath,
            StageSetAssets.StageView view,
            StageSetAssets.StageEnvironment environment
        )
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stagePrefabPath);
            if (prefab == null)
                throw new InvalidOperationException(
                    "3D stage was not generated: " + stagePrefabPath
                );
            var stage = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            stage.name = StageName;

            camera.orthographic = false;
            camera.fieldOfView = view.Fov;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = environment.Fog;
            camera.transform.SetPositionAndRotation(view.Position, Quaternion.Euler(view.Euler));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var stageCamera = camera.gameObject.GetComponent<Hd2dStageCamera>();
            if (stageCamera == null)
                stageCamera = camera.gameObject.AddComponent<Hd2dStageCamera>();
            stageCamera.FocusDistance = view.FocusDistance;
            stageCamera.SpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                Hd2dStageKit.SpriteMaterialPath
            );
            stageCamera.ContactShadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                Hd2dStageKit.ContactShadowMaterialPath
            );
            var key = stage.transform.Find(StageSetAssets.KeyLightName);
            stageCamera.KeyLight = key != null ? key.GetComponent<Light>() : null;

            // Ambient light and fog belong to the scene that is active.
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            StageSetAssets.ApplyEnvironment(environment);
            if (previous.IsValid())
                SceneManager.SetActiveScene(previous);
            return stageCamera;
        }

        /// <summary>A global post-processing volume with the given profile.</summary>
        public static Volume AddVolume(Scene scene, string name, string profilePath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
                throw new InvalidOperationException("Volume profile not found: " + profilePath);
            var volumeObject = new GameObject(name, typeof(Volume));
            SceneManager.MoveGameObjectToScene(volumeObject, scene);
            var volume = volumeObject.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            return volume;
        }

        /// <summary>
        /// The point on the stage's ground under a point of a 1920×1080 screen, seen from a view.
        /// Used to put the stage's own lights where the UI puts a fire.
        /// </summary>
        public static Vector3 GroundUnder(StageSetAssets.StageView view, Vector2 canvasPoint)
        {
            var normalized = new Vector2(canvasPoint.x / 1920f, canvasPoint.y / 1080f);
            var direction =
                Quaternion.Euler(view.Euler)
                * Hd2dStageMath.ViewDirection(normalized, view.Fov, 16f / 9f);
            if (!Hd2dStageMath.GroundHit(view.Position, direction, 0f, out var ground))
                throw new InvalidOperationException("The point does not look down at the ground.");
            return ground;
        }
    }
}
