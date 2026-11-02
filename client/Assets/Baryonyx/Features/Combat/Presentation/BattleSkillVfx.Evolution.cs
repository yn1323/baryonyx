using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// A unique skill's evolution (doc/features/skill-evolution.md#戦闘中の覚醒), before the skill's
    /// effect, all in the colour of the card's element. The omen (<see cref="EvolutionOmen"/>): the
    /// stage darkens, the card just played rises from its user onto an upright circle of runes in a
    /// wider ring, a circle turns under the user, thin lines of light rise from below into it,
    /// shards of crystal circle it, and a star glints on its corner; then it hovers, shivering. To pick the next stage (<see cref="EvolutionPresent"/>), it moves up to
    /// the middle of the top, smaller, its circle and light with it, so the stages it can become
    /// fit below. The burst (<see cref="EvolutionBurst"/>): the card comes back down to where it
    /// rose, cracks with light in snaps, draws in, and breaks like glass in a flash with a hit stop: the card itself splits
    /// along its cracks into pieces that fly apart turning, thin rays and crystals flung out; the
    /// evolved card springs out of the pieces large, in rays of its new stage's light among
    /// drifting glass, then dives into its user, whose skill plays next. A step to a branch's last stage turns a third circle, cracks twice and
    /// bursts twice.
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

        // The evolved card is held up clear of the edges by this much before it goes into its user.
        private const float RevealClearance = 60f;

        // The card breaks all over into pieces seeded on a jittered grid of this many columns and
        // rows, drawn on a mask this many texels across; the pieces part along the cracks, held
        // still long enough to be seen broken, and then are flung far apart, turning, over this
        // long (s). Only their broken edges are lit, so it reads as a card cracking, not a flash.
        private const int PieceColumns = 4;
        private const int PieceRows = 6;
        private const int PieceMaskWidth = 160;
        private const float PartSeconds = 0.18f;
        private const float FlySeconds = 1f;

        // The evolved card shows this long after the break, once the pieces have left the middle,
        // in a second burst of its own light.
        private const float BrokenSeconds = 0.42f;

        // How many thin lines of light rise into the card a second, while it cracks and while it
        // hovers waiting for the next stage.
        private const float RiseRate = 46f;

        // How long the card cracks, back at full size once the stage is picked, before it gives:
        // its four snaps and a moment after them.
        private const float CrackingSeconds = 0.45f;
        private const float HoverRiseRate = 14f;
        private const float PresentClearance = 36f;

        // How dark the stage goes for an evolution, a little darker than for a spell.
        private const float EvolutionDim = 0.62f;

        // Shapes and lights the omen holds while the next stage is picked, all let go at the burst.
        private const float Held = 600f;

        private static readonly Color Evolution = new(1f, 0.84f, 0.42f);

        private RectTransform evolutionHalo;
        private RectTransform evolutionCard;
        private RectTransform evolving;
        private RectTransform evolvingCaster;
        private Color evolvingColor = Evolution;
        private Vector2 omenRest;
        private float riseDue;
        private RenderTexture brokenCard;
        private readonly List<GameObject> pieces = new();
        private readonly List<UnityEngine.Object> pieceAssets = new();
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

        /// <summary>
        /// False where the card rises from something that does not stand on the stage (a row of
        /// the trading house's list): no circle, glow or pool of light is laid at its feet.
        /// </summary>
        public bool GroundUnderCaster { get; set; } = true;

        /// <summary>
        /// True where the card rises to the middle of the screen (the trading house's list)
        /// instead of out toward the enemies beside its user.
        /// </summary>
        public bool EvolveAtCenter { get; set; }

        /// <summary>How large (by the hand's size) the evolved card is held up to be read.</summary>
        public float RevealScale { get; set; } = 1.9f;

        /// <summary>How long the evolved card is held up to be read before it goes into its user (s).</summary>
        public float RevealSeconds { get; set; } = 0.85f;

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
        /// light it gathers, in <paramref name="color"/> (the element of the stage played). Ends with
        /// the card hovering, which goes on until <see cref="EvolutionBurst"/>.
        /// <paramref name="deep"/> is a step to a branch's last stage.
        /// </summary>
        public IEnumerator EvolutionOmen(
            RectTransform card,
            RectTransform caster,
            bool deep,
            Color color
        )
        {
            if (card == null || caster == null || FrontLayer == null)
                yield break;
            ClearEvolution();
            evolvingColor = color;
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
            var rest = EvolveAtCenter
                ? new Vector2(0f, 30f)
                : Inside(
                    new Vector2(Mathf.Lerp(from.x, 0f, 0.55f), from.y + 10f),
                    (CardHeight * 0.5f + TopClearance) / 0.35f
                );
            evolvingRest = omenRest = rest;
            riseDue = 0.15f;
            evolvingScale = EvolutionCardScale;
            Place(card, from, 0.35f, -12f, 0f);
            card.gameObject.SetActive(true);

            if (dimTarget < EvolutionDim)
                DimTo(EvolutionDim, 0.25f);
            // Under the user: a turning circle and a pool of light, lighting them from below.
            if (GroundUnderCaster)
            {
                omenShots.Add(HeldCircle(backLight, feet, color, 380f, floor: true, spin: -50f));
                omenShots.Add(HeldGlow(backLight, feet, color, 360f, 0.7f, floor: true));
                omenLights.Add(HeldLight(feet + Vector2.down * 80f, color, 0.8f, 340f));
            }

            float seconds = deep ? DeepOmenSeconds : OmenSeconds;
            bool halo = false;
            bool gathered = false;
            bool glinted = false;
            bool circled = false;
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
                    Gather(rest, color, deep ? 36 : 26, 640f, seconds - 0.2f);
                }
                // Thin lines of light rise from below into the card, more and more of them.
                for (; t >= riseDue; riseDue += 1f / (RiseRate * Mathf.Lerp(0.4f, 1f, k)))
                    RisingLine();
                if (!circled && t >= 0.3f)
                {
                    circled = true;
                    OrbitShards(deep ? 14 : 10);
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
                            color,
                            700f,
                            0.3f,
                            floor: false,
                            follow: true
                        )
                    );
                    omenShots.Add(
                        HeldCircle(
                            evolutionHalo,
                            rest,
                            color,
                            640f,
                            floor: false,
                            spin: 36f,
                            follow: true
                        )
                    );
                    // A wider, fainter ring the other way round it.
                    omenShots.Add(
                        HeldCircle(
                            evolutionHalo,
                            rest,
                            Color.Lerp(color, Color.white, 0.35f) * new Color(1f, 1f, 1f, 0.45f),
                            900f,
                            floor: false,
                            spin: -14f,
                            follow: true
                        )
                    );
                    if (deep)
                        omenShots.Add(
                            HeldCircle(
                                evolutionHalo,
                                rest,
                                Color.Lerp(color, Color.white, 0.5f),
                                1120f,
                                floor: false,
                                spin: 22f,
                                follow: true
                            )
                        );
                    haloLight = HeldLight(rest, color, 0.8f, 600f);
                    omenLights.Add(haloLight);
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
        /// The burst of an evolution after <see cref="EvolutionOmen"/>: the hovering card comes
        /// back down to where it rose and shatters, and <paramref name="swap"/> is called in the
        /// flash to show the evolved stage on it, which springs out large in rays of
        /// <paramref name="color"/> (the new stage's light), is held up for
        /// <see cref="RevealSeconds"/> and then flies into its user.
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

            // Back down from where it was picked, full size, its circle and light with it, to break
            // where it first rose.
            var from = evolvingRest;
            float fromScale = evolvingScale;
            if ((from - omenRest).sqrMagnitude > 1f || !Mathf.Approximately(fromScale, EvolutionCardScale))
            {
                const float back = 0.24f;
                for (float t = 0f; t < back; t += Time.unscaledDeltaTime)
                {
                    float k = EaseOut(t / back);
                    evolvingRest = Vector2.LerpUnclamped(from, omenRest, k);
                    evolvingScale = Mathf.Lerp(fromScale, EvolutionCardScale, k);
                    if (haloLight != null)
                        haloLight.At = evolvingRest;
                    Place(card, evolvingRest + Shiver(1f) + Kicked(), evolvingScale, 0f, 1f);
                    yield return null;
                }
            }
            evolvingRest = omenRest;
            evolvingScale = EvolutionCardScale;
            var at = evolvingRest;

            // Back at full size, it cracks: light breaks out of it from its middle in snaps (a
            // second web over it for a last step), shivering harder, before it gives.
            omenShots.Add(HeldCrack(at));
            bool webbed = !deep;
            float cracking = deep ? CrackingSeconds + 0.12f : CrackingSeconds;
            for (float t = 0f; t < cracking; t += Time.unscaledDeltaTime)
            {
                if (!webbed && t >= 0.15f)
                {
                    webbed = true;
                    omenShots.Add(HeldCrack(at));
                }
                Place(card, at + Shiver(1f) + Kicked(), evolvingScale, 0f, 1f);
                yield return null;
            }

            // Drawn in, as if holding its breath.
            float squeeze = deep ? 0.14f : 0.1f;
            for (float t = 0f; t < squeeze; t += Time.unscaledDeltaTime)
            {
                float k = t / squeeze;
                Place(card, at + Shiver(1.5f), evolvingScale * (1f - 0.1f * EaseOut(k)), 0f, 1f);
                yield return null;
            }

            // The card as it is on the screen this frame breaks into pieces of itself.
            yield return new WaitForEndOfFrame();
            BreakCard(card, at, color);
            Place(card, at, evolvingScale, 0f, 0f);
            swap?.Invoke();
            Shatter(at, color, deep);
            if (caster != null && GroundUnderCaster)
            {
                var feet = Feet(FrontLayer, caster);
                Ring(backLight, feet, color, 560f, 0.5f, floor: true);
                Pool(feet, color, 380f, 0.1f, 0.5f);
            }

            // Only the shards and the flash for a moment; then the evolved card springs out of
            // them, larger than it hovered, rays of its light turning slowly behind it and glass
            // drifting down round it, held there to be read.
            // It is held up where the card first rose (the middle of the screen in the trading house).
            var stage = EvolveAtCenter
                ? omenRest
                : Inside(omenRest, (CardHeightAt(RevealScale) * 0.5f + RevealClearance) / 0.35f);
            const float broken = BrokenSeconds;
            const float spring = 0.42f;
            float shown = broken + spring + RevealSeconds;
            RevealRays(stage, color, shown + 0.15f);
            Glow(evolutionHalo, stage, color, 900f, shown, 0.45f);
            float drift = 0f;
            bool second = !deep;
            bool popped = false;
            for (float t = 0f; t < shown; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01((t - broken) / spring);
                float scale = EvolutionCardScale
                    * Mathf.LerpUnclamped(0.5f, RevealScale / EvolutionCardScale, BackOut(k));
                float tilt = Mathf.Sin(k * Mathf.PI) * 5f;
                var place = Vector2.LerpUnclamped(at, stage, EaseOut(k)) + Bob(t) * k;
                Place(card, place, scale, tilt, t < broken ? 0f : Mathf.Min(1f, (t - broken) * 8f));
                if (!popped && t >= broken)
                {
                    popped = true;
                    RevealBurst(stage, color);
                }
                if (!second && t >= 0.16f)
                {
                    second = true;
                    Aftershock(at, color);
                }
                for (drift += Time.unscaledDeltaTime; drift >= 0.07f; drift -= 0.07f)
                    DriftGlass(stage, color);
                yield return null;
            }

            yield return Dive(card, caster, stage, color, RevealScale / EvolutionCardScale);
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
                Release(shot, 0.16f);
            omenShots.Clear();
            foreach (var light in omenLights)
                light.Hold = light.Age;
            omenLights.Clear();
            haloLight = null;
            // A card hovering smaller (while picked from) bursts a little smaller. It breaks as a
            // pane of glass, all of it at once in its own shape, not from a flash at its middle.
            float f = Mathf.Lerp(1f, evolvingScale / EvolutionCardScale, 0.6f);
            float pane = evolvingScale / EvolutionCardScale;

            HitStop(deep ? 0.12f : 0.09f);
            Shake(deep ? 0.55f : 0.4f, Vector2.up);
            Punch(at, deep ? 0.06f : 0.045f);
            ScreenFlash(Color.Lerp(Color.white, color, 0.3f), deep ? 0.2f : 0.16f);
            Ring(
                frontLight,
                at,
                Color.Lerp(color, Color.white, 0.5f),
                760f * f,
                0.42f,
                floor: false
            );
            // Glass and crystals wait until the card has been seen split, then fly with its pieces
            // from all over it.
            var after = new Vector2(PartSeconds, PartSeconds + 0.04f);
            Ring(evolutionHalo, at, color, 1100f * f, 0.6f, floor: false);
            Glow(evolutionHalo, at, color, 780f * f, 0.6f, 0.25f);
            Illuminate(at, color, 1f, 640f, 0.12f, 0.6f);

            // Glass: shards in front, flung every way from where the card was and falling, most
            // clear (a half-clear body with a hard bright rim that glints as it turns), some
            // catching the light bright enough to glint through the Bloom; a few large ones behind.
            var glass = Color.Lerp(color, new Color(1f, 0.97f, 1f), 0.6f);
            glass.a = 0.95f;
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    Material = RichMaterialOf(BattleVfxShape.Chip),
                    From = glass,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(700f, 2000f),
                    Direction = 90f,
                    Spread = 360f,
                    Life = new Vector2(0.6f, 1.15f),
                    Size = new Vector2(44f, 120f),
                    Aspect = 1.8f,
                    Shrink = 0.6f,
                    Spin = 900f,
                    Gravity = 1100f,
                    Drag = 2.2f,
                    Area = CardWidth * 0.6f * pane,
                    Delay = after,
                },
                deep ? 34 : 26
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
                    Speed = new Vector2(700f, 2000f),
                    Direction = 90f,
                    Spread = 360f,
                    Life = new Vector2(0.6f, 1.15f),
                    Size = new Vector2(36f, 96f),
                    Aspect = 1.8f,
                    Shrink = 0.6f,
                    Spin = 900f,
                    Gravity = 1100f,
                    Drag = 2.2f,
                    Area = CardWidth * 0.6f * pane,
                    Delay = after,
                },
                deep ? 30 : 22
            );
            // Crystals of the new stage's light, large and faceted, flung out behind the glass
            // and a few in front of it.
            var crystals = new Burst
            {
                Shape = BattleVfxShape.Shard,
                Bright = CoreBright,
                From = Color.Lerp(color, Color.white, 0.3f),
                To = new Color(color.r, color.g, color.b, 0f),
                Speed = new Vector2(350f, 1000f),
                Direction = 90f,
                Spread = 360f,
                Life = new Vector2(0.7f, 1.25f),
                Size = new Vector2(90f, 200f),
                Aspect = 1.35f,
                Shrink = 0.7f,
                Spin = 220f,
                Gravity = 420f,
                Drag = 1.6f,
                Area = CardWidth * 0.5f * pane,
                Delay = after,
            };
            Spray(evolutionHalo, at, crystals, deep ? 18 : 14);
            Spray(frontLight, at, crystals, deep ? 10 : 7);
            Sparks(at, color, 20, 1.2f, Vector2.left);
            Sparks(at, color, 20, 1.2f, Vector2.right);
            RisingMotes(at, color, deep ? 22 : 14);
        }

        // A moment after the burst of a last step, a second, wider one.
        private void Aftershock(Vector2 at, Color color)
        {
            Ring(frontLight, at, color, 1300f, 0.55f, floor: false);
            Sparks(at, color, 16, 1.4f, Vector2.up);
            RisingMotes(at, color, 12);
            Shake(0.3f, Vector2.down);
            ScreenFlash(color, 0.16f);
        }

        // While the next stage is picked, the card keeps bobbing and shivering on its circle.
        private IEnumerator Hover(float since)
        {
            float due = 0f;
            for (float t = since; ; t += Time.unscaledDeltaTime)
            {
                for (due += Time.unscaledDeltaTime * HoverRiseRate; due >= 1f; due -= 1f)
                    RisingLine();
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

        // One thin line of light rising from below the screen into the hovering card, behind it.
        private void RisingLine()
        {
            if (evolutionHalo == null || FrontLayer == null)
                return;
            float scale = evolvingScale / EvolutionCardScale;
            var into =
                evolvingRest
                + new Vector2(
                    UnityEngine.Random.Range(-0.35f, 0.35f) * CardWidth,
                    UnityEngine.Random.Range(-0.3f, 0.1f) * CardHeight
                ) * scale;
            var start = new Vector2(
                into.x + UnityEngine.Random.Range(-1f, 1f) * 620f,
                FrontLayer.rect.yMin - 40f
            );
            var particle = Take(evolutionHalo, BattleVfxShape.Streak);
            float life = UnityEngine.Random.Range(0.32f, 0.55f);
            particle.Position = start;
            particle.Velocity = (into - start) / life;
            particle.Life = life;
            particle.Size = new Vector2(UnityEngine.Random.Range(5f, 9f), 0f);
            particle.StartScale = particle.EndScale = 1f;
            particle.From = Color.Lerp(evolvingColor, Color.white, UnityEngine.Random.value * 0.6f);
            particle.To = new Color(evolvingColor.r, evolvingColor.g, evolvingColor.b, 0f);
            particle.Spin = 0f;
            particle.Gravity = 0f;
            particle.Drag = 0f;
            particle.Stretch = 0.2f;
            particle.Bright = CoreBright;
            particle.Apply();
        }

        // Shards of crystal circling the hovering card on a tilted orbit, some in front of it and
        // some behind, turning slowly and glinting, held until it bursts.
        private void OrbitShards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                bool front = i % 2 == 0;
                float start = (i + UnityEngine.Random.value * 0.6f) / count * Mathf.PI * 2f;
                float reach = UnityEngine.Random.Range(0.62f, 0.95f);
                float speed = UnityEngine.Random.Range(0.35f, 0.6f) * (front ? 1f : -1f);
                float length = UnityEngine.Random.Range(80f, 150f);
                float turn = UnityEngine.Random.Range(-40f, 40f);
                var tint = Color.Lerp(evolvingColor, Color.white, 0.25f);
                var shot = Spawn(
                    front ? frontLight : evolutionHalo,
                    BattleVfxShape.Shard,
                    evolvingRest,
                    new Vector2(length / 1.4f, length),
                    tint
                );
                Animate(
                    shot,
                    Held,
                    (s, k) =>
                    {
                        float age = k * Held;
                        float scale = evolvingScale / EvolutionCardScale;
                        float a = start + age * speed;
                        var offset = new Vector2(
                            Mathf.Cos(a) * CardHeight * reach,
                            Mathf.Sin(a) * CardHeight * reach * 0.5f + Mathf.Sin(age * 1.7f + start) * 14f
                        );
                        s.Holder.anchoredPosition = evolvingRest + offset * scale;
                        float grow = scale * EaseOut(Mathf.Min(1f, age / 0.35f));
                        s.Holder.localScale = new Vector3(grow, grow, 1f);
                        s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, turn + age * 30f);
                        float glint = 0.75f + 0.25f * Mathf.Sin(age * 3f + start * 5f);
                        Show(s, tint * new Color(glint, glint, glint, 1f), 0.5f, 0f, CoreBright);
                    }
                );
                omenShots.Add(shot);
            }
        }

        // Thin straight rays of light shot out from the burst every way.
        private void Rays(Vector2 at, Color color, int count, Vector2 delay = default) =>
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(2400f, 4200f),
                    Direction = 90f,
                    Spread = 360f,
                    Life = new Vector2(0.16f, 0.3f),
                    Size = new Vector2(5f, 10f),
                    Stretch = 0.11f,
                    Drag = 4f,
                    Area = 30f,
                    Delay = delay,
                },
                count
            );

        // The second burst, as the evolved card comes out of the pieces: a star, a hot flash and a
        // wide ring of its light, rays and sparks shot out, a jolt, and its light on those near.
        private void RevealBurst(Vector2 at, Color color)
        {
            Star(at, color, 620f, 0.36f);
            HotFlash(at, color, 360f);
            Ring(frontLight, at, Color.Lerp(color, Color.white, 0.4f), 1400f, 0.55f, floor: false);
            Ring(evolutionHalo, at, color, 1900f, 0.75f, floor: false);
            Rays(at, color, 26);
            Sparks(at, color, 16, 1.4f, Vector2.up);
            Sparks(at, color, 12, 1.2f, Vector2.down);
            RisingMotes(at, color, 18);
            Shake(0.3f, Vector2.up);
            Illuminate(at, color, 0.9f, 720f, 0.1f, 0.5f);
        }

        // Long thin rays of the new stage's light turning slowly behind the evolved card: each a
        // line through its middle, drawn in and let go over seconds.
        private void RevealRays(Vector2 at, Color color, float seconds)
        {
            const int count = 9;
            float offset = UnityEngine.Random.Range(0f, 180f);
            for (int i = 0; i < count; i++)
            {
                float angle = offset + i * 180f / count + UnityEngine.Random.Range(-6f, 6f);
                float length = UnityEngine.Random.Range(1300f, 1900f);
                float width = UnityEngine.Random.Range(10f, 18f);
                float phase = UnityEngine.Random.value * 6f;
                var shot = Spawn(
                    evolutionHalo,
                    BattleVfxShape.Shard,
                    at,
                    new Vector2(width, length),
                    color
                );
                Animate(
                    shot,
                    seconds,
                    (s, k) =>
                    {
                        float age = k * seconds;
                        float grow = 0.3f + 0.7f * EaseOut(Mathf.Min(1f, age / 0.25f));
                        s.Holder.localScale = new Vector3(1f, grow, 1f);
                        s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle + age * 7f);
                        float alpha =
                            0.6f
                            * Mathf.Min(1f, age / 0.12f)
                            * Remaining(k, 0.75f)
                            * (0.75f + 0.25f * Mathf.Sin(age * 4f + phase));
                        var tint = Color.Lerp(color, Color.white, 0.45f);
                        Show(s, new Color(tint.r, tint.g, tint.b, alpha), 0.5f, 0f, ErodeBright);
                    }
                );
            }
        }

        // One shard of clear glass drifting down round the evolved card, turning and glinting.
        private void DriftGlass(Vector2 around, Color color)
        {
            bool front = UnityEngine.Random.value < 0.6f;
            var particle = Take(front ? frontLight : evolutionHalo, BattleVfxShape.Chip);
            particle.Image.material = RichMaterialOf(BattleVfxShape.Chip);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var reach = new Vector2(
                UnityEngine.Random.Range(380f, 820f),
                UnityEngine.Random.Range(240f, 460f)
            );
            particle.Position = around + new Vector2(Mathf.Cos(angle) * reach.x, Mathf.Sin(angle) * reach.y);
            particle.Velocity =
                UnityEngine.Random.insideUnitCircle * 50f + Vector2.down * UnityEngine.Random.Range(20f, 70f);
            particle.Life = UnityEngine.Random.Range(1f, 1.6f);
            float size = UnityEngine.Random.Range(36f, 100f);
            particle.Size = new Vector2(size, size * 1.8f);
            particle.StartScale = 1f;
            particle.EndScale = 0.7f;
            particle.From = Color.Lerp(color, new Color(1f, 0.97f, 1f), 0.6f) * new Color(1f, 1f, 1f, 0.9f);
            particle.To = new Color(color.r, color.g, color.b, 0f);
            particle.Angle = UnityEngine.Random.Range(0f, 360f);
            particle.Spin = UnityEngine.Random.Range(-90f, 90f);
            particle.Gravity = 40f;
            particle.Drag = 0.6f;
            particle.Stretch = 0f;
            particle.Bright = 1f;
            particle.Apply();
        }

        // Breaks the card into pieces of itself: what the screen shows of it this frame (called
        // after it is drawn), cut by a Voronoi pattern of seeds in two rings round where the light
        // broke through, each piece a mask over that picture with its broken edges lit.
        private void BreakCard(RectTransform card, Vector2 at, Color color)
        {
            var layer = card.parent as RectTransform;
            if (layer == null)
                return;
            ClearPieces();
            var canvas = card.GetComponentInParent<Canvas>();
            var eye =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Vector2 low = new(float.MaxValue, float.MaxValue);
            Vector2 high = new(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(eye, corner);
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            int width = Screen.width;
            int height = Screen.height;
            if (brokenCard == null || brokenCard.width != width || brokenCard.height != height)
            {
                if (brokenCard != null)
                    brokenCard.Release();
                brokenCard = new RenderTexture(width, height, 0)
                {
                    name = "EvolutionBrokenCard",
                    hideFlags = HideFlags.DontSave,
                };
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(brokenCard);
            var face = new Rect(
                low.x / width,
                low.y / height,
                (high.x - low.x) / width,
                (high.y - low.y) / height
            );
            if (SystemInfo.graphicsUVStartsAtTop)
                face = new Rect(face.x, 1f - face.y, face.width, -face.height);

            var size = Vector2.Scale(card.rect.size, card.localScale);
            int mw = PieceMaskWidth;
            int mh = Mathf.Max(1, Mathf.RoundToInt(mw * size.y / Mathf.Max(1f, size.x)));
            var seeds = PieceSeeds(mw, mh);
            var owner = new int[mw * mh];
            for (int y = 0; y < mh; y++)
                for (int x = 0; x < mw; x++)
                {
                    int best = 0;
                    float near = float.MaxValue;
                    for (int i = 0; i < seeds.Count; i++)
                    {
                        float d = (seeds[i] - new Vector2(x, y)).sqrMagnitude;
                        if (d < near)
                        {
                            near = d;
                            best = i;
                        }
                    }
                    owner[y * mw + x] = best;
                }

            var edge = Color.Lerp(color, Color.white, 0.65f);
            var flights = new List<(RectTransform Rect, CanvasGroup Group, Vector2 From, Vector2 Way, float Speed, float Spin, float Tumble)>();
            for (int i = 0; i < seeds.Count; i++)
            {
                var mask = new Texture2D(mw, mh, TextureFormat.RGBA32, false)
                {
                    name = "EvolutionPieceMask",
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var rim = new Texture2D(mw, mh, TextureFormat.RGBA32, false)
                {
                    name = "EvolutionPieceRim",
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var maskPixels = new Color32[mw * mh];
                var rimPixels = new Color32[mw * mh];
                Vector2 sum = Vector2.zero;
                int count = 0;
                for (int y = 0; y < mh; y++)
                    for (int x = 0; x < mw; x++)
                    {
                        int index = y * mw + x;
                        if (owner[index] != i)
                            continue;
                        maskPixels[index] = new Color32(255, 255, 255, 255);
                        sum += new Vector2(x + 0.5f, y + 0.5f);
                        count++;
                        // Lit where it broke from a neighbour: a thin bright line, fainter a texel in.
                        int far = 3;
                        for (int r = 1; r <= 2 && far == 3; r++)
                            if (OtherNear(owner, mw, mh, x, y, i, r))
                                far = r;
                        if (far < 3)
                            rimPixels[index] = new Color32(255, 255, 255, (byte)(far == 1 ? 220 : 70));
                    }
                if (count == 0)
                {
                    Destroy(mask);
                    Destroy(rim);
                    continue;
                }
                mask.SetPixels32(maskPixels);
                mask.Apply();
                rim.SetPixels32(rimPixels);
                rim.Apply();
                var sprite = Sprite.Create(mask, new Rect(0f, 0f, mw, mh), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags = HideFlags.DontSave;
                pieceAssets.Add(mask);
                pieceAssets.Add(rim);
                pieceAssets.Add(sprite);

                var middle = new Vector2(sum.x / count / mw, sum.y / count / mh);
                var rect = new GameObject("EvolutionPiece", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(layer, false);
                rect.SetAsLastSibling();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = middle;
                rect.sizeDelta = size;
                var from = at + Vector2.Scale(middle - new Vector2(0.5f, 0.5f), size);
                rect.anchoredPosition = from;
                var cut = rect.gameObject.AddComponent<Image>();
                cut.sprite = sprite;
                cut.raycastTarget = false;
                rect.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var group = rect.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                Fill(rect, "Face", brokenCard, face, Color.white);
                Fill(rect, "Rim", rim, new Rect(0f, 0f, 1f, 1f), edge);
                pieces.Add(rect.gameObject);

                var way = ((from - at).normalized + UnityEngine.Random.insideUnitCircle * 0.35f).normalized;
                float reach = Mathf.Clamp01((from - at).magnitude / (size.magnitude * 0.5f));
                flights.Add(
                    (
                        rect,
                        group,
                        from,
                        way,
                        UnityEngine.Random.Range(1000f, 1800f) * (0.6f + reach),
                        UnityEngine.Random.Range(300f, 900f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f),
                        UnityEngine.Random.Range(5f, 11f)
                    )
                );
            }
            StartCoroutine(FlyPieces(flights));
        }

        // Seeds of the pieces on the card's mask: one in each cell of a grid over the whole
        // card, set off at random within it, so it breaks all over like a pane of glass.
        private static List<Vector2> PieceSeeds(int width, int height)
        {
            var seeds = new List<Vector2>();
            float cellX = width / (float)PieceColumns;
            float cellY = height / (float)PieceRows;
            for (int row = 0; row < PieceRows; row++)
                for (int column = 0; column < PieceColumns; column++)
                    seeds.Add(
                        new Vector2(
                            (column + UnityEngine.Random.Range(0.15f, 0.85f)) * cellX,
                            (row + UnityEngine.Random.Range(0.15f, 0.85f)) * cellY
                        )
                    );
            return seeds;
        }

        // Whether a texel within reach of (x, y) belongs to another piece.
        private static bool OtherNear(int[] owner, int width, int height, int x, int y, int self, int reach)
        {
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;
                    if (owner[ny * width + nx] != self)
                        return true;
                }
            return false;
        }

        // A picture filling a piece, under its mask.
        private static void Fill(RectTransform piece, string name, Texture texture, Rect uv, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(piece, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.color = color;
            image.raycastTarget = false;
        }

        // The pieces part a little along the cracks, still, through the hit stop; then fly apart,
        // turning and tumbling over (as glass catches the light), falling and fading.
        private IEnumerator FlyPieces(
            List<(RectTransform Rect, CanvasGroup Group, Vector2 From, Vector2 Way, float Speed, float Spin, float Tumble)> flights
        )
        {
            const float drag = 1.5f;
            const float gravity = 1700f;
            for (float t = 0f; t < PartSeconds + FlySeconds; t += Time.unscaledDeltaTime)
            {
                foreach (var flight in flights)
                {
                    if (flight.Rect == null)
                        continue;
                    if (t < PartSeconds)
                    {
                        flight.Rect.anchoredPosition = flight.From + flight.Way * (9f * EaseOut(t / PartSeconds));
                        continue;
                    }
                    float fly = t - PartSeconds;
                    float travel = (1f - Mathf.Exp(-drag * fly)) / drag;
                    flight.Rect.anchoredPosition =
                        flight.From
                        + flight.Way * (9f + flight.Speed * travel)
                        + Vector2.down * (0.5f * gravity * fly * fly);
                    flight.Rect.localEulerAngles = new Vector3(0f, 0f, flight.Spin * fly);
                    float tumble = Mathf.Cos(flight.Tumble * fly);
                    float shrink = Mathf.Lerp(1f, 0.75f, fly / FlySeconds);
                    flight.Rect.localScale = new Vector3(
                        Mathf.Sign(tumble) * Mathf.Max(0.12f, Mathf.Abs(tumble)) * shrink,
                        shrink,
                        1f
                    );
                    flight.Group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, fly / FlySeconds));
                }
                yield return null;
            }
            ClearPieces();
        }

        // Takes away the pieces of a broken card and what they were drawn with.
        private void ClearPieces()
        {
            foreach (var piece in pieces)
                if (piece != null)
                    Destroy(piece);
            pieces.Clear();
            foreach (var asset in pieceAssets)
                if (asset != null)
                    Destroy(asset);
            pieceAssets.Clear();
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
                    // It comes in whole, faint and a little large, and settles as it brightens.
                    float settle = EaseOut(Mathf.Min(1f, age / 0.45f));
                    float grow = 1.25f - 0.25f * settle;
                    if (follow)
                    {
                        s.Holder.anchoredPosition = evolvingRest;
                        grow *= evolvingScale / EvolutionCardScale;
                    }
                    s.Holder.localScale = new Vector3(grow, grow * (floor ? FloorTilt : 1f), 1f);
                    var tint = new Color(color.r, color.g, color.b, color.a * settle);
                    Show(s, tint, 0.26f, 0f, ErodeBright * 1.2f);
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
        private Shot HeldCrack(Vector2 at)
        {
            var shot = Spawn(
                frontLight,
                BattleVfxShape.Crack,
                at,
                new Vector2(CardHeight, CardHeight),
                evolvingColor
            );
            shot.Image.material = RichMaterialOf(BattleVfxShape.Crack, CardCrackLook);
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
                    Show(
                        s,
                        Color.Lerp(evolvingColor, Color.white, 0.3f) * flicker,
                        snaps,
                        0f,
                        CoreBright
                    );
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
                    To = new Color(evolvingColor.r, evolvingColor.g, evolvingColor.b, 0f),
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
            Illuminate(at, evolvingColor, 0.4f + 0.12f * step, 420f, 0.03f, 0.15f, spill: false);
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
            ClearPieces();
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
