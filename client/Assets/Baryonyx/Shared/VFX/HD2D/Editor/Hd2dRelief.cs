using System;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    /// <summary>
    /// Works out a normal map for a pixel-art texture of the 3D stage, one normal per dot, so the
    /// stage's lights pick out its bumps: bright dots stand higher than dark ones (the joints
    /// between stones, the gaps in the leaves sink), and a cut-out board rounds off towards its
    /// see-through edge, so a rock or a crate catches a torch on the side that faces it.
    /// </summary>
    public static class Hd2dRelief
    {
        /// <summary>How far in from a see-through edge (in dots) a cut-out rounds off.</summary>
        public const int BevelWidth = 3;

        /// <summary>
        /// The normals of <paramref name="pixels"/> (rows from the bottom, as Unity keeps them),
        /// encoded as colours. <paramref name="relief"/> is how steep a step from black to white
        /// between neighbouring dots stands, <paramref name="bevel"/> how steep the rounded edge of
        /// a cut-out stands. A tile (<paramref name="repeat"/>) reads round its edges.
        /// </summary>
        public static Color32[] Normals(
            Color32[] pixels,
            int width,
            int height,
            float relief,
            float bevel,
            bool repeat
        )
        {
            if (pixels.Length != width * height)
                throw new ArgumentException("The pixels do not fill the size.");
            var lum = new float[pixels.Length];
            var solid = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                solid[i] = p.a >= 128;
                lum[i] = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
            }
            var edge = EdgeHeights(solid, width, height, repeat);

            int Index(int x, int y)
            {
                if (repeat)
                {
                    x = (x % width + width) % width;
                    y = (y % height + height) % height;
                }
                else
                {
                    x = Mathf.Clamp(x, 0, width - 1);
                    y = Mathf.Clamp(y, 0, height - 1);
                }
                return y * width + x;
            }

            var normals = new Color32[pixels.Length];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (!solid[i])
                {
                    normals[i] = new Color32(128, 128, 255, 255);
                    continue;
                }
                // A see-through neighbour has no colour of its own: the dot's own brightness
                // stands in for it, so only the bevel slopes off at the edge.
                float Lum(int index) => solid[index] ? lum[index] : lum[i];
                int left = Index(x - 1, y);
                int right = Index(x + 1, y);
                int down = Index(x, y - 1);
                int up = Index(x, y + 1);
                float dx =
                    (Lum(right) - Lum(left)) * 0.5f * relief
                    + (edge[right] - edge[left]) * 0.5f * bevel;
                float dy =
                    (Lum(up) - Lum(down)) * 0.5f * relief + (edge[up] - edge[down]) * 0.5f * bevel;
                normals[i] = Encode(new Vector3(-dx, -dy, 1f).normalized);
            }
            return normals;
        }

        private static Color32 Encode(Vector3 normal) =>
            new(
                (byte)Mathf.RoundToInt((normal.x * 0.5f + 0.5f) * 255f),
                (byte)Mathf.RoundToInt((normal.y * 0.5f + 0.5f) * 255f),
                (byte)Mathf.RoundToInt((normal.z * 0.5f + 0.5f) * 255f),
                255
            );

        // How high each dot stands on the rounded edge of a cut-out: 0 where it is see-through,
        // rising over the first BevelWidth dots inside like the shoulder of a stone, 1 beyond.
        private static float[] EdgeHeights(bool[] solid, int width, int height, bool repeat)
        {
            var distance = new int[solid.Length];
            for (int i = 0; i < solid.Length; i++)
                distance[i] = solid[i] ? BevelWidth : 0;
            // Each pass lets a dot sit one step further from the edge than its nearest neighbour.
            for (int pass = 0; pass < BevelWidth; pass++)
            {
                var next = (int[])distance.Clone();
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (!solid[i])
                        continue;
                    int nearest = BevelWidth;
                    for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0)
                            continue;
                        int nx = x + ox;
                        int ny = y + oy;
                        if (repeat)
                        {
                            nx = (nx % width + width) % width;
                            ny = (ny % height + height) % height;
                        }
                        else if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                            continue;
                        nearest = Mathf.Min(nearest, distance[ny * width + nx]);
                    }
                    next[i] = Mathf.Min(distance[i], nearest + 1);
                }
                distance = next;
            }
            var heights = new float[solid.Length];
            for (int i = 0; i < solid.Length; i++)
            {
                float t = distance[i] / (float)BevelWidth;
                heights[i] = 1f - (1f - t) * (1f - t);
            }
            return heights;
        }
    }
}
