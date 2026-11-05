using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The cards left in hand as the party's turn ends turn to light and are drawn into the
    /// discard pile (doc/features/screens/battle-effects.md#手札が捨て札へ流れる演出), from the
    /// right card on. The hold: the card lifts a little, its edges lit in the colour of its art.
    /// The shrink: it draws in to a ball of light. The flight: the ball flies up and over in an
    /// arc, quickening, into the pile's picture, a thin tail behind it and sparks falling off.
    /// The landing: a ring spreads from the pile, a twinkle flashes on it, it swells a little and
    /// its count goes up by one.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        // The ball of light: its white core and the haze of the art's colour round it (px).
        private const float SweepBall = 64f;
        private const float SweepHaze = 150f;

        // How wide the tail's thin core is (px), its haze of the art's colour (over the core's),
        // and the points it is laid along.
        private const float SweepTailWidth = 46f;
        private const float SweepTailHaze = 2.6f;
        private const int SweepTailPoints = 18;

        // How far the card lifts in the hold (px), and the glow behind it (its size over the card's).
        private const float SweepLift = 16f;
        private const float SweepGlow = 1.5f;

        // A spark falls off the ball this often (s), and lives this long.
        private const float SweepSparkEvery = 0.017f;
        private const float SweepSparkLife = 0.2f;

        // The landing: the ring's spread and time, the twinkle's size and time, and how much the
        // pile swells.
        private const float SweepRing = 150f;
        private const float SweepRingTime = 0.213f;
        private const float SweepTwinkle = 120f;
        private const float SweepTwinkleTime = 0.16f;
        private const float SweepSwell = 0.18f;

        // The colour when the art has none vivid enough: the gold of the light.
        private static readonly Color SweepGold = new(1f, 0.82f, 0.42f);

        // The cards on their way to the discard pile, not yet counted on it.
        private int sweeping;

        // The pile's swell as a card lands, started again by the next.
        private Coroutine pileSwelling;

        /// <summary>The discard pile's count as shown: the cards still flying to it are not on it yet.</summary>
        private int ShownDiscards => Mathf.Max(0, drawPile.DiscardPile.Count - sweeping);

        /// <summary>
        /// Sends the copy of a card that flowed away to the discard pile, after
        /// <paramref name="delay"/> seconds. Nothing shows when the effects are missing.
        /// </summary>
        private void SweepToDiscard(RectTransform card, float delay)
        {
            if (
                Vfx == null
                || DiscardAnchor == null
                || Vfx.MaterialOf(BattleVfxShape.Streak) == null
            )
            {
                Destroy(card.gameObject);
                return;
            }
            sweeping++;
            StartCoroutine(Sweep(card, delay));
        }

        private IEnumerator Sweep(RectTransform card, float delay)
        {
            var layer = (RectTransform)card.parent;
            BattleVfxImage.EnableShapeChannel(layer);
            var face = card.GetComponent<BattleInspectCardView>();
            var art = face != null && face.Art != null ? face.Art.texture : null;
            var tint = CardArtColor.Of(art, SweepGold);
            var light = Color.Lerp(tint, Color.white, 0.35f);
            float seed = Random.value;

            var group = card.GetComponent<CanvasGroup>();
            if (group == null)
                group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Vector2 start = card.localPosition;
            float startAngle = card.localEulerAngles.z;
            var startScale = card.localScale;
            var size = card.rect.size * Mathf.Abs(startScale.y);

            // Behind the card, its glow; over it, its light as it draws in.
            var glow = SweepBoard("Glow", layer, BattleVfxShape.Glow);
            glow.transform.SetSiblingIndex(card.GetSiblingIndex());
            var tailHaze = SweepTrail(layer);
            var trail = SweepTrail(layer);
            var haze = SweepBoard("Haze", layer, BattleVfxShape.Glow);
            var ball = SweepBoard("Ball", layer, BattleVfxShape.Spark);
            var sparks =
                new List<(BattleVfxImage Image, Vector2 Position, Vector2 Velocity, float Born)>();

            var pile = DiscardAnchor.Find("Pile") as RectTransform;
            if (pile == null)
                pile = DiscardAnchor;
            Vector2 end = layer.InverseTransformPoint(pile.TransformPoint(pile.rect.center));
            Vector2 center = layer.InverseTransformPoint(card.TransformPoint(card.rect.center));
            Vector2 from = center + Vector2.up * SweepLift;
            var bend = new Vector2(
                Mathf.Lerp(from.x, end.x, 0.45f),
                Mathf.Max(from.y, end.y) + Settings.SweepArc
            );

            float hold = Settings.SweepHold;
            float shrink = Settings.SweepShrink;
            float flight = Settings.SweepFlight;
            // The ball sets off as the card is two thirds drawn in.
            float leave = delay + hold + shrink * 0.66f;
            float land = leave + flight;
            float tailTime = flight * Settings.SweepTail;
            var path = new List<Vector2>(SweepTailPoints);
            bool landed = false;
            float nextSpark = leave;

            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                // The card: it lifts and its edges light, then it draws in to the ball.
                float lift = Mathf.Clamp01((t - delay) / Mathf.Max(hold, 1e-4f));
                float drawIn = Mathf.Clamp01((t - delay - hold) / shrink);
                if (card != null)
                {
                    float easeLift = 1f - (1f - lift) * (1f - lift);
                    float scale = Mathf.Lerp(1f + 0.04f * easeLift, 0.12f, drawIn * drawIn);
                    card.localPosition = start + Vector2.up * (SweepLift * easeLift);
                    card.localScale = startScale * scale;
                    card.localEulerAngles = new Vector3(
                        0f,
                        0f,
                        Mathf.LerpAngle(startAngle, 0f, drawIn)
                    );
                    group.alpha = 1f - Mathf.SmoothStep(0f, 1f, drawIn);
                    if (drawIn >= 1f)
                    {
                        Destroy(card.gameObject);
                        card = null;
                    }
                }
                var glowRect = glow.rectTransform;
                glowRect.localPosition = center + Vector2.up * (SweepLift * lift);
                glowRect.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    Mathf.LerpAngle(startAngle, 0f, drawIn)
                );
                glowRect.sizeDelta = size * SweepGlow * Mathf.Lerp(1f, 0.2f, drawIn);
                glow.color = Fade(tint, lift * (1f - drawIn));
                glow.SetShape(seed, lift, 0f, 2f);

                // The ball: grows where the card draws in, then flies the arc, quickening.
                float fly = Mathf.Clamp01((t - leave) / flight);
                float head = 0.2f * fly + 0.8f * fly * fly;
                var at = Bezier(from, bend, end, head);
                float grown = Mathf.SmoothStep(0f, 1f, drawIn);
                float gone = landed ? 1f : 0f;
                ball.rectTransform.localPosition = at;
                ball.rectTransform.sizeDelta =
                    Vector2.one * (SweepBall * grown * Mathf.Lerp(1f, 0.7f, fly));
                ball.color = Fade(light, grown * (1f - gone));
                ball.SetShape(seed, t, 0f, 1.8f);
                haze.rectTransform.localPosition = at;
                haze.rectTransform.sizeDelta =
                    Vector2.one * (SweepHaze * grown * Mathf.Lerp(1f, 0.55f, fly));
                haze.color = Fade(tint, 0.55f * grown * (1f - gone));
                haze.SetShape(seed, t, 0f, 1f);

                // The tail: the stretch of the arc the ball passed over a moment ago, drawn on
                // into the pile after it lands.
                float tailFly = Mathf.Clamp01((t - leave - tailTime) / flight);
                float tail = 0.2f * tailFly + 0.8f * tailFly * tailFly;
                path.Clear();
                if (t > leave && tail < head)
                {
                    // Past the ball by a little, where the streak's bright head sits.
                    float reach = Mathf.Min(1f, head + (head - tail) * 0.15f);
                    for (int i = 0; i < SweepTailPoints; i++)
                        path.Add(
                            Bezier(
                                from,
                                bend,
                                end,
                                Mathf.Lerp(tail, reach, i / (SweepTailPoints - 1f))
                            )
                        );
                }
                float tailWidth = SweepTailWidth * Mathf.Lerp(1f, 0.6f, fly);
                trail.SetPath(path, tailWidth);
                trail.color = Fade(light, 1f);
                trail.SetShape(seed, fly, 0f, 1.8f);
                tailHaze.SetPath(path, tailWidth * SweepTailHaze);
                tailHaze.color = Fade(tint, 0.5f);
                tailHaze.SetShape(seed, fly, 0f, 1.2f);

                // Sparks fall off the ball as it flies.
                while (t > leave && !landed && nextSpark <= t)
                {
                    var spark = SweepBoard("Spark", layer, BattleVfxShape.Spark);
                    var heading = Bezier(from, bend, end, Mathf.Min(1f, head + 0.02f)) - at;
                    sparks.Add(
                        (
                            spark,
                            at + Random.insideUnitCircle * 8f,
                            -heading.normalized * Random.Range(30f, 90f)
                                + Random.insideUnitCircle * 50f,
                            nextSpark
                        )
                    );
                    nextSpark += SweepSparkEvery;
                }
                for (int i = sparks.Count - 1; i >= 0; i--)
                {
                    var (image, position, velocity, born) = sparks[i];
                    float k = (t - born) / SweepSparkLife;
                    if (k >= 1f)
                    {
                        Destroy(image.gameObject);
                        sparks.RemoveAt(i);
                        continue;
                    }
                    float age = t - born;
                    image.rectTransform.localPosition =
                        position + velocity * age + Vector2.down * (60f * age * age);
                    image.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(22f, 8f, k);
                    image.color = Fade(Color.Lerp(light, tint, k), 1f - k);
                    image.SetShape(seed + i * 0.13f, t, 0f, 1.5f);
                }

                if (!landed && t >= land)
                {
                    landed = true;
                    sweeping = Mathf.Max(0, sweeping - 1);
                    RefreshDeck();
                    StartCoroutine(SweepLanding(layer, end, tint, light, seed));
                }
                if (landed && tail >= 1f && sparks.Count == 0)
                    break;
                yield return null;
            }

            if (card != null)
                Destroy(card.gameObject);
            Destroy(glow.gameObject);
            Destroy(trail.gameObject);
            Destroy(tailHaze.gameObject);
            Destroy(haze.gameObject);
            Destroy(ball.gameObject);
        }

        // The landing on the pile: a ring spreads, a twinkle flashes and the pile swells.
        private IEnumerator SweepLanding(
            RectTransform layer,
            Vector2 at,
            Color tint,
            Color light,
            float seed
        )
        {
            var ring = SweepBoard("Ring", layer, BattleVfxShape.Ring);
            var twinkle = SweepBoard("Twinkle", layer, BattleVfxShape.Sparkle);
            ring.rectTransform.localPosition = at;
            twinkle.rectTransform.localPosition = at;
            if (pileSwelling != null)
                StopCoroutine(pileSwelling);
            pileSwelling = StartCoroutine(SwellPile());
            float total = Mathf.Max(SweepRingTime, SweepTwinkleTime);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float r = Mathf.Clamp01(t / SweepRingTime);
                ring.rectTransform.sizeDelta =
                    Vector2.one * (SweepRing * (0.3f + 0.7f * (1f - (1f - r) * (1f - r))));
                ring.color = Fade(tint, 1f - r);
                ring.SetShape(seed, r, r, 1.4f);
                float w = Mathf.Clamp01(t / SweepTwinkleTime);
                float swell = w < 0.2f ? w / 0.2f : 1f - (w - 0.2f) / 0.8f;
                twinkle.rectTransform.sizeDelta =
                    Vector2.one * (SweepTwinkle * (0.3f + 0.7f * swell));
                twinkle.rectTransform.localEulerAngles = new Vector3(0f, 0f, 20f * w);
                twinkle.color = Fade(light, swell);
                twinkle.SetShape(seed, t, 0f, 1.6f);
                yield return null;
            }
            Destroy(ring.gameObject);
            Destroy(twinkle.gameObject);
        }

        // The pile swells at once as a card lands and settles back.
        private IEnumerator SwellPile()
        {
            const float settle = 0.147f;
            for (float t = 0f; t < settle; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / settle;
                DiscardAnchor.localScale = Vector3.one * (1f + SweepSwell * k * k);
                yield return null;
            }
            DiscardAnchor.localScale = Vector3.one;
            pileSwelling = null;
        }

        private BattleVfxImage SweepBoard(string name, RectTransform layer, BattleVfxShape shape)
        {
            var part = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            part.SetParent(layer, false);
            part.anchorMin = part.anchorMax = part.pivot = new Vector2(0.5f, 0.5f);
            part.sizeDelta = Vector2.zero;
            var image = part.gameObject.AddComponent<BattleVfxImage>();
            image.material = Vfx.MaterialOf(shape);
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        private BattleVfxTrail SweepTrail(RectTransform layer)
        {
            var part = new GameObject("Tail", typeof(RectTransform)).GetComponent<RectTransform>();
            part.SetParent(layer, false);
            part.anchorMin = part.anchorMax = part.pivot = new Vector2(0.5f, 0.5f);
            part.sizeDelta = Vector2.zero;
            var trail = part.gameObject.AddComponent<BattleVfxTrail>();
            trail.material = Vfx.MaterialOf(BattleVfxShape.Streak);
            trail.raycastTarget = false;
            trail.color = Color.clear;
            return trail;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static Color Fade(Color color, float alpha) =>
            new(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}
