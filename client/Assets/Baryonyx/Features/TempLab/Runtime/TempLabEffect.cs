using System.Collections.Generic;
using Baryonyx.Vfx.Hd2d;
using UnityEngine;

namespace Baryonyx.TempLab
{
    /// <summary>
    /// A trial effect of TempLab (doc/features/screens/debug-rooms.md) cast from the caster at the
    /// target, both stood on the stage as the battle stands them. It is worked out in fixed steps
    /// from a seed, so <see cref="Seek"/> shows the same moment every time (for capturing), and
    /// drawn in the stage's world: noise densities marched through (<c>TempLabVolume.shader</c>)
    /// and shapes on boards (<c>TempLabGlow.shader</c>), with a light of its own on the stage.
    /// <see cref="TempLabEffectReel"/> plays the trials one after another.
    /// </summary>
    public abstract class TempLabEffect : MonoBehaviour
    {
        // 戦闘のキャラの板（Hd2dStageKit.BoardOrder）の前後に描く順番。
        protected const int GroundOrder = 5;
        protected const int BehindOrder = 9;
        protected const int TrailOrder = 11;
        protected const int FrontOrder = 12;
        protected const int GlowOrder = 13;
        private const float StepSeconds = 1f / 60f;

        [Header("つなぎ先")]
        [Tooltip("舞台を写すカメラ。板をこのカメラへ向ける。")]
        public Camera StageCamera;

        [Tooltip("技を放つ人（舞台に立たせた絵）。")]
        public Hd2dUiBillboard Caster;

        [Tooltip("技が当たる敵（舞台に立たせた絵）。")]
        public Hd2dUiBillboard Target;

        [Tooltip("技と一緒に動き、舞台とキャラを照らす点光源。")]
        public Light EffectLight;

        [Tooltip("炎・煙・土ぼこりのシェーダー（TempLabVolume）。")]
        public Shader VolumeShader;

        [Tooltip("光・筋・地面の跡のシェーダー（TempLabGlow）。")]
        public Shader GlowShader;

        [Tooltip("乱数の種。同じ種なら毎回同じ形と動きになる。")]
        public int Seed = 7;

        [Tooltip("炎と煙の中を進む段の数。多いほど滑らかで重い。")]
        [Range(8, 96)]
        public int RaySteps = 40;

        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int FlowId = Shader.PropertyToID("_Flow");
        private static readonly int LookId = Shader.PropertyToID("_Look");
        private static readonly int BodyId = Shader.PropertyToID("_Body");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int StepsId = Shader.PropertyToID("_Steps");

        private readonly List<Object> owned = new();
        private readonly List<Renderer> renderers = new();
        private MaterialPropertyBlock block;
        private Material volumeMaterial;
        private Material glowMaterial;
        private Material overMaterial;
        private Mesh quad;
        private float simulated;
        private float pending;
        private bool anchored;

        /// <summary>The moment shown now, in seconds from the start.</summary>
        public float Clock => simulated;

        /// <summary>How long the effect lasts, until the last of it is gone.</summary>
        public abstract float Duration { get; }

        protected System.Random Random { get; private set; }

        // 技を放つ所・当たる所・当たる人の足元（地面）・カメラから奥への向き（水平）。
        protected Vector3 Hand { get; private set; }
        protected Vector3 Hit { get; private set; }
        protected Vector3 Feet { get; private set; }
        protected Vector3 Away { get; private set; }

        // 当たる人の絵の大きさ（ワールド）。
        protected Bounds TargetBounds { get; private set; }

        protected Transform View => StageCamera != null ? StageCamera.transform : transform;

        protected virtual void Awake()
        {
            block = new MaterialPropertyBlock();
            if (EffectLight == null)
            {
                EffectLight = new GameObject("EffectLight").AddComponent<Light>();
                EffectLight.transform.SetParent(transform, false);
                EffectLight.type = LightType.Point;
                EffectLight.shadows = LightShadows.None;
            }
            EffectLight.enabled = false;
            Build();
            Restart();
        }

