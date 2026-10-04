using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>The look of a skill's effect; each card's is chosen from its element and effect.</summary>
    public enum BattleSkillVfxKind
    {
        Slash,
        Fire,
        Ice,
        Thunder,
        Heal,
        Guard,
    }

    /// <summary>What a landed hit did, so its impact can hit harder.</summary>
    public enum BattleHitWeight
    {
        Normal,
        Weak,
        Defeat,
    }

    /// <summary>
    /// The shapes the skill effects are worked out from, with no picture (the Battle Vfx Shape
    /// shader, one material each, in this order). The solid ones (smoke, scorch, frost, ice
    /// spikes and chips) are blended normally; the rest are added as light.
    /// </summary>
    public enum BattleVfxShape
    {
        /// <summary>A soft round light (glows, pools of light, the hot flash of a blow).</summary>
        Glow,

        /// <summary>A ring of shock spreading out.</summary>
        Ring,

        /// <summary>The flash of a blow: rays around a hot centre.</summary>
        Star,

        /// <summary>A sweeping sword arc.</summary>
        SlashArc,

        /// <summary>The finishing cross cut.</summary>
        SlashCross,

        /// <summary>A fireball flying along +x (its head at 85% of the width).</summary>
        Fireball,

        /// <summary>An ice lance flying along +x (its tip at 93% of the width).</summary>
        IceSpear,

        /// <summary>A bolt of lightning down to its foot (at 9% of the height).</summary>
        Lightning,

        /// <summary>A pillar of light rising from its foot (at 10% of the height).</summary>
        Pillar,

        /// <summary>A dome of light, its base at 16% of the height.</summary>
        Dome,

        /// <summary>A magic circle seen from above.</summary>
        MagicCircle,

        /// <summary>A streak of a spark flying along +y.</summary>
        Streak,

        /// <summary>A round spark.</summary>
        Spark,

        /// <summary>A twinkle of four rays.</summary>
        Sparkle,

        /// <summary>A thin shard of light.</summary>
        Shard,

        /// <summary>A torn lump of flame.</summary>
        Flame,

        /// <summary>A tongue of flame rising from its root at the bottom.</summary>
        FlameTongue,

        /// <summary>A billow of smoke, mist or dust.</summary>
        Smoke,

        /// <summary>A burnt patch on the floor.</summary>
        Scorch,

        /// <summary>Frost on the floor, seen low from the side.</summary>
        Frost,

        /// <summary>A spike of clear ice standing on its root (at 3% of the height).</summary>
        IceSpike,

        /// <summary>A broken chip of ice or dark debris.</summary>
        Chip,

        /// <summary>The cut-in's streaks, read from the UV so the image's uvRect scrolls them.</summary>
        CutInStreaks,

        /// <summary>A beam along +x across the board, drawn in from its left end.</summary>
        Beam,

        /// <summary>An arrow or a needle of light flying along +x (its head at 82% of the width).</summary>
        Arrow,

        /// <summary>Glowing cracks spreading from the middle (laid on the floor, or upright on a body).</summary>
        Crack,

        /// <summary>A whirlwind seen from the side, its foot at the bottom.</summary>
        Tornado,

        /// <summary>A bubble: a barrier, or a bubble of poison.</summary>
        Bubble,

        /// <summary>A leaf, or (white and long) a feather of light, along +y.</summary>
        Leaf,

        /// <summary>Two chevrons sliding up: a power rising (turned over, falling).</summary>
        Chevron,

        /// <summary>A snowflake crystal of six arms.</summary>
        Snowflake,

        /// <summary>An upright crest shaped like a shield.</summary>
        Crest,

        /// <summary>A sight of rings and ticks closing in on its middle.</summary>
        Reticle,

        /// <summary>A square mote of a crumbling body, a third of the board, never turned.</summary>
        Mote,
    }

    /// <summary>
    /// The skill effects of the mock battle, drawn with uGUI among the actors of the battlefield,
    /// which a camera draws with Bloom. Every shape is worked out by a shader with no picture
    /// (<see cref="BattleVfxShape"/>). Each effect is timed in three beats: a wind-up (the stage
    /// darkens to a night blue, a magic circle turns under the user, light gathers on it and lights
    /// it from below for big skills), the climax (the blow lands with a hit stop, a shake, a burst
    /// split behind and in front of the target, sparks, and a light that falls on the characters,
    /// the floor and the background around it), and a fall-off (the shapes are eaten away from
    /// their faint parts, embers rise and smoke drifts behind the light). Smoke is drawn behind the
    /// light on each side of the actors. Big skills open with a cut-in and move the stage in a
    /// little at the blow; a defeat slows time for a moment. A hit stop freezes the battle (the
    /// actors and the steps of the effects), but the bursts, sparks, lights, shake and flash go on
    /// in real time, so the frozen blow lands in full light. Full-screen flashes are kept to fewer
    /// than three a second (WCAG 2.3.1) and only the bigger skills flash the screen;
    /// <see cref="ShakeStrength"/>, <see cref="FlashStrength"/> and <see cref="UseHitStop"/> tone
    /// the screen effects down.
    /// </summary>
    public sealed partial class BattleSkillVfx : MonoBehaviour
    {
        // One dot of the 4x pixel art: the stage shakes in whole dots, so the pixel art never blurs.
        private const float Dot = 4f;

        // The most the stage moves at full trauma (px).
        private const float MaxShake = 28f;

        // Full-screen flashes at most this often (three a second at most, WCAG 2.3.1).
        public const float FlashGap = 0.34f;

        // The brightest a full-screen flash gets, so a big hit never washes the screen out.
        public const float MaxFlash = 0.3f;

        // The longest one hit stop may freeze the battle (s).
        private const float MaxHitStop = 0.2f;

        // Floor effects (magic circles, rings, pools of light) are flattened to lie on the ground.
        public const float FloorTilt = 0.32f;

        // Particles kept at most; past it the oldest is reused.
        private const int MaxParticles = 360;

        // How dark the stage goes behind the actors for a spell, and for the thunder's night sky.
        private const float SpellDim = 0.5f;
        private const float NightDim = 0.66f;

        [Header("層")]
        [Tooltip("当たりで揺らし、寄せる、背景と戦場の親。")]
        public RectTransform Stage;

        [Tooltip("スキルの間、背景だけを暗くする幕。夜の青を残す。キャラより奥に置く。")]
        public Graphic Dim;

        [Tooltip("床の魔法陣・光だまり・奥の煙と炸裂を描く、キャラより奥の層。")]
        public RectTransform BackLayer;

        [Tooltip("火球や手前の炸裂・火花を描く、キャラより手前の層。")]
        public RectTransform FrontLayer;

        [Tooltip("画面全体の閃光（加算）。Bloomを通さない操作UIの層で、操作UIより奥。")]
        public RawImage Flash;

        [Header("カットイン")]
        public RectTransform CutIn;
        public RawImage CutInBand;
        public RawImage CutInStreaks;
        public RawImage CutInActor;
        public TMP_Text CutInName;

        [Header("マテリアル")]
        [Tooltip("キャラの絵の形をした加算の光（キャラへの照り返し）と、画面全体の閃光。")]
        public Material GlowMaterial;

        [Tooltip(
            "形ごとのマテリアル（BattleVfxShapeの順）。形は画像を使わずにシェーダーで計算する。"
        )]
        public Material[] Shapes = Array.Empty<Material>();

        [Header("強さ（画面の揺れと閃光を抑えるときに下げる）")]
        [Range(0f, 1f)]
        public float ShakeStrength = 1f;

        [Range(0f, 1f)]
        public float FlashStrength = 1f;

        [Tooltip(
            "当たった瞬間に戦闘を一瞬止め（ヒットストップ）、撃破で一瞬遅くし、大技で画面を寄せる。"
        )]
        public bool UseHitStop = true;

        [Tooltip("このコスト以上のカードは、使う前にカットインを出す。")]
        [Min(0)]
        public int CutInCost = 5;

        private readonly List<Particle> particles = new();
        private readonly List<Shot> shots = new();
        private readonly List<LightSource> lights = new();
        private readonly List<BattleActorLight> actorLights = new();
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform backSmoke;
        private RectTransform backLight;
        private RectTransform frontSmoke;
        private RectTransform frontLight;
        private float trauma;
        private Vector2 kick;
        private float shakeSeed;
        private float zoom;
        private Vector2 zoomAt;
        private float flashAlpha;
        private Color flashColor = Color.white;
        private float lastFlash = float.NegativeInfinity;
        private float dimAlpha;
        private float dimTarget;
        private float dimSpeed = 4f;
        private bool timeBent;
        private float stopUntil;
        private float slowUntil;
        private float slowScale = 1f;
        private float savedTimeScale = 1f;

        // Where the cut-in's user and name rest; a cut-in cut short is put back here.
        private Vector2 cutInActorHome;
        private Vector2 cutInNameHome;

        /// <summary>Effects and particles now on screen.</summary>
        public int LiveCount
        {
            get
            {
                int count = 0;
                foreach (var particle in particles)
                    if (particle.Live)
                        count++;
                foreach (var shot in shots)
                    if (shot.Live)
                        count++;
                return count;
            }
        }

        /// <summary>True while a hit stop holds the battle still.</summary>
        public bool HitStopping => timeBent && Time.realtimeSinceStartup < stopUntil;

        /// <summary>True while the battle is stopped or slowed for a blow.</summary>
        public bool TimeBent => timeBent;

        /// <summary>How dark the stage is behind the actors now (0 to 1).</summary>
        public float DimAlpha => dimAlpha;

        /// <summary>How much the stage is moved in toward a blow now (0 at rest).</summary>
        public float Zoom => zoom;

        /// <summary>The colour of a kind's light.</summary>
        public static Color ColorOf(BattleSkillVfxKind kind) =>
            kind switch
            {
                BattleSkillVfxKind.Fire => new Color(1f, 0.55f, 0.16f),
                BattleSkillVfxKind.Ice => new Color(0.5f, 0.84f, 1f),
                BattleSkillVfxKind.Thunder => new Color(1f, 0.88f, 0.35f),
                BattleSkillVfxKind.Heal => new Color(0.55f, 1f, 0.45f),
                BattleSkillVfxKind.Guard => new Color(0.42f, 0.68f, 1f),
                _ => new Color(0.72f, 0.9f, 1f),
            };

        /// <summary>The effect a card shows: healing and guarding by their effect, attacks by element.</summary>
        public static BattleSkillVfxKind KindFor(
            BattleInspectElement element,
            BattleInspectCardEffect effect
        ) =>
            effect switch
            {
                BattleInspectCardEffect.Heal => BattleSkillVfxKind.Heal,
                BattleInspectCardEffect.Guard => BattleSkillVfxKind.Guard,
                _ => element switch
                {
                    BattleInspectElement.Fire => BattleSkillVfxKind.Fire,
                    BattleInspectElement.Ice => BattleSkillVfxKind.Ice,
                    BattleInspectElement.Thunder => BattleSkillVfxKind.Thunder,
                    _ => BattleSkillVfxKind.Slash,
                },
            };

        /// <summary>True when a full-screen flash may show now, keeping under three a second.</summary>
        public static bool FlashAllowed(float last, float now) => now - last >= FlashGap;

        /// <summary>
        /// The stage's offset for a shake: noise (-1 to 1 on each axis) scaled by the square of
        /// the trauma, so small knocks barely move and big ones hit hard, plus the kick toward
        /// the blow, in whole dots so the pixel art never blurs.
        /// </summary>
        public static Vector2 ShakeOffset(float trauma, Vector2 noise, Vector2 kick, float max)
        {
            float amount = Mathf.Clamp01(trauma);
            var offset = noise * (max * amount * amount) + kick;
            return ToDots(offset);
        }

        /// <summary>
        /// How much of a light falls on a corner of a character: it fades out to the light's
        /// radius, and a corner facing the light gets more than one facing away, so the side
        /// toward a burst, or the feet over a light from below, is lit most.
        /// </summary>
        public static float LightOnCorner(
            Vector2 corner,
            Vector2 center,
            Vector2 light,
            float radius
        )
        {
            float distance = Vector2.Distance(center, light);
            if (radius <= 0f || distance >= radius)
                return 0f;
            float falloff = 1f - distance / radius;
            var toLight = light - center;
            var outward = corner - center;
            float facing =
                toLight.sqrMagnitude < 1f || outward.sqrMagnitude < 1f
                    ? 0.6f
                    : 0.3f
                        + 0.7f * Mathf.Max(0f, Vector2.Dot(outward.normalized, toLight.normalized));
            return falloff * falloff * facing;
        }

        private void Awake()
        {
            shakeSeed = UnityEngine.Random.value * 100f;
            if (Flash != null)
                Flash.enabled = false;
            if (Dim != null)
            {
                Dim.color = new Color(Dim.color.r, Dim.color.g, Dim.color.b, 0f);
                Dim.enabled = false;
            }
            if (CutIn != null)
                CutIn.gameObject.SetActive(false);
            if (CutInActor != null)
                cutInActorHome = CutInActor.rectTransform.anchoredPosition;
            if (CutInName != null)
                cutInNameHome = CutInName.rectTransform.anchoredPosition;
            // The shapes take their seed and moment from the boards' second UV.
            BattleVfxImage.EnableShapeChannel(BackLayer);
            BattleVfxImage.EnableShapeChannel(FrontLayer);
            // Smoke is drawn behind the light on each side of the actors, so it never greys a
            // bright centre.
            backSmoke = SubLayer(BackLayer, "Smoke");
            backLight = SubLayer(BackLayer, "Light");
            frontSmoke = SubLayer(FrontLayer, "Smoke");
            frontLight = SubLayer(FrontLayer, "Light");
        }

        private static RectTransform SubLayer(RectTransform layer, string name)
        {
            if (layer == null)
                return null;
            var existing = layer.Find(name) as RectTransform;
            if (existing != null)
                return existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(layer, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void OnDisable() => Steady();

        /// <summary>Runs time again and puts the stage back still where it rests.</summary>
        private void Steady()
        {
            RestoreTime();
            trauma = 0f;
            kick = Vector2.zero;
            zoom = 0f;
            if (Stage != null)
            {
                Stage.anchoredPosition = Vector2.zero;
                Stage.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Ends every effect at once, as if none had played: the shapes, particles and lights are
        /// gone, the stage is still and lit again, the flash and the cut-in are hidden and time
        /// runs again. For cutting a skill short to play another (the card skill lab); whoever
        /// plays the skill stops its own coroutine first.
        /// </summary>
        public void Clear()
        {
            // The effects' own side work (a crackle of lightning that outlives a blow).
            StopAllCoroutines();
            foreach (var particle in particles)
            {
                particle.Live = false;
                particle.Rect.gameObject.SetActive(false);
            }
            foreach (var shot in shots)
            {
                shot.Live = false;
                shot.Animate = null;
                shot.Holder.gameObject.SetActive(false);
            }
            lights.Clear();
            foreach (var light in actorLights)
                if (light != null && light.Lit)
                    light.SetCorners(Color.clear, Color.clear, Color.clear, Color.clear);
            Steady();
            flashAlpha = 0f;
            if (Flash != null)
                Flash.enabled = false;
            dimAlpha = dimTarget = 0f;
            if (Dim != null)
            {
                Dim.color = new Color(Dim.color.r, Dim.color.g, Dim.color.b, 0f);
                Dim.enabled = false;
            }
            if (CutIn != null)
                CutIn.gameObject.SetActive(false);
            if (CutInActor != null)
                CutInActor.rectTransform.anchoredPosition = cutInActorHome;
            if (CutInName != null)
                CutInName.rectTransform.anchoredPosition = cutInNameHome;
        }

        private void Update()
        {
            float unscaled = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            StepTime();
            StepShake(unscaled);
            StepFlash(unscaled);
            StepDim(unscaled);
            StepParticles(unscaled);
            StepShots(unscaled);
            StepLights(unscaled);
        }

        // --- Time --------------------------------------------------------------------------------

        /// <summary>
        /// Freezes the battle for a moment so the blow lands (hit stop). Overlapping stops keep
        /// the longest; the time scale comes back when it ends or when this is disabled.
        /// </summary>
        public void HitStop(float seconds)
        {
            if (!UseHitStop || seconds <= 0f || !isActiveAndEnabled)
                return;
            BendTime();
            stopUntil = Mathf.Max(
                stopUntil,
                Time.realtimeSinceStartup + Mathf.Min(seconds, MaxHitStop)
            );
            StepTime();
        }

        /// <summary>
        /// Runs the battle at <paramref name="scale"/> speed for a moment after any hit stop, so a
        /// finishing blow feels heavy (slow motion).
        /// </summary>
        public void SlowMotion(float scale, float seconds)
        {
            if (!UseHitStop || seconds <= 0f || !isActiveAndEnabled)
                return;
            BendTime();
            slowScale = Mathf.Clamp(scale, 0.05f, 1f);
            slowUntil = Mathf.Max(stopUntil, Time.realtimeSinceStartup) + seconds;
            StepTime();
        }

        private void BendTime()
        {
            if (timeBent)
                return;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            timeBent = true;
        }

        private void StepTime()
        {
            if (!timeBent)
                return;
            float now = Time.realtimeSinceStartup;
            if (now < stopUntil)
                Time.timeScale = 0f;
            else if (now < slowUntil)
                Time.timeScale = savedTimeScale * slowScale;
            else
                RestoreTime();
        }

        private void RestoreTime()
        {
            if (!timeBent)
                return;
            timeBent = false;
            stopUntil = slowUntil = 0f;
            Time.timeScale = savedTimeScale;
        }

        // --- Screen ------------------------------------------------------------------------------

        /// <summary>Knocks the stage: <paramref name="amount"/> 0 to 1 of trauma, kicked toward <paramref name="direction"/>.</summary>
        public void Shake(float amount, Vector2 direction)
        {
            if (ShakeStrength <= 0f)
                return;
            trauma = Mathf.Min(1f, trauma + amount * ShakeStrength);
            if (direction != Vector2.zero)
                kick += direction.normalized * (amount * 20f * ShakeStrength);
        }

        /// <summary>
        /// Moves the stage in toward a blow at <paramref name="at"/> (in the layers' space) by
        /// <paramref name="amount"/> of its size, easing back, like a camera pushing in.
        /// </summary>
        public void Punch(Vector2 at, float amount)
        {
            if (!UseHitStop || ShakeStrength <= 0f)
                return;
            zoom = Mathf.Max(zoom, amount * ShakeStrength);
            zoomAt = at;
        }

        private void StepShake(float dt)
        {
            if (Stage == null)
                return;
            trauma = Mathf.Max(0f, trauma - dt * 1.8f);
            kick *= Mathf.Exp(-dt * 16f);
            if (kick.sqrMagnitude < 0.25f)
                kick = Vector2.zero;
            zoom *= Mathf.Exp(-dt * 7f);
            if (zoom < 0.0005f)
                zoom = 0f;
            float time = Time.unscaledTime * 32f;
            var noise = new Vector2(
                Mathf.PerlinNoise(shakeSeed, time) * 2f - 1f,
                Mathf.PerlinNoise(shakeSeed + 17.3f, time) * 2f - 1f
            );
            // Scaling about the middle carries the blow outward; move it back most of the way, so
            // the stage seems to close in on it.
            var offset =
                ShakeOffset(trauma, noise, kick, MaxShake) + ToDots(-zoomAt * (zoom * 0.8f));
            if (Stage.anchoredPosition != offset)
                Stage.anchoredPosition = offset;
            var scale = new Vector3(1f + zoom, 1f + zoom, 1f);
            if (Stage.localScale != scale)
                Stage.localScale = scale;
        }

        /// <summary>
        /// Lights the whole battlefield for a moment. A flash sooner than <see cref="FlashGap"/>
        /// after the last one only glows faintly, so the screen never flashes three times a second.
        /// </summary>
        public void ScreenFlash(Color color, float peak)
        {
            if (Flash == null || FlashStrength <= 0f)
                return;
            float now = Time.unscaledTime;
            if (FlashAllowed(lastFlash, now))
                lastFlash = now;
            else
                peak *= 0.3f;
            flashColor = color;
            flashAlpha = Mathf.Max(flashAlpha, Mathf.Clamp(peak * FlashStrength, 0f, MaxFlash));
        }

        private void StepFlash(float dt)
        {
            if (Flash == null)
                return;
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 3.2f);
            bool shown = flashAlpha > 0.001f;
            if (Flash.enabled != shown)
                Flash.enabled = shown;
            if (shown)
                Flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
        }

        /// <summary>Darkens the stage behind the actors to <paramref name="alpha"/> over <paramref name="seconds"/>.</summary>
        public void DimTo(float alpha, float seconds)
        {
            dimTarget = Mathf.Clamp01(alpha);
            dimSpeed = seconds > 0f ? Mathf.Abs(dimTarget - dimAlpha) / seconds : 100f;
        }

        private void StepDim(float dt)
        {
            if (Dim == null)
                return;
            dimAlpha = Mathf.MoveTowards(dimAlpha, dimTarget, dimSpeed * dt);
            bool shown = dimAlpha > 0.001f;
            if (Dim.enabled != shown)
                Dim.enabled = shown;
            if (shown)
                Dim.color = new Color(Dim.color.r, Dim.color.g, Dim.color.b, dimAlpha);
        }

        // --- Light on the surroundings -----------------------------------------------------------

        /// <summary>
        /// Lets the effects light a character: a copy of its picture is drawn over it in the
        /// light's colour, brighter on the side the light comes from.
        /// </summary>
        public void RegisterActor(RawImage sprite)
        {
            if (sprite == null || LightOf(sprite) != null)
                return;
            var rect = new GameObject(
                "EffectLight",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            rect.SetParent(sprite.rectTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var light = rect.gameObject.AddComponent<BattleActorLight>();
            light.texture = sprite.texture;
            light.uvRect = sprite.uvRect;
            light.material = GlowMaterial;
            light.raycastTarget = false;
            light.SetCorners(Color.clear, Color.clear, Color.clear, Color.clear);
            actorLights.Add(light);
        }

        /// <summary>The light drawn over a registered character's picture, or null.</summary>
        public BattleActorLight LightOf(RawImage sprite)
        {
            foreach (var light in actorLights)
                if (light != null && light.transform.parent == sprite.transform)
                    return light;
            return null;
        }

        /// <summary>
        /// Lights the surroundings from <paramref name="at"/> (in the layers' space): the characters
        /// within <paramref name="radius"/>, and a soft glow on the background behind them, so the
        /// light reaches what is near. It holds for <paramref name="hold"/> and fades over
        /// <paramref name="fade"/> seconds.
        /// </summary>
        public void Illuminate(
            Vector2 at,
            Color color,
            float strength,
            float radius,
            float hold,
            float fade,
            bool spill = true,
            float flicker = 0f
        )
        {
            lights.Add(
                new LightSource
                {
                    At = at,
                    Color = color,
                    Strength = strength,
                    Radius = radius,
                    Hold = hold,
                    Fade = Mathf.Max(0.01f, fade),
                    Flicker = Mathf.Clamp01(flicker),
                    Seed = UnityEngine.Random.value * 50f,
                }
            );
            if (!spill || backLight == null)
                return;
            // The background around the light comes back out of the dark.
            var glow = Spawn(
                backLight,
                BattleVfxShape.Glow,
                at,
                Vector2.one * radius * 1.6f,
                color
            );
            float peak = 0.38f * Mathf.Clamp01(strength);
            Animate(
                glow,
                hold + fade,
                (s, k) =>
                {
                    float t = k * (hold + fade);
                    float alpha = t < hold ? peak : peak * (1f - EaseIn((t - hold) / fade));
                    Show(s, new Color(color.r, color.g, color.b, alpha), k, 0f);
                }
            );
        }

        /// <summary>A pool of light lying on the floor at <paramref name="feet"/>, brightest at once and fading.</summary>
        private void Pool(Vector2 feet, Color color, float size, float hold, float fade)
        {
            if (backLight == null)
                return;
            var pool = Spawn(backLight, BattleVfxShape.Glow, feet, new Vector2(size, size), color);
            pool.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            Animate(
                pool,
                hold + fade,
                (s, k) =>
                {
                    float t = k * (hold + fade);
                    float alpha = t < hold ? 0.85f : 0.85f * (1f - EaseIn((t - hold) / fade));
                    Show(s, new Color(color.r, color.g, color.b, alpha), k, 0f);
                }
            );
        }

        private void StepLights(float dt)
        {
            for (int i = lights.Count - 1; i >= 0; i--)
            {
                lights[i].Age += dt;
                if (lights[i].Age >= lights[i].Hold + lights[i].Fade)
                    lights.RemoveAt(i);
            }
            if (FrontLayer == null)
                return;
            foreach (var light in actorLights)
            {
                if (light == null)
                    continue;
                if (lights.Count == 0)
                {
                    if (light.Lit)
                        light.SetCorners(Color.clear, Color.clear, Color.clear, Color.clear);
                    continue;
                }
                light.rectTransform.GetWorldCorners(corners);
                var center = Vector2.zero;
                for (int c = 0; c < 4; c++)
                {
                    corners[c] = FrontLayer.InverseTransformPoint(corners[c]);
                    center += (Vector2)corners[c] * 0.25f;
                }
                light.SetCorners(
                    CornerLight(corners[0], center),
                    CornerLight(corners[1], center),
                    CornerLight(corners[2], center),
                    CornerLight(corners[3], center)
                );
            }
        }

        /// <summary>The colour (and in alpha, the strength) of every light falling on one corner.</summary>
        private Color CornerLight(Vector2 corner, Vector2 center)
        {
            var sum = Color.clear;
            float weight = 0f;
            foreach (var light in lights)
            {
                float strength =
                    light.Age < light.Hold
                        ? light.Strength
                        : light.Strength * (1f - EaseIn((light.Age - light.Hold) / light.Fade));
                if (light.Flicker > 0f)
                    strength *= 1f - light.Flicker * Mathf.PerlinNoise(light.Seed, light.Age * 14f);
                float w = strength * LightOnCorner(corner, center, light.At, light.Radius);
                if (w <= 0f)
                    continue;
                sum += light.Color * w;
                weight += w;
            }
            if (weight <= 0f)
                return Color.clear;
            return new Color(sum.r / weight, sum.g / weight, sum.b / weight, Mathf.Min(1f, weight));
        }

        private sealed class LightSource
        {
            public Vector2 At;
            public Color Color;
            public float Strength;
            public float Radius;
            public float Hold;
            public float Fade;
            public float Age;

            /// <summary>How much a fire's light wavers (0 steady, 1 down to nothing at times).</summary>
            public float Flicker;
            public float Seed;
        }

        // --- Skills ----------------------------------------------------------------------------

        /// <summary>
        /// Plays a skill from <paramref name="caster"/> (its drawn body) on <paramref name="targets"/>.
        /// <paramref name="land"/> is called with each target's index as the blow lands there, and
        /// tells how hard it hit. Ends once the last blow has landed and its burst has spread; the
        /// embers and smoke keep drifting after.
        /// </summary>
        public IEnumerator Play(
            BattleSkillVfxKind kind,
            string skill,
            RectTransform caster,
            Texture casterArt,
            bool cutIn,
            IReadOnlyList<RectTransform> targets,
            Func<int, BattleHitWeight> land
        )
        {
            if (cutIn)
                yield return PlayCutIn(casterArt, skill, ColorOf(kind));
            switch (kind)
            {
                case BattleSkillVfxKind.Fire:
                    yield return Fire(caster, targets, land);
                    break;
                case BattleSkillVfxKind.Ice:
                    yield return Ice(caster, targets, land, cutIn);
                    break;
                case BattleSkillVfxKind.Thunder:
                    yield return Thunder(caster, targets, land, cutIn);
                    break;
                case BattleSkillVfxKind.Heal:
                    yield return Heal(caster, targets, land);
                    break;
                case BattleSkillVfxKind.Guard:
                    yield return Guard(caster, targets, land);
                    break;
                default:
                    yield return Slash(caster, targets, land);
                    break;
            }
            DimTo(0f, 0.4f);
        }

        /// <summary>
        /// Aria's sword, sharp and quick: a glint, two sweeping arcs (one behind the enemy, one in
        /// front, so the blade passes through it) and a cross cut that lands the blow. A light
        /// cyan glint lights the enemy for an instant; the screen does not flash.
        /// </summary>
        private IEnumerator Slash(
            RectTransform caster,
            IReadOnlyList<RectTransform> targets,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf(BattleSkillVfxKind.Slash);
            var from = Center(FrontLayer, caster);
            DimTo(0.4f, 0.12f);
            Star(from + new Vector2(40f, 30f), Color.white, 140f, 0.2f);
            Illuminate(from, color, 0.5f, 220f, 0.05f, 0.25f, spill: false);
            yield return Wait(0.14f);

            for (int i = 0; i < targets.Count; i++)
            {
                var at = Center(FrontLayer, targets[i]);
                // Rush lines toward the enemy as the sword is drawn.
                Spray(
                    frontLight,
                    Vector2.Lerp(from, at, 0.55f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.white,
                        To = color,
                        Speed = new Vector2(1800f, 2600f),
                        Direction = Angle(at - from),
                        Spread = 6f,
                        Life = new Vector2(0.08f, 0.16f),
                        Size = new Vector2(8f, 16f),
                        Stretch = 0.05f,
                        Area = 90f,
                    },
                    12
                );
                Arc(backLight, at, -18f, false, color, 1.05f);
                Sparks(at, color, 12, 0.8f, Vector2.right);
                Illuminate(at, color, 0.55f, 300f, 0.03f, 0.2f, spill: false);
                Shake(0.18f, Vector2.right);
                yield return Wait(0.08f);
                Arc(frontLight, at + new Vector2(10f, -16f), 14f, true, color, 0.9f);
                Sparks(at, color, 12, 0.8f, Vector2.left);
                Shake(0.18f, Vector2.left);
                yield return Wait(0.08f);

                var weight = land(i);
                Cross(at, color, weight);
                Impact(targets[i], at, color, weight, 0.6f, Vector2.right, flashScreen: false);
            }
            yield return Wait(0.2f);
        }

        /// <summary>
        /// Toma's fireball, hot and heavy: a magic circle and gathering embers, a fireball flung on
        /// an arc with a tail of sparks and smoke, then a blast split around the target (a wide
        /// dim burst behind it, a small bright core in front), embers flung up, licks of flame and
        /// soot rising behind, and an orange light on everyone near and the floor.
        /// </summary>
        private IEnumerator Fire(
            RectTransform caster,
            IReadOnlyList<RectTransform> targets,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf(BattleSkillVfxKind.Fire);
            yield return Charge(caster, color, 0.3f, big: false);
            var from = Center(FrontLayer, caster) + new Vector2(60f, 30f);
            for (int i = 0; i < targets.Count; i++)
            {
                var at = Center(FrontLayer, targets[i]);
                yield return Missile(
                    BattleVfxShape.Fireball,
                    from,
                    at,
                    new Vector2(280f, 280f),
                    new Vector2(0.85f, 0.5f),
                    0.26f,
                    90f,
                    color
                );
                var weight = land(i);
                Explosion(targets[i], at, weight);
                Impact(
                    targets[i],
                    at,
                    color,
                    weight,
                    0.85f,
                    Vector2.right,
                    flashScreen: true,
                    rays: false
                );
            }
            yield return Wait(0.22f);
        }

        /// <summary>
        /// Toma's ice lance (a cut-in skill), cold and sharp: frost gathers with a light from
        /// below, the lance shoots straight and fast, crystals bloom around the target (behind
        /// and in front of it) and freeze still, then shatter into falling shards and a low mist.
        /// </summary>
        private IEnumerator Ice(
            RectTransform caster,
            IReadOnlyList<RectTransform> targets,
            Func<int, BattleHitWeight> land,
            bool big
        )
        {
            var color = ColorOf(BattleSkillVfxKind.Ice);
            yield return Charge(caster, color, 0.26f, big);
            var from = Center(FrontLayer, caster) + new Vector2(60f, 20f);
            for (int i = 0; i < targets.Count; i++)
            {
                var at = Center(FrontLayer, targets[i]);
                yield return Missile(
                    BattleVfxShape.IceSpear,
                    from,
                    at,
                    new Vector2(320f, 320f),
                    new Vector2(0.93f, 0.5f),
                    0.16f,
                    0f,
                    color
                );
                var weight = land(i);
                Frost(targets[i], at, weight);
                Impact(
                    targets[i],
                    at,
                    color,
                    weight,
                    0.9f,
                    Vector2.right,
                    flashScreen: true,
                    rays: false
                );
                if (big)
                    Punch(at, 0.035f);
            }
            yield return Wait(0.26f);
        }

        /// <summary>
        /// Luka's thunder (a cut-in skill on every enemy), violent: the sky goes dark and a bolt
        /// strikes each enemy in turn, striking again in new shapes, with crackles running round
        /// the target, a ring on the floor and a yellow light. Only the first strike flashes.
        /// </summary>
        private IEnumerator Thunder(
            RectTransform caster,
            IReadOnlyList<RectTransform> targets,
            Func<int, BattleHitWeight> land,
            bool big
        )
        {
            var color = ColorOf(BattleSkillVfxKind.Thunder);
            var violet = new Color(0.72f, 0.48f, 1f);
            DimTo(NightDim, 0.18f);
            yield return Charge(caster, violet, 0.26f, big);
            for (int i = 0; i < targets.Count; i++)
            {
                var feet = Feet(FrontLayer, targets[i]);
                var at = Center(FrontLayer, targets[i]);
                Bolt(feet);
                var weight = land(i);
                Star(at, color, 300f, 0.26f);
                Ring(backLight, feet, violet, 520f, 0.42f, floor: true);
                Pool(feet, color, 420f, 0.1f, 0.5f);
                Illuminate(at, Color.Lerp(color, Color.white, 0.3f), 1f, 460f, 0.12f, 0.45f);
                Sparks(at, color, 22, 1.1f, Vector2.down);
                StartCoroutine(Crackle(targets[i], violet));
                Puffs(
                    backSmoke,
                    feet + Vector2.up * 30f,
                    new Color(0.12f, 0.1f, 0.14f, 0.55f),
                    3,
                    60f,
                    1.1f
                );
                if (i == 0)
                {
                    ScreenFlash(new Color(1f, 0.95f, 0.75f), MaxFlash);
                    if (big)
                        Punch(at, 0.03f);
                }
                HitStop(Weighted(i == 0 ? 0.08f : 0.05f, weight));
                Shake(Weighted(0.42f, weight), Vector2.down);
                yield return Wait(0.17f);
            }
            yield return Wait(0.24f);
        }

        /// <summary>
        /// Mina's heal, warm and calm: a soft green circle under her, then a pillar of light rises
        /// behind the ally, sparkles drift up in front, and a gentle green light settles on the ally
        /// and the floor. No shake, stop nor screen flash.
        /// </summary>
        private IEnumerator Heal(
            RectTransform caster,
            IReadOnlyList<RectTransform> targets,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf(BattleSkillVfxKind.Heal);
            var gold = new Color(1f, 0.92f, 0.55f);
            DimTo(0.32f, 0.25f);
            yield return Charge(caster, color, 0.24f, big: false);
            for (int i = 0; i < targets.Count; i++)
            {
                var feet = Feet(FrontLayer, targets[i]);
                var at = Center(FrontLayer, targets[i]);
                Pillar(feet);
                Ring(backLight, feet, color, 360f, 0.7f, floor: true);
                Pool(feet, color, 380f, 0.5f, 0.6f);
                Illuminate(at, Color.Lerp(color, gold, 0.3f), 0.75f, 300f, 0.5f, 0.6f);
                yield return Wait(0.14f);
                land(i);
                Spray(
                    frontLight,
                    feet + new Vector2(0f, 40f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Sparkle,
                        From = Color.white,
                        To = new Color(gold.r, gold.g, gold.b, 0f),
                        Speed = new Vector2(50f, 200f),
                        Direction = 90f,
                        Spread = 50f,
                        Life = new Vector2(0.6f, 1.2f),
                        Size = new Vector2(16f, 40f),
                        Shrink = 0.2f,
                        Gravity = -140f,
                        Spin = 160f,
                        Area = 70f,
                    },
                    22
                );
            }
            yield return Wait(0.3f);
        }

        /// <summary>
        /// Aria's guard, springy and protective: a blue circle under her, then a hexagon dome
        /// springs up over the whole party (behind them, with a faint rim in front so they stand
        /// inside), shimmers, and breaks into shards that fall as it fades.
        /// </summary>
        private IEnumerator Guard(
            RectTransform caster,
            IReadOnlyList<RectTransform> targets,
            Func<int, BattleHitWeight> land
        )
        {
            var color = ColorOf(BattleSkillVfxKind.Guard);
            DimTo(0.4f, 0.2f);
            yield return Charge(caster, color, 0.22f, big: false);
            if (targets.Count == 0)
                yield break;

            // One dome over everyone: from the lowest feet up past the highest head.
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var target in targets)
            {
                var (low, high) = Bounds(FrontLayer, target);
                min = Vector2.Min(min, low);
                max = Vector2.Max(max, high);
            }
            float width = Mathf.Max(max.x - min.x, max.y - min.y) + 240f;
            var basePoint = new Vector2((min.x + max.x) * 0.5f, min.y - 30f);
            var middle = basePoint + new Vector2(0f, width * 0.32f);
            Dome(backLight, basePoint, width, 1f, breaks: true);
            Dome(frontLight, basePoint, width, 0.3f, breaks: false);
            for (int i = 0; i < targets.Count; i++)
            {
                Ring(backLight, Feet(FrontLayer, targets[i]), color, 300f, 0.45f, floor: true);
                land(i);
            }
            Pool(basePoint + Vector2.up * 20f, color, width * 0.9f, 0.4f, 0.6f);
            Illuminate(middle, color, 0.7f, width * 0.8f, 0.5f, 0.5f);
            Shake(0.14f, Vector2.zero);
            HitStop(0.04f);
            yield return Wait(0.62f);
            // The dome breaks: shards fall from its surface as it fades.
            Spray(
                frontLight,
                middle,
                new Burst
                {
                    Shape = BattleVfxShape.Shard,
                    Bright = ErodeBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(60f, 260f),
                    Spread = 360f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(10f, 22f),
                    Aspect = 2.2f,
                    Gravity = 700f,
                    Drag = 1f,
                    Spin = 540f,
                    Area = width * 0.38f,
                },
                30
            );
            yield return Wait(0.12f);
        }

        /// <summary>
        /// An enemy's attack landing on an ally: red claw marks, a small burst, sparks, a short stop
        /// and shake, and a red light on the ally. Milder than the party's skills, with no flash.
        /// </summary>
        public void Strike(RectTransform target)
        {
            if (FrontLayer == null || target == null)
                return;
            var at = Center(FrontLayer, target);
            var red = new Color(1f, 0.35f, 0.25f);
            Arc(frontLight, at, 200f, true, red, 0.65f);
            Star(at, new Color(1f, 0.6f, 0.4f), 200f, 0.2f);
            Sparks(at, red, 12, 0.7f, Vector2.left);
            Illuminate(at, red, 0.5f, 260f, 0.05f, 0.3f, spill: false);
            HitStop(0.05f);
            Shake(0.3f, Vector2.left);
        }

        /// <summary>
        /// The glint of a weakness found on <paramref name="icon"/>: a twinkle of four long rays
        /// flares at its top right corner and turns as it dies away, a smaller one answers at the
        /// opposite corner, a soft glow in the element's <paramref name="color"/> swells around the
        /// first and a few small twinkles drift up from the icon. No stop, shake or flash: it is a sign,
        /// not a blow, and it stays small so it never hides the target.
        /// </summary>
        public void Glint(RectTransform icon, Color color)
        {
            if (FrontLayer == null || icon == null)
                return;
            var (low, high) = Bounds(FrontLayer, icon);
            var center = (low + high) * 0.5f;
            float size = high.x - low.x;
            // The twinkle sits on the corner, so its hot middle leaves the new icon readable.
            Glow(frontLight, Vector2.Lerp(center, high, 0.5f), color, size * 2.2f, 0.36f, 0.35f);
            Twinkle(high, color, size * 2.6f, 0.5f, 0f, 1f);
            Twinkle(low, color, size, 0.28f, 0.12f, -1f);
            Spray(
                frontLight,
                center,
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(50f, 130f),
                    Direction = 90f,
                    Spread = 150f,
                    Life = new Vector2(0.35f, 0.6f),
                    Size = new Vector2(size * 0.35f, size * 0.55f),
                    Grow = 1f,
                    Shrink = 0.2f,
                    Gravity = -60f,
                    Drag = 2.5f,
                    Spin = 120f,
                    Area = size * 0.4f,
                    Delay = new Vector2(0.02f, 0.14f),
                },
                4
            );
        }

        /// <summary>
        /// A twinkle of four rays that flares fast after <paramref name="delay"/>, then shrinks
        /// as it turns toward <paramref name="turn"/> (1 one way, -1 the other) and takes on the
        /// element's colour.
        /// </summary>
        private void Twinkle(
            Vector2 at,
            Color color,
            float size,
            float seconds,
            float delay,
            float turn
        )
        {
            var shot = Spawn(
                frontLight,
                BattleVfxShape.Sparkle,
                at,
                new Vector2(size, size),
                Color.clear
            );
            float total = seconds + delay;
            float start = delay / total;
            Animate(
                shot,
                total,
                (s, t) =>
                {
                    float k = t <= start ? -1f : (t - start) / (1f - start);
                    if (k < 0f)
                    {
                        Show(s, Color.clear, 0f, 0f);
                        return;
                    }
                    // Out at once with a little overshoot, then drawn back in to a point.
                    float grow = k < 0.14f ? BackOut(k / 0.14f) : 1f - EaseIn((k - 0.14f) / 0.86f);
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    s.Holder.localEulerAngles = new Vector3(0f, 0f, -40f * turn * EaseOut(k));
                    var tint = Color.Lerp(Color.white, color, 0.25f + 0.5f * k);
                    Show(s, tint, k, 0f, CoreBright);
                }
            );
        }

        // --- Beats -----------------------------------------------------------------------------

        /// <summary>
        /// The wind-up of a spell: the stage darkens to a night blue, a magic circle turns under
        /// the user, a glow swells on it and motes of light are drawn in. A big skill also lights
        /// the user from below, the way a great spell is lit from its circle.
        /// </summary>
        private IEnumerator Charge(RectTransform caster, Color color, float seconds, bool big)
        {
            if (dimTarget < SpellDim)
                DimTo(SpellDim, 0.15f);
            var center = Center(FrontLayer, caster);
            var feet = Feet(FrontLayer, caster);
            Circle(feet, color, seconds + 0.55f);
            Pool(feet, color, 300f, seconds, 0.5f);
            Glow(frontLight, center, color, 280f, seconds + 0.2f, 0.6f);
            // From below: strongest at the feet, for the whole wind-up and a little after.
            Illuminate(
                feet + Vector2.down * 90f,
                color,
                big ? 1f : 0.55f,
                big ? 360f : 260f,
                seconds + 0.1f,
                0.4f,
                spill: false
            );
            for (int i = 0; i < 20; i++)
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = UnityEngine.Random.Range(140f, 220f);
                var start = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                bool sparkle = i % 3 == 0;
                var particle = Take(
                    frontLight,
                    sparkle ? BattleVfxShape.Sparkle : BattleVfxShape.Spark
                );
                float life = seconds * UnityEngine.Random.Range(0.6f, 1f);
                particle.Position = start;
                particle.Velocity = (center - start) / life;
                particle.Life = life;
                float size = sparkle
                    ? UnityEngine.Random.Range(22f, 34f)
                    : UnityEngine.Random.Range(10f, 16f);
                particle.Size = new Vector2(size, size);
                particle.EndScale = 0.3f;
                particle.From = new Color(color.r, color.g, color.b, 0.2f);
                particle.To = Color.white;
                particle.Spin = 0f;
                particle.Gravity = 0f;
                particle.Drag = 0f;
                particle.Stretch = 0f;
                particle.Apply();
            }
            yield return Wait(seconds);
            Star(center, Color.white, 180f, 0.18f);
        }

        /// <summary>
        /// Flies a missile (its head at <paramref name="head"/> in the board) from one point to
        /// another on an ease-in arc of <paramref name="lift"/> px, turning along its path,
        /// leaving a trail of sparks.
        /// </summary>
        private IEnumerator Missile(
            BattleVfxShape shape,
            Vector2 from,
            Vector2 to,
            Vector2 size,
            Vector2 head,
            float seconds,
            float lift,
            Color color
        )
        {
            var shot = Spawn(frontLight, shape, from, size, color, head);
            float trail = 0f;
            var last = from;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float ease = k * k * (1.4f - 0.4f * k);
                var position =
                    Vector2.Lerp(from, to, ease) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * lift);
                var heading = position - last;
                if (heading.sqrMagnitude > 0.01f)
                    shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(heading));
                shot.Holder.anchoredPosition = position;
                float flicker = 1f + 0.06f * Mathf.Sin(t * 70f);
                shot.Holder.localScale = new Vector3(flicker, flicker, 1f);
                Show(shot, color, k, 0f, CoreBright);
                trail += Time.deltaTime;
                while (trail > 0.016f)
                {
                    trail -= 0.016f;
                    Spray(
                        frontLight,
                        position,
                        new Burst
                        {
                            Shape = BattleVfxShape.Spark,
                            From = Color.white,
                            To = color,
                            Speed = new Vector2(40f, 160f),
                            Direction = Angle(-heading),
                            Spread = 70f,
                            Life = new Vector2(0.2f, 0.5f),
                            Size = new Vector2(8f, 14f),
                            Gravity = -120f,
                            Drag = 2f,
                            Area = 18f,
                        },
                        2
                    );
                }
                last = position;
                yield return null;
            }
            shot.Live = false;
            shot.Holder.gameObject.SetActive(false);
        }

        /// <summary>
        /// One sweeping sword arc: it swells and sweeps a little along its curve as it cuts, then
        /// is eaten away from its thin tips toward the thick middle.
        /// </summary>
        private void Arc(
            RectTransform layer,
            Vector2 at,
            float angle,
            bool mirror,
            Color color,
            float scale
        )
        {
            var shot = Spawn(
                layer,
                BattleVfxShape.SlashArc,
                at,
                new Vector2(420f, 420f) * scale,
                Color.white
            );
            shot.Image.rectTransform.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
            float sweep = mirror ? -24f : 24f;
            Animate(
                shot,
                0.26f,
                (s, k) =>
                {
                    float grow = 0.75f + 0.35f * EaseOut(Mathf.Min(1f, k * 2.5f));
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    s.Image.rectTransform.localEulerAngles = new Vector3(
                        0f,
                        0f,
                        angle + sweep * EaseOut(Mathf.Min(1f, k * 2f))
                    );
                    Show(s, Color.Lerp(Color.white, color, k), k, Eaten(k, 0.25f), CoreBright);
                }
            );
        }

        /// <summary>The finishing cross cut: an X of light that bursts and is eaten away.</summary>
        private void Cross(Vector2 at, Color color, BattleHitWeight weight)
        {
            float size = weight == BattleHitWeight.Normal ? 420f : 520f;
            var shot = Spawn(
                frontLight,
                BattleVfxShape.SlashCross,
                at,
                new Vector2(size, size),
                Color.white
            );
            Animate(
                shot,
                0.32f,
                (s, k) =>
                {
                    float grow = 0.55f + 0.6f * EaseOut(Mathf.Min(1f, k * 3f));
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    Show(s, Color.Lerp(Color.white, color, k), k, Eaten(k, 0.2f), CoreBright);
                }
            );
        }

        /// <summary>
        /// The fireball's blast, built the way a real one looks: a flash, then a fireball of torn
        /// lumps of flame that swell outward in an instant, slow in the air and roll upward as
        /// they cool from white to yellow, orange, red and dark, with tongues of flame rising
        /// from it. Smoke boils up behind the flame, lit brown by it at first and greying, and
        /// keeps rising after the fire is gone. Embers and dark debris are thrown out, dust rolls
        /// along the ground both ways, and a scorch mark is left on the floor. Most of the fire is
        /// behind the target and only a little is in front, so the target is seen in the blaze.
        /// Nothing is one round shape: every part is many irregular pieces at different times.
        /// </summary>
        private void Explosion(RectTransform target, Vector2 at, BattleHitWeight weight)
        {
            var color = ColorOf(BattleSkillVfxKind.Fire);
            float scale = weight == BattleHitWeight.Normal ? 1f : 1.2f;
            var feet = Feet(FrontLayer, target);
            at = Inside(at, 420f * scale);

            // The floor: a scorch mark that stays a while, and dust rolling out both ways.
            FloorMark(
                BattleVfxShape.Scorch,
                feet,
                Vector2.one * 300f * scale,
                new Color(0.06f, 0.045f, 0.04f),
                0.85f,
                1.8f,
                flat: true
            );
            foreach (float side in new[] { 0f, 180f })
                Spray(
                    backSmoke,
                    feet + Vector2.up * 10f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        From = new Color(0.45f, 0.38f, 0.32f, 0.7f),
                        To = new Color(0.32f, 0.3f, 0.3f, 0.7f),
                        Speed = new Vector2(220f, 520f) * scale,
                        Direction = side,
                        Spread = 22f,
                        Life = new Vector2(0.7f, 1.2f),
                        Size = new Vector2(90f, 150f) * scale,
                        Aspect = 0.45f,
                        Grow = 0.4f,
                        Shrink = 1.9f,
                        Drag = 3.2f,
                        Gravity = -15f,
                        Spin = 15f,
                        Hold = 0.25f,
                        Area = 20f,
                    },
                    5
                );

            // Smoke boils up behind the fire, starting as the fireball forms.
            Spray(
                backSmoke,
                at + Vector2.up * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    From = new Color(0.42f, 0.27f, 0.17f, 0.92f),
                    To = new Color(0.13f, 0.13f, 0.14f, 0.92f),
                    Speed = new Vector2(60f, 200f),
                    Direction = 90f,
                    Spread = 80f,
                    Life = new Vector2(1.3f, 2.2f),
                    Size = new Vector2(130f, 220f) * scale,
                    Grow = 0.5f,
                    Shrink = 2.1f,
                    Gravity = -110f,
                    Drag = 1.3f,
                    Spin = 40f,
                    Hold = 0.3f,
                    Delay = new Vector2(0.06f, 0.4f),
                    Area = 60f * scale,
                },
                12
            );

            // The fireball: torn lumps thrown out fast, braked hard by the air, rolling upward.
            var fireball = new Burst
            {
                Shape = BattleVfxShape.Flame,
                Bright = ErodeBright,
                Fire = true,
                Speed = new Vector2(220f, 620f) * scale,
                Spread = 360f,
                Life = new Vector2(0.4f, 0.75f),
                Size = new Vector2(110f, 200f) * scale,
                Grow = 0.35f,
                Shrink = 1.25f,
                Gravity = -380f,
                Drag = 5f,
                Spin = 80f,
                Hold = 0.15f,
                Delay = new Vector2(0f, 0.08f),
                Area = 40f * scale,
            };
            Spray(backLight, at, fireball, 16);
            // A few smaller, shorter ones in front: the target is caught inside, not hidden.
            fireball.Size = new Vector2(70f, 130f) * scale;
            fireball.Life = new Vector2(0.28f, 0.5f);
            Spray(frontLight, at, fireball, 6);

            // Tongues of flame rising from the fireball a moment later.
            Spray(
                backLight,
                at + Vector2.up * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.FlameTongue,
                    Bright = ErodeBright,
                    Fire = true,
                    Speed = new Vector2(150f, 380f),
                    Direction = 90f,
                    Spread = 60f,
                    Life = new Vector2(0.4f, 0.75f),
                    Size = new Vector2(80f, 150f) * scale,
                    Grow = 0.4f,
                    Shrink = 1.3f,
                    Gravity = -300f,
                    Drag = 2f,
                    Spin = 20f,
                    Hold = 0.2f,
                    Delay = new Vector2(0.05f, 0.22f),
                    Area = 50f * scale,
                },
                8
            );

            // Embers: fast at first, slowed hard by the air, then floating up and cooling.
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = CoreBright,
                    From = new Color(1f, 0.92f, 0.6f),
                    To = new Color(1f, 0.3f, 0.06f, 0f),
                    Speed = new Vector2(250f, 1000f),
                    Spread = 360f,
                    Life = new Vector2(0.4f, 1.2f),
                    Size = new Vector2(8f, 16f),
                    Gravity = -200f,
                    Drag = 3.5f,
                    Area = 40f,
                },
                Mathf.RoundToInt(36 * scale)
            );
            // Dark debris thrown up and falling back.
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = new Color(0.14f, 0.11f, 0.09f),
                    To = new Color(0.1f, 0.08f, 0.07f, 0f),
                    Speed = new Vector2(320f, 820f),
                    Direction = 90f,
                    Spread = 150f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(8f, 16f),
                    Gravity = 1600f,
                    Drag = 0.8f,
                    Spin = 720f,
                    Area = 30f,
                },
                12
            );

            Pool(feet, color, 520f * scale, 0.2f, 0.6f);
            Illuminate(at, color, 1f, 560f * scale, 0.15f, 0.5f, flicker: 0.35f);
        }

        /// <summary>
        /// The ice lance's hit, built like ice forming: spikes of clear ice shoot up out of the
        /// floor around the target in an instant (taller and more behind it, shorter in front, so
        /// it is caught among them), leaning out from the blow, and frost spreads on the floor.
        /// They hold frozen, then crack and crumble while broken pieces fall and a cold mist rolls
        /// low over the floor. Ice is drawn as a see-through solid, not as light.
        /// </summary>
        private void Frost(RectTransform target, Vector2 at, BattleHitWeight weight)
        {
            var color = ColorOf(BattleSkillVfxKind.Ice);
            float scale = weight == BattleHitWeight.Normal ? 1f : 1.2f;
            var feet = Feet(FrontLayer, target);
            var (low, high) = Bounds(FrontLayer, target);
            float width = Mathf.Max(160f, high.x - low.x);

            FloorMark(
                BattleVfxShape.Frost,
                feet,
                new Vector2(width * 1.5f, width * 0.5f) * scale,
                new Color(0.85f, 0.95f, 1f),
                0.75f,
                2f,
                flat: false
            );
            // The mist lies low behind everything and lingers.
            Spray(
                backSmoke,
                feet + Vector2.up * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    From = new Color(0.86f, 0.95f, 1f, 0.75f),
                    To = new Color(0.7f, 0.85f, 1f, 0.75f),
                    Speed = new Vector2(40f, 180f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(1.1f, 1.7f),
                    Size = new Vector2(160f, 260f),
                    Aspect = 0.5f,
                    Grow = 0.5f,
                    Shrink = 1.6f,
                    Gravity = 10f,
                    Drag = 1.8f,
                    Spin = 6f,
                    Hold = 0.3f,
                    Delay = new Vector2(0f, 0.2f),
                    Area = 60f,
                },
                6
            );

            // Spikes: behind the target first, then a shorter row in front.
            for (int i = 0; i < 9; i++)
            {
                bool front = i >= 6;
                float spread = (front ? 0.45f : 0.7f) * width * scale;
                float x = Mathf.Lerp(-spread, spread, (front ? i - 6 : i) / (front ? 2f : 5f));
                x += UnityEngine.Random.Range(-14f, 14f);
                float centered = 1f - Mathf.Abs(x) / Mathf.Max(1f, spread);
                float height =
                    (front ? 130f : 210f)
                    * scale
                    * (0.55f + 0.75f * centered)
                    * UnityEngine.Random.Range(0.85f, 1.15f);
                float lean = -x * 0.2f + UnityEngine.Random.Range(-7f, 7f);
                float delay = (1f - centered) * 0.06f + (front ? 0.03f : 0f);
                Spike(
                    front ? frontLight : backLight,
                    feet + new Vector2(x, front ? -12f : 6f),
                    height,
                    lean,
                    delay,
                    front
                );
            }

            // Broken pieces fall as the spikes crack.
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = Color.white,
                    To = new Color(0.8f, 0.92f, 1f),
                    Speed = new Vector2(160f, 560f),
                    Direction = 90f,
                    Spread = 200f,
                    Life = new Vector2(0.55f, 0.95f),
                    Size = new Vector2(16f, 40f),
                    Gravity = 1400f,
                    Drag = 1f,
                    Spin = 360f,
                    Hold = 0.6f,
                    Delay = new Vector2(0.48f, 0.6f),
                    Area = 70f * scale,
                },
                Mathf.RoundToInt(22 * scale)
            );
            Pool(feet, color, 460f * scale, 0.5f, 0.8f);
            Illuminate(at, color, 0.8f, 480f * scale, 0.5f, 0.6f);
        }

        /// <summary>
        /// One spike of clear ice shooting up from the floor at <paramref name="foot"/>: it grows to
        /// its height in a blink (after <paramref name="delay"/>), holds, then crumbles away.
        /// </summary>
        private void Spike(
            RectTransform layer,
            Vector2 foot,
            float height,
            float lean,
            float delay,
            bool front
        )
        {
            var tint = front ? Color.white : new Color(0.8f, 0.9f, 1f);
            // Each spike takes its own width, tip and facets from its seed.
            var shot = Spawn(
                layer,
                BattleVfxShape.IceSpike,
                foot,
                new Vector2(height * 0.5f, height),
                new Color(tint.r, tint.g, tint.b, 0f),
                new Vector2(0.5f, 0.03f)
            );
            shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, lean);
            const float life = 1f;
            Animate(
                shot,
                life + delay,
                (s, k) =>
                {
                    float t = k * (life + delay) - delay;
                    if (t < 0f)
                    {
                        Show(s, new Color(tint.r, tint.g, tint.b, 0f), 0f, 0f);
                        return;
                    }
                    float grow = EaseOut(Mathf.Min(1f, t / 0.07f));
                    s.Holder.localScale = new Vector3(0.7f + 0.3f * grow, grow, 1f);
                    Show(s, tint, t / life, Eaten(t / life, 0.5f));
                }
            );
        }

        /// <summary>
        /// A mark left on the floor at <paramref name="feet"/> (a scorch, frost): it comes in fast,
        /// stays for <paramref name="seconds"/> and wears away from its faint edges. A
        /// <paramref name="flat"/> shape is laid down onto the floor; one already worked out at a
        /// low angle is used as it is.
        /// </summary>
        private void FloorMark(
            BattleVfxShape shape,
            Vector2 feet,
            Vector2 size,
            Color color,
            float strength,
            float seconds,
            bool flat
        )
        {
            if (backSmoke == null)
                return;
            var mark = Spawn(
                backSmoke,
                shape,
                feet,
                size,
                new Color(color.r, color.g, color.b, 0f)
            );
            if (flat)
            {
                mark.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
                mark.Image.rectTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    UnityEngine.Random.Range(0f, 360f)
                );
            }
            Animate(
                mark,
                seconds,
                (s, k) =>
                {
                    float into = k < 0.06f ? k / 0.06f : 1f;
                    float eaten = k < 0.06f ? 0f : Eaten((k - 0.06f) / 0.94f, 0.5f);
                    Show(s, new Color(color.r, color.g, color.b, strength * into), k, eaten);
                }
            );
        }

        /// <summary>
        /// A lightning bolt from above the screen down to <paramref name="feet"/>. It strikes
        /// three times in new shapes (a new seed, narrowed or widened, nudged sideways), with a
        /// thinner bolt beside it on the second, and breaks up at the end.
        /// </summary>
        private void Bolt(Vector2 feet)
        {
            // The bolt's foot is at 9% of its board's height.
            float top = FrontLayer.rect.yMax + 60f;
            float height = Mathf.Max(600f, (top - feet.y) / 0.88f);
            var size = new Vector2(height * 0.62f, height);
            var pivot = new Vector2(0.5f, 0.09f);
            var main = Spawn(frontLight, BattleVfxShape.Lightning, feet, size, Color.white, pivot);
            var widths = new[] { 1f, 0.8f, 1.2f };
            var shifts = new[] { 0f, -22f, 18f };
            float seed = main.Seed;
            Animate(
                main,
                0.36f,
                (s, k) =>
                {
                    int strike = Mathf.Min(2, Mathf.FloorToInt(k * 3.2f));
                    // Each strike is a new bolt: a new seed draws a new path.
                    s.Seed = Mathf.Repeat(seed + strike * 0.318f, 1f);
                    s.Image.rectTransform.localScale = new Vector3(widths[strike], 1f, 1f);
                    s.Image.rectTransform.anchoredPosition = new Vector2(shifts[strike], 0f);
                    s.Image.rectTransform.localEulerAngles = new Vector3(
                        0f,
                        0f,
                        strike == 2 ? -3f : 0f
                    );
                    Show(s, Color.white, k, Eaten(k, 0.6f), CoreBright);
                }
            );
            var branch = Spawn(
                frontLight,
                BattleVfxShape.Lightning,
                feet + new Vector2(40f, 0f),
                size * 0.75f,
                new Color(0.8f, 0.7f, 1f, 0f),
                pivot
            );
            branch.Image.rectTransform.localScale = new Vector3(0.7f, 1f, 1f);
            branch.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, 7f);
            Animate(
                branch,
                0.3f,
                (s, k) =>
                {
                    // Only from the second strike, then gone with the first.
                    var tint = new Color(0.8f, 0.7f, 1f, k < 0.3f ? 0f : 1f);
                    float eaten = k < 0.3f ? 0f : Eaten((k - 0.3f) / 0.7f, 0.3f);
                    Show(s, tint, k, eaten, ErodeBright);
                }
            );
        }

        /// <summary>Short sparks of electricity that keep running round the target for a moment after the strike.</summary>
        private IEnumerator Crackle(RectTransform target, Color color)
        {
            for (int i = 0; i < 4; i++)
            {
                for (float t = 0f; t < 0.07f; t += Time.unscaledDeltaTime)
                    yield return null;
                if (target == null)
                    yield break;
                var at = Center(FrontLayer, target) + UnityEngine.Random.insideUnitCircle * 60f;
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Streak,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(300f, 700f),
                        Spread = 360f,
                        Life = new Vector2(0.05f, 0.12f),
                        Size = new Vector2(6f, 10f),
                        Stretch = 0.04f,
                        Drag = 6f,
                        Area = 30f,
                    },
                    5
                );
            }
        }

        /// <summary>
        /// The heal's pillar of light behind the ally, rising from the feet and eaten away slowly
        /// from its faint edges up.
        /// </summary>
        private void Pillar(Vector2 feet)
        {
            var color = ColorOf(BattleSkillVfxKind.Heal);
            var shot = Spawn(
                backLight,
                BattleVfxShape.Pillar,
                feet,
                new Vector2(400f, 400f),
                color,
                new Vector2(0.5f, 0.1f)
            );
            Animate(
                shot,
                1.1f,
                (s, k) =>
                {
                    float rise = EaseOut(Mathf.Min(1f, k * 4f));
                    s.Holder.localScale = new Vector3(0.7f + 0.3f * rise, 0.2f + 1.05f * rise, 1f);
                    Show(s, color, k, Eaten(k, 0.55f), ErodeBright);
                }
            );
        }

        /// <summary>
        /// The guard's dome over the party: it springs up and shimmers, then either drops its
        /// hexagons (<paramref name="breaks"/>) or fades evenly (the faint rim in front).
        /// </summary>
        private void Dome(
            RectTransform layer,
            Vector2 basePoint,
            float width,
            float strength,
            bool breaks
        )
        {
            var color = Color.Lerp(ColorOf(BattleSkillVfxKind.Guard), Color.white, 0.25f);
            var shot = Spawn(
                layer,
                BattleVfxShape.Dome,
                basePoint,
                new Vector2(width, width),
                color,
                new Vector2(0.5f, 0.16f)
            );
            Animate(
                shot,
                0.95f,
                (s, k) =>
                {
                    float grow = BackOut(Mathf.Min(1f, k * 4f));
                    s.Holder.localScale = new Vector3(0.6f + 0.4f * grow, 0.3f + 0.7f * grow, 1f);
                    float shimmer = 0.88f + 0.12f * Mathf.Sin(k * 40f);
                    float come = Mathf.Min(1f, k * 8f);
                    float alpha = breaks
                        ? come
                        : strength * shimmer * (k < 0.7f ? come : 1f - (k - 0.7f) / 0.3f);
                    var tint = new Color(color.r, color.g, color.b, alpha);
                    Show(s, tint, k, breaks ? Eaten(k, 0.65f) : 0f, ErodeBright);
                }
            );
        }

        /// <summary>A magic circle lying on the floor under the user, turning as it fades in and out.</summary>
        private void Circle(Vector2 feet, Color color, float seconds)
        {
            if (backLight == null)
                return;
            var shot = Spawn(
                backLight,
                BattleVfxShape.MagicCircle,
                feet,
                new Vector2(330f, 330f),
                color
            );
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -k * 140f);
                    float grow = EaseOut(Mathf.Min(1f, k * 5f));
                    s.Holder.localScale = new Vector3(
                        0.4f + 0.6f * grow,
                        (0.4f + 0.6f * grow) * FloorTilt,
                        1f
                    );
                    // Drawn in around the circle at first, eaten away along it at the end.
                    Show(s, color, k, Eaten(k, 0.75f), ErodeBright * 1.2f);
                }
            );
        }

        /// <summary>A ring of shock spreading from a point (flattened onto the floor when <paramref name="floor"/>).</summary>
        private void Ring(
            RectTransform layer,
            Vector2 at,
            Color color,
            float size,
            float seconds,
            bool floor
        )
        {
            if (layer == null)
                return;
            var shot = Spawn(layer, BattleVfxShape.Ring, at, new Vector2(size, size), color);
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    s.Holder.localScale = RingScale(k, floor);
                    // The ring thins, breaks into arcs and dims on its own as it is used up.
                    Show(s, color, k, k);
                }
            );
        }

        /// <summary>
        /// The size of a shock ring at <paramref name="k"/> (0 to 1 of its life): it springs out
        /// fast and slows as it spreads, flattened onto the floor when <paramref name="floor"/>.
        /// </summary>
        public static Vector3 RingScale(float k, bool floor)
        {
            float grow = 0.15f + 0.85f * EaseOut(Mathf.Clamp01(k));
            return new Vector3(grow, grow * (floor ? FloorTilt : 1f), 1f);
        }

        /// <summary>A burst of light: a star of rays that flares and is eaten away to its centre.</summary>
        private void Star(Vector2 at, Color color, float size, float seconds)
        {
            var shot = Spawn(frontLight, BattleVfxShape.Star, at, new Vector2(size, size), color);
            shot.Image.rectTransform.localEulerAngles = new Vector3(
                0f,
                0f,
                UnityEngine.Random.Range(-20f, 20f)
            );
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    float grow = k < 0.2f ? 0.4f + 0.8f * (k / 0.2f) : 1.2f - 0.4f * (k - 0.2f);
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    Show(s, Color.Lerp(Color.white, color, k), k, Eaten(k, 0.1f), CoreBright);
                }
            );
        }

        /// <summary>A soft glow that swells and fades.</summary>
        private void Glow(
            RectTransform layer,
            Vector2 at,
            Color color,
            float size,
            float seconds,
            float peak
        )
        {
            if (layer == null)
                return;
            var shot = Spawn(layer, BattleVfxShape.Glow, at, new Vector2(size, size), color);
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    float grow = 0.5f + 0.7f * EaseOut(k);
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    var tint = new Color(color.r, color.g, color.b, peak * Mathf.Sin(k * Mathf.PI));
                    Show(s, tint, k, 0f);
                }
            );
        }

        /// <summary>
        /// The climax of a blow on a target: hit stop, screen shake toward the blow, a flash at the
        /// point of the blow (a star of rays for a sword, a soft white-hot glow for fire and ice),
        /// a shock ring along the floor, a pool of light under the target and a light on everyone
        /// near. The bigger skills also flash the screen; a weakness or a defeat hits harder and
        /// longer, with a second, wider ring in the skill's colour.
        /// </summary>
        private void Impact(
            RectTransform target,
            Vector2 at,
            Color color,
            BattleHitWeight weight,
            float power,
            Vector2 direction,
            bool flashScreen,
            bool rays = true
        )
        {
            HitStop(Weighted(0.04f + 0.06f * power, weight));
            Shake(Weighted(0.25f + 0.3f * power, weight), direction);
            if (flashScreen || weight != BattleHitWeight.Normal)
                ScreenFlash(Color.Lerp(Color.white, color, 0.4f), 0.14f + 0.16f * power);
            if (rays)
                Star(at, color, Weighted(150f + 110f * power, weight), 0.24f);
            else
                HotFlash(at, color, Weighted(240f, weight));
            var feet = Feet(FrontLayer, target);
            // A drawn ring suits a sword's stroke; fire and ice show their shock in dust and mist.
            if (rays)
                Ring(
                    backLight,
                    feet,
                    Color.Lerp(color, Color.white, 0.5f),
                    Weighted(360f, weight),
                    0.35f,
                    floor: true
                );
            Pool(feet, color, Weighted(320f, weight), 0.06f, 0.4f);
            Illuminate(
                at,
                Color.Lerp(color, Color.white, 0.35f),
                0.8f,
                Weighted(380f, weight),
                0.05f,
                0.3f,
                spill: false
            );
            if (rays && weight != BattleHitWeight.Normal)
                Ring(backLight, feet, color, 640f, 0.45f, floor: true);
        }

        /// <summary>
        /// A white-hot flash at the point of a blow: a soft glow, bright enough to bloom, that swells
        /// a little and is gone in a few frames.
        /// </summary>
        private void HotFlash(Vector2 at, Color color, float size)
        {
            var shot = Spawn(
                frontLight,
                BattleVfxShape.Glow,
                at,
                new Vector2(size, size),
                Color.white
            );
            Animate(
                shot,
                0.16f,
                (s, k) =>
                {
                    float grow = 0.6f + 0.6f * EaseOut(k);
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    var tint = Color.Lerp(new Color(1f, 0.97f, 0.9f), color, k);
                    tint.a = Remaining(k, 0.1f);
                    Show(s, tint, k, 0f, CoreBright);
                }
            );
        }

        /// <summary>Bright streaks flung from a hit, mostly along <paramref name="direction"/>.</summary>
        private void Sparks(Vector2 at, Color color, int count, float force, Vector2 direction)
        {
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(500f, 1300f) * force,
                    Direction = Angle(direction),
                    Spread = 150f,
                    Life = new Vector2(0.12f, 0.36f),
                    Size = new Vector2(8f, 14f),
                    Stretch = 0.06f,
                    Drag = 5f,
                    Gravity = 600f,
                    Area = 20f,
                },
                count
            );
        }

        /// <summary>
        /// Soft puffs of smoke or mist on a smoke layer (behind the light) that swell and drift up
        /// as they are eaten away.
        /// </summary>
        private void Puffs(
            RectTransform layer,
            Vector2 at,
            Color color,
            int count,
            float spread,
            float life,
            float size = 120f
        )
        {
            Spray(
                layer,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    From = color,
                    To = color,
                    Speed = new Vector2(20f, 80f),
                    Direction = 90f,
                    Spread = 140f,
                    Life = new Vector2(life * 0.6f, life),
                    Size = new Vector2(size * 0.65f, size * 1.25f),
                    Grow = 0.5f,
                    Shrink = 1.8f,
                    Gravity = -40f,
                    Drag = 1f,
                    Spin = 30f,
                    Hold = 0.25f,
                    Area = spread,
                },
                count
            );
        }

        // --- Cut-in ----------------------------------------------------------------------------

        /// <summary>
        /// A band sweeps across the middle of the screen with the user drawn large and the skill's
        /// name, over streaks that race past; then it flies off the other side.
        /// </summary>
        private IEnumerator PlayCutIn(Texture actor, string skill, Color color)
        {
            if (CutIn == null)
                yield break;
            CutIn.gameObject.SetActive(true);
            if (CutInBand != null)
                CutInBand.color = new Color(
                    color.r * 0.16f,
                    color.g * 0.16f,
                    color.b * 0.2f + 0.05f,
                    0.9f
                );
            if (CutInStreaks != null)
                CutInStreaks.color = new Color(color.r, color.g, color.b, 0.75f);
            if (CutInActor != null)
            {
                CutInActor.texture = actor;
                CutInActor.enabled = actor != null;
            }
            if (CutInName != null)
                CutInName.text = skill;
            DimTo(0.6f, 0.12f);
            ScreenFlash(color, 0.18f);

            float width = Mathf.Max(2400f, ((RectTransform)CutIn.parent).rect.width + 600f);
            const float enter = 0.12f;
            const float hold = 0.42f;
            const float leave = 0.12f;
            float total = enter + hold + leave;
            var actorHome =
                CutInActor != null ? CutInActor.rectTransform.anchoredPosition : Vector2.zero;
            var nameHome =
                CutInName != null ? CutInName.rectTransform.anchoredPosition : Vector2.zero;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float x =
                    t < enter ? -width * (1f - EaseOut(t / enter))
                    : t < enter + hold ? 0f
                    : width * EaseIn((t - enter - hold) / leave);
                CutIn.anchoredPosition = new Vector2(x, CutIn.anchoredPosition.y);
                if (CutInStreaks != null)
                {
                    var uv = CutInStreaks.uvRect;
                    uv.x -= Time.unscaledDeltaTime * 3.2f;
                    CutInStreaks.uvRect = uv;
                }
                // The user drifts forward and the name back, so the band seems to rush on.
                if (CutInActor != null)
                    CutInActor.rectTransform.anchoredPosition =
                        actorHome + Vector2.right * (t * 60f);
                if (CutInName != null)
                    CutInName.rectTransform.anchoredPosition = nameHome + Vector2.left * (t * 40f);
                yield return null;
            }
            if (CutInActor != null)
                CutInActor.rectTransform.anchoredPosition = actorHome;
            if (CutInName != null)
                CutInName.rectTransform.anchoredPosition = nameHome;
            CutIn.gameObject.SetActive(false);
        }

        // --- Shots and particles ---------------------------------------------------------------

        // How much brighter than white the light of a shape is drawn: eroding light a little, and
        // the cores (blades, bolts, fireballs, sparks) enough that the camera's Bloom spreads them.
        private const float ErodeBright = 1.2f;
        private const float CoreBright = 1.8f;

        /// <summary>The material a shape is worked out with, or null when it is missing.</summary>
        public Material MaterialOf(BattleVfxShape shape)
        {
            int index = (int)shape;
            return Shapes != null && index < Shapes.Length ? Shapes[index] : null;
        }

        /// <summary>
        /// How much of a shape is eaten away at <paramref name="k"/> (0 to 1 of its life): none
        /// until <paramref name="hold"/>, then all of it by the end.
        /// </summary>
        public static float Eaten(float k, float hold) => 1f - Remaining(k, hold);

        /// <summary>One animated shape: a holder that moves and scales, and the board in it that turns.</summary>
        private sealed class Shot
        {
            public RectTransform Holder;
            public BattleVfxImage Image;
            public bool Live;
            public float Age;
            public float Duration;
            public float Seed;
            public Action<Shot, float> Animate;
        }

        /// <summary>
        /// Shows a shot at a moment: <paramref name="tint"/> colours it (its alpha thins the whole),
        /// <paramref name="progress"/> moves its shape and <paramref name="eaten"/> wears it away.
        /// </summary>
        private static void Show(
            Shot shot,
            Color tint,
            float progress,
            float eaten,
            float bright = 1f
        )
        {
            shot.Image.color = tint;
            shot.Image.SetShape(shot.Seed, progress, eaten, bright);
        }

        private sealed class Particle
        {
            public RectTransform Rect;
            public BattleVfxImage Image;
            public bool Live;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public Vector2 Size;
            public float StartScale = 1f;
            public float EndScale = 1f;
            public Color From;
            public Color To;
            public float Angle;
            public float Spin;
            public float Gravity;
            public float Drag;
            public float Stretch;

            /// <summary>Seconds before it shows (it waits, unseen, where it starts).</summary>
            public float Delay;

            /// <summary>Coloured by the heat of a flame (white, yellow, orange, red, dark) over its life.</summary>
            public bool Fire;

            /// <summary>
            /// When at least 0, it is eaten away after this part of its life instead of fading in
            /// and out.
            /// </summary>
            public float Hold = -1f;

            /// <summary>The seed of its shape, so no two look alike.</summary>
            public float Seed;

            /// <summary>How much brighter than white its light is drawn.</summary>
            public float Bright = 1f;

            public void Apply()
            {
                Live = true;
                Age = -Delay;
                Rect.gameObject.SetActive(true);
                Step(0f);
            }

            public void Step(float dt)
            {
                Age += dt;
                if (Age < 0f)
                {
                    // Waiting to show: nothing drawn, nothing moved.
                    if (Image.color.a != 0f)
                        Image.color = Color.clear;
                    Image.SetShape(Seed, 0f, 0f, Bright);
                    return;
                }
                if (Age >= Life)
                {
                    Live = false;
                    Rect.gameObject.SetActive(false);
                    return;
                }
                Velocity += Vector2.down * (Gravity * dt);
                Velocity *= Mathf.Exp(-Drag * dt);
                Position += Velocity * dt;
                Angle += Spin * dt;
                float k = Age / Life;
                Rect.anchoredPosition = Position;
                // Swells fast at first and slows, as a billow of flame or smoke does.
                float scale = Mathf.Lerp(StartScale, EndScale, EaseOut(k));
                var size = Size * scale;
                if (Stretch > 0f)
                {
                    // A streak points along its flight and grows longer the faster it goes.
                    size.y += Velocity.magnitude * Stretch;
                    Rect.localEulerAngles = new Vector3(
                        0f,
                        0f,
                        Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg - 90f
                    );
                }
                else
                    Rect.localEulerAngles = new Vector3(0f, 0f, Angle);
                Rect.sizeDelta = size;
                Color color;
                // A flame shows its own white-hot core and colours at first, and takes on the
                // cooling red and dark more and more as it burns out.
                if (Fire)
                    color = Color.Lerp(Color.white, FireColor(k), 0.35f + 0.65f * k);
                else
                    color = Color.Lerp(From, To, k);
                float eaten = 0f;
                if (Hold >= 0f)
                {
                    // A flame's strength is all in its heat colour; smoke keeps its own density.
                    // The shape itself is worn away.
                    color.a = Fire ? 1f : From.a;
                    eaten = Eaten(k, Hold);
                }
                else
                    // Comes in over the first tenth of its life, so a burst never pops in hard.
                    color.a *= Mathf.Min(1f, k * 10f + 0.4f);
                Image.color = color;
                Image.SetShape(Seed, k, eaten, Bright);
            }
        }

        /// <summary>
        /// The colour of a flame at <paramref name="k"/> of its life: white-hot, then yellow,
        /// orange and red as it cools, and dark at the end, so it burns out instead of fading.
        /// </summary>
        public static Color FireColor(float k)
        {
            var white = new Color(1f, 0.97f, 0.85f);
            var yellow = new Color(1f, 0.82f, 0.4f);
            var orange = new Color(1f, 0.48f, 0.1f);
            var red = new Color(0.42f, 0.08f, 0.02f);
            var dark = new Color(0.08f, 0.02f, 0.01f);
            // Hot for a short while, then it cools fast: a dying flame darkens into the smoke
            // instead of lingering as a red cloud.
            if (k < 0.1f)
                return Color.Lerp(white, yellow, k / 0.1f);
            if (k < 0.3f)
                return Color.Lerp(yellow, orange, (k - 0.1f) / 0.2f);
            if (k < 0.55f)
                return Color.Lerp(orange, red, (k - 0.3f) / 0.25f);
            return Color.Lerp(red, dark, (k - 0.55f) / 0.45f);
        }

        /// <summary>How a spray of particles is thrown.</summary>
        private struct Burst
        {
            public BattleVfxShape Shape;

            /// <summary>How much brighter than white its light is drawn (0 is taken as 1).</summary>
            public float Bright;
            public Color From;
            public Color To;
            public Vector2 Speed;
            public float Direction;
            public float Spread;
            public Vector2 Life;
            public Vector2 Size;
            public float Aspect;
            public float Shrink;
            public float Gravity;
            public float Drag;
            public float Stretch;
            public float Spin;
            public float Area;

            /// <summary>Each particle waits a random time in this range (s) before it shows.</summary>
            public Vector2 Delay;

            /// <summary>Starts at this part of its size (0 means its full size) and swells or shrinks to <see cref="Shrink"/>.</summary>
            public float Grow;
            public bool Fire;

            /// <summary>0 for the default fade; otherwise eaten away after this part of its life.</summary>
            public float Hold;
        }

        private void Spray(RectTransform layer, Vector2 at, Burst spray, int count)
        {
            if (layer == null)
                return;
            for (int i = 0; i < count; i++)
            {
                var particle = Take(layer, spray.Shape);
                particle.Bright = spray.Bright > 0f ? spray.Bright : 1f;
                float angle =
                    (spray.Direction + UnityEngine.Random.Range(-0.5f, 0.5f) * spray.Spread)
                    * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                particle.Position = at + UnityEngine.Random.insideUnitCircle * spray.Area;
                particle.Velocity =
                    direction * UnityEngine.Random.Range(spray.Speed.x, spray.Speed.y);
                particle.Life = UnityEngine.Random.Range(spray.Life.x, spray.Life.y);
                float size = UnityEngine.Random.Range(spray.Size.x, spray.Size.y);
                particle.Size = new Vector2(size, size * (spray.Aspect > 0f ? spray.Aspect : 1f));
                particle.StartScale = spray.Grow > 0f ? spray.Grow : 1f;
                particle.EndScale = spray.Shrink > 0f ? spray.Shrink : 0.4f;
                particle.From = spray.From;
                particle.To = spray.To;
                particle.Delay = UnityEngine.Random.Range(spray.Delay.x, spray.Delay.y);
                particle.Fire = spray.Fire;
                particle.Hold = spray.Hold > 0f ? spray.Hold : -1f;
                particle.Angle = UnityEngine.Random.Range(0f, 360f);
                particle.Spin = UnityEngine.Random.Range(-spray.Spin, spray.Spin);
                particle.Gravity = spray.Gravity;
                particle.Drag = spray.Drag;
                particle.Stretch = spray.Stretch;
                particle.Apply();
            }
        }

        /// <summary>A free particle on the layer drawing <paramref name="shape"/>.</summary>
        private Particle Take(RectTransform layer, BattleVfxShape shape)
        {
            var found = Free(layer);
            found.Image.texture = null;
            found.Image.material = MaterialOf(shape);
            found.Seed = UnityEngine.Random.value;
            return found;
        }

        /// <summary>A free particle on the layer, made when none is left; the oldest is reused past the cap.</summary>
        private Particle Free(RectTransform layer)
        {
            Particle found = null;
            foreach (var particle in particles)
            {
                if (!particle.Live && particle.Rect.parent == layer)
                {
                    found = particle;
                    break;
                }
            }
            if (found == null && particles.Count >= MaxParticles)
            {
                float oldest = -1f;
                foreach (var particle in particles)
                {
                    float age = particle.Live ? particle.Age / particle.Life : 2f;
                    if (age > oldest)
                    {
                        oldest = age;
                        found = particle;
                    }
                }
                found.Rect.SetParent(layer, false);
            }
            if (found == null)
            {
                var rect = new GameObject(
                    "Particle",
                    typeof(RectTransform)
                ).GetComponent<RectTransform>();
                rect.SetParent(layer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                var image = rect.gameObject.AddComponent<BattleVfxImage>();
                image.raycastTarget = false;
                found = new Particle { Rect = rect, Image = image };
                particles.Add(found);
            }
            found.Image.uvRect = new Rect(0f, 0f, 1f, 1f);
            found.Image.SetShape(0f, 0f, 0f);
            found.Rect.SetAsLastSibling();
            // A reused particle forgets how it was last thrown.
            found.StartScale = 1f;
            found.Delay = 0f;
            found.Fire = false;
            found.Hold = -1f;
            found.Stretch = 0f;
            found.Seed = 0f;
            found.Bright = 1f;
            return found;
        }

        private void StepParticles(float dt)
        {
            if (dt <= 0f)
                return;
            foreach (var particle in particles)
                if (particle.Live)
                    particle.Step(dt);
        }

        /// <summary>
        /// Puts a shape on a layer at <paramref name="at"/>, its <paramref name="pivot"/> there, with
        /// a seed of its own. It stays until <see cref="Animate"/> ends it (or the caller does).
        /// </summary>
        private Shot Spawn(
            RectTransform layer,
            BattleVfxShape shape,
            Vector2 at,
            Vector2 size,
            Color color,
            Vector2? pivot = null
        )
        {
            Shot shot = null;
            foreach (var candidate in shots)
            {
                if (!candidate.Live && candidate.Holder.parent == layer)
                {
                    shot = candidate;
                    break;
                }
            }
            if (shot == null)
            {
                var holder = new GameObject(
                    "Effect",
                    typeof(RectTransform)
                ).GetComponent<RectTransform>();
                holder.SetParent(layer, false);
                holder.anchorMin = holder.anchorMax = holder.pivot = Vector2.one * 0.5f;
                holder.sizeDelta = Vector2.zero;
                var image = new GameObject(
                    "Image",
                    typeof(RectTransform)
                ).AddComponent<BattleVfxImage>();
                image.rectTransform.SetParent(holder, false);
                image.raycastTarget = false;
                shot = new Shot { Holder = holder, Image = image };
                shots.Add(shot);
            }
            shot.Live = true;
            shot.Age = 0f;
            shot.Duration = 0f;
            shot.Animate = null;
            shot.Holder.gameObject.SetActive(true);
            shot.Holder.SetAsLastSibling();
            shot.Holder.anchoredPosition = at;
            shot.Holder.localScale = Vector3.one;
            shot.Holder.localEulerAngles = Vector3.zero;
            var rect = shot.Image.rectTransform;
            var anchor = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localEulerAngles = Vector3.zero;
            shot.Image.texture = null;
            shot.Image.material = MaterialOf(shape);
            shot.Image.uvRect = new Rect(0f, 0f, 1f, 1f);
            shot.Seed = UnityEngine.Random.value;
            Show(shot, color, 0f, 0f);
            return shot;
        }

        /// <summary>Runs <paramref name="animate"/> on the shot with its progress (0 to 1) for <paramref name="seconds"/>, then ends it.</summary>
        private static void Animate(Shot shot, float seconds, Action<Shot, float> animate)
        {
            shot.Duration = Mathf.Max(0.01f, seconds);
            shot.Animate = animate;
            animate(shot, 0f);
        }

        private void StepShots(float dt)
        {
            foreach (var shot in shots)
            {
                if (!shot.Live || shot.Animate == null)
                    continue;
                shot.Age += dt;
                if (shot.Age >= shot.Duration)
                {
                    shot.Live = false;
                    shot.Holder.gameObject.SetActive(false);
                    continue;
                }
                shot.Animate(shot, shot.Age / shot.Duration);
            }
        }

        // --- Helpers ---------------------------------------------------------------------------

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
                yield return null;
        }

        /// <summary>The centre of a character's drawn body, in the layer's space.</summary>
        private static Vector2 Center(RectTransform layer, RectTransform area)
        {
            if (layer == null || area == null)
                return Vector2.zero;
            return layer.InverseTransformPoint(area.TransformPoint(area.rect.center));
        }

        /// <summary>The bottom middle of a character's drawn body (its feet), in the layer's space.</summary>
        private static Vector2 Feet(RectTransform layer, RectTransform area)
        {
            if (layer == null || area == null)
                return Vector2.zero;
            var rect = area.rect;
            return layer.InverseTransformPoint(
                area.TransformPoint(new Vector2(rect.center.x, rect.yMin))
            );
        }

        private static (Vector2 min, Vector2 max) Bounds(RectTransform layer, RectTransform area)
        {
            var rect = area.rect;
            Vector2 min = layer.InverseTransformPoint(area.TransformPoint(rect.min));
            Vector2 max = layer.InverseTransformPoint(area.TransformPoint(rect.max));
            return (Vector2.Min(min, max), Vector2.Max(min, max));
        }

        /// <summary>
        /// Moves a burst of <paramref name="size"/> toward the middle just enough to keep most of
        /// it on screen, for an enemy standing by the edge.
        /// </summary>
        private Vector2 Inside(Vector2 at, float size)
        {
            if (Stage == null || FrontLayer == null)
                return at;
            var scale = FrontLayer.lossyScale.x / Mathf.Max(0.0001f, Stage.lossyScale.x);
            var half = Stage.rect.size * 0.5f / Mathf.Max(0.0001f, scale);
            float x = Mathf.Max(0f, half.x - size * 0.4f);
            float y = Mathf.Max(0f, half.y - size * 0.35f);
            return new Vector2(Mathf.Clamp(at.x, -x, x), Mathf.Clamp(at.y, -y, y));
        }

        private static float Angle(Vector2 direction) =>
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        private static Vector2 ToDots(Vector2 offset) =>
            new Vector2(Mathf.Round(offset.x / Dot), Mathf.Round(offset.y / Dot)) * Dot;

        private static float Weighted(float value, BattleHitWeight weight) =>
            weight switch
            {
                BattleHitWeight.Weak => value * 1.35f,
                BattleHitWeight.Defeat => value * 1.5f,
                _ => value,
            };

        /// <summary>
        /// How much of an eroding shape is left at <paramref name="k"/> (0 to 1 of its life): all
        /// of it until <paramref name="hold"/>, then eaten away to nothing by the end.
        /// </summary>
        public static float Remaining(float k, float hold)
        {
            if (k <= hold)
                return 1f;
            if (hold >= 1f)
                return 0f;
            return 1f - EaseIn((k - hold) / (1f - hold)) * 0.999f;
        }

        private static float EaseOut(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        private static float EaseIn(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k;
        }

        /// <summary>An ease-out that overshoots a little and settles back, like a spring.</summary>
        private static float BackOut(float k)
        {
            const float overshoot = 1.7f;
            float t = k - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }
    }
}
