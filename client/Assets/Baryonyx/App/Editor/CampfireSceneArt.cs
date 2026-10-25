using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Stages.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Draws the night camp of the adventure's rest (doc/features/stage-progression.md, 休憩の鍛える)
    /// into one picture: Home's forest glade (HD-2D) seen from Home's camera, at night. The sun
    /// gives way to a faint blue moonlight, its beams and the motes in them go out, the air turns
    /// to a deep blue and the campfire burns brighter, lighting the camp around it. Nobody stands
    /// in it: the exploration screen stands the adventure's party and the logs on it where Home
    /// stands them (AdventureAssets). Run again after the glade or the fire change.
    /// </summary>
    public static class CampfireSceneArt
    {
        public const string OutputPath =
            "Assets/Baryonyx/Features/Adventure/UI/Art/CampfireNight.png";
        public const int Width = 1920;
        public const int Height = 1080;

        // 夜の空気：上から深い青、まわりから暗い青、下からほとんど来ない光。遠くは濃い青の霧に沈む。
        private static readonly StageSetAssets.StageEnvironment Night = new(
            new Color(0.1f, 0.13f, 0.24f),
            new Color(0.03f, 0.05f, 0.1f),
            14f,
            52f,
            new Color(0.06f, 0.08f, 0.15f),
            new Color(0.03f, 0.03f, 0.04f)
        );

        // 月の光：太陽の向きのまま、青白く、ずっと弱く。
        private static readonly Color Moonlight = new(0.6f, 0.7f, 1f);
        private const float MoonShare = 0.4f;

        // 夜の焚き火は、昼より強く遠くまで照らす。
        private const float FireIntensity = 5f;
        private const float FireRange = 13f;

        // シーンを組んでから描くまでに待つEditorの更新の回数。組んだ直後はURPと色の調整の準備が済んでおらず、
        // 何も描かれない。
        private const int SettleUpdates = 8;

        /// <summary>True from <see cref="Render"/> until the picture is saved and the scene reopened.</summary>
        public static bool Rendering { get; private set; }

        [MenuItem("Baryonyx/Adventure/Render Campfire Scene")]
        public static void Render()
        {
            EditorGuard.RequireEditMode();
            if (Rendering)
                return;
            // 一時的なシーンで描くため、開いているシーンを保存してから閉じ、描き終えたら開き直す。
            var open = EditorSceneManager.GetActiveScene();
            if (open.isDirty)
                EditorSceneManager.SaveOpenScenes();
            string back = open.path;
            Camera camera;
            try
            {
                camera = Stage();
            }
            catch
            {
                Reopen(back);
                throw;
            }
            Rendering = true;
            int waited = 0;
            void Capture()
            {
                if (++waited < SettleUpdates)
                    return;
                EditorApplication.update -= Capture;
                try
                {
                    Save(camera);
                }
                finally
                {
                    Reopen(back);
                    Rendering = false;
                }
                AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);
                ArtAssets.ImportTexture(OutputPath, FilterMode.Bilinear);
            }
            EditorApplication.update += Capture;
        }

        // 夜の野営地を新しいシーンに組み、見下ろすカメラを返す。
        private static Camera Stage()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("CampfireCamera", typeof(Camera)).GetComponent<Camera>();
            Hd2dStageSceneSetup.Apply(
                scene,
                camera,
                StageSetAssets.ForestGladePrefabPath,
                StageSetAssets.HomeView,
                Night
            );
            var stage = GameObject.Find(Hd2dStageSceneSetup.StageName);
            foreach (var child in stage.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Beam") || child.name == "Motes")
                    child.gameObject.SetActive(false);
            var key = stage.transform.Find(StageSetAssets.KeyLightName);
            if (key != null && key.GetComponent<Light>() is { } sun)
            {
                sun.color = Moonlight;
                sun.intensity *= MoonShare;
            }
            HomeSceneSetup.AddCampfire(scene, FireIntensity, FireRange, daylight: false);
            Hd2dStageSceneSetup.AddVolume(
                scene,
                "CampfireLook",
                StageSetAssets.ForestGladeLookPath
            );
            AddNightGrade();
            // 炎と火の粉は、燃え始めてしばらくたった姿で描く。
            foreach (var particles in Object.FindObjectsByType<ParticleSystem>())
                particles.Simulate(3f, true, true);
            return camera;
        }

        // カメラを画面の外の描き先に描き、PNGに保存する（展示室の撮影と同じ描き方）。
        private static void Save(Camera camera)
        {
            var target = RenderTexture.GetTemporary(Width, Height, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                picture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                File.WriteAllBytes(OutputPath, picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void Reopen(string path)
        {
            if (!string.IsNullOrEmpty(path))
                EditorSceneManager.OpenScene(path);
            else
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        }

        // 昼の野営地の色の調整に重ねる夜の色：全体を暗く、青く、少し色を抜く。焚き火の光は強めたため、
        // その周りだけが暖かく浮かぶ。
        private static void AddNightGrade()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(-0.25f);
            grade.colorFilter.Override(new Color(0.74f, 0.82f, 1f));
            grade.saturation.Override(-12f);
            var volume = new GameObject("NightGrade", typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }
    }
}