        protected virtual void OnDestroy()
        {
            foreach (var target in owned)
                if (target != null)
                    Destroy(target);
        }

        /// <summary>Makes the parts the effect draws with (once, from <see cref="Awake"/>).</summary>
        protected abstract void Build();

        /// <summary>Clears what the effect has spawned, to start over.</summary>
        protected abstract void OnRestart();

        /// <summary>Moves the effect on by <paramref name="dt"/> from the moment <paramref name="t"/>.</summary>
        protected abstract void Step(float t, float dt);

        /// <summary>Draws the effect at the moment <paramref name="t"/>.</summary>
        protected abstract void Show(float t);

        /// <summary>Starts the effect over.</summary>
        public void Restart()
        {
            Random = new System.Random(Seed);
            simulated = 0f;
            pending = 0f;
            OnRestart();
        }

        /// <summary>Plays on by <paramref name="seconds"/> and draws it.</summary>
        public void Play(float seconds)
        {
            if (!Anchor())
                return;
            pending += seconds;
            while (pending >= StepSeconds)
            {
                pending -= StepSeconds;
                Step(simulated, StepSeconds);
                simulated += StepSeconds;
            }
            Show(simulated);
        }

        /// <summary>
        /// Shows the effect at <paramref name="seconds"/> from its start, worked out in the same
        /// fixed steps as when it plays, so the same moment always looks the same.
        /// </summary>
        public void Seek(float seconds)
        {
            Restart();
            Play(Mathf.Max(0f, seconds));
        }

        /// <summary>Hides everything the effect draws.</summary>
        public virtual void Hide()
        {
            foreach (var renderer in renderers)
                if (renderer != null)
                    renderer.enabled = false;
            if (EffectLight != null)
                EffectLight.enabled = false;
        }

        // 立たせた絵から、放つ所・当たる所・足元を取る。絵がまだ舞台にないときは待つ。
        private bool Anchor()
        {
            var from = Caster != null ? Caster.VisualRenderer : null;
            var to = Target != null ? Target.VisualRenderer : null;
            if (from == null || to == null)
                return anchored;
            var a = from.bounds;
            var b = to.bounds;
            Hand = new Vector3(a.max.x - a.size.x * 0.22f, a.min.y + a.size.y * 0.56f, a.center.z);
            Hit = new Vector3(
                b.center.x - b.size.x * 0.08f,
                b.min.y + b.size.y * 0.45f,
                b.center.z
            );
            Feet = new Vector3(b.center.x, b.min.y + 0.02f, b.min.z);
            TargetBounds = b;
            var forward = View.forward;
            forward.y = 0f;
            Away = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            anchored = true;
            return true;
        }

        // --- Parts ------------------------------------------------------------------------------

        protected Material VolumeMaterial => volumeMaterial ??= Own(new Material(VolumeShader));

        protected Material GlowMaterial => glowMaterial ??= Own(new Material(GlowShader));

        // キャラの板の深度に隠されず、手前に描く光と筋のマテリアル。
        private Material OverMaterial
        {
            get
            {
                if (overMaterial != null)
                    return overMaterial;
                overMaterial = Own(new Material(GlowShader));
                overMaterial.SetFloat(
                    "_ZTest",
                    (float)UnityEngine.Rendering.CompareFunction.Always
                );
                return overMaterial;
            }
        }

        protected Mesh QuadMesh => quad ??= Own(Quad());

        /// <summary>A renderer of the effect, hidden until the effect draws it.</summary>
        protected MeshRenderer Part(string partName, Material material, Mesh mesh, int order)
        {
            var part = new GameObject(partName) { hideFlags = HideFlags.DontSave };
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            renderers.Add(renderer);
            return renderer;
        }

