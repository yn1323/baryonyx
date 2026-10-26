using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The win (doc/features/screens/battle-effects.md#勝利の演出): the moment the tension of the
    /// battle breaks and the light comes back. Behind the "勝利！" band, shells of thin lines of
    /// light burst like fireworks: a white core flares, the lines shoot out, slow, bend down under
    /// their weight and leave twinkles where they die out, in a soft glow of the light they shed.
    /// The party is lit from its feet and thin lines of light rise off it. Beating the boss is one
    /// step bigger: the stage hushes into the dark a moment before (the caller's), the dark lifts
    /// at once with a soft golden flash and a small knock, three shells burst one after another,
    /// lines rise off the whole floor and sparkles drift down over the field. No mist nor smoke:
    /// a win makes none.
    /// </summary>
    public sealed partial class BattleSkillVfx
    {
        // The light of a win: warm gold, a white-gold for the cores and a pale sky for the boss's
        // second shell, cooler than the gold so the two read apart over the stage.
        private static readonly Color TriumphGold = new(1f, 0.8f, 0.36f);
        private static readonly Color TriumphPale = new(1f, 0.95f, 0.78f);
        private static readonly Color TriumphSky = new(0.62f, 0.86f, 1f);

        /// <summary>How long the boss's win holds the stage dark before the band (s).</summary>
        public const float TriumphHush = 0.4f;

        /// <summary>
        /// Plays the win behind <paramref name="band"/> (the "勝利！" band, in any canvas) and on
        /// <paramref name="allies"/>, the allies still standing; bigger for a <paramref name="boss"/>.
        /// Starts as the band shows. It runs on its own and is cut short by <see cref="Clear"/>.
        /// </summary>
        public void Triumph(IReadOnlyList<RectTransform> allies, RectTransform band, bool boss)
        {
            if (FrontLayer == null || !isActiveAndEnabled)
                return;
            StartCoroutine(TriumphBeats(allies, band, boss));
        }

        private IEnumerator TriumphBeats(
            IReadOnlyList<RectTransform> allies,
            RectTransform band,
            bool boss
        )
        {
            var center = band != null ? LayerPointOf(band) : Vector2.up * 120f;
            if (boss)
            {
                // The dark lifts at once in a soft golden flash: the only flash of the win.
                DimTo(0f, 0.12f);
                ScreenFlash(TriumphPale, 0.32f);
                Shake(0.12f, Vector2.zero);
            }

            // The party's light: the pools, the light on them and the sparkles of the old cheer,
            // with thin lines rising off each one.
            Cheer(allies);
            if (allies != null)
                foreach (var ally in allies)
                    RiseOff(ally, boss ? 18 : 12, boss ? 1.1f : 0.8f);

            // The first shell, behind the band's middle.
            Shell(center, TriumphGold, boss ? 1.25f : 1f, boss ? 48 : 36);
            if (!boss)
                yield break;

            // The boss: lines rise off the whole floor and sparkles drift down over the field,
            // while two more shells burst in the sky over the field, away from the party, the
            // first in the cooler light.
            RiseFromFloor(allies, 36, 1.4f);
            Shower(center, 34, 2.2f);
            yield return Wait(0.32f);
            Shell(center + new Vector2(-80f, 320f), TriumphSky, 0.8f, 28);
            yield return Wait(0.26f);
            Shell(center + new Vector2(560f, 200f), TriumphGold, 0.85f, 30);
        }

        /// <summary>
        /// One shell of thin lines of light burst at <paramref name="at"/>, <paramref name="scale"/>
        /// times the normal, of <paramref name="count"/> lines, in four layers: a white core that
        /// flares and shrinks at once (core); lines that shoot out, slow and bend down, white at the
        /// head and coloured behind (body); twinkles flaring where they die out (afterimage); and the
        /// soft glow of the light they shed, longest (haze).
        /// </summary>
        private void Shell(Vector2 at, Color color, float scale, int count)
        {
            // Haze: wide and faint, so the band and the field under it are lit, not covered.
            Glow(
                frontLight,
                at,
                color,
                900f * scale * scale,
                1.2f,
                Mathf.Min(0.32f, 0.28f * scale * scale * scale)
            );
            // Core: the burst itself, a white star that flares and is eaten away.
            Star(at, color, 240f * scale, 0.32f);
            // Body: the lines, evenly round with a little scatter, so the shell reads as a flower.
            for (int i = 0; i < count; i++)
            {
                float angle = 360f * i / count + Random.Range(-5f, 5f);
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.Lerp(color, Color.white, 0.7f),
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(1100f, 1700f) * scale,
                        Direction = angle,
                        Spread = 4f,
                        Life = new Vector2(0.8f, 1.1f),
                        Size = new Vector2(5f, 8f),
                        Shrink = 0.5f,
                        Stretch = 0.11f,
                        Drag = 2.4f,
                        Gravity = 300f,
                        Area = 12f,
                    },
                    1
                );
            }
            // Shorter white lines inside, so the flower has depth.
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(450f, 800f) * scale,
                    Spread = 360f,
                    Life = new Vector2(0.35f, 0.55f),
                    Size = new Vector2(4f, 6f),
                    Shrink = 0.5f,
                    Stretch = 0.08f,
                    Drag = 3f,
                    Gravity = 200f,
                    Area = 8f,
                },
                count / 2
            );
            // Afterimage: twinkles where the lines thin out, flaring one after another.
            for (int i = 0; i < count * 2 / 3; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float reach = Random.Range(260f, 460f) * scale;
                var point =
                    at
                    + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.85f) * reach
                    + Vector2.down * 40f;
                Twinkle(
                    point,
                    Color.Lerp(color, Color.white, 0.3f),
                    Random.Range(34f, 60f) * scale,
                    Random.Range(0.35f, 0.55f),
                    Random.Range(0.35f, 0.9f),
                    Random.value < 0.5f ? 1f : -1f
                );
            }
        }

        // Thin lines of light rising off an ally, faster and faster, over `seconds`.
        private void RiseOff(RectTransform ally, int count, float seconds)
        {
            if (ally == null)
                return;
            var (min, max) = Bounds(FrontLayer, ally);
            float width = Mathf.Max(80f, max.x - min.x);
            float height = Mathf.Max(120f, max.y - min.y);
            for (int i = 0; i < count; i++)
                RiseLine(
                    i % 3 == 0 ? frontLight : backLight,
                    OnBody(min, width, height, 0.6f),
                    new Color(TriumphPale.r, TriumphPale.g, TriumphPale.b, 0.9f),
                    TriumphGold,
                    new Vector2(3f, 5f),
                    new Vector2(380f, 700f),
                    new Vector2(0.25f, 0.4f),
                    seconds * Random.value,
                    CoreBright
                );
        }

        // Thin lines of light rising off the floor across the field, behind the actors.
        private void RiseFromFloor(IReadOnlyList<RectTransform> allies, int count, float seconds)
        {
            float floor =
                allies != null && allies.Count > 0 ? Feet(FrontLayer, allies[0]).y : -200f;
            float half = Stage != null ? Stage.rect.width * 0.5f : 900f;
            for (int i = 0; i < count; i++)
                RiseLine(
                    backLight,
                    new Vector2(
                        Random.Range(-half * 0.9f, half * 0.9f),
                        floor + Random.Range(-60f, 120f)
                    ),
                    new Color(TriumphGold.r, TriumphGold.g, TriumphGold.b, 0.8f),
                    TriumphGold,
                    new Vector2(3f, 5f),
                    new Vector2(300f, 600f),
                    new Vector2(0.3f, 0.5f),
                    seconds * Mathf.Sqrt(Random.value),
                    CoreBright
                );
        }

        // Sparkles drifting down over the field from above the band, turning as they fall.
        private void Shower(Vector2 center, int count, float seconds)
        {
            float half = Stage != null ? Stage.rect.width * 0.5f : 900f;
            Spray(
                frontLight,
                new Vector2(0f, center.y + 260f),
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(TriumphGold.r, TriumphGold.g, TriumphGold.b, 0f),
                    Speed = new Vector2(30f, 90f),
                    Direction = -90f,
                    Spread = 40f,
                    Life = new Vector2(1.2f, 1.9f),
                    Size = new Vector2(12f, 26f),
                    Shrink = 0.3f,
                    Gravity = 40f,
                    Drag = 0.6f,
                    Spin = 120f,
                    Area = half * 0.85f,
                    Delay = new Vector2(0.2f, seconds * 0.6f),
                },
                count
            );
        }

        /// <summary>The middle of a piece of UI in any canvas, in the effect layers' space.</summary>
        private Vector2 LayerPointOf(RectTransform ui)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(
                CameraOf(ui),
                ui.TransformPoint(ui.rect.center)
            );
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                FrontLayer,
                screen,
                CameraOf(FrontLayer),
                out var local
            );
            return local;
        }

        private static Camera CameraOf(Transform part)
        {
            var canvas = part.GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }
}
