using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// The small boards the tap sparkle is drawn on, laid out again every frame in one mesh and
    /// coloured by <c>TapSparkle.shader</c>. Positions are in the design's pixels (1920 by 1080, x to
    /// the right and y down from the top left). A shape keeps its own numbers and only its board is
    /// drawn at <see cref="SetOrigin"/>'s size. The canvas must carry TexCoord1
    /// (<see cref="EnableChannels"/>).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TapSparkleMarks : MaskableGraphic
    {
        private const int GlowShape = 0;
        private const int DotShape = 1;

        private readonly struct Mark
        {
            public readonly Vector2 Center;
            public readonly float Reach;
            public readonly Color Color;
            public readonly Vector4 Shape;

            public Mark(Vector2 center, float reach, Color color, Vector4 shape)
            {
                Center = center;
                Reach = reach;
                Color = color;
                Shape = shape;
            }
        }

        [Tooltip("TapSparkle.shader。有効になったときに、これでマテリアルを作る。")]
        public Shader Shader;

        private readonly List<Mark> marks = new();
        private Vector2 origin;
        private float scale = 1f;
        private Material made;

        /// <summary>
        /// Adds the boards on a child stretched over the canvas. On the canvas's own object the
        /// boards' values did not reach the shader and nothing showed (2026-10-26).
        /// </summary>
        public static TapSparkleMarks AddTo(Transform canvas, Shader shader)
        {
            var child = new GameObject("Marks", typeof(RectTransform));
            child.SetActive(false);
            var rect = (RectTransform)child.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var marks = child.AddComponent<TapSparkleMarks>();
            marks.raycastTarget = false;
            marks.Shader = shader;
            child.SetActive(true);
            return marks;
        }

        /// <summary>How many boards are laid out now.</summary>
        public int Count => marks.Count;

        // 作ったマテリアルは保存しないため、material に入れずに既定のマテリアルとして渡す。
        public override Material defaultMaterial => made != null ? made : base.defaultMaterial;

        protected override void OnEnable()
        {
            if (made == null && Shader != null)
                made = new Material(Shader) { hideFlags = HideFlags.HideAndDontSave };
            EnableChannels();
            base.OnEnable();
        }

        protected override void OnDestroy()
        {
            if (made != null)
            {
                if (Application.isPlaying)
                    Destroy(made);
                else
                    DestroyImmediate(made);
            }
            base.OnDestroy();
        }

        /// <summary>
        /// Where the next shapes are centred, in the design's pixels, and how large they are drawn
        /// (1 is the size their numbers give).
        /// </summary>
        public void SetOrigin(Vector2 designPoint, float size = 1f)
        {
            origin = designPoint;
            scale = size;
        }

        public void Clear()
        {
            marks.Clear();
            SetVerticesDirty();
        }

        /// <summary>The design's pixel under a screen point.</summary>
        public Vector2 DesignPoint(Vector2 screenPoint)
        {
            var root = canvas;
            var camera =
                root != null && root.renderMode != RenderMode.ScreenSpaceOverlay
                    ? root.worldCamera
                    : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPoint,
                camera,
                out var local
            );
            var rect = rectTransform.rect;
            return new Vector2(local.x - rect.xMin, rect.yMax - local.y);
        }

        /// <summary>A soft glow, added to what is behind it.</summary>
        public void Glow(float x, float y, float radius, Color color, float alpha) =>
            Add(x, y, radius, radius, color, alpha, GlowShape, true);

        /// <summary>A round dot, added (light) or laid over what is behind it.</summary>
        public void Dot(
            float x,
            float y,
            float radius,
            Color color,
            float alpha,
            bool additive = true
        ) => Add(x, y, radius + 2f, radius, color, alpha, DotShape, additive);

        private void Add(
            float x,
            float y,
            float reach,
            float radius,
            Color color,
            float alpha,
            int shape,
            bool additive
        )
        {
            if (alpha <= 0.002f || radius <= 0f)
                return;
            marks.Add(
                new Mark(
                    origin + new Vector2(x, y) * scale,
                    reach,
                    new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha)),
                    new Vector4(shape, additive ? 1f : 0f, radius, scale)
                )
            );
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var vertex = UIVertex.simpleVert;
            foreach (var mark in marks)
            {
                int start = vh.currentVertCount;
                for (int corner = 0; corner < 4; corner++)
                {
                    // The shader works the shape out in its own numbers (uv0); the board on screen
                    // is the same square drawn at the mark's size (shape.w).
                    var local = new Vector2(
                        corner < 2 ? -mark.Reach : mark.Reach,
                        corner == 0 || corner == 3 ? mark.Reach : -mark.Reach
                    );
                    var design = mark.Center + local * mark.Shape.w;
                    vertex.position = new Vector3(rect.xMin + design.x, rect.yMax - design.y);
                    vertex.color = mark.Color;
                    vertex.uv0 = new Vector4(local.x, local.y, 0f, 0f);
                    vertex.uv1 = mark.Shape;
                    vh.AddVert(vertex);
                }
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start + 2, start + 3, start);
            }
        }

        /// <summary>Lets the canvas carry the boards' values to the shader.</summary>
        public void EnableChannels()
        {
            var root = GetComponentInParent<Canvas>(true);
            if (root == null)
                return;
            root.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            if (root.rootCanvas != null)
                root.rootCanvas.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1;
        }
    }
}
