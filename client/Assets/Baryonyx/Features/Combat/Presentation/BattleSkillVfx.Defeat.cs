using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The defeat of an enemy, in three beats. The hold: the battle stops for a moment and the
    /// enemy stays, white-hot and trembling, so the finishing blow reads. The crumble: from the
    /// side the blow came from and from the top, the picture is eaten away dot by dot in a ragged
    /// front that burns white (the Battle Defeat shader), and each dot that goes flies off as a
    /// square mote of its own colour, lit through, rising and blown along the blow, never turned,
    /// so the pixel art's grid never breaks; dust is thrown along the floor and the light falls on
    /// those near. The fall-off: the motes drift up and fade, the dust settles. A boss holds
    /// longer and flashes the screen twice (no more than three times a second), trembles harder,
    /// crumbles slower into more motes and pushes the stage in.
    /// </summary>
    public sealed partial class BattleSkillVfx
    {
        // The hold, white-hot and still, and the crumble that eats the picture away (s).
        private const float CrumbleHold = 0.1f;
        private const float CrumbleTime = 0.55f;
        private const float BossCrumbleHold = 0.42f;
        private const float BossCrumbleTime = 1.1f;

        // How much of the crumble's order is burning white just ahead of its front.
        private const float CrumbleBand = 0.12f;

        // The crumble starts slow and quickens: the share eaten is time to this power.
        private const float CrumbleEase = 1.5f;

        // The motes flown off: a share of the dots, more for a boss.
        private const int CrumbleMotes = 90;
        private const int BossCrumbleMotes = 180;

        // The white-hot light of a crumbling enemy.
        private static readonly Color CrumbleHot = new(1f, 0.95f, 0.86f);

        [Header("撃破")]
        [Tooltip("撃破した敵の絵をドットごとに削って消すマテリアル（Battle Defeatシェーダー）。")]
        public Material DefeatMaterial;

        // The pictures of beaten enemies, worked out once: the order their dots go in.
        private readonly Dictionary<Texture, CrumbleArt> crumbleArt = new();

        private void OnDestroy()
        {
            foreach (var art in crumbleArt.Values)
                art.Release();
            crumbleArt.Clear();
        }

        /// <summary>
        /// A beaten enemy crumbles away (see the class summary): <paramref name="sprite"/> is its
        /// picture, which the caller hides at once, <paramref name="direction"/> the way the blow
        /// went, and <paramref name="boss"/> makes it longer and heavier.
        /// </summary>
        public void Crumble(RawImage sprite, Vector2 direction, bool boss)
        {
            if (FrontLayer == null || sprite == null || sprite.texture == null)
                return;
            if (direction == Vector2.zero)
                direction = Vector2.right;
            direction.Normalize();
            var rect = sprite.rectTransform;
            var worldCorners = new Vector3[4];
            rect.GetWorldCorners(worldCorners);
            Vector2 min = FrontLayer.InverseTransformPoint(worldCorners[0]);
            Vector2 max = FrontLayer.InverseTransformPoint(worldCorners[2]);
            var center = (min + max) * 0.5f;
            var size = max - min;
            var feet = new Vector2(center.x, min.y);
            float hold = boss ? BossCrumbleHold : CrumbleHold;
            float time = boss ? BossCrumbleTime : CrumbleTime;

            var art = ArtOf(sprite, direction);
            if (art != null)
            {
                Body(sprite, art, center, size, hold, time, boss);
                Motes(art, min, size, direction, hold, time, boss);
            }

            // The light of the burning body on the background and on those near.
            float reach = Mathf.Max(size.x, size.y);
            Glow(backLight, center, CrumbleHot, reach * 1.2f, hold + time, boss ? 0.32f : 0.22f);
            Illuminate(center, CrumbleHot, boss ? 1f : 0.75f, reach * 1.3f, hold, time * 0.8f);
            // Dust thrown out along the floor both ways as it falls apart.
            foreach (float side in new[] { 0f, 180f })
                Spray(
                    backSmoke,
                    feet + Vector2.up * 8f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        From = new Color(0.5f, 0.48f, 0.44f, 0.6f),
                        To = new Color(0.38f, 0.37f, 0.36f, 0.6f),
                        Speed = new Vector2(160f, 380f) * (boss ? 1.4f : 1f),
                        Direction = side,
                        Spread = 16f,
                        Life = new Vector2(0.7f, 1.2f),
                        Size = new Vector2(70f, 120f) * (boss ? 1.5f : 1f),
                        Aspect = 0.45f,
                        Grow = 0.4f,
                        Shrink = 1.9f,
                        Drag = 3.2f,
                        Gravity = -15f,
                        Spin = 10f,
                        Hold = 0.25f,
                        Area = size.x * 0.2f,
                        Delay = new Vector2(hold, hold + time * 0.3f),
                    },
                    boss ? 5 : 3
                );
            StartCoroutine(CrumbleBeats(center, direction, hold, boss));
        }

        /// <summary>
        /// The screen's part: the battle holds still (and a boss flashes the screen twice), then
        /// the stage is knocked along the blow as the crumble starts, and time runs slow a moment.
        /// </summary>
        private IEnumerator CrumbleBeats(Vector2 center, Vector2 direction, float hold, bool boss)
        {
            HitStop(hold);
            if (boss)
            {
                ScreenFlash(Color.white, MaxFlash);
                yield return WaitReal(FlashGap + 0.02f);
                ScreenFlash(CrumbleHot, MaxFlash);
                yield return WaitReal(hold - FlashGap - 0.02f);
                Punch(center, 0.03f);
            }
            else
                yield return WaitReal(hold);
            Shake(boss ? 0.6f : 0.3f, direction);
            SlowMotion(boss ? 0.3f : 0.5f, boss ? 0.5f : 0.25f);
        }

        /// <summary>
        /// The enemy's picture, drawn again where it stood (the caller hides the real one): white-hot
        /// and trembling a dot through the hold, then cooling toward its own colours as it is eaten
        /// away behind a front that burns white.
        /// </summary>
        private void Body(
            RawImage sprite,
            CrumbleArt art,
            Vector2 center,
            Vector2 size,
            float hold,
            float time,
            bool boss
        )
        {
            var body = Spawn(frontSmoke, BattleVfxShape.Glow, center, size, Color.white);
            body.Image.texture = sprite.texture;
            body.Image.uvRect = sprite.uvRect;
            body.Image.material = art.Material;
            float dot = size.x / Mathf.Max(1, art.Columns);
            float total = hold + time;
            Animate(
                body,
                total,
                (s, k) =>
                {
                    float t = k * total;
                    float crumble = Mathf.Clamp01((t - hold) / time);
                    // A boss shakes back and forth through its hold; the others only quiver.
                    float tremble =
                        t < hold
                            ? (Mathf.Repeat(t, 0.06f) < 0.03f ? 1f : -1f) * dot * (boss ? 1f : 0.5f)
                            : 0f;
                    s.Holder.anchoredPosition = center + new Vector2(tremble, 0f);
                    // Its own colours come back early in the crumble, so it is the enemy that
                    // breaks up; only the front burns on.
                    s.Image.SetShape(
                        s.Seed,
                        EaseOut(Mathf.Clamp01(crumble / 0.25f)),
                        Crumbled(crumble),
                        t < hold ? 1.4f : 1f
                    );
                }
            );
        }

        /// <summary>
        /// The share of the picture eaten away <paramref name="k"/> (0 to 1) into the crumble.
        /// </summary>
        public static float Crumbled(float k) => Mathf.Pow(Mathf.Clamp01(k), CrumbleEase);

        /// <summary>
        /// When, from the start of the crumble (0 to 1 of it), the dot at
        /// <paramref name="order"/> (0 to 1) goes: as the shader's front passes it.
        /// </summary>
        public static float CrumbleMoment(float order) =>
            Mathf.Pow(Mathf.Clamp01(order / (1f + CrumbleBand)), 1f / CrumbleEase);

        /// <summary>
        /// The motes: a share of the dots, spread over the order they go in, each flying off from
        /// where it was as its dot goes, white-hot and cooling to its colour as it fades.
        /// </summary>
        private void Motes(
            CrumbleArt art,
            Vector2 min,
            Vector2 size,
            Vector2 direction,
            float hold,
            float time,
            bool boss
        )
        {
            if (
                frontLight == null
                || MaterialOf(BattleVfxShape.Mote) == null
                || art.Dots.Length == 0
            )
                return;
            int count = Mathf.Min(art.Dots.Length, boss ? BossCrumbleMotes : CrumbleMotes);
            // Several enemies beaten by one blow share the particles, and leave some for the blow.
            int live = 0;
            foreach (var other in particles)
                if (other.Live)
                    live++;
            count = Mathf.Min(count, Mathf.Max(20, MaxParticles - live - 80));
            float step = art.Dots.Length / (float)count;
            float dot = size.x / Mathf.Max(1, art.Columns);
            for (int i = 0; i < count; i++)
            {
                int index = Mathf.Min(art.Dots.Length - 1, (int)((i + Random.value) * step));
                var source = art.Dots[index];
                var particle = Take(frontLight, BattleVfxShape.Mote);
                particle.Position = min + Vector2.Scale(source.Local, size);
                particle.Velocity =
                    direction * Random.Range(40f, 220f)
                    + Vector2.up * Random.Range(30f, 160f)
                    + Random.insideUnitCircle * 50f;
                particle.Gravity = -Random.Range(120f, 320f);
                particle.Drag = 2.4f;
                particle.Life = Random.Range(0.45f, 0.9f) * (boss ? 1.3f : 1f);
                // The square fills the middle third of its board; some go as two dots.
                particle.Size = Vector2.one * (dot * (Random.value < 0.5f ? 2f : 1f) * 3f);
                particle.StartScale = 1f;
                particle.EndScale = 0.35f;
                var color = source.Color;
                particle.From = Color.Lerp(color, Color.white, 0.3f);
                particle.To = new Color(color.r, color.g, color.b, 0f);
                particle.Bright = 1.8f;
                particle.Angle = 0f;
                particle.Spin = 0f;
                particle.Stretch = 0f;
                particle.Delay = hold + time * CrumbleMoment(source.Order);
                particle.Apply();
            }
        }

        private static IEnumerator WaitReal(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }

        // --- The crumbling picture -------------------------------------------------------------

        /// <summary>
        /// The order a picture's dots go in, worked out once per picture (again when its crop or
        /// the side of the blow changes), or null when the picture cannot be read.
        /// </summary>
        private CrumbleArt ArtOf(RawImage sprite, Vector2 direction)
        {
            if (DefeatMaterial == null)
                return null;
            var texture = sprite.texture;
            bool rightward = direction.x >= 0f;
            if (crumbleArt.TryGetValue(texture, out var art))
            {
                if (art.Uv == sprite.uvRect && art.Rightward == rightward)
                    return art;
                art.Release();
                crumbleArt.Remove(texture);
            }
            var pixels = ReadPicture(texture);
            if (pixels == null)
                return null;
            art = CrumbleArt.Make(
                pixels,
                texture.width,
                texture.height,
                sprite.uvRect,
                rightward ? Vector2.right : Vector2.left,
                Random.value * 50f
            );
            art.Rightward = rightward;
            art.Material = new Material(DefeatMaterial) { name = "Defeat " + texture.name };
            art.Material.SetTexture("_OrderTex", art.Order);
            art.Material.SetFloat("_Band", CrumbleBand);
            crumbleArt[texture] = art;
            return art;
        }

        /// <summary>The picture's pixels, read straight or copied back from the GPU.</summary>
        private static Color32[] ReadPicture(Texture texture)
        {
            if (texture is Texture2D readable && readable.isReadable)
            {
                try
                {
                    return readable.GetPixels32();
                }
                catch (Exception)
                {
                    // A format the CPU cannot unpack: copied through the GPU below.
                }
            }
            var target = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );
            var active = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                var copy = new Texture2D(
                    texture.width,
                    texture.height,
                    TextureFormat.RGBA32,
                    false
                );
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply(false);
                var pixels = copy.GetPixels32();
                Discard(copy);
                return pixels;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void Discard(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        /// <summary>One solid dot of a crumbling picture.</summary>
        public struct CrumbleDot
        {
            /// <summary>Its middle in the picture's crop, 0 to 1 from the bottom left.</summary>
            public Vector2 Local;
            public Color Color;

            /// <summary>When it goes, 0 (first) to 1 (last), as stored in the order map.</summary>
            public float Order;
        }

        /// <summary>
        /// A picture worked out for crumbling: its solid dots in the order they go, and the same
        /// order in a map the size of the picture for the shader.
        /// </summary>
        public sealed class CrumbleArt
        {
            public Rect Uv;
            public bool Rightward;
            public int Columns;
            public int Rows;
            public CrumbleDot[] Dots = Array.Empty<CrumbleDot>();
            public Texture2D Order;
            public Material Material;

            /// <summary>
            /// Orders the solid dots of the crop <paramref name="uv"/> of a picture: first on the
            /// side the blow came from (against <paramref name="direction"/>) and at the top,
            /// with clumps and grain of noise so the front is ragged; then spread evenly from 0 to
            /// 1, so the dots go at a steady rate with no wait for the empty corners.
            /// </summary>
            public static CrumbleArt Make(
                Color32[] pixels,
                int width,
                int height,
                Rect uv,
                Vector2 direction,
                float seed
            )
            {
                int x0 = Mathf.Clamp(Mathf.FloorToInt(uv.xMin * width), 0, width);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(uv.xMax * width), x0, width);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(uv.yMin * height), 0, height);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(uv.yMax * height), y0, height);
                int columns = Mathf.Max(1, x1 - x0);
                int rows = Mathf.Max(1, y1 - y0);
                var dots = new List<CrumbleDot>();
                var keys = new List<float>();
                var texels = new List<int>();
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        var pixel = pixels[y * width + x];
                        if (pixel.a < 128)
                            continue;
                        var local = new Vector2((x - x0 + 0.5f) / columns, (y - y0 + 0.5f) / rows);
                        float along = Vector2.Dot(local - new Vector2(0.5f, 0.5f), direction);
                        float clump = Mathf.PerlinNoise(seed + x * 0.2f, seed + y * 0.2f);
                        float grain = Mathf.Repeat(
                            Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.55f,
                            1f
                        );
                        keys.Add(
                            along
                                + (1f - local.y) * 0.25f
                                + (clump - 0.5f) * 0.3f
                                + (grain - 0.5f) * 0.08f
                        );
                        dots.Add(new CrumbleDot { Local = local, Color = pixel });
                        texels.Add(y * width + x);
                    }
                }
                var ranks = new int[dots.Count];
                for (int i = 0; i < ranks.Length; i++)
                    ranks[i] = i;
                Array.Sort(ranks, (a, b) => keys[a].CompareTo(keys[b]));

                var order = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                {
                    name = "CrumbleOrder",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var map = new Color32[width * height];
                var sorted = new CrumbleDot[dots.Count];
                for (int rank = 0; rank < ranks.Length; rank++)
                {
                    int i = ranks[rank];
                    byte value = (byte)
                        Mathf.RoundToInt(255f * rank / Mathf.Max(1, ranks.Length - 1));
                    map[texels[i]] = new Color32(value, value, value, 255);
                    var dot = dots[i];
                    dot.Order = value / 255f;
                    sorted[rank] = dot;
                }
                order.SetPixels32(map);
                order.Apply(false, true);
                return new CrumbleArt
                {
                    Uv = uv,
                    Columns = columns,
                    Rows = rows,
                    Dots = sorted,
                    Order = order,
                };
            }

            public void Release()
            {
                Discard(Material);
                Discard(Order);
                Material = null;
                Order = null;
            }
        }
    }
}
