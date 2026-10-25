using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// A unique skill's evolution (doc/features/skill-evolution.md#戦闘中の覚醒), before the skill's
    /// effect. The omen (<see cref="EvolutionOmen"/>): the stage darkens, the card just played rises
    /// from its user onto an upright circle of runes, a circle turns under the user, light gathers
    /// into the card and cracks it, and a star glints on its corner; then it hovers, shivering.
    /// To pick the next stage (<see cref="EvolutionPresent"/>), it moves up to the middle of the
    /// top, smaller, its circle and light with it, so the stages it can become fit below. The burst (<see cref="EvolutionBurst"/>): the card draws in,
    /// shatters like glass in a flash with a hit stop, and the evolved card springs out of the
    /// shards in the light of its new stage, then dives into its user, whose skill plays next.
    /// A step to a branch's last stage turns a second circle, cracks twice and bursts twice.
    /// </summary>
    public sealed partial class BattleSkillVfx
    {
        /// <summary>How much larger than in the hand the evolving card hovers (as a card picked up is).</summary>
        public const float EvolutionCardScale = 1.6f;

        // The omen's length (s), until the card hovers cracked; a step to a last stage takes longer.
        private const float OmenSeconds = 1.05f;
        private const float DeepOmenSeconds = 1.2f;

        // The card rises from its user over this long (s).
        private const float RiseSeconds = 0.5f;

        // Room kept between the hovering card and the top of the screen (px).
        private const float TopClearance = 150f;

        // While the next stage is picked, the card hovers at the top this large, this far down.
        private const float PresentScale = 0.6f;
        private const float PresentClearance = 70f;

        // How dark the stage goes for an evolution, a little darker than for a spell.
        private const float EvolutionDim = 0.62f;

        // Shapes and lights the omen holds while the next stage is picked, all let go at the burst.
        private const float Held = 600f;

        private static readonly Color Evolution = new(1f, 0.84f, 0.42f);

        private RectTransform evolutionHalo;
        private RectTransform evolutionCard;
        private RectTransform evolving;
        private RectTransform evolvingCaster;
        private CanvasGroup evolvingGroup;
        private Vector2 evolvingRest;
        private float evolvingScale = EvolutionCardScale;
        private float evolvingShiver;

        // The jolt of the last snap of the cracks (px), dying away in a few frames.
        private Vector2 evolvingKick;
        private LightSource haloLight;
        private Coroutine hover;
        private readonly List<Shot> omenShots = new();
        private readonly List<LightSource> omenLights = new();

        /// <summary>
        /// The layer the evolving card is put on: in front of the actors, behind the light drawn
        /// over it (cracks, shards) and in front of its halo.
        /// </summary>
        public RectTransform EvolutionLayer => evolutionCard;

        /// <summary>True from the omen until the evolved card has gone into its user.</summary>
        public bool Evolving => evolving != null;

        // Between the smoke and the light in front of the actors: the halo, then the card.
        private void MakeEvolutionLayers()
        {
            evolutionHalo = SubLayer(FrontLayer, "EvolutionHalo");
            evolutionCard = SubLayer(FrontLayer, "EvolutionCard");
            if (frontLight == null)
                return;
            evolutionHalo.SetSiblingIndex(frontLight.GetSiblingIndex());
            evolutionCard.SetSiblingIndex(frontLight.GetSiblingIndex());
        }

        /// <summary>
        /// The omen of an evolution: <paramref name="card"/> (on <see cref="EvolutionLayer"/>,
        /// showing the stage played) rises from <paramref name="caster"/> and is cracked by the
        /// light it gathers. Ends with the card hovering, which goes on until
        /// <see cref="EvolutionBurst"/>. <paramref name="deep"/> is a step to a branch's last stage.
        /// </summary>
        public IEnumerator EvolutionOmen(RectTransform card, RectTransform caster, bool deep)
        {
            if (card == null || caster == null || FrontLayer == null)
                yield break;
            ClearEvolution();
            evolving = card;
            evolvingCaster = caster;
            if (!card.TryGetComponent(out evolvingGroup))
                evolvingGroup = card.gameObject.AddComponent<CanvasGroup>();
            evolvingGroup.blocksRaycasts = false;
            evolvingGroup.interactable = false;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);

            var from = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            // Out toward the enemies, clear of the user, wholly on screen and below the top of it
            // (the turn order in battle): Inside keeps 35% of its size from the edge.
            var rest = Inside(
                new Vector2(Mathf.Lerp(from.x, 0f, 0.55f), from.y + 10f),
                (CardHeight * 0.5f + TopClearance) / 0.35f
            );
            evolvingRest = rest;
            evolvingScale = EvolutionCardScale;
            Place(card, from, 0.35f, -12f, 0f);
            card.gameObject.SetActive(true);

            if (dimTarget < EvolutionDim)
                DimTo(EvolutionDim, 0.25f);
            // Under the user: a turning circle and a pool of light, lighting them from below.
            omenShots.Add(HeldCircle(backLight, feet, Evolution, 380f, floor: true, spin: -50f));
            omenShots.Add(HeldGlow(backLight, feet, Evolution, 360f, 0.7f, floor: true));
            omenLights.Add(HeldLight(feet + Vector2.down * 80f, Evolution, 0.8f, 340f));

            float seconds = deep ? DeepOmenSeconds : OmenSeconds;
            bool halo = false;
            bool gathered = false;
            int cracks = 0;
            bool glinted = false;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / RiseSeconds);
                evolvingShiver = Mathf.Clamp01((t - 0.6f) / 0.4f);
                var at = Vector2.LerpUnclamped(from, rest, EaseOut(k));
                Place(
                    card,
                    at + Bob(t) * k + Shiver(evolvingShiver) + Kicked(),
                    Mathf.LerpUnclamped(0.35f, EvolutionCardScale, BackOut(k)),
                    Mathf.Lerp(-12f, 0f, EaseOut(k)),
                    Mathf.Clamp01(t / 0.12f)
                );
                if (!gathered && t >= 0.12f)
                {
                    gathered = true;
                    Gather(rest, Evolution, deep ? 36 : 26, 640f, seconds - 0.2f);
                }
                if (!halo && t >= 0.22f)
                {
                    // Behind the card: an upright circle of runes turning, a second the other way
                    // for a last step, and a soft glow.
                    halo = true;
                    omenShots.Add(
                        HeldGlow(
                            evolutionHalo,
                            rest,
                            Evolution,
                            640f,
                            0.5f,
                            floor: false,
                            follow: true
                        )
                    );
                    omenShots.Add(
                        HeldCircle(
                            evolutionHalo,
                            rest,
                            Evolution,
                            620f,
                            floor: false,
                            spin: 36f,
                            follow: true
                        )
                    );
                    if (deep)
                        omenShots.Add(
                            HeldCircle(
                                evolutionHalo,
                                rest,
                                Color.Lerp(Evolution, Color.white, 0.5f),
                                880f,
                                floor: false,
                                spin: -22f,
                                follow: true
                            )
                        );
                    haloLight = HeldLight(rest, Evolution, 0.7f, 560f);
                    omenLights.Add(haloLight);
                }
                if ((cracks == 0 && t >= 0.55f) || (deep && cracks == 1 && t >= 0.8f))
                {
                    // Light breaks out of the card from its middle, a second time for a last step.
                    omenShots.Add(HeldCrack(rest, cracks == 0 ? 0f : 34f, cracks == 0 ? 1f : 1.2f));
                    cracks++;
                }
                if (!glinted && t >= seconds - 0.22f)
                {
                    // The glint of an idea on the card's upper corner.
                    glinted = true;
                    Star(
                        rest + new Vector2(CardWidth * 0.36f, CardHeight * 0.4f),
                        Color.white,
                        deep ? 220f : 170f,
                        0.32f
                    );
                }
                yield return null;
            }
            hover = StartCoroutine(Hover(seconds));
        }

        /// <summary>
        /// Moves the hovering card up to the middle of the top, smaller, with its circle and
        /// light, so the stages it can become are picked below it; it goes on hovering there.
        /// </summary>
        public IEnumerator EvolutionPresent()
        {
            var card = evolving;
            if (card == null || FrontLayer == null)
                yield break;
            if (hover != null)
                StopCoroutine(hover);
            var from = evolvingRest;
            float fromScale = evolvingScale;
            var to = new Vector2(
                0f,
                FrontLayer.rect.yMax - PresentClearance - CardHeightAt(PresentScale) * 0.5f
            );
            const float seconds = 0.35f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = EaseOut(t / seconds);
                evolvingRest = Vector2.LerpUnclamped(from, to, k);
                evolvingScale = Mathf.Lerp(fromScale, PresentScale, k);
                if (haloLight != null)
                    haloLight.At = evolvingRest;
                Place(card, evolvingRest + Shiver(evolvingShiver), evolvingScale, 0f, 1f);
                yield return null;
            }
            evolvingRest = to;
            evolvingScale = PresentScale;
            if (haloLight != null)
                haloLight.At = to;
            hover = StartCoroutine(Hover(0f));
        }

        /// <summary>The point on the screen just under the hovering card, where the branches to the stages start.</summary>
        public Vector2 EvolvingScreenPoint()
        {
            if (FrontLayer == null)
                return Vector2.zero;
            var canvas = FrontLayer.GetComponentInParent<Canvas>();
            var eye =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            var under = evolvingRest + Vector2.down * (CardHeightAt(evolvingScale) * 0.5f);
            return RectTransformUtility.WorldToScreenPoint(eye, FrontLayer.TransformPoint(under));
        }

        /// <summary>
        /// The burst of an evolution after <see cref="EvolutionOmen"/>: the hovering card shatters
        /// and <paramref name="swap"/> is called in the flash to show the evolved stage on it,
        /// which then flies into its user. <paramref name="color"/> is the new stage's light.
        /// Without an omen, only <paramref name="swap"/> is done.
        /// </summary>
        public IEnumerator EvolutionBurst(Color color, bool deep, Action swap)
        {
            var card = evolving;
            if (card == null)
            {
                swap?.Invoke();
                yield break;
            }
            if (hover != null)
                StopCoroutine(hover);
            hover = null;
            var caster = evolvingCaster;
            var at = evolvingRest;

            // Drawn in, as if holding its breath.
            const float squeeze = 0.08f;
            for (float t = 0f; t < squeeze; t += Time.unscaledDeltaTime)
            {
                float k = t / squeeze;
                Place(card, at + Shiver(1f), evolvingScale * (1f - 0.1f * EaseOut(k)), 0f, 1f);
                yield return null;
            }

            swap?.Invoke();
            Shatter(at, color, deep);
            if (caster != null)
            {
                var feet = Feet(FrontLayer, caster);
                Ring(backLight, feet, color, 560f, 0.5f, floor: true);
                Pool(feet, color, 380f, 0.1f, 0.5f);
            }

            // Only the shards and the flash for a moment, then the evolved card springs out of
            // them a size larger and settles, a little tilted.
            const float broken = 0.07f;
            float shown = (deep ? 0.7f : 0.55f) + broken;
            bool second = !deep;
            for (float t = 0f; t < shown; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01((t - broken) / 0.26f);
                float scale = evolvingScale * Mathf.LerpUnclamped(0.55f, 1.08f, BackOut(k));
                float tilt = Mathf.Sin(k * Mathf.PI) * 4f;
                Place(card, at + Bob(t + 1f), scale, tilt, t < broken ? 0f : Mathf.Min(1f, k * 4f));
                if (!second && t >= 0.16f)
                {
                    second = true;
                    Aftershock(at, color);
                }
                yield return null;
            }

            yield return Dive(card, caster, at, color, 1.08f);
        }

        /// <summary>
        /// The card left as it is after <see cref="EvolutionOmen"/> (doc/features/skill-awakening.md,
        /// "覚醒しない"): no burst; the circles and the light around it fade, its cracks close as it
        /// settles a little smaller, and it goes back into its user in a soft light.
        /// </summary>
        public IEnumerator EvolutionSettle()
        {
            var card = evolving;
            if (card == null)
                yield break;
            if (hover != null)
                StopCoroutine(hover);
            hover = null;
            var caster = evolvingCaster;
            var at = evolvingRest;
            foreach (var shot in omenShots)
                Release(shot, 0.35f);
            omenShots.Clear();
            foreach (var light in omenLights)
                light.Hold = light.Age;
            omenLights.Clear();
            haloLight = null;
            DimTo(0f, 0.4f);

            // It calms: the shiver stops and it sinks a little, as if the light had gone out of it.
            const float settle = 0.3f;
            for (float t = 0f; t < settle; t += Time.unscaledDeltaTime)
            {
                float k = EaseOut(t / settle);
                Place(
                    card,
                    at + Vector2.down * (24f * k),
                    evolvingScale * (1f - 0.12f * k),
                    0f,
                    1f
                );
                yield return null;
            }
            yield return Dive(card, caster, at + Vector2.down * 24f, Steel, 0.88f);
        }

        // The card goes into its user, leaving a trail of light, and its user glows.
        private IEnumerator Dive(
            RectTransform card,
            RectTransform caster,
            Vector2 at,
            Color color,
            float from
        )
        {
            const float dive = 0.26f;
            var into = caster != null ? Center(FrontLayer, caster) : at;
            var start = card.anchoredPosition;
            float trail = 0f;
            for (float t = 0f; t < dive; t += Time.unscaledDeltaTime)
            {
                float k = t / dive;
                var p = Vector2.LerpUnclamped(start, into, EaseIn(k));
                Place(
                    card,
                    p,
                    evolvingScale * Mathf.Lerp(from, 0.2f, EaseIn(k)),
                    Mathf.Lerp(0f, -18f, k),
                    1f - Mathf.Clamp01((k - 0.6f) / 0.4f)
                );
                for (trail += Time.unscaledDeltaTime; trail >= 0.016f; trail -= 0.016f)
                    Mote(p, color);
                yield return null;
            }
            if (caster != null)
            {
                Star(into, color, 260f, 0.24f);
                Glow(frontLight, into, color, 380f, 0.45f, 0.6f);
                Illuminate(into, color, 0.9f, 360f, 0.05f, 0.4f, spill: false);
            }
            card.gameObject.SetActive(false);
            evolving = null;
            evolvingCaster = null;
        }

        // The card shatters like glass: a hit stop, a flash, a star and rings of shock, glass
        // shards flung out and falling, sparks, and the light of the new stage all around.
        private void Shatter(Vector2 at, Color color, bool deep)
        {
            foreach (var shot in omenShots)
                Release(shot, shot.Holder.parent == evolutionHalo ? 0.3f : 0.2f);
            omenShots.Clear();
            foreach (var light in omenLights)
                light.Hold = light.Age;
            omenLights.Clear();
            haloLight = null;
            // A card hovering smaller (while picked from) bursts a little smaller.
            float f = Mathf.Lerp(1f, evolvingScale / EvolutionCardScale, 0.6f);
            // The instant it gives: every crack runs to the edge at once and flashes white.
            var flash = Spawn(
                frontLight,
                BattleVfxShape.Crack,
                at,
                new Vector2(CardHeight, CardHeight) * (1.15f * f),
                Color.white
            );
            flash.Image.material = RichMaterialOf(BattleVfxShape.Crack, CardCrackLook);
            Animate(
                flash,
                0.07f,
                (s, k) => Show(s, Color.white, CardSnapAt.Length + k * 0.99f, k, CoreBright * 1.5f)
            );

            HitStop(deep ? 0.12f : 0.09f);
            Shake(deep ? 0.55f : 0.4f, Vector2.up);
            Punch(at, deep ? 0.06f : 0.045f);
            ScreenFlash(Color.Lerp(Color.white, color, 0.3f), deep ? 0.2f : 0.16f);
            HotFlash(at, color, 560f * f);
            Star(at, color, (deep ? 520f : 420f) * f, 0.3f);
            Ring(
                frontLight,
                at,
                Color.Lerp(Evolution, Color.white, 0.4f),
                760f * f,
                0.42f,
                floor: false
            );
            Ring(evolutionHalo, at, color, 1100f * f, 0.6f, floor: false);
            Glow(evolutionHalo, at, color, 780f * f, 0.75f, 0.7f);
            Halo(at, color, (deep ? 760f : 680f) * f);
            Illuminate(at, color, 1f, 640f, 0.12f, 0.6f);

            // Glass: shards in front, flung every way from where the card was and falling, most
            // clear (a half-clear body with a hard bright rim that glints as it turns), some
            // catching the light bright enough to glint through the Bloom; a few large ones behind.
            var glass = new Color(1f, 0.97f, 0.88f, 0.95f);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    Material = RichMaterialOf(BattleVfxShape.Chip),
                    From = glass,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(380f, 1200f),
                    Direction = 90f,
                    Spread = 360f,
                    Life = new Vector2(0.55f, 1.05f),
                    Size = new Vector2(30f, 70f),
                    Aspect = 1.5f,
                    Shrink = 0.6f,
                    Spin = 900f,
                    Gravity = 1100f,
                    Drag = 2.2f,
                    Area = CardWidth * 0.4f,
                },
                deep ? 30 : 22
            );
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    Bright = CoreBright,
                    From = glass,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(380f, 1200f),
                    Direction = 90f,
                    Spread = 360f,
                    Life = new Vector2(0.55f, 1.05f),
                    Size = new Vector2(26f, 64f),
                    Aspect = 1.5f,
                    Shrink = 0.6f,
                    Spin = 900f,
                    Gravity = 1100f,
                    Drag = 2.2f,
                    Area = CardWidth * 0.4f,
                },
                deep ? 26 : 18
            );
            Spray(
                evolutionHalo,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = new Color(Evolution.r, Evolution.g, Evolution.b, 0.8f),
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(250f, 700f),
                    Direction = 90f,
                    Spread = 360f,
                    Life = new Vector2(0.6f, 1f),
                    Size = new Vector2(40f, 72f),
                    Aspect = 1.4f,
                    Shrink = 0.7f,
                    Spin = 360f,
                    Gravity = 900f,
                    Drag = 1.2f,
                    Area = CardWidth * 0.3f,
                },
                deep ? 14 : 10
            );
            Sparks(at, Evolution, 18, 1.2f, Vector2.left);
            Sparks(at, Evolution, 18, 1.2f, Vector2.right);
            RisingMotes(at, color, deep ? 22 : 14);
        }

        // A moment after the burst of a last step, a second, wider one.
        private void Aftershock(Vector2 at, Color color)
        {
            HotFlash(at, Color.white, 440f);
            Ring(frontLight, at, color, 1300f, 0.55f, floor: false);
            Sparks(at, color, 16, 1.4f, Vector2.up);
            RisingMotes(at, color, 12);
            Shake(0.3f, Vector2.down);
            ScreenFlash(color, 0.16f);
        }

        // The circle of runes in the new stage's colour, spun fast out and eaten away.
        private void Halo(Vector2 at, Color color, float size)
        {
            var shot = Spawn(
                evolutionHalo,
                BattleVfxShape.MagicCircle,
                at,
                new Vector2(size, size),
                color
            );
            Animate(
                shot,
                0.7f,
                (s, k) =>
                {
                    s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -k * 220f);
                    float grow = 0.8f + 0.5f * EaseOut(k);
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    Show(s, color, 0.26f, Eaten(k, 0.35f), ErodeBright * 1.3f);
                }
            );
        }

        // While the next stage is picked, the card keeps bobbing and shivering on its circle.
        private IEnumerator Hover(float since)
        {
            for (float t = since; ; t += Time.unscaledDeltaTime)
            {
                if (evolving != null)
                    Place(
                        evolving,
                        evolvingRest + Bob(t) + Shiver(evolvingShiver) + Kicked(),
                        evolvingScale,
                        0f,
                        1f
                    );
                yield return null;
            }
        }

        // Motes of light that drift up from a burst and fade.
        private void RisingMotes(Vector2 at, Color color, int count) =>
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(60f, 280f),
                    Direction = 90f,
                    Spread = 360f,
                    Life = new Vector2(0.6f, 1.2f),
                    Size = new Vector2(18f, 34f),
                    Gravity = -60f,
                    Drag = 1.5f,
                    Area = CardWidth * 0.5f,
                },
                count
            );

        // One mote of the evolved card's trail.
        private void Mote(Vector2 at, Color color)
        {
            var particle = Take(frontLight, BattleVfxShape.Sparkle);
            particle.Position = at + UnityEngine.Random.insideUnitCircle * 30f;
            particle.Velocity = UnityEngine.Random.insideUnitCircle * 60f;
            particle.Life = UnityEngine.Random.Range(0.25f, 0.4f);
            float size = UnityEngine.Random.Range(18f, 30f);
            particle.Size = new Vector2(size, size);
            particle.EndScale = 0.3f;
            particle.From = Color.white;
            particle.To = new Color(color.r, color.g, color.b, 0f);
            particle.Spin = 0f;
            particle.Gravity = 0f;
            particle.Drag = 2f;
            particle.Stretch = 0f;
            particle.Apply();
        }

        // A magic circle held until let go: drawn in around it, then turning (flattened onto the floor when floor).
        private Shot HeldCircle(
            RectTransform layer,
            Vector2 at,
            Color color,
            float size,
            bool floor,
            float spin,
            bool follow = false
        )
        {
            var shot = Spawn(layer, BattleVfxShape.MagicCircle, at, new Vector2(size, size), color);
            Animate(
                shot,
                Held,
                (s, k) =>
                {
                    float age = k * Held;
                    s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, age * spin);
                    float grow = 0.4f + 0.6f * EaseOut(Mathf.Min(1f, age / 0.3f));
                    if (follow)
                    {
                        s.Holder.anchoredPosition = evolvingRest;
                        grow *= evolvingScale / EvolutionCardScale;
                    }
                    s.Holder.localScale = new Vector3(grow, grow * (floor ? FloorTilt : 1f), 1f);
                    // The circle's lines are drawn in by a quarter of its progress.
                    Show(s, color, Mathf.Min(0.26f, age / 0.35f * 0.26f), 0f, ErodeBright * 1.2f);
                }
            );
            return shot;
        }

        // A soft glow held until let go, breathing a little.
        private Shot HeldGlow(
            RectTransform layer,
            Vector2 at,
            Color color,
            float size,
            float peak,
            bool floor,
            bool follow = false
        )
        {
            var shot = Spawn(layer, BattleVfxShape.Glow, at, new Vector2(size, size), color);
            if (floor)
                shot.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            Animate(
                shot,
                Held,
                (s, k) =>
                {
                    float age = k * Held;
                    if (follow)
                    {
                        s.Holder.anchoredPosition = evolvingRest;
                        float scale = evolvingScale / EvolutionCardScale;
                        s.Holder.localScale = new Vector3(scale, scale, 1f);
                    }
                    float alpha =
                        peak * Mathf.Min(1f, age / 0.25f) * (0.85f + 0.15f * Mathf.Sin(age * 5f));
                    Show(s, new Color(color.r, color.g, color.b, alpha), 0f, 0f);
                }
            );
            return shot;
        }

        // When the cracks snap a step further over the card (s after they start), as glass that
        // is stressed cracks: in sudden snaps (ぴき), each reaching at once a step further out,
        // nothing growing between them (doc/features/skill-evolution.md#覚醒のエフェクト).
        private static readonly float[] CardSnapAt = { 0f, 0.11f, 0.2f, 0.3f };

        // How long the cracks a snap has just broken stay white (s).
        private const float CardSnapGlint = 0.09f;

        // The crack shader's look for light leaking through thin cracks in a card (snaps in k).
        private const float CardCrackLook = 3f;

        // Cracks of light over the card from its middle, held glowing until let go: thin lines
        // that snap outward step by step (CardSnapAt), the stretch each snap breaks flashing
        // white; at each snap the card jolts a dot, a chip of light is flicked off and the light
        // leaking through it pulses on those near.
        private Shot HeldCrack(Vector2 at, float turn, float size)
        {
            var shot = Spawn(
                frontLight,
                BattleVfxShape.Crack,
                at,
                new Vector2(CardHeight, CardHeight) * size,
                Evolution
            );
            shot.Image.material = RichMaterialOf(BattleVfxShape.Crack, CardCrackLook);
            shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, turn);
            int snapped = 0;
            Animate(
                shot,
                Held,
                (s, k) =>
                {
                    float age = k * Held;
                    int made = 0;
                    while (made < CardSnapAt.Length && age >= CardSnapAt[made])
                        made++;
                    float since = made > 0 ? age - CardSnapAt[made - 1] : 0f;
                    float snaps = made + Mathf.Min(0.99f, since / CardSnapGlint);
                    var center = evolving != null ? evolving.anchoredPosition : at;
                    if (made > snapped)
                    {
                        snapped = made;
                        Snap(center, made);
                    }
                    float flicker = 0.88f + 0.12f * Mathf.PerlinNoise(s.Seed * 9f, age * 12f);
                    s.Holder.anchoredPosition = center;
                    float scale = evolvingScale / EvolutionCardScale;
                    s.Holder.localScale = new Vector3(scale, scale, 1f);
                    Show(s, Evolution * flicker, snaps, 0f, CoreBright);
                }
            );
            return shot;
        }

        // One snap of the card's cracks: a jolt of a dot, a chip of light flicked off where it
        // broke and the light through the cracks pulsing a little stronger each time.
        private void Snap(Vector2 at, int step)
        {
            float scale = evolvingScale / EvolutionCardScale;
            var off = UnityEngine.Random.insideUnitCircle.normalized * CardWidth * 0.3f * scale;
            evolvingKick = off.normalized * Dot;
            Spray(
                frontLight,
                at + off,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(Evolution.r, Evolution.g, Evolution.b, 0f),
                    Speed = new Vector2(300f, 700f),
                    Direction = Angle(off),
                    Spread = 60f,
                    Life = new Vector2(0.08f, 0.16f),
                    Size = new Vector2(4f, 7f),
                    Stretch = 0.05f,
                    Drag = 5f,
                    Gravity = 900f,
                },
                3
            );
            Illuminate(at, Evolution, 0.4f + 0.12f * step, 420f, 0.03f, 0.15f, spill: false);
            Shake(0.02f + 0.01f * step, Vector2.zero);
        }

        // Lets a held shape go: it swells a little and is eaten away.
        private static void Release(Shot shot, float seconds)
        {
            if (!shot.Live)
                return;
            var tint = shot.Image.color;
            var scale = shot.Holder.localScale;
            var turn = shot.Image.rectTransform.localEulerAngles;
            shot.Age = 0f;
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    float grow = 1f + 0.25f * EaseOut(k);
                    s.Holder.localScale = new Vector3(scale.x * grow, scale.y * grow, 1f);
                    s.Image.rectTransform.localEulerAngles = turn + new Vector3(0f, 0f, k * 40f);
                    var faded = new Color(tint.r, tint.g, tint.b, tint.a * (1f - EaseIn(k)));
                    Show(s, faded, 0.26f, k, ErodeBright);
                }
            );
        }

        // A light on those near, held until let go.
        private LightSource HeldLight(Vector2 at, Color color, float strength, float radius)
        {
            var light = new LightSource
            {
                At = at,
                Color = color,
                Strength = strength,
                Radius = radius,
                Hold = Held,
                Fade = 0.4f,
                Seed = UnityEngine.Random.value * 50f,
            };
            lights.Add(light);
            return light;
        }

        private void Place(RectTransform card, Vector2 at, float scale, float tilt, float alpha)
        {
            card.anchoredPosition = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
            card.localScale = new Vector3(scale, scale, 1f);
            card.localEulerAngles = new Vector3(0f, 0f, tilt);
            if (evolvingGroup != null)
                evolvingGroup.alpha = alpha;
        }

        // The jolt of a snap of the cracks, dying away.
        private Vector2 Kicked()
        {
            var kick = evolvingKick;
            evolvingKick = Vector2.MoveTowards(
                evolvingKick,
                Vector2.zero,
                Dot * 25f * Time.unscaledDeltaTime
            );
            return kick;
        }

        // A slow rise and fall while it floats.
        private static Vector2 Bob(float t) => new(0f, Mathf.Sin(t * 2.6f) * 7f);

        // A shiver of up to a dot, changing every few frames.
        private static Vector2 Shiver(float amount)
        {
            if (amount <= 0f)
                return Vector2.zero;
            float step = Mathf.Floor(Time.unscaledTime * 22f);
            return new Vector2(
                    Mathf.PerlinNoise(step * 0.37f, 1.3f) - 0.5f,
                    Mathf.PerlinNoise(2.9f, step * 0.41f) - 0.5f
                ) * (2f * Dot * amount);
        }

        // The size of the hovering card, by the battle card's size (70 by 98 dots of 3 px).
        private static float CardWidth => 210f * EvolutionCardScale;
        private static float CardHeight => CardHeightAt(EvolutionCardScale);

        private static float CardHeightAt(float scale) => 294f * scale;

        // Ends an evolution cut short: the card is hidden and the held shapes and lights let go.
        private void ClearEvolution()
        {
            hover = null;
            if (evolving != null)
                evolving.gameObject.SetActive(false);
            evolving = null;
            evolvingCaster = null;
            evolvingGroup = null;
            evolvingScale = EvolutionCardScale;
            haloLight = null;
            foreach (var shot in omenShots)
            {
                shot.Live = false;
                shot.Animate = null;
                shot.Holder.gameObject.SetActive(false);
            }
            omenShots.Clear();
            foreach (var light in omenLights)
                light.Hold = light.Age;
            omenLights.Clear();
        }
    }
}
