using System;
using Baryonyx.Vfx.Hd2d;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Shows one of the battle's backgrounds (<see cref="BattleStage"/>): the chosen 3D stage
    /// is turned on and the others off, and the scene takes on that stage's air (the ambient
    /// light, the fog and the colour behind it), its look (the lens and grade of
    /// <see cref="LookVolume"/>) and its key light, which the characters' shadow boards turn to.
    /// Choosing another stage in the Inspector switches it at once, outside Play Mode too.
    /// The stages and their air are put here by the scene's builder.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class BattleStageSelector : MonoBehaviour
    {
        [Tooltip("表示する戦闘の背景。")]
        public BattleStage Stage;

        [Tooltip("キャラを立たせる舞台のカメラ。選んだ背景の主光源と背景色を渡す。")]
        public Hd2dStageCamera StageCamera;

        [Tooltip("選んだ背景のレンズと色調（Volume Profile）を入れるVolume。")]
        public Volume LookVolume;

        [Tooltip(
            "選べる背景と、その空気（環境光・霧）・レンズと色調・主光源。シーンの生成処理が設定する。"
        )]
        public BattleStageOption[] Options = Array.Empty<BattleStageOption>();

        /// <summary>The background shown now, or null before one has been shown.</summary>
        public BattleStageOption Shown { get; private set; }

        private void OnEnable() => Apply();

#if UNITY_EDITOR
        // Turning objects on and off is not allowed while the Inspector validates a change.
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall -= ApplyLater;
            UnityEditor.EditorApplication.delayCall += ApplyLater;
        }

        private void ApplyLater()
        {
            if (this != null && isActiveAndEnabled)
                Apply();
        }
#endif

        /// <summary>The option for a background, or null if the scene has none for it.</summary>
        public BattleStageOption Find(BattleStage stage)
        {
            foreach (var option in Options)
                if (option != null && option.Stage == stage)
                    return option;
            return null;
        }

        /// <summary>Shows <see cref="Stage"/> and gives the scene its air, look and light.</summary>
        public void Apply()
        {
            var chosen = Find(Stage);
            foreach (var option in Options)
                if (option?.Root != null && option.Root.activeSelf != (option == chosen))
                    option.Root.SetActive(option == chosen);
            Shown = chosen;
            if (chosen == null)
                return;

            if (LookVolume != null)
                LookVolume.sharedProfile = chosen.Look;
            if (StageCamera != null)
            {
                StageCamera.KeyLight = chosen.KeyLight;
                StageCamera.Camera.backgroundColor = chosen.Fog;
            }
            // Ambient light and fog belong to the active scene; another scene opened beside this
            // one in the Editor keeps its own.
            if (gameObject.scene == SceneManager.GetActiveScene())
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = chosen.AmbientSky;
                RenderSettings.ambientEquatorColor = chosen.AmbientEquator;
                RenderSettings.ambientGroundColor = chosen.AmbientGround;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = chosen.Fog;
                RenderSettings.fogStartDistance = chosen.FogStart;
                RenderSettings.fogEndDistance = chosen.FogEnd;
            }
            Hd2dUiBillboard.RefreshAll();
        }
    }

    /// <summary>One background the battle can be fought on, with the air around it.</summary>
    [Serializable]
    public sealed class BattleStageOption
    {
        public BattleStage Stage;

        [Tooltip("背景の3Dの舞台（シーンに置いたPrefab）。")]
        public GameObject Root;

        [Tooltip("影を落とす主光源。キャラの影の板がこの光へ向く。")]
        public Light KeyLight;

        [Tooltip("背景ごとのレンズと色調。")]
        public VolumeProfile Look;

        [Tooltip("空から（上から）の環境光。")]
        public Color AmbientSky;

        [Tooltip("周りから（横から）の環境光。")]
        public Color AmbientEquator;

        [Tooltip("地面から（下から）の環境光。")]
        public Color AmbientGround;

        [Tooltip("霧と、舞台の外の背景の色。")]
        public Color Fog;

        [Min(0f)]
        public float FogStart;

        [Min(0f)]
        public float FogEnd;
    }
}
