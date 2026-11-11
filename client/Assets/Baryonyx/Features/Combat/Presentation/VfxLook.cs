using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>What the look lab's sample is drawn as (the shader's _Form).</summary>
    public enum VfxLookForm
    {
        Line,
        Orb,
        Ring,
        Mass,
    }

    /// <summary>
    /// The values the look lab's sliders move, in the sliders' order. Most go to the sample's
    /// material (Vfx Look Lab shader); the floor light and the actor light are the light the
    /// sample throws on the ground and on the enemy, the hold is how far through its life the
    /// sample starts to go, the backdrop darkens the stage behind the sample, and the speed is
    /// how fast the sample plays.
    /// </summary>
    public enum VfxLookParam
    {
        EdgeSoft,
        EdgeNoise,
        BodyWidth,
        BodyOpacity,
        CoreWidth,
        CoreWhite,
        CoreBright,
        HaloWidth,
        HaloStrength,
        HazeAmount,
        HazeReach,
        HazeFill,
        HazeSoft,
        HazeGlow,
        HazeShade,
        HazeFollow,
        HazeSide,
        HazeRise,
        HazeWind,
        HazeEmit,
        EmitSpeed,
        HazeClump,
        HazeClumpSize,
        ClumpTurbulence,
        ClumpSource,
        ClumpCarry,
        ClumpSwirl,
        ClumpWake,
        ClumpBlast,
        ClumpFloor,
        NoiseScale,
        NoiseWarp,
        NoiseFlow,
        Heat,
        Rays,
        Sparks,
        FloorLight,
        ActorLight,
        SubAfter,
        SubPressure,
        SubSpiral,
        SubWake,
        SubSpread,
        SubTaper,
        SubSoft,
        SubVary,
        Additive,
        Intensity,
        Steps,
        Hold,
        DissolveSoft,
        EatSoft,
        EatNoise,
        EatOuter,
        EatWake,
        EatCarry,
        EatThin,
        EatCool,
        EatRim,
        EatLag,
        Backdrop,
        Speed,
    }

    /// <summary>
    /// The look of an effect as the look lab tunes it (doc/features/screens/debug-rooms.md): how
    /// hard or misty its edge is, and how much of it is core, halo and haze. The sliders' ranges
    /// and names, the looks to start from, the tints, and the text a look is copied as.
    /// </summary>
    public static class VfxLook
    {
        /// <summary>One slider: its group and name, its range, and the material's property.</summary>
        public readonly struct Spec
        {
            public readonly string Group;
            public readonly string Label;
            public readonly float Min;
            public readonly float Max;
            public readonly bool Whole;
            public readonly string Property;

            public Spec(
                string group,
                string label,
                float min,
                float max,
                string property,
                bool whole = false
            )
            {
                Group = group;
                Label = label;
                Min = min;
                Max = max;
                Property = property;
                Whole = whole;
            }
        }

        /// <summary>The sliders, in <see cref="VfxLookParam"/>'s order.</summary>
        public static readonly Spec[] Specs =
        {
            new("輪郭", "やわらかさ", 0f, 1f, "_EdgeSoft"),
            new("輪郭", "崩し", 0f, 1f, "_EdgeNoise"),
            new("本体", "太さ", 0f, 1f, "_BodyWidth"),
            new("本体", "濃さ", 0f, 1f, "_BodyOpacity"),
            new("芯", "太さ", 0f, 1f, "_CoreWidth"),
            new("芯", "白さ", 0f, 1f, "_CoreWhite"),
            new("芯", "明るさ", 0f, 4f, "_CoreBright"),
            new("にじみ", "広がり", 0f, 1f, "_HaloWidth"),
            new("にじみ", "強さ", 0f, 1.5f, "_HaloStrength"),
            new("もや", "量", 0f, 1f, "_HazeAmount"),
            new("もや", "広がり", 0f, 1f, "_HazeReach"),
            new("もや", "密度", 0f, 1f, "_HazeFill"),
            new("もや", "やわらかさ", 0f, 1f, "_HazeSoft"),
            new("もや", "明るさ", 0f, 1f, "_HazeGlow"),
            new("もや", "陰影", 0f, 1f, "_HazeShade"),
            new("もや", "線に沿わせる", 0f, 1f, "_HazeFollow"),
            new("もや", "内外の寄せ", -1f, 1f, "_HazeSide"),
            new("もや", "浮き沈み", -1f, 1f, "_HazeRise"),
            new("もや", "横流れ", -1f, 1f, "_HazeWind"),
            new("湧き方", "主役から湧く", 0f, 1f, "_HazeEmit"),
            new("湧き方", "湧く勢い", 0f, 1f, "_EmitSpeed"),
            new("湧き方", "濃淡の強さ", 0f, 1f, "_HazeClump"),
            new("湧き方", "濃淡の大きさ", 0f, 1f, "_HazeClumpSize"),
            new("湧き方", "空気の乱れ", 0f, 1f, "_ClumpTurbulence"),
            new("湧き方", "吹き出しのむら", 0f, 1f, "_ClumpSource"),
            new("湧き方", "流れの先", 0f, 1f, "_ClumpCarry"),
            new("湧き方", "渦", 0f, 1f, "_ClumpSwirl"),
            new("湧き方", "動きの後ろ", 0f, 1f, "_ClumpWake"),
            new("湧き方", "最初の一吹き", 0f, 1f, "_ClumpBlast"),
            new("湧き方", "床に溜まる", 0f, 1f, "_ClumpFloor"),
            new("ノイズ", "細かさ", 0f, 1f, "_NoiseScale"),
            new("ノイズ", "うねり", 0f, 1f, "_NoiseWarp"),
            new("ノイズ", "流れ", 0f, 1f, "_NoiseFlow"),
            new("色と光", "熱の色づき", 0f, 1f, "_Heat"),
            new("色と光", "光条", 0f, 1f, "_Rays"),
            new("色と光", "粒", 0f, 1f, "_Sparks"),
            new("色と光", "床の光", 0f, 2f, null),
            new("色と光", "敵への光", 0f, 2f, null),
            new("サブVFX", "残像の帯", 0f, 1f, "_SubAfter"),
            new("サブVFX", "風圧の筋", 0f, 1f, "_SubPressure"),
            new("サブVFX", "巻きつく帯", 0f, 1f, "_SubSpiral"),
            new("サブVFX", "地を這う余波", 0f, 1f, "_SubWake"),
            new("サブVFX", "広がり", 0f, 1f, "_SubSpread"),
            new("サブVFX", "端の薄れ", 0f, 1f, "_SubTaper"),
            new("サブVFX", "やわらかさ", 0f, 1f, "_SubSoft"),
            new("サブVFX", "ばらつき", 0f, 1f, "_SubVary"),
            new("仕上げ", "加算", 0f, 1f, "_Additive"),
            new("仕上げ", "明るさ", 0.2f, 4f, "_Intensity"),
            new("仕上げ", "階調の段", 0f, 6f, "_Steps", whole: true),
            new("消え方", "消え始め", 0.1f, 0.9f, null),
            new("消え方", "薄れて消える", 0f, 1f, "_DissolveSoft"),
            new("消え方", "削れ目のやわらかさ", 0f, 1f, "_EatSoft"),
            new("消え方", "むら", 0f, 1f, "_EatNoise"),
            new("消え方", "外から／芯から", -1f, 1f, "_EatOuter"),
            new("消え方", "後ろから／先から", -1f, 1f, "_EatWake"),
            new("消え方", "風上から／風下から", -1f, 1f, "_EatCarry"),
            new("消え方", "細る", 0f, 1f, "_EatThin"),
            new("消え方", "冷める", 0f, 1f, "_EatCool"),
            new("消え方", "削れ目の光", 0f, 1f, "_EatRim"),
            new("消え方", "もやの余韻", 0f, 1f, "_EatLag"),
            new("場面", "背景の暗さ", 0f, 0.85f, null),
            new("場面", "速さ", 0.25f, 2f, null),
        };

        public static int Count => Specs.Length;

        /// <summary>The forms' names, in <see cref="VfxLookForm"/>'s order.</summary>
        public static readonly string[] FormNames = { "線", "玉", "輪", "煙" };

        /// <summary>
        /// The looks to start from: a hard, flat look cut into steps;
        /// the battle's own way of drawing a stroke of light (a soft body with a faint wide
        /// halo and a thin white core, added as light and eaten away along a noise, as GlowStroke
        /// and Keep in BattleVfxShapes.cginc do), as near as this sample's sliders come to it;
        /// a bright form in a soft haze that comes out of it and is lit by it, with rays, sparks
        /// and a pool of light on the floor; and the same bright form followed, in place of the
        /// haze, by each of the sub effects: ribbons of afterimage, lines of pressure, a winding
        /// ribbon, and a wake along the floor.
        /// </summary>
        public static readonly (string Name, float[] Values)[] Presets =
        {
            (
                "くっきり",
                Look(
                    edge: (0f, 0.15f),
                    body: (0.45f, 1f),
                    core: (0.5f, 0.9f, 1.6f),
                    halo: (0.12f, 0.35f),
                    haze: (0f, 0.3f, 0.3f),
                    lit: (0.05f, 0f, 0f),
                    clump: (0f, 0.5f, 0f, 0.5f),
                    along: (0f, 0f),
                    carry: (0.3f, 0f),
                    causes: (1f, 0f, 0f, 0f, 0f, 0f, 0f),
                    noise: (0.45f, 0.4f, 0.3f),
                    light: (0f, 0f, 0f, 0.2f, 0.2f),
                    finish: (0f, 1f, 3f),
                    fade: (0.45f, 0f, 0.05f, 1f, 0f, 0f, 0f, 0f, 0f, 0.3f, 0f)
                )
            ),
            (
                "今の基準",
                Look(
                    edge: (0.3f, 0.1f),
                    body: (0.3f, 1f),
                    core: (0.3f, 0.75f, 1.8f),
                    halo: (0.25f, 0.3f),
                    haze: (0.15f, 0.35f, 0.3f),
                    lit: (0.3f, 0f, 0.2f),
                    clump: (0.3f, 0.5f, 0f, 0.5f),
                    along: (0f, 0f),
                    carry: (0.3f, 0f),
                    causes: (1f, 0f, 0f, 0f, 0f, 0f, 0f),
                    noise: (0.4f, 0.4f, 0.3f),
                    light: (0.2f, 0f, 0f, 0.3f, 0.3f),
                    finish: (1f, 1.3f, 0f),
                    fade: (0.45f, 0f, 0.3f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f),
                    backdrop: 0.4f
                )
            ),
            (
                "光ともや",
                Bright(
                    haze: 0.7f,
                    rays: 0.18f,
                    sparks: 0.3f,
                    floor: 1.4f,
                    actor: 1.1f,
                    sub: (0f, 0f, 0f, 0f, 0.5f)
                )
            ),
            ("残像の帯", Bright(sub: (1f, 0f, 0f, 0.5f, 0.5f))),
            ("風圧の筋", Bright(sub: (0f, 1f, 0f, 0.4f, 0.5f))),
            ("巻く帯", Bright(sub: (0f, 0f, 1f, 0f, 0.5f))),
            ("地の余波", Bright(sub: (0f, 0.3f, 0f, 1f, 0.6f))),
        };

        // A bright form that lights the floor and the enemy and goes with care (its light first,
        // from its start and from upwind, thinning), with the sub effects given: the base the
        // looks that differ only in what follows the form are made from.
        private static float[] Bright(
            (float After, float Pressure, float Spiral, float Wake, float Spread) sub,
            float haze = 0f,
            float rays = 0f,
            float sparks = 0.15f,
            float floor = 0.7f,
            float actor = 0.9f
        ) =>
            Look(
                edge: (0.2f, 0.1f),
                body: (0.35f, 1f),
                core: (0.4f, 0.9f, 2.2f),
                halo: (0.3f, 0.45f),
                haze: (haze, 0.42f, 0.5f),
                lit: (0.7f, 0.4f, 0.6f),
                clump: (0.8f, 0.5f, 1f, 0.55f),
                along: (0.6f, 0f),
                carry: (0.35f, 0.25f),
                causes: (0.5f, 0.7f, 0.5f, 0.25f, 0.3f, 0.7f, 0.15f),
                noise: (0.45f, 0.6f, 0.45f),
                light: (0.8f, rays, sparks, floor, actor),
                finish: (0.55f, 1.1f, 0f),
                fade: (0.4f, 0.15f, 0.3f, 0.45f, 0.35f, 0.6f, 0.4f, 0.5f, 0.7f, 0.5f, 0.6f),
                backdrop: 0.5f,
                sub: sub
            );

        // How far the sub effects thin out toward their ends, how soft their edges are, and how
        // much each ribbon and line differs from the next, in every look to start from: sharp,
        // even ribbons at even gaps read as stuck on beside the form.
        private const float SubTaperStart = 0.6f;
        private const float SubSoftStart = 0.8f;
        private const float SubVaryStart = 0.6f;

        // A look's values in VfxLookParam's order, named by the group they belong to.
        private static float[] Look(
            (float Soft, float Noise) edge,
            (float Width, float Opacity) body,
            (float Width, float White, float Bright) core,
            (float Width, float Strength) halo,
            (float Amount, float Reach, float Fill) haze,
            (float Soft, float Glow, float Shade) lit,
            (float Amount, float Size, float Emit, float Speed) clump,
            (float Follow, float Side) along,
            (float Rise, float Wind) carry,
            (
                float Turbulence,
                float Source,
                float Carry,
                float Swirl,
                float Wake,
                float Blast,
                float Floor
            ) causes,
            (float Scale, float Warp, float Flow) noise,
            (float Heat, float Rays, float Sparks, float Floor, float Actor) light,
            (float Additive, float Intensity, float Steps) finish,
            (
                float Hold,
                float Even,
                float Soft,
                float Noise,
                float Outer,
                float Wake,
                float Carry,
                float Thin,
                float Cool,
                float Rim,
                float Lag
            ) fade,
            float backdrop = 0.35f,
            float speed = 1f,
            (float After, float Pressure, float Spiral, float Wake, float Spread)? sub = null
        ) =>
            new[]
            {
                edge.Soft,
                edge.Noise,
                body.Width,
                body.Opacity,
                core.Width,
                core.White,
                core.Bright,
                halo.Width,
                halo.Strength,
                haze.Amount,
                haze.Reach,
                haze.Fill,
                lit.Soft,
                lit.Glow,
                lit.Shade,
                along.Follow,
                along.Side,
                carry.Rise,
                carry.Wind,
                clump.Emit,
                clump.Speed,
                clump.Amount,
                clump.Size,
                causes.Turbulence,
                causes.Source,
                causes.Carry,
                causes.Swirl,
                causes.Wake,
                causes.Blast,
                causes.Floor,
                noise.Scale,
                noise.Warp,
                noise.Flow,
                light.Heat,
                light.Rays,
                light.Sparks,
                light.Floor,
                light.Actor,
                sub?.After ?? 0f,
                sub?.Pressure ?? 0f,
                sub?.Spiral ?? 0f,
                sub?.Wake ?? 0f,
                sub?.Spread ?? 0.5f,
                SubTaperStart,
                SubSoftStart,
                SubVaryStart,
                finish.Additive,
                finish.Intensity,
                finish.Steps,
                fade.Hold,
                fade.Even,
                fade.Soft,
                fade.Noise,
                fade.Outer,
                fade.Wake,
                fade.Carry,
                fade.Thin,
                fade.Cool,
                fade.Rim,
                fade.Lag,
                backdrop,
                speed,
            };

        /// <summary>The look a lab opens with the first time: the battle's own.</summary>
        public const int DefaultPreset = 1;

        /// <summary>
        /// Where the stroke's tip lies on the sample's board (its half size is 1, up is +y,
        /// before the board is tilted): where the wake along the floor starts. It follows the
        /// arc in the Vfx Look Lab shader (centre (0, -0.78), radius 0.98, 0.86 rad each way).
        /// </summary>
        public static readonly Vector2 LineTip = new(
            0.98f * Mathf.Sin(0.86f),
            -0.78f + 0.98f * Mathf.Cos(0.86f)
        );

        /// <summary>The colours a sample can be tinted: the elements' lights, and a smoke's grey.</summary>
        public static readonly (string Name, Color Color)[] Tints =
        {
            ("炎", new Color(1f, 0.47f, 0.16f)),
            ("氷", new Color(0.45f, 0.82f, 1f)),
            ("雷", new Color(1f, 0.9f, 0.4f)),
            ("癒し", new Color(0.5f, 1f, 0.62f)),
            ("闇", new Color(0.62f, 0.36f, 1f)),
            ("煙", new Color(0.42f, 0.42f, 0.45f)),
        };

        /// <summary>A preset's values, as a copy that can be changed.</summary>
        public static float[] Preset(int index) =>
            (float[])Presets[Mathf.Clamp(index, 0, Presets.Length - 1)].Values.Clone();

        /// <summary>A value kept inside its slider's range, and whole where the slider is.</summary>
        public static float Clamp(VfxLookParam param, float value)
        {
            var spec = Specs[(int)param];
            value = Mathf.Clamp(value, spec.Min, spec.Max);
            return spec.Whole ? Mathf.Round(value) : value;
        }

        /// <summary>
        /// The values as one line to remember between Plays ("0.3,0.1,..."), read back by
        /// <see cref="Parse"/>.
        /// </summary>
        public static string Join(float[] values) =>
            string.Join(
                ",",
                values.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture))
            );

        /// <summary>
        /// The values of a line <see cref="Join"/> made, each kept inside its range. Null when
        /// the line is not one of this many values (an older lab's, or none).
        /// </summary>
        public static float[] Parse(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;
            var parts = line.Split(',');
            if (parts.Length != Count)
                return null;
            var values = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                if (
                    !float.TryParse(
                        parts[i],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float value
                    )
                )
                    return null;
                values[i] = Clamp((VfxLookParam)i, value);
            }
            return values;
        }

        /// <summary>
        /// A look written out to be pasted into a chat or a note: the form and the tint, then a
        /// group to a line ("輪郭　やわらかさ 0.30　崩し 0.10").
        /// </summary>
        public static string Describe(VfxLookForm form, int tint, float[] values)
        {
            var text = new StringBuilder();
            text.Append("形 ").Append(FormNames[(int)form]);
            text.Append("　色 ").Append(Tints[Mathf.Clamp(tint, 0, Tints.Length - 1)].Name);
            string group = null;
            for (int i = 0; i < Count; i++)
            {
                var spec = Specs[i];
                if (spec.Group != group)
                {
                    group = spec.Group;
                    text.Append('\n').Append(group);
                }
                text.Append('　').Append(spec.Label).Append(' ').Append(Format(spec, values[i]));
            }
            return text.ToString();
        }

        /// <summary>A value as its slider shows it: whole, or to two places.</summary>
        public static string Format(Spec spec, float value) =>
            value.ToString(spec.Whole ? "0" : "0.00", CultureInfo.InvariantCulture);
    }
}
