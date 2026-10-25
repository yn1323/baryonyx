using System;
using System.Collections.Generic;
using static Baryonyx.Combat.CardAction;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The card skills (provisional: doc/catalog/skills/README.md). Each card's power is a part
    /// of its user's stat (doc/features/combat.md, how power is decided); the user is the battle
    /// mock's party member, not a decided character. The first six are the mock's first cards.
    /// </summary>
    public static class CardSkills
    {
        private const CardTarget Enemy = CardTarget.OneEnemy;
        private const CardTarget Enemies = CardTarget.AllEnemies;
        private const CardTarget Random = CardTarget.RandomEnemies;
        private const CardTarget Chain = CardTarget.ChainEnemies;
        private const CardTarget Ally = CardTarget.OneAlly;
        private const CardTarget Allies = CardTarget.AllAllies;
        private const CardTarget Self = CardTarget.Self;
        private const CardTarget Nobody = CardTarget.None;
        private const CardStat Phy = CardStat.PhysicalAttack;
        private const CardStat Mag = CardStat.MagicAttack;
        private const CardStat Def = CardStat.PhysicalDefense;

        private static readonly CardElement[] Magic =
        {
            CardElement.Fire,
            CardElement.Ice,
            CardElement.Thunder,
        };

        /// <summary>
        /// How many sparkle styles the card skills take, one each in their order; the unique
        /// skills and the prototypes take the styles after them.
        /// </summary>
        public const int FoilStyles = 50;

        public static readonly IReadOnlyList<CardSkill> All = Styled(
            0,
            // --- The mock's first six ---------------------------------------------
            Card(
                "Slash",
                "斬り払い",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Attack,
                Enemy,
                1,
                2.4f,
                "敵1体に{0}ダメージ。[出血]中の敵には1.5倍。[合体]フレイムエンチャント。",
                Damage(Enemy, Phy, 150).Bonus(CardCondition.OnBleeding, 50)
            ).Fuses("FlameEnchant", "FlameSlash"),
            Card(
                "Fire",
                "ファイア",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemy,
                1,
                2.4f,
                "敵1体に{0}ダメージ。[弱点]を突くと60%で[やけど]2ターン。",
                Damage(Enemy, Mag, 150),
                Apply(Enemy, CardStatus.Burn, 2).WithChance(60).Only(CardCondition.OnWeakness)
            ),
            Card(
                "Ice",
                "アイスランス",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Attack,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。60%で[凍え]2ターン。[合体]ライトニングボルト。",
                Damage(Enemy, Mag, 120),
                Apply(Enemy, CardStatus.Chill, 2).WithChance(60)
            ).Fuses("LightningBolt", "FrostSpear"),
            Card(
                "Thunder",
                "サンダー",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Enemies,
                2,
                2.2f,
                "敵全体に{0}ダメージ。[麻痺]中の敵には1.5倍。",
                Damage(Enemies, Mag, 100).Bonus(CardCondition.OnParalyzed, 50)
            ),
            Card(
                "Heal",
                "ヒール",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Ally,
                1,
                2.5f,
                "味方1体のHPを{0}回復する。",
                Heal(Ally, Mag, 100)
            ),
            Card(
                "Guard",
                "ガード",
                CardUser.Aria,
                CardElement.None,
                CardKind.Guard,
                Allies,
                1,
                2.5f,
                "味方全体に[ブロック]{0}。捨てたとき：自分に[ブロック]{1}。",
                Block(Allies, Def, 35),
                Block(Self, Def, 50).WhenDiscarded()
            ),
            // --- Aria: slash, blunt, guarding the party ---------------------------
            Card(
                "Whirlwind",
                "旋風斬",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Attack,
                Enemies,
                2,
                2f,
                "敵全体に{0}ダメージ。60%で[出血]2ターン。",
                Damage(Enemies, Phy, 90),
                Apply(Enemies, CardStatus.Bleed, 2).WithChance(60)
            ),
            Card(
                "Iai",
                "居合一閃",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Attack,
                Enemy,
                3,
                2.8f,
                "敵1体に{0}ダメージ。HPが半分以下の敵には1.5倍。[保持]。",
                Damage(Enemy, Phy, 420).Bonus(CardCondition.OnWounded, 50)
            ).Is(CardTrait.Retain | CardTrait.CutIn),
            Card(
                "BladeDance",
                "千刃乱舞",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Attack,
                Random,
                2,
                1.6f,
                "手札をすべて[捨てる]。ランダムな敵に{0}ダメージ×3（＋捨てた枚数）。[消滅]。",
                Discard(-1),
                Damage(Random, Phy, 50, hits: 3).Discarded()
            ).Is(CardTrait.Exhaust | CardTrait.CutIn),
            Card(
                "Hone",
                "研ぎ澄ます",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Support,
                Nobody,
                0,
                2f,
                "次に使う斬カードの威力+50%。カードを1枚引く。[霧散]。",
                NextCardBoost(50, CardElement.Slash),
                Simple(CardActionKind.Draw, Nobody, 1)
            ).Is(CardTrait.Ethereal),
            Card(
                "ShieldBash",
                "シールドバッシュ",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Attack,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}と、自分の[ブロック]の50%のダメージ。",
                Damage(Enemy, Def, 80).WithBlock(50)
            ),
            Card(
                "ArmorBreak",
                "鎧砕き",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Debuff,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。[防御ダウン]2ターン。[初手]。",
                Damage(Enemy, Phy, 80),
                Apply(Enemy, CardStatus.Vulnerable, 2).WithChance(100)
            ).Is(CardTrait.Innate),
            Card(
                "EarthSplitter",
                "大地割り",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Attack,
                Enemies,
                2,
                2.4f,
                "敵全体に{0}ダメージ。[防御ダウン]中は1.5倍。[連携]鎧砕き：先に敵全体を[防御ダウン]2ターン。",
                Damage(Enemies, Phy, 110).Bonus(CardCondition.OnVulnerable, 50),
                Apply(Enemies, CardStatus.Vulnerable, 2).WithChance(100).OnCombo()
            ).Combo("ArmorBreak"),
            Card(
                "GiantImpact",
                "ギガントインパクト",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Attack,
                Enemy,
                3,
                2.8f,
                "敵1体に{0}ダメージ。自分に[ブロック]があれば1.5倍。[眠り札]。",
                Damage(Enemy, Phy, 380).Bonus(CardCondition.OnGuarded, 50)
            ).Is(CardTrait.Sleeper | CardTrait.CutIn),
            Card(
                "StrideStrike",
                "健脚の一撃",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Attack,
                Enemy,
                1,
                2.4f,
                "敵1体に{0}ダメージ。[歩み]。",
                Damage(Enemy, Phy, 150).WithAct(10, 100)
            ),
            Card(
                "GuardianOath",
                "守護の誓い",
                CardUser.Aria,
                CardElement.None,
                CardKind.Guard,
                Self,
                2,
                2f,
                "自分に[ブロック]{0}。2ターン、敵の攻撃を引きつける。[保持]。",
                Block(Self, Def, 90),
                Apply(Self, CardStatus.Taunt, 2)
            ).Is(CardTrait.Retain),
            Card(
                "WarCry",
                "鬨の声",
                CardUser.Aria,
                CardElement.None,
                CardKind.Buff,
                Allies,
                1,
                2f,
                "味方全体の攻撃力を2ターン25%上げる。[初手]。",
                Apply(Allies, CardStatus.AttackUp, 2, 25)
            ).Is(CardTrait.Innate),
            // --- Toma: fire and ice -----------------------------------------------
            Card(
                "Embers",
                "火の粉",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemy,
                0,
                2.2f,
                "敵1体に{0}ダメージ。60%で[やけど]2ターン。捨てたとき：ランダムな敵に60%で[やけど]2ターン。",
                Damage(Enemy, Mag, 40),
                Apply(Enemy, CardStatus.Burn, 2).WithChance(60),
                Apply(Random, CardStatus.Burn, 2).WithChance(60).WhenDiscarded()
            ),
            Card(
                "FlamePillar",
                "フレイムピラー",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemy,
                2,
                2.4f,
                "敵1体に{0}ダメージ。[やけど]中の敵には1.5倍。",
                Damage(Enemy, Mag, 220).Bonus(CardCondition.OnBurning, 50)
            ),
            Card(
                "FireStorm",
                "ファイアストーム",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemies,
                2,
                2.2f,
                "敵全体に{0}ダメージ。60%で[やけど]2ターン。",
                Damage(Enemies, Mag, 100),
                Apply(Enemies, CardStatus.Burn, 2).WithChance(60)
            ),
            Card(
                "BlastSigil",
                "爆炎の刻印",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemy,
                1,
                2.4f,
                "敵1体に[刻印]。次のターン、敵全体に{0}ダメージ。[初手]。",
                Apply(Enemy, CardStatus.BlastSigil, 1, Mag, 150)
            ).Is(CardTrait.Innate),
            Card(
                "Meteor",
                "メテオ",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemies,
                3,
                3f,
                "敵全体に{0}ダメージ。[やけど]中の敵には2倍。[眠り札]。",
                Damage(Enemies, Mag, 250).Bonus(CardCondition.OnBurning, 100)
            ).Is(CardTrait.Sleeper | CardTrait.CutIn),
            Card(
                "FlameEnchant",
                "フレイムエンチャント",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Buff,
                Ally,
                1,
                2f,
                "味方1体の攻撃に、3ターン炎属性を加える。[合体]斬り払い。",
                Apply(Ally, CardStatus.FireBlade, 3)
            ).Fuses("Slash", "FlameSlash"),
            Card(
                "Icicles",
                "つらら落とし",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Attack,
                Random,
                1,
                1.7f,
                "ランダムな敵に{0}ダメージ×4。捨てたとき：ランダムな敵に{1}ダメージ×2。",
                Damage(Random, Mag, 40, hits: 4),
                Damage(Random, Mag, 40, hits: 2).WhenDiscarded()
            ),
            Card(
                "Blizzard",
                "ブリザード",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Attack,
                Enemies,
                2,
                2.2f,
                "敵全体に{0}ダメージ。60%で[凍え]2ターン。[眠り札]。",
                Damage(Enemies, Mag, 90),
                Apply(Enemies, CardStatus.Chill, 2).WithChance(60)
            ).Is(CardTrait.Sleeper),
            Card(
                "IceMirror",
                "アイスミラー",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Guard,
                Ally,
                1,
                1.6f,
                "味方1体が次に受ける単体攻撃を、1回はね返す。[保持]。",
                Apply(Ally, CardStatus.Reflect, 1)
            ).Is(CardTrait.Retain),
            Card(
                "AbsoluteZero",
                "絶対零度",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Attack,
                Enemy,
                3,
                2.4f,
                "敵1体に{0}ダメージ。[凍え]中の敵には1.5倍。40%で[凍結]させる。[消滅]。",
                Damage(Enemy, Mag, 450).Bonus(CardCondition.OnChilled, 50),
                Apply(Enemy, CardStatus.Freeze, 1).WithChance(40)
            ).Is(CardTrait.Exhaust | CardTrait.CutIn),
            Card(
                "QuickCast",
                "詠唱短縮",
                CardUser.Toma,
                CardElement.None,
                CardKind.Support,
                Nobody,
                0,
                2f,
                "次に使う炎・氷・雷カードのコストを0にする。[霧散]。",
                NextCardFree(Magic)
            ).Is(CardTrait.Ethereal),
            // --- Luka: pierce, thunder, tricks ------------------------------------
            Card(
                "VitalThrust",
                "急所突き",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Attack,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。[弱点]を突くとエネルギー+1。",
                Damage(Enemy, Phy, 130),
                Simple(CardActionKind.Energy, Nobody, 1).Only(CardCondition.OnWeakness)
            ),
            Card(
                "PoisonNeedle",
                "毒針",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Debuff,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。[毒]を3つ重ねる。捨てたとき：ランダムな敵に[毒]を2つ重ねる。",
                Damage(Enemy, Phy, 50),
                Apply(Enemy, CardStatus.Poison, 0, 3).WithChance(100),
                Apply(Random, CardStatus.Poison, 0, 2).WithChance(100).WhenDiscarded()
            ),
            Card(
                "ShadowStitch",
                "影縫い",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Debuff,
                Enemy,
                1,
                2f,
                "敵1体に{0}ダメージ。その敵の行動順を1つ遅らせる。捨てたとき：ランダムな敵の行動順を1つ遅らせる。",
                Damage(Enemy, Phy, 90),
                Simple(CardActionKind.Delay, Enemy, 1),
                Simple(CardActionKind.Delay, Random, 1).WhenDiscarded()
            ),
            Card(
                "InsightArrow",
                "看破の矢",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Debuff,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。その敵の[弱点]をすべて見抜く。[初手]。",
                Damage(Enemy, Phy, 90),
                Simple(CardActionKind.RevealWeakness, Enemy)
            ).Is(CardTrait.Innate),
            Card(
                "ArrowRain",
                "アローレイン",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Attack,
                Enemies,
                2,
                1.8f,
                "敵全体に{0}ダメージ×2。[毒]がある敵には1.5倍。[連携]毒針：先に敵全体へ[毒]を2つ重ねる。",
                Damage(Enemies, Phy, 50, hits: 2).Bonus(CardCondition.OnPoisoned, 50),
                Apply(Enemies, CardStatus.Poison, 0, 2).WithChance(100).OnCombo()
            ).Combo("PoisonNeedle"),
            Card(
                "ShadowSnipe",
                "シャドウスナイプ",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Attack,
                Enemy,
                3,
                2.6f,
                "敵1体に{0}ダメージ。[弱点]を突くと威力+50%。[保持]。",
                Damage(Enemy, Phy, 400).Bonus(CardCondition.OnWeakness, 50)
            ).Is(CardTrait.Retain | CardTrait.CutIn),
            Card(
                "LightningBolt",
                "ライトニングボルト",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。60%で[麻痺]1ターン。[合体]アイスランス。",
                Damage(Enemy, Mag, 110),
                Apply(Enemy, CardStatus.Paralysis, 1).WithChance(60)
            ).Fuses("Ice", "FrostSpear"),
            Card(
                "ChainLightning",
                "チェインライトニング",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Chain,
                2,
                1.8f,
                "敵1体に{0}ダメージ×3。1回ごとに別の敵へ移る。",
                Damage(Chain, Mag, 90, hits: 3)
            ),
            Card(
                "Thundercloud",
                "雷雲",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Nobody,
                2,
                2f,
                "3ターンの間、毎ターンランダムな敵1体に{0}ダメージ。[消滅]。",
                Apply(Nobody, CardStatus.Thundercloud, 3, Mag, 100)
            ).Is(CardTrait.Exhaust),
            Card(
                "ThunderSpear",
                "雷神の槍",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Enemy,
                3,
                2.6f,
                "手札を1枚[捨てる]。敵1体に{0}、さらに敵全体に{1}ダメージ。",
                Discard(1),
                Damage(Enemy, Mag, 380),
                Damage(Enemies, Mag, 60)
            ).Is(CardTrait.CutIn),
            Card(
                "Gale",
                "疾風迅雷",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Buff,
                Allies,
                1,
                2f,
                "味方全体の速度を2ターン上げる。カードを1枚引く。",
                Apply(Allies, CardStatus.SpeedUp, 2, 30),
                Simple(CardActionKind.Draw, Nobody, 1)
            ),
            Card(
                "Scout",
                "偵察",
                CardUser.Luka,
                CardElement.None,
                CardKind.Support,
                Nobody,
                0,
                2f,
                "カードを2枚引き、手札を1枚[捨てる]。",
                Simple(CardActionKind.Draw, Nobody, 2),
                Discard(1)
            ),
            // --- Mina: healing, protecting, holy blows ----------------------------
            Card(
                "StepPrayer",
                "万歩の祈り",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Allies,
                2,
                2.2f,
                "味方全体のHPを{0}回復。[歩み]。",
                Heal(Allies, Mag, 80).WithAct(10, 100)
            ),
            Card(
                "Regen",
                "リジェネ",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Ally,
                1,
                2.2f,
                "味方1体を、3ターンの間、毎ターン{0}回復する。[眠り札]。",
                Apply(Ally, CardStatus.Regen, 3, Mag, 35)
            ).Is(CardTrait.Sleeper),
            Card(
                "Resurrection",
                "リザレクション",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Ally,
                2,
                2f,
                "味方1体のHPを最大の半分回復。倒れていれば復活。[消滅]。",
                Simple(CardActionKind.Revive, Ally, 50)
            ).Is(CardTrait.Exhaust | CardTrait.CutIn),
            Card(
                "HolyHammer",
                "聖なる鉄槌",
                CardUser.Mina,
                CardElement.Blunt,
                CardKind.Attack,
                Enemy,
                2,
                2.2f,
                "敵1体に{0}ダメージ。[強化]を1つ消す。",
                Damage(Enemy, Mag, 230),
                Simple(CardActionKind.Dispel, Enemy, 1)
            ),
            Card(
                "Protect",
                "プロテクト",
                CardUser.Mina,
                CardElement.None,
                CardKind.Guard,
                Allies,
                1,
                2f,
                "味方全体の受けるダメージを、2ターン25%減らす。[保持]。",
                Apply(Allies, CardStatus.Protect, 2, 25)
            ).Is(CardTrait.Retain),
            Card(
                "Purify",
                "浄化の光",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Allies,
                0,
                2.2f,
                "味方全体の[状態異常]を消し、HPを{0}回復。[霧散]。",
                Simple(CardActionKind.Cleanse, Allies),
                Heal(Allies, Mag, 25)
            ).Is(CardTrait.Ethereal),
            Card(
                "ManaPrayer",
                "マナの祈り",
                CardUser.Mina,
                CardElement.None,
                CardKind.Support,
                Nobody,
                0,
                1.5f,
                "エネルギー+2。[消滅]。",
                Simple(CardActionKind.Energy, Nobody, 2)
            ).Is(CardTrait.Exhaust),
            Card(
                "Revelation",
                "天啓",
                CardUser.Mina,
                CardElement.None,
                CardKind.Support,
                Nobody,
                1,
                2f,
                "捨て札からカードを1枚選び、手札に加える。[消滅]。",
                TakeFromDiscard(1)
            ).Is(CardTrait.Exhaust),
            Card(
                "HolyLight",
                "ホーリーライト",
                CardUser.Mina,
                CardElement.None,
                CardKind.Attack,
                Enemies,
                2,
                2.2f,
                "敵全体に{0}ダメージ。味方全体のHPを{1}回復。",
                Damage(Enemies, Mag, 70),
                Heal(Allies, Mag, 35)
            ),
            Card(
                "DivineShield",
                "ディバインシールド",
                CardUser.Mina,
                CardElement.None,
                CardKind.Guard,
                Ally,
                2,
                1.6f,
                "味方1体は、次のターンまでダメージを受けない。[消滅]。",
                Apply(Ally, CardStatus.Invincible, 1)
            ).Is(CardTrait.Exhaust)
        );

        /// <summary>
        /// Cards tried only in the battle mock, not among the card skills (<see cref="All"/>): not
        /// counted with them, not offered in the loadout, and without art of their own.
        /// 蛇牙の連撃 has many effects, to try the keyword notes stacked beside the raised card
        /// (doc/features/combat.md, card keywords).
        /// </summary>
        public static readonly IReadOnlyList<CardSkill> Prototypes = Styled(
            // 固有スキル（4人×3つの12種類）の後の光り方。
            FoilStyles + 12,
            Borrow(
                "VitalThrust",
                Card(
                    "SerpentFang",
                    "蛇牙の連撃",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    2,
                    1.6f,
                    "敵1体に{0}ダメージ×2。60%で[出血]・[毒]・[防御ダウン]。[弱点]でエネルギー+1。",
                    Damage(Enemy, Phy, 45, hits: 2),
                    Apply(Enemy, CardStatus.Bleed, 2).WithChance(60),
                    Apply(Enemy, CardStatus.Poison, 0, 2).WithChance(60),
                    Apply(Enemy, CardStatus.Vulnerable, 2).WithChance(60),
                    Simple(CardActionKind.Energy, Nobody, 1).Only(CardCondition.OnWeakness)
                )
            )
        );

        /// <summary>
        /// The fused cards (合体): two cards of the hand marked [合体] with each other become one
        /// of these for the battle (doc/features/combat.md, カードのキーワード). Not owned, not in
        /// the loadout, and without art of their own.
        /// </summary>
        public static readonly IReadOnlyList<CardSkill> Fusions = Styled(
            // 試作のカードの後の光り方。
            FoilStyles + 13,
            Borrow(
                "Slash",
                Card(
                    "FlameSlash",
                    "炎の斬り払い",
                    CardUser.Aria,
                    CardElement.Fire,
                    CardKind.Attack,
                    Enemy,
                    1,
                    1f,
                    "敵1体に{0}の炎属性ダメージ。60%で[やけど]3ターン。[消滅]。",
                    Damage(Enemy, Phy, 250),
                    Apply(Enemy, CardStatus.Burn, 3).WithChance(60)
                ).Is(CardTrait.Exhaust)
            ),
            Borrow(
                "Ice",
                Card(
                    "FrostSpear",
                    "氷雷の槍",
                    CardUser.Toma,
                    CardElement.Ice,
                    CardKind.Attack,
                    Enemy,
                    1,
                    1f,
                    "敵1体に{0}ダメージ。60%で[凍え]2ターンと[麻痺]1ターン。[消滅]。",
                    Damage(Enemy, Mag, 200),
                    Apply(Enemy, CardStatus.Chill, 2).WithChance(60),
                    Apply(Enemy, CardStatus.Paralysis, 1).WithChance(60)
                ).Is(CardTrait.Exhaust)
            )
        );

        /// <summary>
        /// The card (or prototype, or a card skill's awakened stage such as "Fire.1",
        /// doc/features/skill-awakening.md) with <paramref name="id"/>, or null.
        /// </summary>
        public static CardSkill Find(string id)
        {
            foreach (var card in All)
                if (card.Id == id)
                    return card;
            foreach (var card in Prototypes)
                if (card.Id == id)
                    return card;
            foreach (var card in Fusions)
                if (card.Id == id)
                    return card;
            return id != null && id.IndexOf('.') > 0 ? CardSkillAwakenings.Find(id) : null;
        }

        // カードに、first から順に光り方を割り当てる。
        private static IReadOnlyList<CardSkill> Styled(int first, params CardSkill[] cards)
        {
            for (int i = 0; i < cards.Length; i++)
                cards[i].FoilStyle = first + i;
            return Array.AsReadOnly(cards);
        }

        // カードに、消滅・保持・霧散・初手・眠り札とカットインを付ける。
        private static CardSkill Is(this CardSkill card, CardTrait traits)
        {
            card.Traits |= traits;
            return card;
        }

        // 手札にそろうと into に合体する相手を付ける。
        private static CardSkill Fuses(this CardSkill card, string partner, string into)
        {
            card.FuseWith = partner;
            card.FusesInto = into;
            return card;
        }

        // 同じターンに先に使っていると、連携の効果が出る相手を付ける。
        private static CardSkill Combo(this CardSkill card, string partner)
        {
            card.ComboWith = partner;
            return card;
        }

        private static CardSkill Borrow(string art, CardSkill card)
        {
            card.BorrowedArt = art;
            return card;
        }

        private static CardSkill Card(
            string id,
            string name,
            CardUser user,
            CardElement element,
            CardKind kind,
            CardTarget target,
            int cost,
            float limit,
            string text,
            params CardAction[] actions
        ) =>
            new()
            {
                Id = id,
                Name = name,
                User = user,
                Element = element,
                Kind = kind,
                Target = target,
                Cost = cost,
                MaxMultiplier = limit,
                Text = text,
                Actions = Array.AsReadOnly(actions),
            };
    }
}
