using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Stands a pixel-art character of the UI in the 3D stage of a <see cref="Hd2dStageCamera"/>.
    /// The UI keeps deciding where the character is, how big it is and how it moves (stepping
    /// out, shaking, flashing, fading); this finds the ground under its feet and draws the
    /// picture there as a board parallel to the camera, so it is never seen edge-on and thin. The
    /// board takes the stage's lights, a separate upright board turned to the key light casts its
    /// shadow on the ground (the board seen by the camera leans back with the camera, and its own
    /// shadow would come out squashed), and a soft contact shadow ties the feet to the floor.
    /// The UI picture and its 2D shadow are not drawn while the character stands in 3D.
    /// Without a stage camera in the scene nothing changes. Outside Play Mode the boards are made
    /// too, never saved, so the scene shows the characters on the stage while it is edited.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [DefaultExecutionOrder(1000)]
    public sealed class Hd2dUiBillboard : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int UnlitId = Shader.PropertyToID("_Unlit");
        private static readonly int[] Quad = { 0, 1, 2, 2, 3, 0 };

        [Tooltip(
            "3Dの舞台に立たせる絵（RawImageまたはImage）。3Dのときは画面に描かず、位置・大きさ・色・発光だけを使う。"
        )]
        public Graphic Picture;

        [Tooltip(
            "足元の影のUI。中心を足元として地面に置き、3Dでは同じ範囲に接地影を敷く。未指定なら絵の下端の中央を足元にする。"
        )]
        public Graphic FootShadow;

        [Tooltip("3Dのときに描かないUI（2Dのときだけの飾り）。")]
        public Graphic[] HideWhenStaged = Array.Empty<Graphic>();

        [Tooltip("光源からの影を落とす（影の板を立てる）。")]
        public bool CastShadow = true;

        [Tooltip("舞台の光を受ける。焚き火のように自分で光る絵はオフにし、元の色のまま描く。")]
        public bool Lit = true;

        [Tooltip("接地影の濃さ。0で出さない。")]
        [Range(0f, 1f)]
        public float ContactShadowStrength = 0.8f;

        [Tooltip(
            "描く順番（Sorting Order）。同じCanvasの演出のうち、これより小さい順番のものは奥に、大きいものは手前に描く。"
        )]
        public int SortingOrder = 10;

        [Tooltip("影の板を向ける光。未指定ならステージのカメラの主光源。")]
        public Light ShadowLight;

        private readonly Vector3[] corners = new Vector3[4];
        private readonly Vector3[] visualVertices = new Vector3[4];
        private readonly Vector3[] shadowVertices = new Vector3[4];
        private readonly Vector3[] contactVertices = new Vector3[4];
        private readonly Vector3[] normals = new Vector3[4];
        private readonly Vector2[] uvs = new Vector2[4];
        private readonly Color[] contactColors = new Color[4];
        private readonly List<CanvasGroup> groups = new();
        private static readonly List<Hd2dUiBillboard> All = new();

        private bool staged;
        private Part visual;
        private Part shadowBoard;
        private Part contact;
        private MaterialPropertyBlock block;

        /// <summary>True while the character is drawn in 3D instead of on the UI.</summary>
        public bool Staged => staged;

        /// <summary>The board seen by the camera, while staged (for tests and previews).</summary>
        public Renderer VisualRenderer => staged ? visual.Renderer : null;

        /// <summary>The upright board that casts the shadow, while staged.</summary>
        public Renderer ShadowRenderer => staged ? shadowBoard.Renderer : null;

        /// <summary>The contact shadow under the feet, while staged.</summary>
        public Renderer ContactRenderer => staged ? contact.Renderer : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        private void OnEnable()
        {
            All.Add(this);
            if (Application.isPlaying)
                return;
            // Outside Play Mode LateUpdate runs only when something changes; the boards follow
            // the Game view's size and the screen's layout before every frame drawn.
            RenderPipelineManager.beginContextRendering += RefreshBeforeRendering;
            Refresh();
        }

        private void OnDisable()
        {
            All.Remove(this);
            RenderPipelineManager.beginContextRendering -= RefreshBeforeRendering;
            SetStaged(false, null);
        }

        private void LateUpdate() => Refresh();

        private void RefreshBeforeRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!Application.isPlaying)
                Refresh();
        }

        /// <summary>Stands or takes down every character for the stage camera now on.</summary>
        public static void RefreshAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i] == null)
                    All.RemoveAt(i);
                else
                    All[i].Refresh();
            }
        }

        private void Refresh()
        {
            var stage = Hd2dStageCamera.Active;
            bool wanted =
                isActiveAndEnabled
                && Picture != null
                && stage != null
                && stage.isActiveAndEnabled
                && stage.CanStage
                && stage.gameObject.scene == gameObject.scene;
            if (wanted != staged)
                SetStaged(wanted, stage);
            if (staged)
                Draw(stage);
        }

        private void SetStaged(bool value, Hd2dStageCamera stage)
        {
            if (value == staged)
                return;
            if (value)
            {
                block ??= new MaterialPropertyBlock();
                var scene = gameObject.scene;
                visual = Part.Create($"{name} (HD-2D)", scene, stage.SpriteMaterial);
                shadowBoard = Part.Create($"{name} (HD-2D Shadow)", scene, stage.SpriteMaterial);
                contact = Part.Create(
                    $"{name} (HD-2D Contact Shadow)",
                    scene,
                    stage.ContactShadowMaterial
                );
                // A scene that is being unloaded takes no new objects; the screen goes with it.
                if (visual.Object == null || shadowBoard.Object == null || contact.Object == null)
                {
                    DestroyParts();
                    return;
                }
                visual.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shadowBoard.Renderer.shadowCastingMode = UnityEngine
                    .Rendering
                    .ShadowCastingMode
                    .ShadowsOnly;
                contact.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else
                DestroyParts();
            staged = value;
            Hide(staged);
        }

        private void DestroyParts()
        {
            visual.Destroy();
            shadowBoard.Destroy();
            contact.Destroy();
            visual = shadowBoard = contact = default;
        }

        // The UI picture and its decorations stay in place (the screen still uses their rects
        // for taps and layout); only their drawing is skipped.
        private void Hide(bool hidden)
        {
            SetCulled(Picture, hidden);
            SetCulled(FootShadow, hidden);
            if (HideWhenStaged != null)
                foreach (var graphic in HideWhenStaged)
                    SetCulled(graphic, hidden);
        }

        private static void SetCulled(Graphic graphic, bool culled)
        {
            if (graphic != null && graphic.canvasRenderer.cull != culled)
                graphic.canvasRenderer.cull = culled;
        }

        private void Draw(Hd2dStageCamera stage)
        {
            Hide(true);
            var canvas = Picture.canvas;
            var texture = Picture.mainTexture;
            float alpha = Picture.color.a * Picture.canvasRenderer.GetAlpha() * GroupAlpha();
            if (
                canvas == null
                || texture == null
                || !Picture.isActiveAndEnabled
                || alpha <= 0.002f
                || !TryFoot(stage, canvas, out var foot, out var footWorld, out var size)
            )
            {
                SetVisible(false);
                return;
            }

            var root = (RectTransform)canvas.rootCanvas.transform;
            var offset = UiOffset(stage);
            float unit = stage.WorldPerCanvasUnit(footWorld, size.y);
            var right = stage.RestRight;
            var up = stage.RestUp;
            var shadowRight = Hd2dStageMath.ShadowBoardRight(TowardsLight(stage, footWorld), right);
            Picture.rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                var d = (CanvasPoint(root, corners[i]) - offset - foot) * unit;
                visualVertices[i] = footWorld + right * d.x + up * d.y;
                shadowVertices[i] = footWorld + shadowRight * d.x + Vector3.up * d.y;
            }
            FillUvs();

            for (int i = 0; i < 4; i++)
                normals[i] = -stage.RestForward;
            visual.Set(visualVertices, normals, uvs);
            var shadowNormal = Vector3.Cross(shadowRight, Vector3.up);
            for (int i = 0; i < 4; i++)
                normals[i] = shadowNormal;
            shadowBoard.Set(shadowVertices, normals, uvs);

            var tint = Picture.color;
            block.Clear();
            block.SetTexture(BaseMapId, texture);
            block.SetColor(BaseColorId, new Color(tint.r, tint.g, tint.b, alpha));
            block.SetFloat(UnlitId, Lit ? 0f : 1f);
            var material = Picture.material;
            if (material != null && material.HasProperty(FlashAmountId))
            {
                block.SetFloat(FlashAmountId, material.GetFloat(FlashAmountId));
                if (material.HasProperty(FlashColorId))
                    block.SetColor(FlashColorId, material.GetColor(FlashColorId));
            }
            visual.Renderer.SetPropertyBlock(block);
            shadowBoard.Renderer.SetPropertyBlock(block);
            visual.Renderer.sortingOrder = SortingOrder;

            bool contactShown = ContactShadowStrength > 0f && FillContact(stage, root, offset);
            if (contactShown)
            {
                for (int i = 0; i < 4; i++)
                    contactColors[i] = new Color(1f, 1f, 1f, alpha * ContactShadowStrength);
                contact.Set(contactVertices, null, ContactUvs, contactColors);
                contact.Renderer.sortingOrder = SortingOrder - 1;
            }

            visual.Renderer.enabled = true;
            shadowBoard.Renderer.enabled = CastShadow && alpha > 0.5f;
            contact.Renderer.enabled = contactShown;
        }

        private static readonly Vector2[] ContactUvs =
        {
            new(0f, 0f),
            new(0f, 1f),
            new(1f, 1f),
            new(1f, 0f),
        };

        private void SetVisible(bool shown)
        {
            visual.Renderer.enabled = shown;
            shadowBoard.Renderer.enabled = shown;
            contact.Renderer.enabled = shown;
        }

        // The feet: the centre of the 2D shadow, or the bottom middle of the picture; in canvas
        // units from the bottom left, with the stage's own movement taken out.
        private bool TryFoot(
            Hd2dStageCamera stage,
            Canvas canvas,
            out Vector2 foot,
            out Vector3 footWorld,
            out Vector2 size
        )
        {
            var root = (RectTransform)canvas.rootCanvas.transform;
            size = root.rect.size;
            Vector3 world;
            if (FootShadow != null)
            {
                FootShadow.rectTransform.GetWorldCorners(corners);
                world = (corners[0] + corners[2]) * 0.5f;
            }
            else
            {
                Picture.rectTransform.GetWorldCorners(corners);
                world = (corners[0] + corners[3]) * 0.5f;
            }
            foot = CanvasPoint(root, world) - UiOffset(stage);
            footWorld = default;
            if (size.x <= 0f || size.y <= 0f)
                return false;
            return stage.TryGroundPoint(
                new Vector2(foot.x / size.x, foot.y / size.y),
                out footWorld
            );
        }

        // The contact shadow covers on the ground what the 2D shadow covered on the screen.
        private bool FillContact(Hd2dStageCamera stage, RectTransform root, Vector2 offset)
        {
            var size = root.rect.size;
            if (FootShadow != null)
            {
                FootShadow.rectTransform.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    var point = CanvasPoint(root, corners[i]) - offset;
                    if (
                        !stage.TryGroundPoint(
                            new Vector2(point.x / size.x, point.y / size.y),
                            out var ground
                        )
                    )
                        return false;
                    contactVertices[i] = ground + Vector3.up * 0.01f;
                }
                return true;
            }

            var center = (visualVertices[0] + visualVertices[3]) * 0.5f;
            float width = Vector3.Distance(visualVertices[0], visualVertices[3]) * 0.5f;
            var across = stage.RestRight;
            across.y = 0f;
            across = across.sqrMagnitude > 1e-6f ? across.normalized : Vector3.right;
            var along = Vector3.Cross(across, Vector3.up) * -1f;
            center.y = stage.GroundHeight + 0.01f;
            float half = width * 0.5f;
            float depth = width * 0.25f;
            contactVertices[0] = center - across * half - along * depth;
            contactVertices[1] = center - across * half + along * depth;
            contactVertices[2] = center + across * half + along * depth;
            contactVertices[3] = center + across * half - along * depth;
            return true;
        }

        private void FillUvs()
        {
            var rect = new Rect(0f, 0f, 1f, 1f);
            if (Picture is RawImage raw)
                rect = raw.uvRect;
            else if (Picture is Image image && image.sprite != null)
            {
                var outer = UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite);
                rect = Rect.MinMaxRect(outer.x, outer.y, outer.z, outer.w);
            }
            uvs[0] = new Vector2(rect.xMin, rect.yMin);
            uvs[1] = new Vector2(rect.xMin, rect.yMax);
            uvs[2] = new Vector2(rect.xMax, rect.yMax);
            uvs[3] = new Vector2(rect.xMax, rect.yMin);
        }

        private Vector3 TowardsLight(Hd2dStageCamera stage, Vector3 footWorld)
        {
            var light = ShadowLight != null ? ShadowLight : stage.KeyLight;
            if (light == null)
                return -stage.RestForward;
            return light.type == LightType.Directional
                ? -light.transform.forward
                : light.transform.position - footWorld;
        }

        private Vector2 UiOffset(Hd2dStageCamera stage)
        {
            var root = stage.UiOffsetRoot;
            return root != null && transform.IsChildOf(root) ? stage.UiOffset : Vector2.zero;
        }

        private float GroupAlpha()
        {
            float alpha = 1f;
            Picture.GetComponentsInParent(false, groups);
            foreach (var group in groups)
            {
                if (!group.enabled)
                    continue;
                alpha *= group.alpha;
                if (group.ignoreParentGroups)
                    break;
            }
            return alpha;
        }

        /// <summary>A point of the root canvas in canvas units from its bottom left corner.</summary>
        public static Vector2 CanvasPoint(RectTransform root, Vector3 world)
        {
            var local = root.InverseTransformPoint(world);
            return (Vector2)local - root.rect.min;
        }

        // One generated quad: its object, mesh and renderer, owned by the billboard.
        private struct Part
        {
            public GameObject Object;
            public Mesh Mesh;
            public MeshRenderer Renderer;

            // An empty part when the scene is being unloaded and refuses the new object.
            public static Part Create(string name, Scene scene, Material material)
            {
                var created = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                // Outside Play Mode the parts belong to the open scene but are never saved.
                var flags = Application.isPlaying ? HideFlags.None : HideFlags.HideAndDontSave;
                created.hideFlags = flags;
                try
                {
                    SceneManager.MoveGameObjectToScene(created, scene);
                }
                catch (ArgumentException)
                {
                    Release(created);
                    return default;
                }
                var part = new Part
                {
                    Object = created,
                    Mesh = new Mesh { name = name, hideFlags = flags },
                };
                part.Mesh.MarkDynamic();
                part.Mesh.vertices = new Vector3[4];
                part.Mesh.SetTriangles(Quad, 0);
                part.Object.GetComponent<MeshFilter>().sharedMesh = part.Mesh;
                part.Renderer = part.Object.GetComponent<MeshRenderer>();
                part.Renderer.sharedMaterial = material;
                part.Renderer.receiveShadows = false;
                part.Renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                part.Renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                part.Renderer.enabled = false;
                return part;
            }

            public void Set(
                Vector3[] vertices,
                Vector3[] normals,
                Vector2[] uvs,
                Color[] colors = null
            )
            {
                Mesh.vertices = vertices;
                if (normals != null)
                    Mesh.normals = normals;
                Mesh.uv = uvs;
                if (colors != null)
                    Mesh.colors = colors;
                Mesh.RecalculateBounds();
            }

            public void Destroy()
            {
                Release(Object);
                Release(Mesh);
            }

            private static void Release(UnityEngine.Object target)
            {
                if (target == null)
                    return;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(target);
                else
                    UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
