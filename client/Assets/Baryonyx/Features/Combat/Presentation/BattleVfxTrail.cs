using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// A band of an effect's shape bent along a path (Battle Vfx Shape shader): the board's first
    /// UV runs across the band (x) and along it (y, 0 at its tail, 1 at its head), so a shape drawn
    /// along +y, as the streak is, follows the curve. Like <see cref="BattleVfxImage"/>, it writes
    /// the shape's own values into the second UV, which the canvas must carry
    /// (<see cref="BattleVfxImage.EnableShapeChannel"/>).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleVfxTrail : MaskableGraphic
    {
        private readonly List<Vector2> points = new();
        private float width = 1f;
        private Vector4 shape;

        /// <summary>The shape's values (see <see cref="BattleVfxImage.Shape"/>).</summary>
        public void SetShape(float seed, float progress, float eaten, float brightness = 1f)
        {
            var value = new Vector4(seed, progress, eaten, brightness);
            if (shape == value)
                return;
            shape = value;
            SetVerticesDirty();
        }

        /// <summary>
        /// Lays the band along <paramref name="path"/> (in this graphic's space, tail first),
        /// <paramref name="bandWidth"/> across. Fewer than two points draw nothing.
        /// </summary>
        public void SetPath(IReadOnlyList<Vector2> path, float bandWidth)
        {
            points.Clear();
            // The board covers the band round its pivot, so a mask's culling, which looks at the
            // board, never drops a band drawn beyond it.
            var reach = Vector2.zero;
            for (int i = 0; i < path.Count; i++)
            {
                points.Add(path[i]);
                reach = Vector2.Max(reach, new Vector2(Mathf.Abs(path[i].x), Mathf.Abs(path[i].y)));
            }
            width = bandWidth;
            var pivot = rectTransform.pivot;
            var cover = (reach + Vector2.one * bandWidth) * 2f;
            if (
                pivot != new Vector2(0.5f, 0.5f)
                || cover.x > rectTransform.rect.width
                || cover.y > rectTransform.rect.height
            )
            {
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = Vector2.Max(rectTransform.sizeDelta, cover);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int count = points.Count;
            if (count < 2)
                return;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.uv1 = shape;
            float half = width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var along = points[Mathf.Min(i + 1, count - 1)] - points[Mathf.Max(i - 1, 0)];
                var side =
                    along.sqrMagnitude > 1e-6f
                        ? new Vector2(-along.y, along.x).normalized
                        : Vector2.up;
                float v = i / (float)(count - 1);
                vertex.position = points[i] + side * half;
                vertex.uv0 = new Vector4(0f, v);
                vh.AddVert(vertex);
                vertex.position = points[i] - side * half;
                vertex.uv0 = new Vector4(1f, v);
                vh.AddVert(vertex);
                if (i == 0)
                    continue;
                int at = i * 2;
                vh.AddTriangle(at - 2, at - 1, at);
                vh.AddTriangle(at - 1, at + 1, at);
            }
        }
    }
}
