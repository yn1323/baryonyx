using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>What a passive skill does in battle; each kind is one passive (doc/features/passives.md).</summary>
    public enum CombatPassiveKind
    {
        StarChart,
        ThermalShock,
        HeadStart,
        ChainedArrows,
        Cover,
        IaiForm,
        OverflowingPrayer,
        WalkersBlessing,
        HolyFlameHammer,
        PurifyingVow,
        MarkTheWeak,
        SilentKill,
        ChargedShield,
        ManaShield,
        Riposte,
        OpenWounds,
        Copycat,
        StandingAlone,
    }

    /// <summary>
    /// One passive skill: who has it, the level that unlocks it, its name and its text. It works
    /// by itself in battle while its holder stands (<see cref="CombatPassiveRules"/>).
    /// </summary>
    public sealed class CombatPassive
    {
        /// <summary>The id of its definition (doc/catalog/passives), e.g. "passive-star-chart".</summary>
        public string Id;
        public CombatPassiveKind Kind;
        public string Name;

        /// <summary>The description, with keywords in brackets ("[やけど]") as a card's text has them.</summary>
        public string Text;

        /// <summary>The character who has it (the mock data's id, e.g. "toma").</summary>
        public string Character;
        public int UnlockLevel;

        /// <summary>Its icon's file name under the passives' art folder, without the extension.</summary>
        public string Icon => "Passive" + Kind;

        /// <summary>The description without the keywords' brackets.</summary>
        public string PlainText => CardKeywords.Plain(Text);
    }

    /// <summary>
    /// The passive skills of the mock data's nine characters, two each (provisional:
    /// doc/features/passives.md). The first unlocks at level 1 and the second at level 10.
    /// </summary>
    public static class CombatPassives
    {
        public const int FirstUnlockLevel = 1;
        public const int SecondUnlockLevel = 10;

        public static readonly IReadOnlyList<CombatPassive> All = new[]
        {
            Pair(
                "toma",
                CombatPassiveKind.StarChart,
                "star-chart",
                "星図の計算",
                "ターンの終わりに残ったエネルギーを1まで、次のターンへ持ち越す。"
            ),
            Second(
                "toma",
                CombatPassiveKind.ThermalShock,
                "thermal-shock",
                "寒暖の理",
                "[やけど]と[凍え]が両方付いた敵へのダメージ+30%。"
            ),
            Pair(
                "luka",
                CombatPassiveKind.HeadStart,
                "head-start",
                "早駆け",
                "戦闘の始めに配る手札+1。"
            ),
            Second(
                "luka",
                CombatPassiveKind.ChainedArrows,
                "chained-arrows",
                "連ね矢",
                "ルカの×回数の攻撃は、回数+1。"
            ),
            Pair(
                "aria",
                CombatPassiveKind.Cover,
                "cover",
                "かばう",
                "HPが30%以下の味方への単体攻撃を、1ターンに1回、代わりに受ける。"
            ),
            Second(
                "aria",
                CombatPassiveKind.IaiForm,
                "iai-form",
                "居合の型",
                "毎ターン、味方が最初に使う斬属性の攻撃のダメージ+40%。"
            ),
            Pair(
                "mina",
                CombatPassiveKind.OverflowingPrayer,
                "overflowing-prayer",
                "あふれる祈り",
                "ミナの回復で最大HPを超えた分を、その味方の[ブロック]にする。"
            ),
            Second(
                "mina",
                CombatPassiveKind.WalkersBlessing,
                "walkers-blessing",
                "歩み手の加護",
                "ミナの回復量が、今日のACTが多いほど上がる（最大+50%）。"
            ),
            Pair(
                "anselm",
                CombatPassiveKind.HolyFlameHammer,
                "holy-flame-hammer",
                "聖火の鉄槌",
                "アンセルムの打属性の攻撃は、[やけど]2ターンを付ける。"
            ),
            Second(
                "anselm",
                CombatPassiveKind.PurifyingVow,
                "purifying-vow",
                "浄めの誓い",
                "味方に付く[状態異常]を、1ターンに1回防ぐ。"
            ),
            Pair(
                "greta",
                CombatPassiveKind.MarkTheWeak,
                "mark-the-weak",
                "急所狙い",
                "HPが半分以下の敵へ、グレタの攻撃のダメージ+30%。"
            ),
            Second(
                "greta",
                CombatPassiveKind.SilentKill,
                "silent-kill",
                "影の一刺し",
                "グレタの攻撃で敵を倒すと、エネルギー+1（1ターンに1回）。"
            ),
            Pair(
                "lutz",
                CombatPassiveKind.ChargedShield,
                "charged-shield",
                "帯電の盾",
                "ルッツが[ブロック]を得るたびに、ランダムな敵に属攻の20%の雷属性ダメージ。"
            ),
            Second(
                "lutz",
                CombatPassiveKind.ManaShield,
                "mana-shield",
                "魔力の盾",
                "ルッツの雷属性の攻撃は、与えたダメージの20%を自分の[ブロック]にする。"
            ),
            Pair(
                "rita",
                CombatPassiveKind.Riposte,
                "riposte",
                "切り返し",
                "リタが単体攻撃を受けると、その敵に物攻の30%の斬属性ダメージ。"
            ),
            Second(
                "rita",
                CombatPassiveKind.OpenWounds,
                "open-wounds",
                "傷口を開く",
                "[出血]中の敵にリタの攻撃が当たるたびに、[出血]のダメージがもう1回起きる。"
            ),
            Pair(
                "ritsu",
                CombatPassiveKind.Copycat,
                "copycat",
                "見よう見まね",
                "ほかの味方が氷・雷属性のカードを使うたびに、リツの氷・雷属性のダメージ+10%（最大+50%）。"
            ),
            Second(
                "ritsu",
                CombatPassiveKind.StandingAlone,
                "standing-alone",
                "独り立ち",
                "見よう見まねが最大になると、リツの氷・雷属性のカードのコスト-1。"
            ),
        };

        /// <summary>The passives of <paramref name="character"/>, first to unlock first; none for an unknown one.</summary>
        public static IReadOnlyList<CombatPassive> Of(string character)
        {
            var found = new List<CombatPassive>();
            foreach (var passive in All)
                if (passive.Character == character)
                    found.Add(passive);
            return found;
        }

        /// <summary>The passives of <paramref name="character"/> that <paramref name="level"/> has unlocked.</summary>
        public static IReadOnlyList<CombatPassive> UnlockedAt(string character, int level)
        {
            var found = new List<CombatPassive>();
            foreach (var passive in Of(character))
                if (passive.UnlockLevel <= level)
                    found.Add(passive);
            return found;
        }

        public static CombatPassive Find(CombatPassiveKind kind)
        {
            foreach (var passive in All)
                if (passive.Kind == kind)
                    return passive;
            return null;
        }

        private static CombatPassive Pair(
            string character,
            CombatPassiveKind kind,
            string id,
            string name,
            string text
        ) => Passive(character, kind, id, name, text, FirstUnlockLevel);

        private static CombatPassive Second(
            string character,
            CombatPassiveKind kind,
            string id,
            string name,
            string text
        ) => Passive(character, kind, id, name, text, SecondUnlockLevel);

        private static CombatPassive Passive(
            string character,
            CombatPassiveKind kind,
            string id,
            string name,
            string text,
            int level
        ) =>
            new()
            {
                Id = "passive-" + id,
                Kind = kind,
                Name = name,
                Text = text,
                Character = character,
                UnlockLevel = level,
            };
    }

    /// <summary>
    /// The numbers and the plain rules of the passive skills (provisional:
    /// doc/catalog/passives). The battle applies them as its turns and blows go.
    /// </summary>
    public static class CombatPassiveRules
    {
        /// <summary>The most energy 星図の計算 carries over to the next turn.</summary>
        public const int CarriedEnergy = 1;

        /// <summary>The cards 早駆け adds to the opening hand.</summary>
        public const int HeadStartCards = 1;

        /// <summary>How low (percent of the max HP) an ally's HP is for かばう to step in.</summary>
        public const int CoverHpPercent = 30;

        public const int ThermalShockPercent = 30;
        public const int IaiFormPercent = 40;
        public const int MarkTheWeakPercent = 30;

        /// <summary>歩み手の加護: the heal grows by this for every 1,000 ACT walked today, up to the cap.</summary>
        public const int WalkersStepPercent = 5;
        public const int WalkersCapPercent = 50;

        /// <summary>聖火の鉄槌: the burn's turns and its base chance, the same as a status a blow adds.</summary>
        public const int HolyFlameTurns = 2;
        public const int HolyFlameChance = 60;

        /// <summary>帯電の盾: the bolt's part of 属攻.</summary>
        public const int ChargedShieldPercent = 20;

        /// <summary>魔力の盾: the part of the damage dealt that turns into block.</summary>
        public const int ManaShieldPercent = 20;

        /// <summary>切り返し: the counter's part of 物攻.</summary>
        public const int RipostePercent = 30;

        /// <summary>見よう見まね: the bonus each ice or thunder card of another ally adds, and its cap.</summary>
        public const int CopycatStepPercent = 10;
        public const int CopycatCapPercent = 50;

        /// <summary>独り立ち: how much the costs go down.</summary>
        public const int StandingAloneCostDown = 1;

        /// <summary>The energy left at the end of a turn that 星図の計算 carries over.</summary>
        public static int Carry(int energyLeft) => Math.Max(0, Math.Min(CarriedEnergy, energyLeft));

        /// <summary>True when an ally at <paramref name="hp"/> of <paramref name="maxHp"/> is low enough for かばう.</summary>
        public static bool IsLow(int hp, int maxHp) =>
            hp > 0 && maxHp > 0 && hp * 100 <= maxHp * CoverHpPercent;

        /// <summary>
        /// True when <paramref name="coverer"/>'s かばう takes the blow aimed at
        /// <paramref name="target"/>: the target is low, the coverer is another ally who is not
        /// low itself, and it has not covered yet this turn.
        /// </summary>
        public static bool Covers(
            int target,
            int targetHp,
            int targetMaxHp,
            int coverer,
            int covererHp,
            int covererMaxHp,
            bool usedThisTurn
        ) =>
            !usedThisTurn
            && coverer >= 0
            && coverer != target
            && covererHp > 0
            && IsLow(targetHp, targetMaxHp)
            && !IsLow(covererHp, covererMaxHp);

        /// <summary>How much 歩み手の加護 grows a heal with today's <paramref name="act"/>, in percent.</summary>
        public static int WalkersBonusPercent(int act) =>
            act <= 0
                ? 0
                : Math.Min(WalkersCapPercent, act / CardRules.ActPerStep * WalkersStepPercent);

        /// <summary>見よう見まね's bonus after <paramref name="seen"/> ice or thunder cards of the others, in percent.</summary>
        public static int CopycatPercent(int seen) =>
            Math.Min(CopycatCapPercent, Math.Max(0, seen) * CopycatStepPercent);

        /// <summary>True once 見よう見まね is at its cap, when 独り立ち works.</summary>
        public static bool CopycatFull(int seen) => CopycatPercent(seen) >= CopycatCapPercent;

        /// <summary>True for the elements 見よう見まね and 独り立ち watch: ice and thunder.</summary>
        public static bool IsCopycatElement(CardElement element) =>
            element is CardElement.Ice or CardElement.Thunder;

        /// <summary>The block 魔力の盾 gives for <paramref name="dealt"/> damage.</summary>
        public static int ManaShieldBlock(int dealt) =>
            dealt <= 0 ? 0 : Math.Max(1, Percent(dealt, ManaShieldPercent));

        /// <summary>How much healing goes past the max HP, which あふれる祈り turns into block.</summary>
        public static int Overflow(int hp, int maxHp, int heal) => Math.Max(0, hp + heal - maxHp);

        /// <summary><paramref name="value"/> grown by <paramref name="percent"/>, rounded half away from zero.</summary>
        public static int Grow(int value, int percent) =>
            percent == 0
                ? value
                : (int)Math.Round(value * (1 + percent / 100.0), MidpointRounding.AwayFromZero);

        /// <summary><paramref name="percent"/> of <paramref name="value"/>, rounded half away from zero.</summary>
        public static int Percent(int value, int percent) =>
            (int)Math.Round(value * percent / 100.0, MidpointRounding.AwayFromZero);

        /// <summary>True when the card deals damage more than once in a row ("×回数").</summary>
        public static bool HitsMoreThanOnce(CardSkill skill)
        {
            foreach (var action in skill.Actions)
                if (action.Kind == CardActionKind.Damage && action.Hits > 1)
                    return true;
            return false;
        }

        /// <summary>
        /// The card with 連ね矢: each damage that lands more than once lands once more. The card
        /// itself is left as it is; a copy of it is returned, or the card when nothing changes.
        /// </summary>
        public static CardSkill WithExtraHit(CardSkill skill)
        {
            if (skill == null || !HitsMoreThanOnce(skill))
                return skill;
            var actions = new List<CardAction>();
            foreach (var action in skill.Actions)
            {
                if (action.Kind == CardActionKind.Damage && action.Hits > 1)
                {
                    var more = action.Copy();
                    more.Hits++;
                    actions.Add(more);
                }
                else
                    actions.Add(action);
            }
            var copy = skill.Copy();
            copy.Actions = actions;
            return copy;
        }
    }
}
