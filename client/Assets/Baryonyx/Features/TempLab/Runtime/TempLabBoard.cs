using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.TempLab
{
    /// <summary>
    /// The boards of one TempLab trial drawn in one mesh with <c>TempLabGlow.shader</c>: glows,
    /// streaks, bands along a path and marks laid on the ground, built in world space each frame.
    /// What the vertex values mean is written in the shader.
    /// </summary>
    public sealed class TempLabBoard
    {
        // 形の番号（TempLabGlow.shader の形の並び）。
        public const int GlowShape = 0;
        public const int EmberShape = 1;
        public const int ShockShape = 2;
        public const int ScorchShape = 3;
        public const int BandShape = 4;
        public const int WispShape = 5;
        public const int GlintShape = 6;

        private readonly List<Vector3> vertices = new();
        private readonly List<Color> colors = new();
        private readonly List<Vector4> uv0 = new();
        private readonly List<Vector4> uv1 = new();
        private readonly List<int> triangles = new();

        public TempLabBoard(Mesh mesh, Renderer renderer)
        {
            Mesh = mesh;
            Renderer = renderer;
        }

        public Mesh Mesh { get; }

        public Renderer Renderer { get; }

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            uv0.Clear();
            uv1.Clear();
            triangles.Clear();
        }

        /// <summary>A square facing the camera, half its side <paramref name="half"/> wide.</summary>
        public void Billboard(
            Vector3 center,
            Transform view,
            float half,
            Color color,
            int shape,
            float seed,
            Vector4 values
        )
        {
            var right = view.right * half;
            var up = view.up * half;
            Add(
                center - right - up,
                center - right + up,
                center + right + up,
                center + right - up,
                -1f,
                1f,
                color,
                shape,
                seed,
                values
            );
        }

        /// <summary>
        /// A streak from its tail to its head, its width taken across in the camera's plane, a
        /// little longer than the path so the head is round.
        /// </summary>
        public void Streak(
            Vector3 head,
            Vector3 tail,
            Transform view,
            float width,
            Color color,
            Vector4 values
        )
        {
            var forward = view.forward;
            var along = head - tail;
            along -= forward * Vector3.Dot(along, forward);
            if (along.sqrMagnitude < width * width * 0.25f)
                along = (along.sqrMagnitude > 1e-8f ? along.normalized : Vector3.up) * width * 0.5f;
            var dir = along.normalized;
            var side = Vector3.Cross(forward, dir).normalized * width;
            var front = head + dir * width;
            var back = front - along - dir * width;
            // 頭の半径（板の長さに対する割合）。シェーダーが頭を丸く描くのに使う。
            values.z = width / (along.magnitude + width);
            Add(
                back - side,
                back + side,
                front + side,
                front - side,
                0f,
                1f,
                color,
                EmberShape,
                0f,
                values
            );
        }

        /// <summary>
        /// A band along <paramref name="points"/>, from the tail (u 0) to the head (u 1); each
        /// point is widened by its <paramref name="sides"/> (half the width, already scaled).
        /// </summary>
        public void Strip(
            IReadOnlyList<Vector3> points,
            IReadOnlyList<Vector3> sides,
            Color color,
            int shape,
            float seed,
            Vector4 values,
            float uFrom = 0f,
            float uTo = 1f
        )
        {
            int count = points.Count;
            for (int i = 0; i < count - 1; i++)
            {
                float u0 = Mathf.Lerp(uFrom, uTo, i / (count - 1f));
                float u1 = Mathf.Lerp(uFrom, uTo, (i + 1) / (count - 1f));
                Add(
                    points[i] - sides[i],
                    points[i] + sides[i],
                    points[i + 1] + sides[i + 1],
                    points[i + 1] - sides[i + 1],
                    u0,
                    u1,
                    color,
                    shape,
                    seed,
                    values
                );
            }
        }

        /// <summary>A square laid on the ground.</summary>
        public void Flat(
            Vector3 center,
            float half,
            Color color,
            int shape,
            float seed,
            Vector4 values
        ) =>
            Add(
                center + new Vector3(-half, 0f, -half),
                center + new Vector3(-half, 0f, half),
                center + new Vector3(half, 0f, half),
                center + new Vector3(half, 0f, -half),
                -1f,
                1f,
                color,
                shape,
                seed,
                values
            );

        // a b c d は尾の下・尾の上・頭の上・頭の下。x は uFrom→uTo、y は -1→1。
        private void Add(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            float uFrom,
            float uTo,
            Color color,
            int shape,
            float seed,
            Vector4 values
        )
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            uv0.Add(new Vector4(uFrom, -1f, shape, seed));
            uv0.Add(new Vector4(uFrom, 1f, shape, seed));
            uv0.Add(new Vector4(uTo, 1f, shape, seed));
            uv0.Add(new Vector4(uTo, -1f, shape, seed));
            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
                uv1.Add(values);
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start);
        }

        public void Apply()
        {
            Mesh.Clear();
            Mesh.SetVertices(vertices);
            Mesh.SetColors(colors);
            Mesh.SetUVs(0, uv0);
            Mesh.SetUVs(1, uv1);
            Mesh.SetTriangles(triangles, 0);
            // 板はワールドの位置で作るので、視野の外として捨てられないよう広く取る。
            Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        }
    }
}
