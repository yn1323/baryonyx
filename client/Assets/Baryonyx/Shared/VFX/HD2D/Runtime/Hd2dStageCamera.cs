using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// The camera of a 3D stage (HD-2D): it looks down on a ground and walls built in 3D, and the
    /// pixel-art characters of the UI are stood on that ground by <see cref="Hd2dUiBillboard"/>.
    /// Where on the ground a character stands is worked out from the camera's resting pose, so
    /// that when the camera drifts with the UI of the stage (the slow circle and the shake of the
    /// battle) or sways on its own, the characters stay on their spot and things nearer and
    /// farther than <see cref="FocusDistance"/> part from each other, as in a real camera move.
    /// While it is on, the 2D-only parts (<see cref="Hd2dFlatOnly"/>) give way to the stage.
    /// It runs outside Play Mode too, so the scene shows the 3D stage while it is edited; the
    /// camera only moves in Play Mode.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(900)]
    public sealed class Hd2dStageCamera : MonoBehaviour
    {
        [Header("UIとの対応")]
        [Tooltip(
            "UIの舞台を動かすRectTransform（戦闘の円運動と揺れなど）。anchoredPositionの合計だけカメラも動かし、キャラはこの動きを除いた位置に立たせる。"
        )]
        public RectTransform[] UiOffsetSources = new RectTransform[0];

        [Tooltip("UIの動きと同じだけ画面上で動かす奥行き（m）。キャラが立つあたりにする。")]
        [Min(0.1f)]
        public float FocusDistance = 14f;

        [Tooltip("UIの高さ（CanvasScalerの基準解像度の高さ）。")]
        [Min(1f)]
        public float CanvasHeight = 1080f;

        [Tooltip("キャラを立たせる地面の高さ（m）。")]
        public float GroundHeight;

        [Header("ゆっくりした揺れ")]
        [Tooltip("UIに関係なくカメラだけを円く動かす半径（設計座標のpx）。0で止める。")]
        [Min(0f)]
        public float SwayRadius;

        [Tooltip("揺れを1周する秒数。")]
        [Min(1f)]
        public float SwayPeriod = 30f;

        [Header("登場の動き")]
        [Tooltip(
            "Play Modeの開始時にカメラを置く、休みの位置からのずれ（カメラの右・上・前の向き、m）。ここからゆっくり休みの位置へ寄る。0で動かさない。"
        )]
        public Vector3 IntroOffset;

        [Tooltip("登場の動きの秒数。")]
        [Min(0.1f)]
        public float IntroSeconds = 3f;

        [Header("キャラの板")]
        [Tooltip("キャラの影の板を向ける光（ふつうは影を落とす主光源）。")]
        public Light KeyLight;

        [Tooltip("キャラの板のマテリアル（Baryonyx/HD2D/Stage Sprite）。")]
        public Material SpriteMaterial;

        [Tooltip("足元の接地影のマテリアル（Baryonyx/HD2D/Contact Shadow）。")]
        public Material ContactShadowMaterial;

        private Camera view;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool resting = true;
        private double introStart;

        private static readonly List<Hd2dStageCamera> Enabled = new();

        /// <summary>The stage camera that is on, if any; the one turned on last wins.</summary>
        public static Hd2dStageCamera Active => Enabled.Count > 0 ? Enabled[^1] : null;

        public Camera Camera => view != null ? view : view = GetComponent<Camera>();

        /// <summary>The UI's own offset this frame (canvas units), which the characters undo.</summary>
        public Vector2 UiOffset { get; private set; }

        /// <summary>The first offset source: the UI under it moves with the camera.</summary>
        public RectTransform UiOffsetRoot =>
            UiOffsetSources != null && UiOffsetSources.Length > 0 ? UiOffsetSources[0] : null;

        public Vector3 RestPosition => resting ? transform.position : restPosition;
        public Quaternion RestRotation => resting ? transform.rotation : restRotation;
        public Vector3 RestRight => RestRotation * Vector3.right;
        public Vector3 RestUp => RestRotation * Vector3.up;
        public Vector3 RestForward => RestRotation * Vector3.forward;

        /// <summary>True when the characters can be stood (both materials are set).</summary>
        public bool CanStage => SpriteMaterial != null && ContactShadowMaterial != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Enabled.Clear();

        private void OnEnable()
        {
            restPosition = transform.position;
            restRotation = transform.rotation;
            resting = !Application.isPlaying;
            introStart = Time.unscaledTimeAsDouble;
            Enabled.Remove(this);
            Enabled.Add(this);
            Hd2dFlatOnly.ApplyAll();
            Hd2dUiBillboard.RefreshAll();
        }

        private void OnDisable()
        {
            if (!resting)
                transform.SetPositionAndRotation(restPosition, restRotation);
            resting = true;
            Enabled.Remove(this);
            Hd2dFlatOnly.ApplyAll();
            Hd2dUiBillboard.RefreshAll();
        }

        private void LateUpdate()
        {
            if (resting)
                return;
            var offset = Vector2.zero;
            if (UiOffsetSources != null)
                foreach (var source in UiOffsetSources)
                    if (source != null)
                        offset += source.anchoredPosition;
            UiOffset = offset;

            var sway = Vector2.zero;
            if (SwayRadius > 0f)
            {
                float angle =
                    (float)(Time.unscaledTimeAsDouble % SwayPeriod) / SwayPeriod * Mathf.PI * 2f;
                sway = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SwayRadius;
            }

            var shift = Hd2dStageMath.CameraShift(
                offset + sway,
                FocusDistance,
                Camera.fieldOfView,
                CanvasHeight
            );
            float intro = Hd2dStageMath.IntroRemaining(
                (float)(Time.unscaledTimeAsDouble - introStart),
                IntroSeconds
            );
            transform.SetPositionAndRotation(
                restPosition
                    + RestRight * shift.x
                    + RestUp * shift.y
                    + restRotation * IntroOffset * intro,
                restRotation
            );
        }

        /// <summary>
        /// The ground under a point of the screen seen from the resting pose.
        /// <paramref name="normalized"/> runs from (0, 0) at the bottom left to (1, 1) at the top right.
        /// </summary>
        public bool TryGroundPoint(Vector2 normalized, out Vector3 ground)
        {
            var direction =
                RestRotation
                * Hd2dStageMath.ViewDirection(normalized, Camera.fieldOfView, Camera.aspect);
            return Hd2dStageMath.GroundHit(RestPosition, direction, GroundHeight, out ground);
        }

        /// <summary>How far in front of the resting camera a point is, along its view.</summary>
        public float RestDepth(Vector3 world) => Vector3.Dot(world - RestPosition, RestForward);

        /// <summary>World units per canvas unit for a board facing the camera at a point.</summary>
        public float WorldPerCanvasUnit(Vector3 world, float canvasHeight) =>
            Hd2dStageMath.WorldPerCanvasUnit(RestDepth(world), Camera.fieldOfView, canvasHeight);
    }
}
