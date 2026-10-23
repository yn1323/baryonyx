using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Temple
{
    /// <summary>
    /// The shards of a broken window (Temple Summon Glass shader, shard mode), drawn as one mesh
    /// over the window's square. Each shard is a cell of the <see cref="TempleGlassPattern"/> that
    /// shows the window's picture where it was, flies out from the impact, turns, tilts toward the
    /// camera and falls; its edge flashes when its face turns to the light, and it is eaten away
    /// from its edge as it fades. <see cref="Pose"/> places every shard at a time after the break,
    /// so any moment can be drawn again without running time.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TempleGlassShards : MaskableGraphic
    {
        // 窓の絵と、絵の中の窓の範囲（UV）。
        public Texture Picture;
        public Rect PictureRect = new(0, 0, 1, 1);

        // 動き（設計座標の画素と秒）。重さで落ち、外へ飛び、手前へ寄る。
        public float Gravity = 1100f;
        public float Spread = 520f;
        public float TowardCamera = 0.9f;

        private struct Motion
        {
            public float Seed;
            public Vector2 Velocity;
            public float Spin;
            public float Tilt;
            public float TiltSpeed;
            public float Grow;
            public float FadeStart;
        }

        private TempleGlassPattern pattern;
        private Motion[] motions = Array.Empty<Motion>();
        private float time;
        private float brightness = 1f;
        private float fadeEnd = 1.6f;

        public override Texture mainTexture => Picture != null ? Picture : s_WhiteTexture;

        /// <summary>Takes the pattern and gives each shard its own motion from the seed.</summary>
        public void Setup(TempleGlassPattern glass, int seed, float fadeAt)
        {
            pattern = glass;
            fadeEnd = fadeAt;
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            var shards = glass?.Shards ?? Array.Empty<TempleGlassPattern.Shard>();
            motions = new Motion[shards.Count];
            for (int i = 0; i < shards.Count; i++)
            {
                var shard = shards[i];
                var c = shard.Centroid;
                var away = c.sqrMagnitude > 1e-4f ? c.normalized : Vector2.up;
                // 打点に近い破片ほど速く飛ぶ。少し上へ跳ねてから落ちる。
                float speed = Spread * (0.35f + 0.9f * (1f - c.magnitude)) * (0.7f + 0.6f * Next());
                var direction = (
                    away + new Vector2(Next() - 0.5f, Next() - 0.5f) * 0.6f
                ).normalized;
                motions[i] = new Motion
                {
                    Seed = Next(),
                    Velocity = direction * speed + Vector2.up * (120f + 160f * Next()),
                    Spin = (Next() - 0.5f) * 6f,
                    Tilt = Next() * Mathf.PI * 2f,
                    TiltSpeed = (3f + 6f * Next()) * (Next() < 0.5f ? -1f : 1f),
                    Grow = TowardCamera * (0.4f + 1.2f * Next()) * (1.2f - c.magnitude * 0.6f),
                    FadeStart = 0.45f + 0.5f * Next(),
                };
            }
            SetVerticesDirty();
        }

        /// <summary>Places the shards <paramref name="seconds"/> after the break.</summary>
        public void Pose(float seconds, float bright)
        {
            time = Mathf.Max(0f, seconds);
            brightness = bright;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (pattern == null || motions.Length == 0)
                return;
            var rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var origin = rect.center;
            var shards = pattern.Shards;
            for (int i = 0; i < shards.Count && i < motions.Length; i++)
            {
                var shard = shards[i];
                var m = motions[i];
                float t = time;
                var home = shard.Centroid * radius;
                var at = home + m.Velocity * t + 0.5f * Gravity * t * t * Vector2.down;
                float spin = m.Spin * t;
                float tilt = m.Tilt + m.TiltSpeed * t;
                // 手前へ寄るほど大きく、傾くほど横に細く見える。
                float grow = 1f + m.Grow * t;
                float squash = Mathf.Lerp(0.25f, 1f, Mathf.Abs(Mathf.Cos(tilt)));
                // 面が光の向きに来た一瞬だけ、縁が強く光る。
                float glint = Mathf.Exp(-Mathf.Pow(Mathf.Sin(tilt * 0.5f + 0.6f), 2f) / 0.02f);
                float eaten = Mathf.InverseLerp(m.FadeStart, fadeEnd, t);
                var param = new Vector4(m.Seed, eaten, 0f, brightness * (0.12f + 3.5f * glint));
                float cos = Mathf.Cos(spin);
                float sin = Mathf.Sin(spin);
                Vector3 Place(Vector2 point)
                {
                    var local = (point - shard.Centroid) * radius;
                    local = new Vector2(local.x * squash, local.y) * grow;
                    local = new Vector2(
                        cos * local.x - sin * local.y,
                        sin * local.x + cos * local.y
                    );
                    return origin + at + local;
                }
                int start = vh.currentVertCount;
                AddVertex(vh, Place(shard.Centroid), shard.Centroid, param);
                param.z = 1f;
                foreach (var point in shard.Outline)
                    AddVertex(vh, Place(point), point, param);
                int n = shard.Outline.Length;
                for (int k = 0; k < n; k++)
                    vh.AddTriangle(start, start + 1 + k, start + 1 + (k + 1) % n);
            }
        }

        private void AddVertex(VertexHelper vh, Vector3 position, Vector2 point, Vector4 param)
        {
            var board = (point + Vector2.one) * 0.5f;
            var uv = new Vector2(
                PictureRect.x + board.x * PictureRect.width,
                PictureRect.y + board.y * PictureRect.height
            );
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = uv;
            vertex.uv1 = param;
            vh.AddVert(vertex);
        }
    }
}
