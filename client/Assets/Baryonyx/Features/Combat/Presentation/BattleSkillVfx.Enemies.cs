using System;
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
        // read as a crystal's spell.
        private static readonly (string[] Words, EnemyBlow Blow)[] EnemyBlowWords =
        {
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
            (new[] { "岩", "石", "地", "崩し", "突き上げ", "黒曜", "つぶて", "投げ" }, EnemyBlow.Rock),
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
        /// <paramref name="skill"/> really is (<see cref="EnemyBlowOf"/>): claws leave the
        /// streaks of their swipe, jaws snap shut, a thrust drives straight in, a slam drives dust
        /// out along the floor, fire bursts and smokes, ice freezes the floor, lightning strikes
        /// from the attacker, poison splashes and spreads as a heavy gas, water splashes, wind
        /// gusts through, a shriek shakes the dust. A breath, a throw or a bolt comes from
        /// <paramref name="attacker"/> (its drawn body, when known). Milder than the party's
        /// skills: short stops and shakes and no flash but the light of lightning.
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
            var from =
                attacker != null ? Center(FrontLayer, attacker) : at + Vector2.right * 420f;
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
                    BiteBlow(at, toward);
                    break;
                case EnemyBlow.Pierce:
                    PierceBlow(at, toward);
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
                    SoundBlow(at, feet, toward);
                    break;
                case EnemyBlow.Magic:
                    MagicBlow(at, feet, EnemySpellColor(skill), toward);
                    break;
                default:
                    ClawBlow(at, toward);
                    break;
            }
        }

        // The streak of an enemy's claws: a warm, pale red, so a blow on the party reads at once.
        private static readonly Color ClawStreak = new(1f, 0.55f, 0.5f);

        /// <summary>
        /// Three claws raking across: the streaks of their swipe, a few sparks, a jolt.
        /// </summary>
        private void ClawBlow(Vector2 at, Vector2 toward)
        {
            for (int i = 0; i < 3; i++)
                Arc(
                    frontLight,
                    at + new Vector2(-10f + i * 14f, 26f - i * 26f),
                    200f + i * 4f,
                    true,
                    ClawStreak,
                    0.55f - i * 0.04f
                );
            Sparks(at, new Color(1f, 0.6f, 0.35f), 8, 0.6f, toward);
            Illuminate(at, ClawStreak, 0.4f, 240f, 0.05f, 0.25f, spill: false);
            HitStop(0.05f);
            Shake(0.3f, toward);
        }

        /// <summary>
        /// Jaws snapping shut: the streaks of the upper and lower jaw closing, a spatter, a jolt.
        /// </summary>
        private void BiteBlow(Vector2 at, Vector2 toward)
        {
            Arc(frontLight, at + Vector2.up * 30f, 180f, false, ClawStreak, 0.5f);
            Arc(frontLight, at + Vector2.down * 30f, 0f, false, ClawStreak, 0.45f);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    From = new Color(0.9f, 0.2f, 0.18f),
                    To = new Color(0.5f, 0.08f, 0.06f, 0f),
                    Speed = new Vector2(120f, 380f),
                    Direction = Angle(toward),
                    Spread = 120f,
                    Life = new Vector2(0.3f, 0.55f),
                    Size = new Vector2(4f, 8f),
                    Gravity = 1400f,
                    Area = 20f,
                },
                10
            );
            HitStop(0.06f);
            Shake(0.35f, toward);
        }

        /// <summary>
        /// A thrust driven straight in: its streak, a glint and sparks out the far side.
        /// </summary>
        private void PierceBlow(Vector2 at, Vector2 toward)
        {
            Line(frontLight, at - toward * 180f, at + toward * 40f, ClawStreak, 46f, 0.2f, 0.25f);
            Twinkle(at, Color.Lerp(ClawStreak, Color.white, 0.5f), 120f, 0.16f, 0f, 1f);
            Sparks(at, new Color(1f, 0.6f, 0.35f), 8, 0.7f, toward);
            HitStop(0.05f);
            Shake(0.25f, toward);
        }

        /// <summary>
        /// A slam: a flash of the hit, its shock driving dust out along the floor, a heavy jolt.
        /// </summary>
        private void BluntBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            Star(at, new Color(1f, 0.75f, 0.6f), 170f, 0.18f);
            Shockwave(feet, 300f, 0.7f);
            Illuminate(at, new Color(1f, 0.7f, 0.55f), 0.35f, 240f, 0.04f, 0.2f, spill: false);
            HitStop(0.07f);
            Shake(0.45f, toward);
        }

        /// <summary>
        /// Rock and earth: the stone bursts on the ally into chips (their tops lit, so they read on
        /// the dark floor) flung up and falling, a cloud of its dust thrown out, the floor cracked.
        /// </summary>
        private void RockBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            Star(at, new Color(1f, 0.85f, 0.7f), 130f, 0.1f);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    From = new Color(0.8f, 0.7f, 0.58f),
                    To = new Color(0.44f, 0.37f, 0.3f),
                    Speed = new Vector2(240f, 700f),
                    Direction = Angle(toward * 0.3f + Vector2.up * 0.7f),
                    Spread = 160f,
                    Life = new Vector2(0.5f, 0.9f),
                    Size = new Vector2(10f, 26f),
                    Gravity = 1700f,
                    Drag = 0.5f,
                    Spin = 540f,
                    Area = 30f,
                },
                18
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
                    Life = new Vector2(0.6f, 1f),
                    Size = new Vector2(110f, 190f),
                    Grow = 0.6f,
                    Shrink = 1.6f,
                    Gravity = 60f,
                    Drag = 3f,
                    Hold = 0.3f,
                    Area = 24f,
                },
                5
            );
            Shockwave(feet, 340f, 0.8f);
            Puffs(backSmoke, feet + Vector2.up * 30f, KickedDust, 3, 40f, 0.9f, 160f);
            Fissure(backSmoke, feet, 260f, 1.4f);
            HitStop(0.07f);
            Shake(0.5f, Vector2.down);
        }

        /// <summary>
        /// Fire: lumps of flame burst on the ally and roll up, sparks fly, dark smoke rises behind
        /// in the fire's light, and the floor is scorched; a breath streams from the attacker.
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
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Flame,
                    Fire = true,
                    Speed = new Vector2(60f, 260f),
                    Direction = 90f,
                    Spread = 160f,
                    Life = new Vector2(0.4f, 0.7f),
                    Size = new Vector2(70f, 130f),
                    Grow = 0.4f,
                    Shrink = 1.5f,
                    Gravity = -260f,
                    Drag = 2f,
                    Spin = 60f,
                    Hold = 0.2f,
                    Area = 40f,
                },
                9
            );
            Sparks(at, orange, 10, 0.7f, Vector2.up);
            Puffs(
                backSmoke,
                at + Vector2.up * 40f,
                new Color(0.16f, 0.13f, 0.12f, 0.8f),
                3,
                40f,
                1.2f,
                150f
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
        /// Ice: the floor round the ally freezes over in clear plates, chips of clear ice fly and
        /// small spikes of it jut up; a breath streams from the attacker as cold air, condensed
        /// and heavy, rolling along low.
        /// </summary>
        private void IceBlow(Vector2 at, Vector2 feet, Vector2 from, Vector2 toward, bool breath)
        {
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
            FloorMark(
                BattleVfxShape.Frost,
                feet,
                new Vector2(260f, 90f),
                ColorOf(BattleSkillVfxKind.Ice),
                0.9f,
                1.4f,
                flat: false
            );
            for (int i = 0; i < 3; i++)
                Spike(
                    frontLight,
                    feet + new Vector2(-50f + i * 50f, -6f),
                    UnityEngine.Random.Range(60f, 100f),
                    (i - 1) * -16f,
                    i * 0.03f,
                    true
                );
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Chip,
                    Material = RichMaterialOf(BattleVfxShape.Chip),
                    From = ColorOf(BattleSkillVfxKind.Ice),
                    To = ColorOf(BattleSkillVfxKind.Ice),
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
            Illuminate(at, IceLight, 0.5f, 260f, 0.1f, 0.3f, spill: false);
            HitStop(0.05f);
            Shake(0.3f, toward);
        }

        /// <summary>
        /// Lightning from the attacker to the ally: the bolt, its light on everything for an
        /// instant (the exposure raised, as a real bolt does), sparks and a wisp of burnt smoke.
        /// </summary>
        private void ThunderBlow(Vector2 at, Vector2 feet, Vector2 from)
        {
            var yellow = ColorOf(BattleSkillVfxKind.Thunder);
            Zap(from, at, yellow, 0.3f);
            ScreenFlash(new Color(0.85f, 0.9f, 1f), 0.25f);
            Sparks(at, yellow, 14, 0.9f, Vector2.down);
            Puffs(
                backSmoke,
                at + Vector2.up * 20f,
                new Color(0.18f, 0.17f, 0.2f, 0.6f),
                2,
                30f,
                0.9f,
                110f
            );
            Illuminate(at, Color.Lerp(yellow, Color.white, 0.4f), 0.9f, 340f, 0.05f, 0.3f);
            HitStop(0.06f);
            Shake(0.35f, Vector2.down);
        }

        /// <summary>
        /// Poison, spores or ink: a spatter of it on the ally falling back under its weight, and a
        /// heavy, tinted gas sinking and creeping low round its feet.
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
                    From = venom,
                    To = new Color(0.4f, 0.2f, 0.5f, 0f),
                    Speed = new Vector2(120f, 420f),
                    Direction = Angle(toward * 0.4f + Vector2.up * 0.6f),
                    Spread = 140f,
                    Life = new Vector2(0.35f, 0.7f),
                    Size = new Vector2(4f, 9f),
                    Gravity = 1300f,
                    Area = 24f,
                },
                14
            );
            Spray(
                backSmoke,
                feet + Vector2.up * 12f,
                new Burst
                {
                    Shape = BattleVfxShape.Smoke,
                    Bright = 0.7f,
                    From = new Color(0.45f, 0.35f, 0.55f, 0.55f),
                    To = new Color(0.45f, 0.35f, 0.55f, 0.55f),
                    Speed = new Vector2(40f, 140f),
                    Direction = 0f,
                    Spread = 360f,
                    Life = new Vector2(1f, 1.4f),
                    Size = new Vector2(140f, 220f),
                    Aspect = 0.35f,
                    Grow = 0.4f,
                    Shrink = 1.6f,
                    Drag = 2f,
                    Level = true,
                    Hold = 0.4f,
                    Area = 30f,
                },
                4
            );
            HitStop(0.04f);
            Shake(0.2f, toward);
        }

        /// <summary>
        /// Water or mud: a splash on the ally, its drops thrown out and falling back (mud dark and
        /// dull, water catching the light).
        /// </summary>
        private void WaterBlow(Vector2 at, Vector2 feet, Vector2 toward, bool mud)
        {
            var water = mud ? new Color(0.36f, 0.27f, 0.2f) : new Color(0.65f, 0.85f, 1f);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = mud ? 0.8f : 1.2f,
                    From = mud ? new Color(0.55f, 0.43f, 0.32f) : Color.white,
                    To = new Color(water.r, water.g, water.b, 0f),
                    Speed = new Vector2(200f, 620f),
                    Direction = Angle(toward * 0.4f + Vector2.up * 0.6f),
                    Spread = 150f,
                    Life = new Vector2(0.35f, 0.7f),
                    Size = new Vector2(4f, 10f),
                    Stretch = 0.012f,
                    Gravity = 1500f,
                    Area = 30f,
                },
                22
            );
            Shockwave(feet, 240f, 0.4f);
            HitStop(0.05f);
            Shake(0.3f, toward);
        }

        // Dry sand in the evening light: pale and warm, so a wave of it reads on the red floor.
        private static readonly Color Sand = new(0.9f, 0.76f, 0.56f);

        /// <summary>
        /// A wave of sand: a low wall of it sweeps in along the floor and over the ally, its grains
        /// streaming through and dropping out as it slows (sand does not hang like smoke).
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
                30
            );
            HitStop(0.05f);
            Shake(0.35f, toward);
        }

        /// <summary>
        /// A gust: streaks of air rushing through the ally, dust swept along the floor and leaves
        /// tumbling.
        /// </summary>
        private void WindBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
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
                },
                14
            );
            DustKick(feet, toward, 220f);
            Spray(
                frontLight,
                at,
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
                },
                6
            );
            HitStop(0.04f);
            Shake(0.3f, toward);
        }

        /// <summary>
        /// A shriek or a howl: its wave of pressure shakes dust out along the floor and jolts the
        /// ally.
        /// </summary>
        private void SoundBlow(Vector2 at, Vector2 feet, Vector2 toward)
        {
            Shockwave(feet, 360f, 0.6f);
            HitStop(0.03f);
            Shake(0.4f, toward);
        }

        /// <summary>
        /// A spell or a curse: its light bursting on the ally and motes of it drifting off.
        /// </summary>
        private void MagicBlow(Vector2 at, Vector2 feet, Color color, Vector2 toward)
        {
            HotFlash(at, color, 200f);
            Twinkle(at, Color.Lerp(color, Color.white, 0.4f), 180f, 0.25f, 0f, 1f);
            Spray(
                frontLight,
                at,
                new Burst
                {
                    Shape = BattleVfxShape.Spark,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(60f, 260f),
                    Direction = 90f,
                    Spread = 300f,
                    Life = new Vector2(0.4f, 0.9f),
                    Size = new Vector2(3f, 7f),
                    Gravity = -60f,
                    Drag = 2.5f,
                    Area = 30f,
                },
                16
            );
            Illuminate(at, color, 0.7f, 300f, 0.1f, 0.35f, spill: false);
            Pool(feet, color, 220f, 0.1f, 0.4f);
            HitStop(0.04f);
            Shake(0.25f, toward);
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
    }
}
