using System;
using System.Collections;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>What an enemy's technique really is, as its blow is drawn.</summary>
    public enum EnemyBlow
    {
        /// <summary>Claws or a blade raking across (the default for a blow).</summary>
        Claw,

        /// <summary>Jaws snapping shut.</summary>
        Bite,

        /// <summary>A thrust or a sting driven straight in.</summary>
        Pierce,

        /// <summary>A body, a fist or a weight slamming into it.</summary>
        Blunt,

        /// <summary>Rock or earth: thrown stones, a heaving floor, a cave-in.</summary>
        Rock,

        Fire,

        /// <summary>Ice, frost, snow and a cold breath.</summary>
        Ice,

        Thunder,

        /// <summary>Poison, spores, ink and rot.</summary>
        Poison,

        /// <summary>Water, mud, sand and nectar.</summary>
        Water,

        Wind,

        /// <summary>A shriek or a howl: a wave of pressure.</summary>
        Sound,

        /// <summary>A spell, a curse or an enchanted light (the default for magic).</summary>
        Magic,
    }

    public sealed partial class BattleSkillVfx
    {
        // The words a technique's name is read by, first match wins: the element before the shape
        // of the blow, so 火の玉 burns and つらら刺し freezes, and spells before water, so 水晶 is
        // read as a crystal's spell; a will-o'-the-wisp is a spell, not a fire, though it says 火.
        private static readonly (string[] Words, EnemyBlow Blow)[] EnemyBlowWords =
        {
            (new[] { "迷い火" }, EnemyBlow.Magic),
            (new[] { "火", "炎", "燃", "焔", "溶岩", "熱", "炉", "延焼" }, EnemyBlow.Fire),
            (new[] { "氷", "冷", "霜", "雪", "つらら", "白霧" }, EnemyBlow.Ice),
            (new[] { "電", "雷", "放電" }, EnemyBlow.Thunder),
            (new[] { "毒", "胞子", "墨", "朽ち" }, EnemyBlow.Poison),
            (
                new[] { "詠唱", "呪", "光", "時", "刻印", "水晶", "迷い火", "ものまね", "釣り糸" },
                EnemyBlow.Magic
            ),
            (new[] { "泥", "砂", "波", "水", "露", "蜜", "蒸気" }, EnemyBlow.Water),
            (new[] { "風", "羽ばたき", "息吹" }, EnemyBlow.Wind),
            (new[] { "金切り声", "遠吠え", "合唱", "声" }, EnemyBlow.Sound),
            (
                new[] { "岩", "石", "地", "崩し", "突き上げ", "黒曜", "つぶて", "投げ" },
                EnemyBlow.Rock
            ),
            (new[] { "かみ", "噛", "牙", "丸のみ", "喉笛", "吸い" }, EnemyBlow.Bite),
            (new[] { "突き", "針", "槍", "刺し" }, EnemyBlow.Pierce),
            (new[] { "爪", "引き裂き", "斬り", "なぎ払い", "はさみ", "乱れ打ち" }, EnemyBlow.Claw),
            (
                new[]
                {
                    "たいあたり",
                    "体当たり",
                    "一撃",
                    "打ち",
                    "砕き",
                    "のしかかり",
                    "回転",
                    "ころがり",
                    "足払い",
                    "急降下",
                    "拳",
                    "鉄槌",
                    "叩",
                },
                EnemyBlow.Blunt
            ),
        };

        /// <summary>
        /// What the technique named <paramref name="skill"/> really is, read from its name; a
        /// name it cannot read is a spell when <paramref name="magic"/> and a rake of claws
        /// otherwise.
        /// </summary>
        public static EnemyBlow EnemyBlowOf(string skill, bool magic)
        {
            if (!string.IsNullOrEmpty(skill))
                foreach (var (words, blow) in EnemyBlowWords)
                foreach (var word in words)
                    if (skill.Contains(word, StringComparison.Ordinal))
                        return blow;
            return magic ? EnemyBlow.Magic : EnemyBlow.Claw;
        }

        // The colour an enemy's spell glows in, read from its name: a curse purple, a light gold.
        private static Color EnemySpellColor(string skill)
        {
            if (string.IsNullOrEmpty(skill))
                return Violet;
            if (skill.Contains("光", StringComparison.Ordinal))
                return new Color(1f, 0.82f, 0.45f);
            if (skill.Contains("水晶", StringComparison.Ordinal))
                return new Color(0.55f, 0.9f, 1f);
            if (skill.Contains("迷い火", StringComparison.Ordinal))
                return new Color(0.5f, 0.95f, 0.75f);
            return Violet;
        }

        /// <summary>
        /// An enemy's blow landing on an ally, drawn as what its technique
        /// <paramref name="skill"/> really is (<see cref="EnemyBlowOf"/>), each built as real ones
        /// look, of about four layers that come and go at their own times
        /// (doc/features/screens/battle-effects.md#敵の技の演出): claws rake three streaks across,
        /// jaws close from above and below and snap, a thrust drives a needle of light straight in
        /// and out the far side, a slam knocks dust off the body and along the floor, a thrown stone
        /// bursts into heavy chunks and fast chips, fire bursts and smokes, ice freezes the floor
        /// and juts up, lightning comes down the same channel twice from the attacker, poison
        /// spatters and bubbles in a heavy gas, water splashes and leaves a puddle, sand sweeps
        /// in, wind streams through, a shriek pulses the dust, and a spell bursts in the way its
        /// kind does. A breath, a throw or a bolt comes from <paramref name="attacker"/> (its
        /// drawn body, when known). Milder than the party's skills: short stops and shakes, and
        /// no flash of the screen but the light of lightning.
        /// </summary>
        public void Strike(
            RectTransform target,
            string skill = null,
            RectTransform attacker = null,
            bool magic = false
        )
        {
            if (FrontLayer == null || target == null)
                return;
            var at = Center(FrontLayer, target);
            var feet = Feet(FrontLayer, target);
            var from = attacker != null ? Center(FrontLayer, attacker) : at + Vector2.right * 420f;
            var toward = at - from;
            toward = toward.sqrMagnitude > 1f ? toward.normalized : Vector2.left;
            bool breath =
                skill != null
                && (
                    skill.Contains("息", StringComparison.Ordinal)
                    || skill.Contains("吐き", StringComparison.Ordinal)
                    || skill.Contains("霧", StringComparison.Ordinal)
                );
            switch (EnemyBlowOf(skill, magic))
            {
                case EnemyBlow.Bite:
                    BiteBlow(at, feet, toward);
                    break;
                case EnemyBlow.Pierce:
                    PierceBlow(at, feet, toward);
                    break;
                case EnemyBlow.Blunt:
                    BluntBlow(at, feet, toward);
                    break;
                case EnemyBlow.Rock:
                    RockBlow(at, feet, toward);
                    break;
                case EnemyBlow.Fire:
                    FireBlow(at, feet, from, toward, breath);
                    break;
                case EnemyBlow.Ice:
                    IceBlow(at, feet, from, toward, breath);
                    break;
                case EnemyBlow.Thunder:
                    ThunderBlow(at, feet, from);
                    break;
                case EnemyBlow.Poison:
                    PoisonBlow(at, feet, toward);
                    break;
                case EnemyBlow.Water:
                    if (skill != null && skill.Contains("砂", StringComparison.Ordinal))
                        SandBlow(at, feet, toward);
                    else
                        WaterBlow(
                            at,
                            feet,
                            toward,
                            skill != null && skill.Contains("泥", StringComparison.Ordinal)
                        );
                    break;
                case EnemyBlow.Wind:
                    WindBlow(at, feet, toward);
                    break;
                case EnemyBlow.Sound:
                    SoundBlow(at, feet, from, toward);
                    break;
                case EnemyBlow.Magic:
                    MagicBlow(at, feet, skill, toward);
                    break;
                default:
                    ClawBlow(at, feet, toward);
                    break;
            }
        }

        // The streak of an enemy's claws: a warm, pale red, so a blow on the party reads at once.
        private static readonly Color ClawStreak = new(1f, 0.55f, 0.5f);

        // The streak of jaws: a deeper red than claws, as a bite reads as flesh, not a blade.
        private static readonly Color JawStreak = new(1f, 0.38f, 0.36f);

        // The sparks claws and stings strike off armour.
        private static readonly Color ClawSpark = new(1f, 0.6f, 0.35f);

        // The looks of a slash arc's layers (vfx-shapes adds them to the shader; until then each
        // is drawn as the whole arc, so the layers still differ by their timing and strength).
        private const float RakeBody = 1f;
        private const float RakeHaze = 2f;
        private const float RakeCore = 3f;
        private const float RakeTrace = 4f;

        /// <summary>
        /// Claws raking across, as a real swipe looks: three streaks one just after another, each
        /// in four layers on one seed (a soft haze of the swipe behind, the streak itself, its
        /// white-hot edge only as it is drawn, and a thin trace left in the eye that breaks up
        /// slowest), the middle claw behind the ally and the outer two before it; a few sparks off
        /// armour, the dust the swipe's wind flicks along the floor, and a small light on the ally
        /// and the floor. No mist: a swipe makes none.
        /// </summary>
        private void ClawBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            for (int i = 0; i < 3; i++)
            {
                var layer = i == 1 ? backLight : frontLight;
                var center = at + new Vector2(-10f + i * 14f, 26f - i * 26f);
                float angle = 200f + i * 4f;
                float scale = 0.55f - i * 0.04f;
                float delay = i * 0.025f;
                float seed = UnityEngine.Random.value;
                var trace = new Color(ClawStreak.r, ClawStreak.g, ClawStreak.b, 0.85f);
                var haze = new Color(ClawStreak.r, ClawStreak.g, ClawStreak.b, 0.5f);
                BlowRake(
                    backLight,
                    center,
                    angle,
                    true,
                    haze,
                    scale,
                    RakeHaze,
                    seed,
                    delay,
                    0f,
                    0.36f,
                    0.2f,
                    0.2f,
                    1f
                );
                BlowRake(
                    layer,
                    center,
                    angle,
                    true,
                    trace,
                    scale,
                    RakeTrace,
                    seed,
                    delay + 0.04f,
                    0.04f,
                    0.55f,
                    0.2f,
                    0.25f,
                    1.6f
                );
                BlowRake(
                    layer,
                    center,
                    angle,
                    true,
                    ClawStreak,
                    scale,
                    RakeBody,
                    seed,
                    delay,
                    0f,
                    0.24f,
                    0.2f,
                    0.3f,
                    CoreBright
                );
                BlowRake(
                    layer,
                    center,
                    angle,
                    true,
                    Color.white,
                    scale,
                    RakeCore,
                    seed,
                    delay,
                    0f,
                    0.1f,
                    0.2f,
                    0.4f,
                    2.6f
                );
            }
            Sparks(at, ClawSpark, 6, 0.6f, toward);
            DustKick(feet, toward, 150f);
            Illuminate(at, ClawStreak, 0.45f, 240f, 0.04f, 0.22f, spill: false);
            BlowPool(feet, ClawStreak, 240f, 0.35f, 0.04f, 0.22f);
            HitStop(0.05f);
            Shake(0.3f, toward);
        }

        // How long jaws take to close before they snap (s).
        private const float JawClose = 0.06f;

        /// <summary>
        /// Jaws snapping shut, as a bite is: the upper and lower jaw close on the ally from above
        /// and below (not a sweep across, as claws are) and meet with a snap: a dark-red haze, the
        /// jaws' streaks with their white edges flashing only at the snap, a row of tooth glints
        /// along each, a spatter of drops falling back under their weight, and a jolt down.
        /// </summary>
        private void BiteBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            for (int jaw = 0; jaw < 2; jaw++)
            {
                bool upper = jaw == 0;
                var center = at + (upper ? Vector2.up : Vector2.down) * 30f;
                var travel = (upper ? Vector2.up : Vector2.down) * 50f;
                float angle = upper ? 180f : 0f;
                float seed = UnityEngine.Random.value;
                var layer = upper ? backLight : frontLight;
                var trace = new Color(JawStreak.r, JawStreak.g, JawStreak.b, 0.85f);
                var haze = new Color(JawStreak.r, JawStreak.g, JawStreak.b, 0.45f);
                BlowRake(
                    backLight,
                    center,
                    angle,
                    false,
                    haze,
                    0.48f,
                    RakeHaze,
                    seed,
                    0f,
                    0f,
                    JawClose + 0.3f,
                    JawClose,
                    0.3f,
                    1f,
                    travel
                );
                BlowRake(
                    layer,
                    center,
                    angle,
                    false,
                    trace,
                    0.48f,
                    RakeTrace,
                    seed,
                    JawClose,
                    JawClose,
                    0.5f,
                    JawClose,
                    0.25f,
                    1.6f,
                    travel
                );
                BlowRake(
                    layer,
                    center,
                    angle,
                    false,
                    JawStreak,
                    0.48f,
                    RakeBody,
                    seed,
                    0f,
                    0f,
                    JawClose + 0.18f,
                    JawClose,
                    0.4f,
                    CoreBright,
                    travel
                );
                BlowRake(
                    frontLight,
                    center,
                    angle,
                    false,
                    Color.white,
                    0.48f,
                    RakeCore,
                    seed,
                    JawClose - 0.01f,
                    JawClose - 0.01f,
                    0.07f,
                    JawClose,
                    0.3f,
                    2.6f
                );
                // A tooth glint every third of the jaw, at the snap.
                for (int tooth = 0; tooth < 4; tooth++)
                    Spray(
                        frontLight,
                        at + new Vector2(-48f + tooth * 32f, upper ? 14f : -14f),
                        new Burst
                        {
                            Shape = BattleVfxShape.Spark,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(JawStreak.r, JawStreak.g, JawStreak.b, 0f),
                            Speed = new Vector2(0f, 10f),
                            Direction = upper ? 270f : 90f,
                            Life = new Vector2(0.1f, 0.14f),
                            Size = new Vector2(7f, 9f),
                            Aspect = 3f,
                            Level = true,
                            Delay = new Vector2(JawClose, JawClose + 0.02f),
                        },
                        1
                    );
            }
            // The spatter: fine drops flung and fast, and a few heavy ones that fall soon.
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    From = new Color(0.9f, 0.2f, 0.18f),
                    To = new Color(0.5f, 0.08f, 0.06f, 0f),
                    Speed = new Vector2(160f, 420f),
                    Direction = Angle(toward),
                    Spread = 120f,
                    Life = new Vector2(0.3f, 0.5f),
                    Size = new Vector2(4f, 7f),
                    Gravity = 1400f,
                    Area = 20f,
                    Delay = new Vector2(JawClose, JawClose + 0.03f),
                },
                10
            );
            Spray(
                frontSmoke,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    From = new Color(0.55f, 0.08f, 0.07f),
                    To = new Color(0.3f, 0.04f, 0.03f, 0f),
                    Speed = new Vector2(60f, 180f),
                    Direction = Angle(toward * 0.5f + Vector2.up * 0.5f),
                    Spread = 90f,
                    Life = new Vector2(0.5f, 0.75f),
                    Size = new Vector2(9f, 13f),
                    Gravity = 1100f,
                    Drag = 1.5f,
                    Area = 16f,
                    Delay = new Vector2(JawClose, JawClose + 0.04f),
                },
                4
            );
            BlowAfter(
                JawClose,
                () =>
                {
                    Illuminate(at, JawStreak, 0.4f, 220f, 0.03f, 0.2f, spill: false);
                    BlowPool(feet, JawStreak, 200f, 0.25f, 0.03f, 0.2f);
                    HitStop(0.06f);
                    Shake(0.35f, Vector2.down);
                }
            );
        }

        /// <summary>
        /// A thrust or a sting driven straight in, as a real one is: a needle of light rushes in
        /// along the thrust and stops in the ally, a thin trace of its path is left that breaks up,
        /// a soft haze lies along the path behind, a glint flares at the point where it goes in,
        /// and sparks and a puff of dust burst out the far side.
        /// </summary>
        private void PierceBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            const float drive = 0.05f;
            var hot = Color.Lerp(ClawStreak, Color.white, 0.55f);
            BlowHaze(
                backLight,
                at - toward * 240f,
                at + toward * 40f,
                80f,
                ClawStreak,
                0.3f,
                0f,
                0.3f
            );
            Line(frontLight, at - toward * 240f, at + toward * 30f, ClawStreak, 12f, 0.45f, 0.3f);
            BlowNeedle(at - toward * 200f, at + toward * 26f, hot, drive);
            Twinkle(at, hot, 120f, 0.16f, drive, 1f);
            Spray(
                frontLight,
                at + toward * 24f,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(ClawSpark.r, ClawSpark.g, ClawSpark.b, 0f),
                    Speed = new Vector2(500f, 1100f),
                    Direction = Angle(toward),
                    Spread = 50f,
                    Life = new Vector2(0.12f, 0.3f),
                    Size = new Vector2(6f, 10f),
                    Stretch = 0.05f,
                    Drag = 5f,
                    Gravity = 500f,
                    Area = 10f,
                    Delay = new Vector2(drive, drive + 0.02f),
                },
                10
            );
            Spray(
                frontSmoke,
                at + toward * 30f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.45f,
                    From = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.35f),
                    To = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.35f),
                    Speed = new Vector2(200f, 400f),
                    Direction = Angle(toward),
                    Spread = 40f,
                    Life = new Vector2(0.35f, 0.55f),
                    Size = new Vector2(50f, 80f),
                    Grow = 0.4f,
                    Shrink = 1.6f,
                    Drag = 5f,
                    Hold = 0.3f,
                    Delay = new Vector2(drive, drive + 0.03f),
                },
                2
            );
            BlowAfter(
                drive,
                () =>
                {
                    Illuminate(at, hot, 0.45f, 220f, 0.03f, 0.2f, spill: false);
                    BlowPool(feet, ClawStreak, 180f, 0.25f, 0.03f, 0.18f);
                    HitStop(0.05f);
                    Shake(0.25f, toward);
                }
            );
        }

        /// <summary>
        /// A slam, as a real blow of a weight is: a white-hot flash at the point of contact in a
        /// soft warm glow, dust knocked off the body along the blow, grit flung that way, the
        /// shock driving dust out along the floor, and dust settling a moment later; a heavy jolt.
        /// </summary>
        private void BluntBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            var warm = new Color(1f, 0.72f, 0.55f);
            HotFlash(at, warm, 190f);
            Glow(backLight, at, warm, 300f, 0.28f, 0.35f);
            for (int front = 0; front < 2; front++)
                Spray(
                    front == 0 ? backSmoke : frontSmoke,
                    at + toward * 10f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = 0.5f,
                        From = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.5f),
                        To = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.5f),
                        Speed = new Vector2(300f, 650f),
                        Direction = Angle(toward),
                        Spread = 70f,
                        Life = new Vector2(0.35f, 0.6f),
                        Size = new Vector2(70f, 120f) * (front == 0 ? 1f : 0.7f),
                        Grow = 0.4f,
                        Shrink = 1.7f,
                        Drag = 5f,
                        Hold = 0.25f,
                        Area = 20f,
                        Delay = new Vector2(0f, 0.03f),
                    },
                    3
                );
            Spray(
                frontSmoke,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = new Color(0.5f, 0.42f, 0.34f),
                    To = new Color(0.34f, 0.28f, 0.22f),
                    Speed = new Vector2(300f, 700f),
                    Direction = Angle(toward * 0.8f + Vector2.up * 0.2f),
                    Spread = 70f,
                    Life = new Vector2(0.35f, 0.6f),
                    Size = new Vector2(5f, 10f),
                    Gravity = 1600f,
                    Drag = 0.6f,
                    Spin = 540f,
                    Area = 16f,
                },
                8
            );
            Shockwave(feet, 320f, 0.8f);
            Spray(
                backSmoke,
                feet + Vector2.up * 10f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.45f,
                    From = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.4f),
                    To = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.4f),
                    Speed = new Vector2(20f, 60f),
                    Direction = 90f,
                    Spread = 60f,
                    Life = new Vector2(0.8f, 1.1f),
                    Size = new Vector2(90f, 140f),
                    Aspect = 0.6f,
                    Grow = 0.5f,
                    Shrink = 1.5f,
                    Gravity = -20f,
                    Drag = 1.5f,
                    Hold = 0.3f,
                    Area = 50f,
                    Delay = new Vector2(0.1f, 0.2f),
                },
                3
            );
            Illuminate(at, warm, 0.4f, 240f, 0.03f, 0.2f, spill: false);
            BlowPool(feet, warm, 220f, 0.3f, 0.03f, 0.2f);
            HitStop(0.07f);
            Shake(0.45f, toward);
        }

        // How long a thrown stone takes over its last stretch to the ally (s).
        private const float StoneFlight = 0.07f;

        /// <summary>
        /// Rock and earth, as a thrown stone is: it flies in over its last stretch with a wake of
        /// dust and bursts on the ally into a few heavy chunks that fly slow and low and many small
        /// chips that fly fast (their tops lit, so they read on the dark floor), a cloud of its
        /// dust thrown out and settling, the shock along the floor, and the floor cracked.
        /// </summary>
        private void RockBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            var start = at - toward * 300f + Vector2.up * 70f;
            if (frontSmoke != null)
            {
                var stone = Spawn(
                    frontSmoke,
                    BattleVfxShape.Chip,
                    start,
                    Vector2.one * 46f,
                    new Color(0.62f, 0.54f, 0.45f)
                );
                Animate(
                    stone,
                    StoneFlight,
                    (s, k) =>
                    {
                        s.Holder.anchoredPosition =
                            Vector2.Lerp(start, at, k) + Vector2.up * (20f * k * (1f - k));
                        s.Holder.localEulerAngles = new Vector3(0f, 0f, -720f * k);
                        Show(s, new Color(0.62f, 0.54f, 0.45f), k, 0f);
                    }
                );
            }
            Spray(
                backSmoke,
                start,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.45f,
                    From = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.25f),
                    To = new Color(KickedDust.r, KickedDust.g, KickedDust.b, 0.25f),
                    Speed = new Vector2(3000f, 3600f),
                    Direction = Angle(at - start),
                    Spread = 4f,
                    Life = new Vector2(0.3f, 0.45f),
                    Size = new Vector2(50f, 80f),
                    Grow = 0.5f,
                    Shrink = 1.4f,
                    Drag = 12f,
                    Hold = 0.2f,
                    Delay = new Vector2(0f, StoneFlight),
                },
                3
            );
            BlowAfter(StoneFlight, () => RockBurst(at, feet, toward));
        }

        private void RockBurst(Vector2 at, Vector2 feet, Vector2 toward)
        {
            Star(at, new Color(1f, 0.85f, 0.7f), 120f, 0.1f);
            var up = Angle(toward * 0.3f + Vector2.up * 0.7f);
            // Heavy chunks, slow and low; some behind the ally.
            for (int front = 0; front < 2; front++)
                Spray(
                    front == 0 ? backSmoke : frontSmoke,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Chip,
                        From = new Color(0.8f, 0.7f, 0.58f),
                        To = new Color(0.44f, 0.37f, 0.3f),
                        Speed = new Vector2(160f, 380f),
                        Direction = up,
                        Spread = 150f,
                        Life = new Vector2(0.6f, 0.9f),
                        Size = new Vector2(20f, 32f),
                        Gravity = 1900f,
                        Drag = 0.3f,
                        Spin = 260f,
                        Area = 24f,
                    },
                    3
                );
            // Small chips, fast and far.
            Spray(
                frontSmoke,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = new Color(0.82f, 0.72f, 0.6f),
                    To = new Color(0.44f, 0.37f, 0.3f),
                    Speed = new Vector2(450f, 950f),
                    Direction = up,
                    Spread = 170f,
                    Life = new Vector2(0.35f, 0.6f),
                    Size = new Vector2(6f, 12f),
                    Gravity = 1600f,
                    Drag = 0.8f,
                    Spin = 720f,
                    Area = 20f,
                },
                14
            );
            Spray(
                backSmoke,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Material = RichMaterialOf(BattleVfxShape.Smoke),
                    Bright = 0.6f,
                    From = KickedDust,
                    To = KickedDust,
                    Speed = new Vector2(140f, 380f),
                    Direction = Angle(toward * 0.5f + Vector2.up * 0.5f),
                    Spread = 200f,
                    Life = new Vector2(0.7f, 1.1f),
                    Size = new Vector2(110f, 190f),
                    Grow = 0.6f,
                    Shrink = 1.6f,
                    Gravity = 60f,
                    Drag = 3f,
                    Hold = 0.3f,
                    Area = 24f,
                    Delay = new Vector2(0.02f, 0.08f),
                },
                5
            );
            Shockwave(feet, 340f, 0.8f);
            Fissure(backSmoke, feet, 260f, 1.4f);
            Illuminate(at, new Color(1f, 0.8f, 0.6f), 0.3f, 220f, 0.02f, 0.15f, spill: false);
            HitStop(0.07f);
            Shake(0.5f, Vector2.down);
        }

        /// <summary>
        /// Fire, as a real burst of it is: a white-hot flash, lumps of flame that swell, roll up
        /// and cool from white through orange to dark, most of them behind the ally and a few
        /// small ones before it so it is seen in the blaze; fast sparks at once and slow embers
        /// floating up after; dark smoke rising behind once the flame has cooled; a scorch on the
        /// floor and a flickering light. A breath streams from the attacker first.
        /// </summary>
        private void FireBlow(Vector2 at, Vector2 feet, Vector2 from, Vector2 toward, bool breath)
        {
            var orange = ColorOf(BattleSkillVfxKind.Fire);
            if (breath)
                Spray(
                    frontLight,
                    from + toward * 80f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Flame,
                        Fire = true,
                        Speed = new Vector2(900f, 1300f),
                        Direction = Angle(toward),
                        Spread = 14f,
                        Life = new Vector2(0.25f, 0.45f),
                        Size = new Vector2(60f, 110f),
                        Grow = 0.4f,
                        Shrink = 1.6f,
                        Drag = 1.5f,
                        Spin = 60f,
                        Area = 20f,
                    },
                    14
                );
            HotFlash(at, orange, 220f);
            for (int front = 0; front < 2; front++)
                Spray(
                    front == 0 ? backLight : frontLight,
                    at + (front == 0 ? Vector2.up * 20f : Vector2.down * 10f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Flame,
                        Fire = true,
                        Speed = new Vector2(60f, 260f) * (front == 0 ? 1f : 0.6f),
                        Direction = 90f,
                        Spread = 160f,
                        Life = new Vector2(0.4f, 0.7f) * (front == 0 ? 1f : 0.6f),
                        Size = new Vector2(80f, 140f) * (front == 0 ? 1f : 0.55f),
                        Grow = 0.4f,
                        Shrink = 1.5f,
                        Gravity = -260f,
                        Drag = 2f,
                        Spin = 60f,
                        Hold = 0.2f,
                        Area = front == 0 ? 50f : 30f,
                        Delay = new Vector2(0f, 0.06f),
                    },
                    front == 0 ? 7 : 3
                );
            Sparks(at, orange, 10, 0.7f, Vector2.up);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = CoreBright,
                    From = new Color(1f, 0.7f, 0.3f),
                    To = new Color(0.7f, 0.12f, 0.03f, 0f),
                    Speed = new Vector2(30f, 110f),
                    Direction = 90f,
                    Spread = 120f,
                    Life = new Vector2(0.6f, 1f),
                    Size = new Vector2(5f, 9f),
                    Gravity = -140f,
                    Drag = 1.2f,
                    Area = 60f,
                    Delay = new Vector2(0.15f, 0.45f),
                },
                10
            );
            Spray(
                backSmoke,
                at + Vector2.up * 50f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.35f,
                    From = new Color(0.2f, 0.16f, 0.14f, 0.7f),
                    To = new Color(0.16f, 0.15f, 0.15f, 0.7f),
                    Speed = new Vector2(40f, 110f),
                    Direction = 90f,
                    Spread = 50f,
                    Life = new Vector2(1.1f, 1.5f),
                    Size = new Vector2(110f, 170f),
                    Grow = 0.5f,
                    Shrink = 1.7f,
                    Gravity = -60f,
                    Drag = 1f,
                    Spin = 20f,
                    Hold = 0.3f,
                    Area = 40f,
                    Delay = new Vector2(0.15f, 0.35f),
                },
                4
            );
            FloorMark(
                BattleVfxShape.Scorch,
                feet,
                new Vector2(200f, 64f),
                new Color(0.12f, 0.08f, 0.06f),
                0.7f,
                1.4f,
                flat: false
            );
            Illuminate(at, orange, 0.8f, 320f, 0.15f, 0.4f, flicker: 0.4f);
            Pool(feet, orange, 260f, 0.1f, 0.4f);
            HitStop(0.05f);
            Shake(0.3f, toward);
        }

        /// <summary>
        /// Ice, as water freezing at once is: a cold white glint where it strikes, the floor round
        /// the ally freezing over in clear plates, spikes of clear ice jutting up (a tall one
        /// behind and two lower before it, one after another), clear chips flung and fine snow
        /// drifting down, and cold air sinking and creeping along the floor in a cold light; a
        /// breath streams from the attacker as cold air, condensed and heavy, rolling along low.
        /// </summary>
        private void IceBlow(Vector2 at, Vector2 feet, Vector2 from, Vector2 toward, bool breath)
        {
            var ice = ColorOf(BattleSkillVfxKind.Ice);
            if (breath)
                Spray(
                    backSmoke,
                    from + toward * 80f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = 1.1f,
                        From = new Color(0.8f, 0.92f, 1f, 0.6f),
                        To = new Color(0.8f, 0.92f, 1f, 0.6f),
                        Speed = new Vector2(700f, 1000f),
                        Direction = Angle(toward) - 6f,
                        Spread = 10f,
                        Life = new Vector2(0.5f, 0.8f),
                        Size = new Vector2(120f, 200f),
                        Aspect = 0.45f,
                        Grow = 0.4f,
                        Shrink = 1.8f,
                        Drag = 1.6f,
                        Level = true,
                        Hold = 0.35f,
                        Area = 20f,
                    },
                    6
                );
            Twinkle(at, IceLight, 150f, 0.2f, 0f, 1f);
            FloorMark(
                BattleVfxShape.Frost,
                feet,
                new Vector2(260f, 90f),
                ice,
                0.9f,
                1.4f,
                flat: false
            );
            Spike(
                backLight,
                feet + new Vector2(10f, 8f),
                UnityEngine.Random.Range(110f, 130f),
                6f,
                0f,
                false
            );
            Spike(
                frontLight,
                feet + new Vector2(-62f, -8f),
                UnityEngine.Random.Range(64f, 80f),
                18f,
                0.03f,
                true
            );
            Spike(
                frontLight,
                feet + new Vector2(58f, -12f),
                UnityEngine.Random.Range(70f, 86f),
                -14f,
                0.06f,
                true
            );
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    Material = RichMaterialOf(BattleVfxShape.Chip),
                    From = ice,
                    To = ice,
                    Speed = new Vector2(160f, 480f),
                    Direction = 90f,
                    Spread = 200f,
                    Life = new Vector2(0.45f, 0.8f),
                    Size = new Vector2(8f, 20f),
                    Gravity = 1600f,
                    Spin = 540f,
                    Hold = 0.6f,
                    Area = 24f,
                },
                10
            );
            Spray(
                frontLight,
                at + Vector2.up * 60f,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    From = new Color(0.9f, 0.97f, 1f, 0.8f),
                    To = new Color(0.7f, 0.85f, 1f, 0f),
                    Speed = new Vector2(10f, 50f),
                    Direction = 270f,
                    Spread = 120f,
                    Life = new Vector2(0.7f, 1.1f),
                    Size = new Vector2(3f, 6f),
                    Gravity = 60f,
                    Drag = 1f,
                    Area = 90f,
                    Delay = new Vector2(0.05f, 0.3f),
                },
                12
            );
            Spray(
                backSmoke,
                feet + Vector2.up * 6f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.8f,
                    From = new Color(0.78f, 0.9f, 1f, 0.32f),
                    To = new Color(0.62f, 0.76f, 0.9f, 0.32f),
                    Speed = new Vector2(40f, 130f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(0.9f, 1.3f),
                    Size = new Vector2(150f, 230f),
                    Aspect = 0.3f,
                    Grow = 0.4f,
                    Shrink = 1.8f,
                    Gravity = 15f,
                    Drag = 1.6f,
                    Level = true,
                    Hold = 0.4f,
                    Area = 30f,
                    Delay = new Vector2(0.08f, 0.2f),
                },
                3
            );
            Illuminate(at, IceLight, 0.5f, 260f, 0.1f, 0.3f, spill: false);
            BlowPool(feet, IceLight, 280f, 0.45f, 0.1f, 0.5f);
            HitStop(0.05f);
            Shake(0.3f, toward);
        }

        /// <summary>
        /// Lightning from the attacker to the ally, as a real stroke is: a faint stepped leader
        /// feels its way across, the stroke lights the channel white-hot with the exposure raised
        /// for an instant, the same channel lights again a tenth of a second later, and a thin
        /// trace of it is left in the eye (<see cref="BlowBolt"/>); its light reaches all along
        /// the channel and pools wide on the floor at each stroke, sparks fly, and a wisp of
        /// burnt smoke and a small scorch are left.
        /// </summary>
        private void ThunderBlow(Vector2 at, Vector2 feet, Vector2 from)
        {
            var yellow = ColorOf(BattleSkillVfxKind.Thunder);
            BlowBolt(from, at, yellow);
            StartCoroutine(BlowStrokes(from, at, feet, yellow));
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(yellow.r, yellow.g, yellow.b, 0f),
                    Speed = new Vector2(450f, 1200f),
                    Direction = 270f,
                    Spread = 150f,
                    Life = new Vector2(0.12f, 0.36f),
                    Size = new Vector2(8f, 14f),
                    Stretch = 0.06f,
                    Drag = 5f,
                    Gravity = 600f,
                    Area = 20f,
                    Delay = new Vector2(BlowLeader, BlowLeader + 0.02f),
                },
                14
            );
            Spray(
                backSmoke,
                at + Vector2.up * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.4f,
                    From = new Color(0.18f, 0.17f, 0.2f, 0.55f),
                    To = new Color(0.2f, 0.2f, 0.22f, 0.55f),
                    Speed = new Vector2(20f, 70f),
                    Direction = 90f,
                    Spread = 60f,
                    Life = new Vector2(0.8f, 1.1f),
                    Size = new Vector2(70f, 110f),
                    Grow = 0.5f,
                    Shrink = 1.7f,
                    Gravity = -50f,
                    Drag = 1f,
                    Hold = 0.3f,
                    Area = 30f,
                    Delay = new Vector2(0.15f, 0.3f),
                },
                2
            );
            FloorMark(
                BattleVfxShape.Scorch,
                feet,
                new Vector2(150f, 48f),
                new Color(0.1f, 0.08f, 0.08f),
                0.55f,
                1.2f,
                flat: false
            );
        }

        // The stepped leader's time, and when the strokes down the same channel come after it.
        private const float BlowLeader = 0.05f;
        private static readonly float[] BlowStrokeAt = { 0f, 0.11f };
        private static readonly float[] BlowStrokePeak = { 1f, 0.55f };

        // The looks of the rich lightning's layers (ShapeRichLightning's parts).
        private const float BlowBoltCore = 1f;
        private const float BlowBoltChannel = 2f;
        private const float BlowBoltHalo = 3f;
        private const float BlowBoltTrace = 4f;

        // The light of the strokes t s after the leader has struck: each flares and dies in tens of milliseconds.
        private static float BlowStrokeLight(float t)
        {
            float light = 0f;
            for (int i = 0; i < BlowStrokeAt.Length; i++)
                if (t >= BlowStrokeAt[i])
                    light += BlowStrokePeak[i] * Mathf.Exp(-(t - BlowStrokeAt[i]) / 0.035f);
            return light;
        }

        /// <summary>
        /// A stroke of lightning from <paramref name="from"/> to <paramref name="to"/> as four
        /// layers on one channel (one seed): the halo of lit air, faint for the leader and
        /// pulsing with the strokes; the trace left in the eye, fading and breaking up slowest;
        /// the channel and its branches, the faint leader then lit at each stroke; and the core,
        /// a white-hot line seen only at the strokes.
        /// </summary>
        private void BlowBolt(Vector2 from, Vector2 to, Color color)
        {
            if (frontLight == null)
                return;
            // The bolt's foot is at 9% of its board's height and its top 4% from the top.
            float height = Mathf.Max(200f, Vector2.Distance(from, to) / 0.87f);
            float seed = UnityEngine.Random.value;
            float turn = Angle(from - to) - 90f;
            var halo = Color.Lerp(color, Color.white, 0.25f);
            Shot Layer(float look)
            {
                var shot = Spawn(
                    frontLight,
                    BattleVfxShape.Lightning,
                    to,
                    new Vector2(height * 0.62f, height),
                    Color.clear,
                    new Vector2(0.5f, 0.09f)
                );
                shot.Seed = seed;
                shot.Image.material = RichMaterialOf(BattleVfxShape.Lightning, look);
                shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, turn);
                return shot;
            }
            float Stage(float t) =>
                t < BlowLeader
                    ? 0.5f * t / BlowLeader
                    : 0.5f + 0.5f * Mathf.Clamp01((t - BlowLeader) / 0.4f);
            float Strokes(float t) => t < BlowLeader ? 0f : BlowStrokeLight(t - BlowLeader);

            const float haloLife = BlowLeader + 0.4f;
            Animate(
                Layer(BlowBoltHalo),
                haloLife,
                (s, k) =>
                {
                    float t = k * haloLife;
                    // The lit air fades as a whole: worn away, its wide glow broke into slabs.
                    float light = t < BlowLeader ? 0.45f : 0.2f + Strokes(t);
                    light *= 1f - EaseIn(Mathf.Clamp01((k - 0.5f) / 0.5f));
                    Show(
                        s,
                        new Color(halo.r, halo.g, halo.b, Mathf.Clamp01(light)),
                        Stage(t),
                        0f,
                        1f + Strokes(t)
                    );
                }
            );
            const float traceLife = BlowLeader + 0.6f;
            Animate(
                Layer(BlowBoltTrace),
                traceLife,
                (s, k) =>
                {
                    float t = k * traceLife;
                    float since = Mathf.Max(0f, t - BlowLeader);
                    float alpha = t < BlowLeader ? 0f : 0.7f * Mathf.Exp(-since / 0.3f);
                    Show(
                        s,
                        new Color(color.r, color.g, color.b, alpha),
                        Stage(t),
                        Eaten(since / (traceLife - BlowLeader), 0.3f)
                    );
                }
            );
            const float channelLife = BlowLeader + 0.32f;
            Animate(
                Layer(BlowBoltChannel),
                channelLife,
                (s, k) =>
                {
                    float t = k * channelLife;
                    float alpha = t < BlowLeader ? 0.8f : Mathf.Clamp01(0.15f + Strokes(t));
                    Show(
                        s,
                        new Color(halo.r, halo.g, halo.b, alpha),
                        Stage(t),
                        Eaten(k, 0.55f),
                        1f + 1.5f * Strokes(t)
                    );
                }
            );
            const float coreLife = BlowLeader + 0.25f;
            Animate(
                Layer(BlowBoltCore),
                coreLife,
                (s, k) =>
                {
                    float t = k * coreLife;
                    float light = Strokes(t);
                    Show(
                        s,
                        new Color(1f, 1f, 1f, Mathf.Clamp01(light * 1.5f)),
                        Stage(t),
                        0f,
                        1f + 6f * light
                    );
                }
            );
        }

        /// <summary>
        /// The light of an enemy's lightning, in real time: as it strikes, the exposure is raised
        /// once and the battle stops; at each stroke the characters are lit from the channel and
        /// from where it lands, and the floor round the ally in a wide pool.
        /// </summary>
        private IEnumerator BlowStrokes(Vector2 from, Vector2 to, Vector2 feet, Color color)
        {
            yield return WaitReal(BlowLeader);
            ScreenFlash(new Color(0.85f, 0.9f, 1f), 0.25f);
            HitStop(0.06f);
            Shake(0.35f, Vector2.down);
            var light = Color.Lerp(color, Color.white, 0.45f);
            float distance = Vector2.Distance(from, to);
            float last = 0f;
            for (int i = 0; i < BlowStrokeAt.Length; i++)
            {
                if (BlowStrokeAt[i] > last)
                    yield return WaitReal(BlowStrokeAt[i] - last);
                last = BlowStrokeAt[i];
                float peak = BlowStrokePeak[i];
                Illuminate(
                    Vector2.Lerp(from, to, 0.5f),
                    light,
                    1.1f * peak,
                    distance * 0.6f + 300f,
                    0.02f,
                    0.16f,
                    spill: false
                );
                Illuminate(to, Color.white, 0.9f * peak, 340f, 0.02f, 0.12f, spill: false);
                BlowPool(feet, light, 760f, 0.7f * peak, 0.02f, 0.2f);
            }
        }

        // Poison's colours: the spatter's sick green, its heavy body dark, its gas a dull violet.
        private static readonly Color PoisonDark = new(0.26f, 0.2f, 0.3f);

        /// <summary>
        /// Poison, spores or ink, as a thick venom is: a spatter of fine drops and a few heavy,
        /// viscous ones that fall soon; bubbles welling up at the feet and bursting; a heavy gas
        /// in two layers (a dark body sinking and creeping low, and paler wisps over it), with a
        /// faint sick light flickering in it; and a little vapour off the ally where it struck.
        /// </summary>
        private void PoisonBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            var venom = Venom;
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = CoreBright,
                    From = venom,
                    To = new Color(0.4f, 0.2f, 0.5f, 0f),
                    Speed = new Vector2(120f, 420f),
                    Direction = Angle(toward * 0.4f + Vector2.up * 0.6f),
                    Spread = 140f,
                    Life = new Vector2(0.35f, 0.6f),
                    Size = new Vector2(6f, 11f),
                    Gravity = 1300f,
                    Area = 24f,
                },
                12
            );
            Spray(
                frontSmoke,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    From = new Color(0.3f, 0.42f, 0.18f),
                    To = new Color(PoisonDark.r, PoisonDark.g, PoisonDark.b, 0f),
                    Speed = new Vector2(70f, 200f),
                    Direction = Angle(toward * 0.3f + Vector2.up * 0.7f),
                    Spread = 100f,
                    Life = new Vector2(0.6f, 0.9f),
                    Size = new Vector2(10f, 15f),
                    Gravity = 900f,
                    Drag = 2f,
                    Area = 16f,
                },
                5
            );
            Spray(
                backSmoke,
                feet + Vector2.up * 12f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.6f,
                    From = new Color(PoisonDark.r, PoisonDark.g, PoisonDark.b, 0.75f),
                    To = new Color(PoisonDark.r, PoisonDark.g, PoisonDark.b, 0.75f),
                    Speed = new Vector2(40f, 140f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(1.1f, 1.5f),
                    Size = new Vector2(150f, 230f),
                    Aspect = 0.35f,
                    Grow = 0.4f,
                    Shrink = 1.6f,
                    Drag = 2f,
                    Level = true,
                    Hold = 0.4f,
                    Area = 30f,
                    Delay = new Vector2(0.03f, 0.12f),
                },
                3
            );
            Spray(
                frontSmoke,
                feet + Vector2.up * 30f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.8f,
                    From = new Color(0.55f, 0.65f, 0.42f, 0.4f),
                    To = new Color(0.45f, 0.38f, 0.55f, 0.4f),
                    Speed = new Vector2(30f, 90f),
                    Direction = 90f,
                    Spread = 120f,
                    Life = new Vector2(0.7f, 1f),
                    Size = new Vector2(70f, 110f),
                    Aspect = 0.6f,
                    Grow = 0.5f,
                    Shrink = 1.6f,
                    Gravity = -30f,
                    Drag = 1.5f,
                    Spin = 30f,
                    Hold = 0.3f,
                    Area = 50f,
                    Delay = new Vector2(0.1f, 0.3f),
                },
                3
            );
            for (int front = 0; front < 2; front++)
                Spray(
                    front == 0 ? backLight : frontLight,
                    feet + Vector2.up * (front == 0 ? 8f : -6f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Bubble,
                        From = venom,
                        To = new Color(venom.r, venom.g, venom.b, 0.6f),
                        Speed = new Vector2(20f, 60f),
                        Direction = 90f,
                        Spread = 30f,
                        Life = new Vector2(0.35f, 0.6f),
                        Size = new Vector2(22f, 36f),
                        Grow = 0.3f,
                        Shrink = 1.1f,
                        Gravity = -40f,
                        Hold = 0.7f,
                        Area = 70f,
                        Delay = new Vector2(0.1f, 0.7f),
                    },
                    front == 0 ? 4 : 3
                );
            Puffs(frontSmoke, at, new Color(0.55f, 0.6f, 0.45f, 0.3f), 2, 30f, 0.6f, 70f);
            Illuminate(
                feet + Vector2.up * 30f,
                venom,
                0.35f,
                220f,
                0.3f,
                0.6f,
                spill: false,
                flicker: 0.5f
            );
            HitStop(0.04f);
            Shake(0.2f, toward);
        }

        /// <summary>
        /// Water or mud, as a real splash is: a crown of drops thrown up and falling back with
        /// tails (water catching the light, mud dark and dull), a few heavy drops, a fine spray
        /// drifting off (water only), a white glint on the water where it struck, and a puddle
        /// left on the floor with faint ripples (water) or a splat and lumps (mud).
        /// </summary>
        private void WaterBlow(Vector2 at, Vector2 feet, Vector2 toward, bool mud)
        {
            var water = mud ? new Color(0.36f, 0.27f, 0.2f) : new Color(0.65f, 0.85f, 1f);
            // Water's drops are light caught in it; mud's are matter, drawn solid so they read
            // dark on the warm floor.
            Spray(
                mud ? frontSmoke : frontLight,
                at,
                new Burst
                {
                    Shape = mud ? BattleVfxShape.Chip : BattleVfxShape.Spark,
                    Bright = mud ? 1f : 1.2f,
                    From = mud ? new Color(0.4f, 0.3f, 0.22f) : Color.white,
                    To = new Color(water.r, water.g, water.b, mud ? 1f : 0f),
                    Speed = new Vector2(200f, 620f),
                    Direction = Angle(toward * 0.4f + Vector2.up * 0.6f),
                    Spread = 150f,
                    Life = new Vector2(0.35f, 0.7f),
                    Size = mud ? new Vector2(7f, 14f) : new Vector2(4f, 10f),
                    Stretch = 0.012f,
                    Gravity = 1500f,
                    Area = 30f,
                },
                20
            );
            Spray(
                mud ? frontSmoke : frontLight,
                at,
                new Burst
                {
                    Shape = mud ? BattleVfxShape.Chip : BattleVfxShape.Spark,
                    Bright = mud ? 0.8f : 1f,
                    From = mud ? new Color(0.42f, 0.32f, 0.24f) : new Color(0.8f, 0.92f, 1f),
                    To = new Color(water.r, water.g, water.b, mud ? 1f : 0f),
                    Speed = new Vector2(120f, 300f),
                    Direction = 90f,
                    Spread = 120f,
                    Life = new Vector2(0.45f, 0.7f),
                    Size = mud ? new Vector2(24f, 40f) : new Vector2(10f, 16f),
                    Gravity = 1700f,
                    Spin = mud ? 300f : 0f,
                    Area = 24f,
                },
                mud ? 8 : 4
            );
            if (mud)
                // Clumps of mud thrown up whole, soft-edged and heavy, falling soon.
                Spray(
                    frontSmoke,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = 0.35f,
                        From = new Color(0.32f, 0.2f, 0.11f, 0.95f),
                        To = new Color(0.26f, 0.17f, 0.1f, 0.95f),
                        Speed = new Vector2(150f, 380f),
                        Direction = Angle(toward * 0.4f + Vector2.up * 0.6f),
                        Spread = 120f,
                        Life = new Vector2(0.4f, 0.6f),
                        Size = new Vector2(50f, 80f),
                        Grow = 0.6f,
                        Shrink = 0.9f,
                        Gravity = 1500f,
                        Spin = 120f,
                        Hold = 0.5f,
                        Area = 20f,
                    },
                    6
                );
            if (mud)
                // Wet mud catches the light in small glints on its lumps.
                Spray(
                    frontLight,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Spark,
                        Bright = CoreBright,
                        From = new Color(1f, 0.92f, 0.8f, 0.8f),
                        To = new Color(0.6f, 0.5f, 0.4f, 0f),
                        Speed = new Vector2(150f, 450f),
                        Direction = 90f,
                        Spread = 130f,
                        Life = new Vector2(0.25f, 0.45f),
                        Size = new Vector2(5f, 8f),
                        Gravity = 1500f,
                        Area = 24f,
                    },
                    8
                );
            if (!mud)
            {
                Twinkle(
                    at + new Vector2(-10f, 20f),
                    new Color(0.75f, 0.9f, 1f),
                    100f,
                    0.16f,
                    0f,
                    1f
                );
                Spray(
                    frontSmoke,
                    at,
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Bright = 1f,
                        From = new Color(0.82f, 0.92f, 1f, 0.22f),
                        To = new Color(0.82f, 0.92f, 1f, 0.22f),
                        Speed = new Vector2(60f, 200f),
                        Direction = Angle(toward * 0.5f + Vector2.up * 0.5f),
                        Spread = 120f,
                        Life = new Vector2(0.5f, 0.8f),
                        Size = new Vector2(70f, 110f),
                        Grow = 0.4f,
                        Shrink = 1.6f,
                        Gravity = -20f,
                        Drag = 3f,
                        Hold = 0.3f,
                        Area = 20f,
                        Delay = new Vector2(0.02f, 0.08f),
                    },
                    3
                );
            }
            FloorMark(
                BattleVfxShape.Scorch,
                feet,
                new Vector2(mud ? 290f : 250f, mud ? 90f : 76f),
                mud ? new Color(0.2f, 0.14f, 0.1f) : new Color(0.1f, 0.16f, 0.22f),
                mud ? 0.85f : 0.45f,
                1.5f,
                flat: false
            );
            if (!mud)
                for (int i = 0; i < 2; i++)
                {
                    float delay = 0.08f + i * 0.18f;
                    BlowAfter(
                        delay,
                        () =>
                            Ring(
                                backLight,
                                feet,
                                new Color(0.7f, 0.85f, 1f, 0.35f),
                                220f,
                                0.6f,
                                floor: true
                            )
                    );
                }
            HitStop(0.05f);
            Shake(0.3f, toward);
        }

        // Dry sand in the evening light: pale and warm, so a wave of it reads on the red floor.
        private static readonly Color Sand = new(0.9f, 0.76f, 0.56f);

        /// <summary>
        /// A wave of sand: a low wall of it sweeps in along the floor and over the ally, its grains
        /// streaming through, the sand caught on the ally trickling off it a moment later, and a
        /// thin drift of it left on the floor (sand does not hang like smoke).
        /// </summary>
        private void SandBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            float angle = toward.x >= 0f ? 0f : 180f;
            var heading = new Vector2(Mathf.Sign(toward.x == 0f ? -1f : toward.x), 0f);
            var material = RichMaterialOf(BattleVfxShape.Smoke);
            for (int front = 0; front < 2; front++)
                Spray(
                    front == 0 ? backSmoke : frontSmoke,
                    feet - heading * 200f + Vector2.up * (front == 0 ? 40f : 0f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Smoke,
                        Material = material,
                        Bright = 0.7f,
                        From = Sand,
                        To = Sand,
                        Speed = new Vector2(700f, 1100f),
                        Direction = angle,
                        Spread = 14f,
                        Life = new Vector2(0.5f, 0.75f),
                        Size = new Vector2(200f, 320f) * (front == 0 ? 1f : 0.75f),
                        Aspect = 0.45f,
                        Grow = 0.5f,
                        Shrink = 1.6f,
                        Drag = 2.6f,
                        Level = true,
                        Hold = 0.25f,
                        Area = 40f,
                    },
                    front == 0 ? 4 : 3
                );
            Spray(
                frontLight,
                at - heading * 120f,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    From = Sand,
                    To = new Color(Sand.r * 0.8f, Sand.g * 0.75f, Sand.b * 0.7f, 0f),
                    Speed = new Vector2(500f, 1100f),
                    Direction = angle + (angle == 0f ? 8f : -8f),
                    Spread = 30f,
                    Life = new Vector2(0.35f, 0.65f),
                    Size = new Vector2(3f, 6f),
                    Stretch = 0.012f,
                    Gravity = 900f,
                    Drag = 1.5f,
                    Area = 70f,
                },
                26
            );
            Spray(
                frontSmoke,
                at + Vector2.up * 30f,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = Sand,
                    To = new Color(Sand.r * 0.8f, Sand.g * 0.75f, Sand.b * 0.7f, 0.6f),
                    Speed = new Vector2(10f, 60f),
                    Direction = 270f,
                    Spread = 60f,
                    Life = new Vector2(0.3f, 0.5f),
                    Size = new Vector2(3f, 5f),
                    Gravity = 1400f,
                    Area = 50f,
                    Delay = new Vector2(0.2f, 0.5f),
                },
                12
            );
            FloorMark(
                BattleVfxShape.Scorch,
                feet - heading * 30f,
                new Vector2(280f, 60f),
                Sand * 0.75f,
                0.4f,
                1.3f,
                flat: false
            );
            HitStop(0.05f);
            Shake(0.35f, toward);
        }

        /// <summary>
        /// A gust, as wind shows itself: streams of air running through the ally, seen by the dust
        /// and specks they carry and open between them; fast thin streaks; two thin curls of air
        /// rolling round the ally; dust swept along the floor and leaves tumbling. No mist stays:
        /// the wind blows on through.
        /// </summary>
        private void WindBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            BlowStream(at + Vector2.down * 20f, toward, 0.55f);
            Spray(
                frontLight,
                at - toward * 220f,
                new Burst
                {
                    Shape = BattleVfxShape.Streak,
                    From = new Color(1f, 1f, 1f, 0.6f),
                    To = new Color(1f, 1f, 1f, 0f),
                    Speed = new Vector2(1600f, 2200f),
                    Direction = Angle(toward),
                    Spread = 8f,
                    Life = new Vector2(0.18f, 0.3f),
                    Size = new Vector2(3f, 6f),
                    Stretch = 0.05f,
                    Area = 110f,
                    Delay = new Vector2(0f, 0.15f),
                },
                14
            );
            var air = new Color(0.92f, 0.96f, 1f, 0.35f);
            bool left = toward.x < 0f;
            BlowRake(
                backLight,
                at + Vector2.up * 40f,
                left ? 150f : 30f,
                left,
                air,
                0.62f,
                RakeTrace,
                UnityEngine.Random.value,
                0.02f,
                0f,
                0.4f,
                0.2f,
                0.2f,
                1f
            );
            BlowRake(
                frontLight,
                at + Vector2.down * 30f,
                left ? 190f : -10f,
                !left,
                air,
                0.5f,
                RakeTrace,
                UnityEngine.Random.value,
                0.08f,
                0f,
                0.4f,
                0.2f,
                0.2f,
                1f
            );
            DustKick(feet, toward, 220f);
            Spray(
                frontLight,
                at - toward * 60f,
                new Burst
                {
                    Shape = BattleVfxShape.Leaf,
                    From = new Color(0.55f, 0.75f, 0.35f),
                    To = new Color(0.45f, 0.6f, 0.3f, 0f),
                    Speed = new Vector2(500f, 900f),
                    Direction = Angle(toward) + 10f,
                    Spread = 40f,
                    Life = new Vector2(0.5f, 0.8f),
                    Size = new Vector2(12f, 18f),
                    Gravity = 200f,
                    Drag = 1.2f,
                    Spin = 600f,
                    Area = 60f,
                    Delay = new Vector2(0f, 0.1f),
                },
                6
            );
            HitStop(0.04f);
            Shake(0.3f, toward);
        }

        /// <summary>
        /// Streams of air through a band across <paramref name="at"/>, running along
        /// <paramref name="toward"/> for <paramref name="seconds"/>: the wind-stream shape (fibrous
        /// streams that snake and gust), its pattern running with the wind while the band stays,
        /// pale and thin, in at once and thinning out.
        /// </summary>
        private void BlowStream(Vector2 at, Vector2 toward, float seconds)
        {
            if (frontSmoke == null)
                return;
            var shot = Spawn(
                frontSmoke,
                BattleVfxShape.Tornado,
                at,
                new Vector2(760f, 240f),
                Color.clear
            );
            shot.Image.material = RichMaterialOf(BattleVfxShape.Tornado);
            shot.Image.rectTransform.localEulerAngles = new Vector3(
                0f,
                0f,
                toward.x < 0f ? 180f : 0f
            );
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    float alpha = Mathf.Min(1f, k * 6f) * (1f - EaseIn(k));
                    Show(s, new Color(0.9f, 0.94f, 1f, 0.8f * alpha), k, 0f);
                }
            );
        }

        /// <summary>
        /// A shriek or a howl, as a loud sound does: the air shows nothing, so its wave is drawn
        /// only as a few faint fronts spreading from the attacker; where it reaches the ally the
        /// floor's dust is shaken up in three pulses, pebbles hop, and the ally is shaken in
        /// small, quick jolts rather than one knock.
        /// </summary>
        private void SoundBlow(Vector2 at, Vector2 feet, Vector2 from, Vector2 toward)
        {
            float distance = Vector2.Distance(from, at);
            var front = new Color(0.95f, 0.92f, 0.86f, 0.14f);
            for (int i = 0; i < 3; i++)
            {
                int pulse = i;
                BlowAfter(
                    pulse * 0.08f,
                    () =>
                    {
                        Ring(
                            backLight,
                            from + toward * 40f,
                            front,
                            distance * 2.4f,
                            0.4f,
                            floor: false
                        );
                        Shockwave(feet, 300f - pulse * 40f, 0.5f - pulse * 0.1f);
                        Hop(feet, 120f, 3, 0.6f);
                        Shake(0.18f, pulse % 2 == 0 ? toward : -toward);
                    }
                );
            }
            Spray(
                frontLight,
                feet + Vector2.up * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.Mote,
                    From = new Color(0.8f, 0.72f, 0.6f, 0.7f),
                    To = new Color(0.8f, 0.72f, 0.6f, 0f),
                    Speed = new Vector2(40f, 140f),
                    Direction = 90f,
                    Spread = 70f,
                    Life = new Vector2(0.4f, 0.7f),
                    Size = new Vector2(8f, 12f),
                    Gravity = 300f,
                    Area = 90f,
                    Delay = new Vector2(0f, 0.25f),
                },
                12
            );
            HitStop(0.03f);
        }

        /// <summary>
        /// A spell, in the way its kind bursts (read from its name): a curse gathers in, then
        /// marks the ally with a sigil in a dark mist that clings and creeps, edged with its light;
        /// a light bursts into rays and rising motes; a crystal shatters into turning shards; a
        /// will-o'-the-wisp's flame licks up round the ally. Each lights the ally and the floor.
        /// </summary>
        private void MagicBlow(Vector2 at, Vector2 feet, string skill, Vector2 toward)
        {
            var color = EnemySpellColor(skill);
            bool Has(string word) =>
                skill != null && skill.Contains(word, StringComparison.Ordinal);
            if (Has("光"))
                LightBurst(at, feet, color);
            else if (Has("水晶"))
                CrystalBurst(at, feet, color);
            else if (Has("迷い火"))
                WispFlames(at, feet, color);
            else
                CurseMark(at, feet, color);
            HitStop(0.04f);
            Shake(0.25f, toward);
        }

        // A curse: drawn in for an instant, then a sigil on the ally in a dark clinging mist.
        private void CurseMark(Vector2 at, Vector2 feet, Color color)
        {
            const float gather = 0.1f;
            Gather(at, color, 10, 170f, gather);
            Spray(
                backSmoke,
                at + Vector2.down * 20f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.5f,
                    From = new Color(0.22f, 0.12f, 0.32f, 0.55f),
                    To = new Color(0.16f, 0.1f, 0.22f, 0.55f),
                    Speed = new Vector2(20f, 70f),
                    Direction = 90f,
                    Spread = 160f,
                    Life = new Vector2(0.9f, 1.2f),
                    Size = new Vector2(120f, 190f),
                    Grow = 0.4f,
                    Shrink = 1.5f,
                    Drag = 1.5f,
                    Spin = 40f,
                    Hold = 0.35f,
                    Area = 50f,
                    Delay = new Vector2(gather, gather + 0.1f),
                },
                3
            );
            Spray(
                frontSmoke,
                feet + Vector2.up * 10f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.5f,
                    From = new Color(0.2f, 0.1f, 0.28f, 0.4f),
                    To = new Color(0.16f, 0.1f, 0.22f, 0.4f),
                    Speed = new Vector2(30f, 90f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(0.8f, 1.1f),
                    Size = new Vector2(110f, 170f),
                    Aspect = 0.4f,
                    Grow = 0.4f,
                    Shrink = 1.6f,
                    Drag = 2f,
                    Level = true,
                    Hold = 0.35f,
                    Area = 30f,
                    Delay = new Vector2(gather, gather + 0.15f),
                },
                2
            );
            if (frontLight != null)
            {
                var sigil = Spawn(
                    frontLight,
                    BattleVfxShape.MagicCircle,
                    at,
                    new Vector2(150f, 150f),
                    Color.clear
                );
                BlowLater(
                    sigil,
                    gather,
                    0.55f,
                    (s, k) =>
                    {
                        float grow = 0.6f + 0.4f * EaseOut(Mathf.Min(1f, k * 4f));
                        s.Holder.localScale = new Vector3(grow * 0.7f, grow, 1f);
                        s.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, 60f * k);
                        Show(
                            s,
                            new Color(color.r, color.g, color.b, 0.75f),
                            k,
                            Eaten(k, 0.4f),
                            ErodeBright
                        );
                    }
                );
            }
            BlowAfter(
                gather,
                () =>
                {
                    HotFlash(at, color, 160f);
                    Spray(
                        frontLight,
                        at,
                        new Burst
                        {
                            Shape = BattleVfxShape.Spark,
                            Bright = CoreBright,
                            From = Color.Lerp(color, Color.white, 0.4f),
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(40f, 160f),
                            Direction = 90f,
                            Spread = 300f,
                            Life = new Vector2(0.5f, 0.9f),
                            Size = new Vector2(3f, 6f),
                            Gravity = -40f,
                            Drag = 2.5f,
                            Area = 50f,
                        },
                        10
                    );
                    Illuminate(at, color, 0.6f, 280f, 0.1f, 0.4f, spill: false);
                    BlowPool(feet, color, 240f, 0.4f, 0.1f, 0.4f);
                }
            );
        }

        // A light: a white-hot flash, rays, motes rising, and a warm light on the ally and the floor.
        private void LightBurst(Vector2 at, Vector2 feet, Color color)
        {
            HotFlash(at, color, 220f);
            Star(at, color, 260f, 0.26f);
            Glow(backLight, at, color, 320f, 0.4f, 0.35f);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(40f, 180f),
                    Direction = 90f,
                    Spread = 160f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(12f, 22f),
                    Shrink = 0.2f,
                    Gravity = -80f,
                    Drag = 1.6f,
                    Spin = 160f,
                    Area = 40f,
                    Delay = new Vector2(0.03f, 0.15f),
                },
                10
            );
            Illuminate(at, color, 0.8f, 320f, 0.1f, 0.35f, spill: false);
            Pool(feet, color, 260f, 0.1f, 0.4f);
        }

        // A crystal: it rings and shatters, turning shards flung out and catching the light.
        private void CrystalBurst(Vector2 at, Vector2 feet, Color color)
        {
            Twinkle(at, color, 170f, 0.22f, 0f, 1f);
            HotFlash(at, color, 170f);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Shard,
                    Bright = CoreBright,
                    From = Color.Lerp(color, Color.white, 0.5f),
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(250f, 650f),
                    Direction = 90f,
                    Spread = 300f,
                    Life = new Vector2(0.35f, 0.65f),
                    Size = new Vector2(14f, 26f),
                    Shrink = 0.6f,
                    Gravity = 900f,
                    Drag = 1.5f,
                    Spin = 720f,
                    Area = 20f,
                },
                12
            );
            Spray(
                backLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Shard,
                    From = color,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(150f, 400f),
                    Direction = 90f,
                    Spread = 300f,
                    Life = new Vector2(0.4f, 0.7f),
                    Size = new Vector2(18f, 30f),
                    Shrink = 0.6f,
                    Gravity = 900f,
                    Drag = 1.5f,
                    Spin = 540f,
                    Area = 20f,
                },
                5
            );
            Illuminate(at, color, 0.6f, 280f, 0.08f, 0.3f, spill: false);
            BlowPool(feet, color, 240f, 0.45f, 0.08f, 0.35f);
        }

        // A will-o'-the-wisp: small cold tongues of light lick up round the ally and burn out,
        // giving no smoke and none of a fire's yellow (its light is the spell's own colour).
        private void WispFlames(Vector2 at, Vector2 feet, Color color)
        {
            HotFlash(at, color, 150f);
            Glow(backLight, at + Vector2.down * 20f, color, 280f, 0.7f, 0.3f);
            for (int front = 0; front < 2; front++)
                Spray(
                    front == 0 ? backLight : frontLight,
                    feet + Vector2.up * (front == 0 ? 30f : 10f),
                    new Burst
                    {
                        Shape = BattleVfxShape.Spark,
                        Bright = CoreBright,
                        From = Color.Lerp(color, Color.white, 0.4f),
                        To = new Color(color.r, color.g, color.b, 0f),
                        Speed = new Vector2(60f, 160f),
                        Direction = 90f,
                        Spread = 30f,
                        Life = new Vector2(0.35f, 0.6f),
                        Size = new Vector2(16f, 26f),
                        Aspect = 2.4f,
                        Shrink = 0.3f,
                        Gravity = -300f,
                        Drag = 1.5f,
                        Level = true,
                        Area = 70f,
                        Delay = new Vector2(0f, 0.3f),
                    },
                    front == 0 ? 6 : 5
                );
            Illuminate(at, color, 0.6f, 280f, 0.3f, 0.4f, spill: false, flicker: 0.6f);
            BlowPool(feet, color, 240f, 0.4f, 0.3f, 0.4f);
        }

        /// <summary>
        /// A crack opening in the floor at <paramref name="feet"/>, about <paramref name="size"/>
        /// wide, as a real one looks (the shader's dark crack): it splits open, holds for
        /// <paramref name="seconds"/> and wears away from its tips.
        /// </summary>
        private void Fissure(RectTransform layer, Vector2 feet, float size, float seconds)
        {
            if (layer == null)
                return;
            var shot = Spawn(layer, BattleVfxShape.Crack, feet, Vector2.one * size, KickedDust);
            shot.Image.material = RichMaterialOf(BattleVfxShape.Crack);
            shot.Holder.localScale = new Vector3(1f, FloorTilt, 1f);
            shot.Image.rectTransform.localEulerAngles = new Vector3(
                0f,
                0f,
                UnityEngine.Random.Range(0f, 360f)
            );
            Animate(
                shot,
                seconds,
                (s, k) => Show(s, KickedDust, Mathf.Min(1f, k * 4f), Eaten(k, 0.6f))
            );
        }

        // --- The parts of an enemy's blow ------------------------------------------------------

        /// <summary>
        /// Runs <paramref name="animate"/> on the shot with its progress (0 to 1) over
        /// <paramref name="seconds"/>, after it has waited <paramref name="delay"/> unseen.
        /// </summary>
        private static void BlowLater(
            Shot shot,
            float delay,
            float seconds,
            Action<Shot, float> animate
        )
        {
            delay = Mathf.Max(0f, delay);
            float total = delay + seconds;
            Animate(
                shot,
                total,
                (s, t) =>
                {
                    float time = t * total - delay;
                    if (time < 0f)
                    {
                        Show(s, Color.clear, 0f, 0f);
                        return;
                    }
                    animate(s, Mathf.Clamp01(time / seconds));
                }
            );
        }

        /// <summary>Does <paramref name="then"/> <paramref name="seconds"/> of real time from now (at once when 0).</summary>
        private void BlowAfter(float seconds, Action then)
        {
            if (seconds <= 0f)
                then();
            else
                StartCoroutine(BlowAfterReal(seconds, then));
        }

        private static IEnumerator BlowAfterReal(float seconds, Action then)
        {
            yield return WaitReal(seconds);
            then();
        }

        /// <summary>
        /// One layer of a curved streak (a claw, a jaw, a curl of air) on the slash arc's shape:
        /// in <paramref name="look"/>, with its own <paramref name="seed"/> (the same for the
        /// layers of one streak, so they share its shape), after <paramref name="delay"/> (and
        /// <paramref name="lead"/> s after the streak began), drawn tip to tip over
        /// <paramref name="pace"/> s from the streak's start and gone after <paramref name="life"/> s,
        /// eaten away from <paramref name="hold"/> of it; it closes in from
        /// <paramref name="travel"/> off its place as it is drawn.
        /// </summary>
        private void BlowRake(
            RectTransform layer,
            Vector2 at,
            float angle,
            bool mirror,
            Color color,
            float scale,
            float look,
            float seed,
            float delay,
            float lead,
            float life,
            float pace,
            float hold,
            float bright,
            Vector2 travel = default
        )
        {
            if (layer == null)
                return;
            var shot = Spawn(
                layer,
                BattleVfxShape.SlashArc,
                at,
                new Vector2(420f, 420f) * scale,
                Color.clear
            );
            shot.Image.material = RichMaterialOf(BattleVfxShape.SlashArc, look);
            shot.Seed = seed;
            shot.Image.rectTransform.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
            float sweep = mirror ? -24f : 24f;
            BlowLater(
                shot,
                delay,
                life,
                (s, k) =>
                {
                    // Timed from the streak's start, so a late layer matches an early one.
                    float drawn = Mathf.Clamp01((lead + k * life) / Mathf.Max(0.01f, pace));
                    float grow = 0.75f + 0.35f * EaseOut(Mathf.Min(1f, drawn * 2.5f));
                    s.Holder.localScale = new Vector3(grow, grow, 1f);
                    s.Holder.anchoredPosition = at + travel * (1f - EaseIn(Mathf.Min(1f, drawn)));
                    s.Image.rectTransform.localEulerAngles = new Vector3(
                        0f,
                        0f,
                        angle + sweep * EaseOut(Mathf.Min(1f, drawn * 2f))
                    );
                    Show(s, color, drawn, Eaten(k, hold), bright);
                }
            );
        }

        /// <summary>
        /// A soft haze of light along a stretch from <paramref name="from"/> to
        /// <paramref name="to"/>, <paramref name="width"/> px across: the air lit round a streak,
        /// in fast after <paramref name="delay"/> and fading over <paramref name="seconds"/>.
        /// </summary>
        private void BlowHaze(
            RectTransform layer,
            Vector2 from,
            Vector2 to,
            float width,
            Color color,
            float peak,
            float delay,
            float seconds
        )
        {
            if (layer == null)
                return;
            float length = Mathf.Max(width, Vector2.Distance(from, to) + width * 0.5f);
            var shot = Spawn(
                layer,
                BattleVfxShape.Glow,
                (from + to) * 0.5f,
                new Vector2(length, width),
                Color.clear
            );
            shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(to - from));
            BlowLater(
                shot,
                delay,
                seconds,
                (s, k) =>
                {
                    float alpha = peak * Mathf.Min(1f, k * 8f) * (1f - EaseIn(k));
                    Show(s, new Color(color.r, color.g, color.b, alpha), k, 0f);
                }
            );
        }

        /// <summary>A pool of light on the floor at <paramref name="feet"/> as strong as <paramref name="peak"/>, held and fading.</summary>
        private void BlowPool(
            Vector2 feet,
            Color color,
            float size,
            float peak,
            float hold,
            float fade
        )
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
                    float alpha = t < hold ? peak : peak * (1f - EaseIn((t - hold) / fade));
                    Show(s, new Color(color.r, color.g, color.b, alpha), k, 0f);
                }
            );
        }

        /// <summary>
        /// A needle of light rushing from <paramref name="from"/> to <paramref name="to"/> in
        /// <paramref name="drive"/> s, its bright head first, then stopping there and thinning out.
        /// </summary>
        private void BlowNeedle(Vector2 from, Vector2 to, Color color, float drive)
        {
            if (frontLight == null)
                return;
            float length = Mathf.Min(220f, Vector2.Distance(from, to));
            var shot = Spawn(
                frontLight,
                BattleVfxShape.Streak,
                from,
                new Vector2(16f, length),
                color,
                new Vector2(0.5f, 0.95f)
            );
            shot.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, Angle(to - from) - 90f);
            const float stay = 0.16f;
            Animate(
                shot,
                drive + stay,
                (s, k) =>
                {
                    float t = k * (drive + stay);
                    s.Holder.anchoredPosition = Vector2.Lerp(from, to, Mathf.Min(1f, t / drive));
                    float after = Mathf.Max(0f, t - drive) / stay;
                    s.Image.rectTransform.localScale = new Vector3(
                        1f - 0.6f * after,
                        1f - 0.5f * after,
                        1f
                    );
                    var tint = color;
                    tint.a = 1f - EaseIn(after);
                    Show(s, tint, k, 0f, CoreBright);
                }
            );
        }
    }
}