        /// <summary>
        /// A mesh of boards drawn at <paramref name="order"/> (see <see cref="TempLabBoard"/>).
        /// Boards over the characters are drawn whole, never hidden by the characters' depth;
        /// marks on the ground are hidden by what stands on it.
        /// </summary>
        protected TempLabBoard Board(string partName, int order, bool overCharacters = true)
        {
            var mesh = Own(new Mesh { name = partName });
            mesh.MarkDynamic();
            var material = overCharacters ? OverMaterial : GlowMaterial;
            return new TempLabBoard(mesh, Part(partName, material, mesh, order));
        }

        /// <summary>Draws a board mesh made this frame.</summary>
        protected void Draw(TempLabBoard board)
        {
            board.Apply();
            board.Renderer.enabled = true;
        }

        /// <summary>
        /// Draws a volume of fire, smoke or dust (see <c>TempLabVolume.shader</c> for the values):
        /// a board facing the camera, big enough to cover the ball and its tail and ragged edge.
        /// </summary>
        protected void Volume(
            MeshRenderer renderer,
            Vector3 center,
            float radius,
            Vector3 flow,
            float stretch,
            float time,
            float seed,
            float heat,
            float soot,
            float density,
            float erode,
            float warp,
            float occlusion,
            Vector3 glowAt,
            float glow,
            float opacity
        )
        {
            float bound = radius * (1.75f + stretch) * 2.3f;
            var t = renderer.transform;
            t.SetPositionAndRotation(center, View.rotation);
            t.localScale = Vector3.one * bound;
            block.Clear();
            block.SetVector(CenterId, new Vector4(center.x, center.y, center.z, radius));
            block.SetVector(FlowId, new Vector4(flow.x, flow.y, flow.z, stretch));
            block.SetVector(LookId, new Vector4(time, seed, heat, soot));
            block.SetVector(BodyId, new Vector4(density, erode, warp, occlusion));
            block.SetVector(GlowId, new Vector4(glowAt.x, glowAt.y, glowAt.z, glow));
            block.SetFloat(OpacityId, opacity);
            block.SetFloat(StepsId, RaySteps);
            renderer.SetPropertyBlock(block);
            renderer.enabled = true;
        }

        /// <summary>Puts the effect's light at <paramref name="position"/>; off when it is too faint.</summary>
        protected void Light(Vector3 position, Color color, float intensity, float range)
        {
            if (EffectLight == null)
                return;
            EffectLight.enabled = intensity > 0.02f;
            EffectLight.color = color;
            EffectLight.range = range;
            EffectLight.intensity = intensity;
            EffectLight.transform.position = position;
        }

        private T Own<T>(T target)
            where T : Object
        {
            target.hideFlags = HideFlags.DontSave;
            owned.Add(target);
            return target;
        }

        // 中心に置く1m四方の板（-0.5〜0.5）。
        private static Mesh Quad()
        {
            var mesh = new Mesh { name = "TempLabQuad" };
            mesh.SetVertices(
                new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                }
            );
            mesh.SetTriangles(new[] { 0, 1, 2, 2, 3, 0 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        // --- Helpers ----------------------------------------------------------------------------

        // 熱い粒の色。TempLabVfx.hlsl の FireColor と同じ並び（暗い赤→橙→黄→白）を明るめに取る。
        protected static Color EmberColor(float heat)
        {
            float t = Mathf.Clamp01(heat);
            var c = Color.Lerp(
                new Color(0.6f, 0.06f, 0.01f),
                new Color(2.8f, 0.8f, 0.14f),
                Smooth(0f, 0.4f, t)
            );
            c = Color.Lerp(c, new Color(4.4f, 2.8f, 0.8f), Smooth(0.35f, 0.75f, t));
            return Color.Lerp(c, new Color(6.5f, 5.8f, 4.4f), Smooth(0.75f, 1f, t));
        }

        protected static float Smooth(float a, float b, float x) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        protected static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        protected float Range(float min, float max) =>
            min + (float)Random.NextDouble() * (max - min);

        protected Vector3 OnSphere()
        {
            float z = Range(-1f, 1f);
            float a = Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }
    }
}
