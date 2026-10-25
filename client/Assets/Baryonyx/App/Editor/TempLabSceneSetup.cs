using System.Linq;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Showcase.Editor;
using Baryonyx.Stages.Editor;
using Baryonyx.TempLab;
using Baryonyx.Vfx.Hd2d;
using Baryonyx.Vfx.Hd2d.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds TempLab.unity, the room for quick trials (<see cref="TempLabRoom"/>): the battle's
    /// 3D stone circle on the highland at dusk with the battle's camera, bloom, lens and grade, as
    /// behind the card skill lab, and an empty canvas over it. Rebuilding it takes a trial out.
    /// </summary>
    public static class TempLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/TempLab.unity";

        [MenuItem("Baryonyx/App/Create Temp Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();

            // The dusk highland is the battle's; it is made here only if it is missing, so
            // building the lab leaves it as it is.
            if (
                AssetDatabase.LoadAssetAtPath<GameObject>(StageSetAssets.DuskHighlandPrefabPath)
                    == null
                || AssetDatabase.LoadAssetAtPath<VolumeProfile>(StageSetAssets.DuskHighlandLookPath)
                    == null
            )
                StageSetAssets.EnsureAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    var camera = ScreenScenes.AddCamera(
                        scene,
                        "StageCamera",
                        ScreenScenes.CameraColor
                    );
                    Hd2dStageSceneSetup.Apply(
                        scene,
                        camera,
                        StageSetAssets.DuskHighlandPrefabPath,
                        StageSetAssets.BattleView,
                        StageSetAssets.DuskEnvironment
                    );
                    // The battle's bloom, with the highland's lens and grade over it.
                    Hd2dStageSceneSetup.AddVolume(
                        scene,
                        "BattlePostProcessVolume",
                        BattleSkillVfxAssets.PostProcessProfilePath
                    );
                    Hd2dStageSceneSetup
                        .AddVolume(scene, "StageLookVolume", StageSetAssets.DuskHighlandLookPath)
                        .priority = 1f;

                    var canvas = UiBuild.CanvasRoot("TempLabCanvas");
                    SceneManager.MoveGameObjectToScene(canvas.gameObject, scene);
                    var room = canvas.gameObject.AddComponent<TempLabRoom>();
                    room.StageCamera = camera;
                    room.Stage = scene
                        .GetRootGameObjects()
                        .First(root => root.name == Hd2dStageSceneSetup.StageName)
                        .transform;
                    room.Sandbox = UiBuild.SafeArea(canvas);
                    AddTrials(room);
                    ScreenScenes.AddEventSystem(scene);
                }
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // The showcase loads scenes through SceneManager, which only finds scenes in the build.
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        private const string ShaderFolder = "Assets/Baryonyx/Features/TempLab/Shaders";

        // 試作：トーマがモスウルフへ放つファイア・斬撃・雷（TempLabEffect）を、順に繰り返す。
        // 2人は戦闘と同じく舞台に立たせる。
        private static void AddTrials(TempLabRoom room)
        {
            var caster = Actor(room.Sandbox, "BattleToma", new Vector2(-520f, -250f), 4f);
            var target = Actor(room.Sandbox, "MossWolf", new Vector2(470f, -250f), 3f);
            var reel = new GameObject("TempLabEffects").AddComponent<TempLabEffectReel>();
            SceneManager.MoveGameObjectToScene(reel.gameObject, room.gameObject.scene);
            reel.Effects = new TempLabEffect[]
            {
                Trial<TempLabFireball>(reel.transform, room, caster, target),
                Trial<TempLabSlash>(reel.transform, room, caster, target),
                Trial<TempLabLightning>(reel.transform, room, caster, target),
            };
        }

        private static T Trial<T>(
            Transform parent,
            TempLabRoom room,
            Hd2dUiBillboard caster,
            Hd2dUiBillboard target
        )
            where T : TempLabEffect
        {
            var effect = new GameObject(typeof(T).Name).AddComponent<T>();
            effect.transform.SetParent(parent, false);
            effect.StageCamera = room.StageCamera;
            effect.Caster = caster;
            effect.Target = target;
            var light = new GameObject("EffectLight").AddComponent<Light>();
            light.transform.SetParent(effect.transform, false);
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.enabled = false;
            effect.EffectLight = light;
            effect.VolumeShader = AssetDatabase.LoadAssetAtPath<Shader>(
                ShaderFolder + "/TempLabVolume.shader"
            );
            effect.GlowShader = AssetDatabase.LoadAssetAtPath<Shader>(
                ShaderFolder + "/TempLabGlow.shader"
            );
            return effect;
        }

        private static Hd2dUiBillboard Actor(
            RectTransform parent,
            string art,
            Vector2 feet,
            float dot
        )
        {
            var image = UiBuild.PixelActor(parent, art, BattleInspectAssets.Art(art), feet, dot);
            return Hd2dStageKit.Stand(image.gameObject, image, null);
        }
    }
}
