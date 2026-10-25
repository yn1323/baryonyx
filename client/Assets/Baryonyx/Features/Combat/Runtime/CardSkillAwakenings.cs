using System;
using System.Collections.Generic;
using static Baryonyx.Combat.CardAction;

namespace Baryonyx.Combat
{
    /// <summary>
    /// What one card skill can awaken into (provisional: doc/features/skill-awakening.md): two
    /// branches of one step each, its own name, cost and actions. A stage keeps its card skill's
    /// user, element, multiplier limit and sparkle; one card that awakened stays so. Each stage
    /// has art of its own ("Card" + the card skill's id + the branch, as CardFire1).
    /// </summary>
    public sealed class CardSkillAwakening
    {
        /// <summary>The card skill it starts as (one of <see cref="CardSkills.All"/>).</summary>
        public CardSkill Skill;

        /// <summary>The two stages it can become ("Fire.1" and "Fire.2").</summary>
        public IReadOnlyList<CardSkill> Branches = Array.Empty<CardSkill>();
    }

    /// <summary>
    /// Every card skill's awakening, in the order of <see cref="CardSkills.All"/>
    /// (doc/catalog/skills/README.md, the awakened stages). The stages' intent and art are
    /// written in each card skill's definition.
    /// </summary>
    public static class CardSkillAwakenings
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

        public static readonly IReadOnlyList<CardSkillAwakening> All = Array.AsReadOnly(
            new[]
            {
                // --- 戦闘画面のモックの最初の6枚 ---
                Of(
                    "Slash",
                    // 1本目の枝：自ら出血を付け、次の一撃を強くする。
                    Stage(
                        "Slash",
                        1,
                        "斬り払い・裂",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[出血]中の敵には1.5倍。60%で[出血]2ターン。[合体]フレイムエンチャント。",
                        Damage(Enemy, Phy, 140).Bonus(CardCondition.OnBleeding, 50),
                        Apply(Enemy, CardStatus.Bleed, 2).WithChance(60)
                    ).Fuses("FlameEnchant", "FlameSlash"),
                    // 2本目の枝：斬りながら構えて身を守る。
                    Stage(
                        "Slash",
                        2,
                        "斬り払い・構",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[出血]中の敵には1.5倍。自分に[ブロック]{1}。[合体]フレイムエンチャント。",
                        Damage(Enemy, Phy, 120).Bonus(CardCondition.OnBleeding, 50),
                        Block(Self, Def, 30)
                    ).Fuses("FlameEnchant", "FlameSlash")
                ),
                Of(
                    "Fire",
                    // 1本目の枝：やけどの敵をさらに焼く。
                    Stage(
                        "Fire",
                        1,
                        "ファイア・灼",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[やけど]中の敵には1.5倍。[弱点]を突くと60%で[やけど]2ターン。",
                        Damage(Enemy, Mag, 130).Bonus(CardCondition.OnBurning, 50),
                        Apply(Enemy, CardStatus.Burn, 2).WithChance(60).Only(CardCondition.OnWeakness)
                    ),
                    // 2本目の枝：小さな火球を2つ続けて放つ。
                    Stage(
                        "Fire",
                        2,
                        "ファイア・連",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ×2。[弱点]を突くと60%で[やけど]2ターン。",
                        Damage(Enemy, Mag, 80, hits: 2),
                        Apply(Enemy, CardStatus.Burn, 2).WithChance(60).Only(CardCondition.OnWeakness)
                    )
                ),
                Of(
                    "Ice",
                    // 1本目の枝：凍えた敵を深く貫く。
                    Stage(
                        "Ice",
                        1,
                        "アイスランス・穿",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[凍え]中の敵には1.5倍。60%で[凍え]2ターン。[合体]ライトニングボルト。",
                        Damage(Enemy, Mag, 110).Bonus(CardCondition.OnChilled, 50),
                        Apply(Enemy, CardStatus.Chill, 2).WithChance(60)
                    ).Fuses("LightningBolt", "FrostSpear"),
                    // 2本目の枝：刺さった所から霜を広げる。
                    Stage(
                        "Ice",
                        2,
                        "アイスランス・霜",
                        CardKind.Attack,
                        Enemy,
                        2,
                        "敵1体に{0}ダメージ。敵全体に60%で[凍え]2ターン。[合体]ライトニングボルト。",
                        Damage(Enemy, Mag, 120),
                        Apply(Enemies, CardStatus.Chill, 2).WithChance(60)
                    ).Fuses("LightningBolt", "FrostSpear")
                ),
                Of(
                    "Thunder",
                    // 1本目の枝：弱点の敵を痺れさせる。
                    Stage(
                        "Thunder",
                        1,
                        "サンダー・痺",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。[麻痺]中の敵には1.5倍。[弱点]を突いた敵を60%で[麻痺]1ターン。",
                        Damage(Enemies, Mag, 90).Bonus(CardCondition.OnParalyzed, 50),
                        Apply(Enemies, CardStatus.Paralysis, 1).WithChance(60).Only(CardCondition.OnWeakness)
                    ),
                    // 2本目の枝：雷を1体に束ねて落とす。
                    Stage(
                        "Thunder",
                        2,
                        "サンダー・落雷",
                        CardKind.Attack,
                        Enemy,
                        2,
                        "敵1体に{0}ダメージ。[麻痺]中の敵には1.5倍。",
                        Damage(Enemy, Mag, 220).Bonus(CardCondition.OnParalyzed, 50)
                    )
                ),
                Of(
                    "Heal",
                    // 1本目の枝：傷とともに状態異常も癒やす。
                    Stage(
                        "Heal",
                        1,
                        "ヒール・癒",
                        CardKind.Heal,
                        Ally,
                        1,
                        "味方1体のHPを{0}回復し、[状態異常]を消す。",
                        Heal(Ally, Mag, 90),
                        Simple(CardActionKind.Cleanse, Ally)
                    ),
                    // 2本目の枝：癒やしを全員に広げる。
                    Stage(
                        "Heal",
                        2,
                        "ヒール・円",
                        CardKind.Heal,
                        Allies,
                        1,
                        "味方全体のHPを{0}回復する。",
                        Heal(Allies, Mag, 45)
                    )
                ),
                Of(
                    "Guard",
                    // 1本目の枝：守りを2重にする。
                    Stage(
                        "Guard",
                        1,
                        "ガード・堅",
                        CardKind.Guard,
                        Allies,
                        1,
                        "味方全体に[ブロック]{0}。受けるダメージを1ターン15%減らす。捨てたとき：自分に[ブロック]{1}。",
                        Block(Allies, Def, 30),
                        Apply(Allies, CardStatus.Protect, 1, 15),
                        Block(Self, Def, 50).WhenDiscarded()
                    ),
                    // 2本目の枝：守りながら次の手を整える。
                    Stage(
                        "Guard",
                        2,
                        "ガード・備",
                        CardKind.Guard,
                        Allies,
                        1,
                        "味方全体に[ブロック]{0}。カードを1枚引く。捨てたとき：自分に[ブロック]{1}。",
                        Block(Allies, Def, 30),
                        Simple(CardActionKind.Draw, Nobody, 1),
                        Block(Self, Def, 50).WhenDiscarded()
                    )
                ),
                // --- アリア：斬・打と、仲間の守り ---
                Of(
                    "Whirlwind",
                    // 1本目の枝：出血の敵を深く斬る。
                    Stage(
                        "Whirlwind",
                        1,
                        "旋風斬・裂",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。[出血]中の敵には1.5倍。60%で[出血]2ターン。",
                        Damage(Enemies, Phy, 80).Bonus(CardCondition.OnBleeding, 50),
                        Apply(Enemies, CardStatus.Bleed, 2).WithChance(60)
                    ),
                    // 2本目の枝：2回転して斬る。
                    Stage(
                        "Whirlwind",
                        2,
                        "旋風斬・嵐",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ×2。60%で[出血]2ターン。",
                        Damage(Enemies, Phy, 50, hits: 2),
                        Apply(Enemies, CardStatus.Bleed, 2).WithChance(60)
                    )
                ),
                Of(
                    "Iai",
                    // 1本目の枝：斬った傷を開いたままにする。
                    Stage(
                        "Iai",
                        1,
                        "居合一閃・断",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "敵1体に{0}ダメージ。HPが半分以下の敵には1.5倍。60%で[出血]3ターン。[保持]。",
                        Damage(Enemy, Phy, 400).Bonus(CardCondition.OnWounded, 50),
                        Apply(Enemy, CardStatus.Bleed, 3).WithChance(60)
                    ).Is(CardTrait.Retain | CardTrait.CutIn),
                    // 2本目の枝：抜いて返す2太刀にする。
                    Stage(
                        "Iai",
                        2,
                        "居合一閃・燕",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "敵1体に{0}ダメージ×2。HPが半分以下の敵には1.5倍。[保持]。",
                        Damage(Enemy, Phy, 220, hits: 2).Bonus(CardCondition.OnWounded, 50)
                    ).Is(CardTrait.Retain | CardTrait.CutIn)
                ),
                Of(
                    "BladeDance",
                    // 1本目の枝：乱舞の跡に出血を残す。
                    Stage(
                        "BladeDance",
                        1,
                        "千刃乱舞・紅",
                        CardKind.Attack,
                        Random,
                        2,
                        "手札をすべて[捨てる]。ランダムな敵に{0}ダメージ×3（＋捨てた枚数）。敵全体に60%で[出血]2ターン。[消滅]。",
                        Discard(-1),
                        Damage(Random, Phy, 45, hits: 3).Discarded(),
                        Apply(Enemies, CardStatus.Bleed, 2).WithChance(60)
                    ).Is(CardTrait.Exhaust | CardTrait.CutIn),
                    // 2本目の枝：散った刃の分だけ手を引き直す。
                    Stage(
                        "BladeDance",
                        2,
                        "千刃乱舞・閃",
                        CardKind.Attack,
                        Random,
                        2,
                        "手札をすべて[捨てる]。ランダムな敵に{0}ダメージ×3（＋捨てた枚数）。捨てた枚数だけカードを引く。[消滅]。",
                        Discard(-1),
                        Damage(Random, Phy, 50, hits: 3).Discarded(),
                        Simple(CardActionKind.Draw, Nobody, 0).Discarded()
                    ).Is(CardTrait.Exhaust | CardTrait.CutIn)
                ),
                Of(
                    "Hone",
                    // 1本目の枝：研いだ刃で次の手を引き寄せる。
                    Stage(
                        "Hone",
                        1,
                        "研ぎ澄ます・冴",
                        CardKind.Support,
                        Nobody,
                        0,
                        "次に使う斬カードの威力+50%。カードを2枚引き、手札を1枚[捨てる]。[霧散]。",
                        NextCardBoost(50, CardElement.Slash),
                        Simple(CardActionKind.Draw, Nobody, 2),
                        Discard(1)
                    ).Is(CardTrait.Ethereal),
                    // 2本目の枝：斬だけでなく打と貫も研ぐ。
                    Stage(
                        "Hone",
                        2,
                        "研ぎ澄ます・刃",
                        CardKind.Support,
                        Nobody,
                        0,
                        "次に使う斬・打・貫カードの威力+50%。カードを1枚引く。[霧散]。",
                        NextCardBoost(50, CardElement.Slash, CardElement.Blunt, CardElement.Pierce),
                        Simple(CardActionKind.Draw, Nobody, 1)
                    ).Is(CardTrait.Ethereal)
                ),
                Of(
                    "ShieldBash",
                    // 1本目の枝：打撃で敵をよろめかせる。
                    Stage(
                        "ShieldBash",
                        1,
                        "シールドバッシュ・震",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}と、自分の[ブロック]の50%のダメージ。その敵の行動順を1つ遅らせる。",
                        Damage(Enemy, Def, 80).WithBlock(50),
                        Simple(CardActionKind.Delay, Enemy, 1)
                    ),
                    // 2本目の枝：構えたまま打つと重くなる。
                    Stage(
                        "ShieldBash",
                        2,
                        "シールドバッシュ・砕",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}と、自分の[ブロック]の50%のダメージ。自分に[ブロック]があれば1.5倍。",
                        Damage(Enemy, Def, 80).WithBlock(50).Bonus(CardCondition.OnGuarded, 50)
                    )
                ),
                Of(
                    "ArmorBreak",
                    // 1本目の枝：敵の列をまとめて崩す。
                    Stage(
                        "ArmorBreak",
                        1,
                        "鎧砕き・崩",
                        CardKind.Debuff,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。[防御ダウン]2ターン。[初手]。",
                        Damage(Enemies, Phy, 60),
                        Apply(Enemies, CardStatus.Vulnerable, 2).WithChance(100)
                    ).Is(CardTrait.Innate),
                    // 2本目の枝：鎧とともに強化も剥がす。
                    Stage(
                        "ArmorBreak",
                        2,
                        "鎧砕き・剥",
                        CardKind.Debuff,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[防御ダウン]2ターン。[強化]を1つ消す。[初手]。",
                        Damage(Enemy, Phy, 80),
                        Apply(Enemy, CardStatus.Vulnerable, 2).WithChance(100),
                        Simple(CardActionKind.Dispel, Enemy, 1)
                    ).Is(CardTrait.Innate)
                ),
                Of(
                    "EarthSplitter",
                    // 1本目の枝：崩してから割る1枚で完結させる。
                    Stage(
                        "EarthSplitter",
                        1,
                        "大地割り・崩",
                        CardKind.Attack,
                        Enemies,
                        3,
                        "敵全体に[防御ダウン]2ターン。敵全体に{0}ダメージ。[防御ダウン]中の敵には1.5倍。",
                        Apply(Enemies, CardStatus.Vulnerable, 2).WithChance(100),
                        Damage(Enemies, Phy, 110).Bonus(CardCondition.OnVulnerable, 50)
                    ),
                    // 2本目の枝：割れ目から岩の壁を起こす。
                    Stage(
                        "EarthSplitter",
                        2,
                        "大地割り・岩陣",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。[防御ダウン]中の敵には1.5倍。味方全体に[ブロック]{1}。",
                        Damage(Enemies, Phy, 100).Bonus(CardCondition.OnVulnerable, 50),
                        Block(Allies, Def, 20)
                    )
                ),
                Of(
                    "GiantImpact",
                    // 1本目の枝：自分で構えを作ってから打つ。
                    Stage(
                        "GiantImpact",
                        1,
                        "ギガントインパクト・要塞",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "自分に[ブロック]{0}。敵1体に{1}ダメージ。自分に[ブロック]があれば1.5倍。[眠り札]。",
                        Block(Self, Def, 60),
                        Damage(Enemy, Phy, 360).Bonus(CardCondition.OnGuarded, 50)
                    ).Is(CardTrait.Sleeper | CardTrait.CutIn),
                    // 2本目の枝：衝撃を周りにも広げる。
                    Stage(
                        "GiantImpact",
                        2,
                        "ギガントインパクト・衝",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "敵1体に{0}、さらに敵全体に{1}ダメージ。自分に[ブロック]があれば1.5倍。[眠り札]。",
                        Damage(Enemy, Phy, 300).Bonus(CardCondition.OnGuarded, 50),
                        Damage(Enemies, Phy, 60).Bonus(CardCondition.OnGuarded, 50)
                    ).Is(CardTrait.Sleeper | CardTrait.CutIn)
                ),
                Of(
                    "StrideStrike",
                    // 1本目の枝：駆け抜けて次の手へつなぐ。
                    Stage(
                        "StrideStrike",
                        1,
                        "健脚の一撃・駆",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[歩み]。カードを1枚引く。",
                        Damage(Enemy, Phy, 130).WithAct(10, 100),
                        Simple(CardActionKind.Draw, Nobody, 1)
                    ),
                    // 2本目の枝：踏み鳴らして全体を揺らす。
                    Stage(
                        "StrideStrike",
                        2,
                        "健脚の一撃・踏",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。[歩み]。",
                        Damage(Enemies, Phy, 100).WithAct(10, 100)
                    )
                ),
                Of(
                    "GuardianOath",
                    // 1本目の枝：引きつけた攻撃に耐え抜く。
                    Stage(
                        "GuardianOath",
                        1,
                        "守護の誓い・不動",
                        CardKind.Guard,
                        Self,
                        2,
                        "自分に[ブロック]{0}。2ターン、敵の攻撃を引きつけ、受けるダメージを25%減らす。[保持]。",
                        Block(Self, Def, 90),
                        Apply(Self, CardStatus.Taunt, 2),
                        Apply(Self, CardStatus.Protect, 2, 25)
                    ).Is(CardTrait.Retain),
                    // 2本目の枝：引きつけた攻撃を返す。
                    Stage(
                        "GuardianOath",
                        2,
                        "守護の誓い・報復",
                        CardKind.Guard,
                        Self,
                        2,
                        "自分に[ブロック]{0}。2ターン、敵の攻撃を引きつける。次に受ける単体攻撃を1回はね返す。[保持]。",
                        Block(Self, Def, 70),
                        Apply(Self, CardStatus.Taunt, 2),
                        Apply(Self, CardStatus.Reflect, 1)
                    ).Is(CardTrait.Retain)
                ),
                Of(
                    "WarCry",
                    // 1本目の枝：士気でエネルギーを生む。
                    Stage(
                        "WarCry",
                        1,
                        "鬨の声・猛",
                        CardKind.Buff,
                        Allies,
                        1,
                        "味方全体の攻撃力を2ターン25%上げる。エネルギー+1。[初手]。[消滅]。",
                        Apply(Allies, CardStatus.AttackUp, 2, 25),
                        Simple(CardActionKind.Energy, Nobody, 1)
                    ).Is(CardTrait.Innate | CardTrait.Exhaust),
                    // 2本目の枝：攻めながら陣を固める。
                    Stage(
                        "WarCry",
                        2,
                        "鬨の声・陣",
                        CardKind.Buff,
                        Allies,
                        1,
                        "味方全体の攻撃力を2ターン20%上げる。味方全体に[ブロック]{0}。[初手]。",
                        Apply(Allies, CardStatus.AttackUp, 2, 20),
                        Block(Allies, Def, 20)
                    ).Is(CardTrait.Innate)
                ),
                // --- トーマ：炎と氷 ---
                Of(
                    "Embers",
                    // 1本目の枝：火の粉を全体へ撒く。
                    Stage(
                        "Embers",
                        1,
                        "火の粉・散",
                        CardKind.Attack,
                        Random,
                        1,
                        "ランダムな敵に{0}ダメージ×3。敵全体に60%で[やけど]2ターン。捨てたとき：敵全体に60%で[やけど]1ターン。",
                        Damage(Random, Mag, 30, hits: 3),
                        Apply(Enemies, CardStatus.Burn, 2).WithChance(60),
                        Apply(Enemies, CardStatus.Burn, 1).WithChance(60).WhenDiscarded()
                    ),
                    // 2本目の枝：小さな火で次の手を照らす。
                    Stage(
                        "Embers",
                        2,
                        "火の粉・灯",
                        CardKind.Attack,
                        Enemy,
                        0,
                        "敵1体に60%で[やけど]2ターン。カードを1枚引く。捨てたとき：ランダムな敵に60%で[やけど]2ターン。",
                        Apply(Enemy, CardStatus.Burn, 2).WithChance(60),
                        Simple(CardActionKind.Draw, Nobody, 1),
                        Apply(Random, CardStatus.Burn, 2).WithChance(60).WhenDiscarded()
                    )
                ),
                Of(
                    "FlamePillar",
                    // 1本目の枝：焼いてから柱を立てる1枚で完結させる。
                    Stage(
                        "FlamePillar",
                        1,
                        "フレイムピラー・業火",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "敵1体に[やけど]2ターン。敵1体に{0}ダメージ。[やけど]中の敵には1.5倍。",
                        Apply(Enemy, CardStatus.Burn, 2).WithChance(100),
                        Damage(Enemy, Mag, 220).Bonus(CardCondition.OnBurning, 50)
                    ),
                    // 2本目の枝：柱を2体に立てる。
                    Stage(
                        "FlamePillar",
                        2,
                        "フレイムピラー・連柱",
                        CardKind.Attack,
                        Chain,
                        2,
                        "敵1体に{0}ダメージ×2。1回ごとに別の敵へ移る。[やけど]中の敵には1.5倍。",
                        Damage(Chain, Mag, 120, hits: 2).Bonus(CardCondition.OnBurning, 50)
                    )
                ),
                Of(
                    "FireStorm",
                    // 1本目の枝：やけどの敵を強く焼く。
                    Stage(
                        "FireStorm",
                        1,
                        "ファイアストーム・烈",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。[やけど]中の敵には1.5倍。60%で[やけど]2ターン。",
                        Damage(Enemies, Mag, 90).Bonus(CardCondition.OnBurning, 50),
                        Apply(Enemies, CardStatus.Burn, 2).WithChance(60)
                    ),
                    // 2本目の枝：炎を2度巻きつける。
                    Stage(
                        "FireStorm",
                        2,
                        "ファイアストーム・渦",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ×2。60%で[やけど]2ターン。",
                        Damage(Enemies, Mag, 55, hits: 2),
                        Apply(Enemies, CardStatus.Burn, 2).WithChance(60)
                    )
                ),
                Of(
                    "BlastSigil",
                    // 1本目の枝：爆発を待つ間も焼く。
                    Stage(
                        "BlastSigil",
                        1,
                        "爆炎の刻印・連鎖",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に[刻印]と[やけど]2ターン。次のターン、敵全体に{0}ダメージ。[初手]。",
                        Apply(Enemy, CardStatus.BlastSigil, 1, Mag, 150),
                        Apply(Enemy, CardStatus.Burn, 2).WithChance(100)
                    ).Is(CardTrait.Innate),
                    // 2本目の枝：刻んだ瞬間にも火を噴く。
                    Stage(
                        "BlastSigil",
                        2,
                        "爆炎の刻印・即爆",
                        CardKind.Attack,
                        Enemy,
                        2,
                        "敵1体に{0}ダメージ。[刻印]。次のターン、敵全体に{1}ダメージ。[初手]。",
                        Damage(Enemy, Mag, 100),
                        Apply(Enemy, CardStatus.BlastSigil, 1, Mag, 150)
                    ).Is(CardTrait.Innate)
                ),
                Of(
                    "Meteor",
                    // 1本目の枝：先に焼いて必ず2倍にする。
                    Stage(
                        "Meteor",
                        1,
                        "メテオ・焔",
                        CardKind.Attack,
                        Enemies,
                        3,
                        "敵全体に[やけど]2ターン。敵全体に{0}ダメージ。[やけど]中の敵には2倍。[眠り札]。",
                        Apply(Enemies, CardStatus.Burn, 2).WithChance(100),
                        Damage(Enemies, Mag, 220).Bonus(CardCondition.OnBurning, 100)
                    ).Is(CardTrait.Sleeper | CardTrait.CutIn),
                    // 2本目の枝：隕石を4つに割って降らせる。
                    Stage(
                        "Meteor",
                        2,
                        "メテオ・流星",
                        CardKind.Attack,
                        Random,
                        3,
                        "ランダムな敵に{0}ダメージ×4。[やけど]中の敵には2倍。[眠り札]。",
                        Damage(Random, Mag, 110, hits: 4).Bonus(CardCondition.OnBurning, 100)
                    ).Is(CardTrait.Sleeper | CardTrait.CutIn)
                ),
                Of(
                    "FlameEnchant",
                    // 1本目の枝：炎を全員の武器に宿す。
                    Stage(
                        "FlameEnchant",
                        1,
                        "フレイムエンチャント・陣",
                        CardKind.Buff,
                        Allies,
                        2,
                        "味方全体の攻撃に、2ターン炎属性を加える。[合体]斬り払い。",
                        Apply(Allies, CardStatus.FireBlade, 2)
                    ).Fuses("Slash", "FlameSlash"),
                    // 2本目の枝：1人の攻撃を燃え上がらせる。
                    Stage(
                        "FlameEnchant",
                        2,
                        "フレイムエンチャント・猛",
                        CardKind.Buff,
                        Ally,
                        1,
                        "味方1体の攻撃に3ターン炎属性を加え、攻撃力を3ターン20%上げる。[合体]斬り払い。",
                        Apply(Ally, CardStatus.FireBlade, 3),
                        Apply(Ally, CardStatus.AttackUp, 3, 20)
                    ).Fuses("Slash", "FlameSlash")
                ),
                Of(
                    "Icicles",
                    // 1本目の枝：降ったつららで全体を冷やす。
                    Stage(
                        "Icicles",
                        1,
                        "つらら落とし・凍",
                        CardKind.Attack,
                        Random,
                        1,
                        "ランダムな敵に{0}ダメージ×4。敵全体に60%で[凍え]1ターン。捨てたとき：ランダムな敵に{1}ダメージ×2。",
                        Damage(Random, Mag, 35, hits: 4),
                        Apply(Enemies, CardStatus.Chill, 1).WithChance(60),
                        Damage(Random, Mag, 40, hits: 2).WhenDiscarded()
                    ),
                    // 2本目の枝：凍えた敵に深く刺さる。
                    Stage(
                        "Icicles",
                        2,
                        "つらら落とし・鋭",
                        CardKind.Attack,
                        Random,
                        1,
                        "ランダムな敵に{0}ダメージ×4。[凍え]中の敵には1.5倍。捨てたとき：ランダムな敵に{1}ダメージ×3。",
                        Damage(Random, Mag, 35, hits: 4).Bonus(CardCondition.OnChilled, 50),
                        Damage(Random, Mag, 40, hits: 3).WhenDiscarded()
                    )
                ),
                Of(
                    "Blizzard",
                    // 1本目の枝：凍えた敵を強く打つ。
                    Stage(
                        "Blizzard",
                        1,
                        "ブリザード・凍",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。[凍え]中の敵には1.5倍。60%で[凍え]2ターン。[眠り札]。",
                        Damage(Enemies, Mag, 80).Bonus(CardCondition.OnChilled, 50),
                        Apply(Enemies, CardStatus.Chill, 2).WithChance(60)
                    ).Is(CardTrait.Sleeper),
                    // 2本目の枝：吹雪の中に氷の壁を立てる。
                    Stage(
                        "Blizzard",
                        2,
                        "ブリザード・氷壁",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。60%で[凍え]2ターン。味方全体に[ブロック]{1}。[眠り札]。",
                        Damage(Enemies, Mag, 70),
                        Apply(Enemies, CardStatus.Chill, 2).WithChance(60),
                        Block(Allies, Def, 20)
                    ).Is(CardTrait.Sleeper)
                ),
                Of(
                    "IceMirror",
                    // 1本目の枝：鏡を全員の前に立てる。
                    Stage(
                        "IceMirror",
                        1,
                        "アイスミラー・双",
                        CardKind.Guard,
                        Allies,
                        2,
                        "味方全体が次に受ける単体攻撃を、1回はね返す。[保持]。",
                        Apply(Allies, CardStatus.Reflect, 1)
                    ).Is(CardTrait.Retain),
                    // 2本目の枝：鏡に厚い氷を重ねる。
                    Stage(
                        "IceMirror",
                        2,
                        "アイスミラー・氷華",
                        CardKind.Guard,
                        Ally,
                        1,
                        "味方1体に[ブロック]{0}。次に受ける単体攻撃を、1回はね返す。[保持]。",
                        Block(Ally, Def, 40),
                        Apply(Ally, CardStatus.Reflect, 1)
                    ).Is(CardTrait.Retain)
                ),
                Of(
                    "AbsoluteZero",
                    // 1本目の枝：全体を冷やしてから1体を凍らせる。
                    Stage(
                        "AbsoluteZero",
                        1,
                        "絶対零度・氷河",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "敵全体に[凍え]2ターン。敵1体に{0}ダメージ。[凍え]中の敵には1.5倍。40%で[凍結]させる。[消滅]。",
                        Apply(Enemies, CardStatus.Chill, 2).WithChance(100),
                        Damage(Enemy, Mag, 420).Bonus(CardCondition.OnChilled, 50),
                        Apply(Enemy, CardStatus.Freeze, 1).WithChance(40)
                    ).Is(CardTrait.Exhaust | CardTrait.CutIn),
                    // 2本目の枝：凍気を全体に広げる。
                    Stage(
                        "AbsoluteZero",
                        2,
                        "絶対零度・氷葬",
                        CardKind.Attack,
                        Enemies,
                        3,
                        "敵全体に{0}ダメージ。[凍え]中の敵には1.5倍。[弱点]を突いた敵を40%で[凍結]させる。[消滅]。",
                        Damage(Enemies, Mag, 220).Bonus(CardCondition.OnChilled, 50),
                        Apply(Enemies, CardStatus.Freeze, 1).WithChance(40).Only(CardCondition.OnWeakness)
                    ).Is(CardTrait.Exhaust | CardTrait.CutIn)
                ),
                Of(
                    "QuickCast",
                    // 1本目の枝：詠唱を縮めて次の呪文を呼ぶ。
                    Stage(
                        "QuickCast",
                        1,
                        "詠唱短縮・速",
                        CardKind.Support,
                        Nobody,
                        0,
                        "次に使う炎・氷・雷カードのコストを0にする。カードを1枚引く。[霧散]。",
                        NextCardFree(Magic),
                        Simple(CardActionKind.Draw, Nobody, 1)
                    ).Is(CardTrait.Ethereal),
                    // 2本目の枝：縮めた詠唱を取っておく。
                    Stage(
                        "QuickCast",
                        2,
                        "詠唱短縮・瞬",
                        CardKind.Support,
                        Nobody,
                        0,
                        "次に使う炎・氷・雷カードのコストを0にする。[保持]。",
                        NextCardFree(Magic)
                    ).Is(CardTrait.Retain)
                ),
                // --- ルカ：貫・雷と、搦め手 ---
                Of(
                    "VitalThrust",
                    // 1本目の枝：弱点への一突きを重くする。
                    Stage(
                        "VitalThrust",
                        1,
                        "急所突き・穿",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[弱点]を突くと威力+50%、エネルギー+1。",
                        Damage(Enemy, Phy, 120).Bonus(CardCondition.OnWeakness, 50),
                        Simple(CardActionKind.Energy, Nobody, 1).Only(CardCondition.OnWeakness)
                    ),
                    // 2本目の枝：2度突いて急所を探る。
                    Stage(
                        "VitalThrust",
                        2,
                        "急所突き・連",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ×2。[弱点]を突くとエネルギー+1。",
                        Damage(Enemy, Phy, 70, hits: 2),
                        Simple(CardActionKind.Energy, Nobody, 1).Only(CardCondition.OnWeakness)
                    )
                ),
                Of(
                    "PoisonNeedle",
                    // 1本目の枝：毒を深く重ねる。
                    Stage(
                        "PoisonNeedle",
                        1,
                        "毒針・蝕",
                        CardKind.Debuff,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[毒]がある敵には1.5倍。[毒]を4つ重ねる。捨てたとき：ランダムな敵に[毒]を2つ重ねる。",
                        Damage(Enemy, Phy, 50).Bonus(CardCondition.OnPoisoned, 50),
                        Apply(Enemy, CardStatus.Poison, 0, 4).WithChance(100),
                        Apply(Random, CardStatus.Poison, 0, 2).WithChance(100).WhenDiscarded()
                    ),
                    // 2本目の枝：毒を全体へ撒く。
                    Stage(
                        "PoisonNeedle",
                        2,
                        "毒針・散",
                        CardKind.Debuff,
                        Random,
                        1,
                        "ランダムな敵に{0}ダメージ×3。敵全体に[毒]を1つ重ねる。捨てたとき：敵全体に[毒]を1つ重ねる。",
                        Damage(Random, Phy, 30, hits: 3),
                        Apply(Enemies, CardStatus.Poison, 0, 1).WithChance(100),
                        Apply(Enemies, CardStatus.Poison, 0, 1).WithChance(100).WhenDiscarded()
                    )
                ),
                Of(
                    "ShadowStitch",
                    // 1本目の枝：影ごと縛りつける。
                    Stage(
                        "ShadowStitch",
                        1,
                        "影縫い・縛",
                        CardKind.Debuff,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。その敵の行動順を1つ遅らせ、60%で[麻痺]1ターン。捨てたとき：ランダムな敵の行動順を1つ遅らせる。",
                        Damage(Enemy, Phy, 80),
                        Simple(CardActionKind.Delay, Enemy, 1),
                        Apply(Enemy, CardStatus.Paralysis, 1).WithChance(60),
                        Simple(CardActionKind.Delay, Random, 1).WhenDiscarded()
                    ),
                    // 2本目の枝：縫い止めて次の手へ走る。
                    Stage(
                        "ShadowStitch",
                        2,
                        "影縫い・影走",
                        CardKind.Debuff,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。その敵の行動順を1つ遅らせる。カードを1枚引く。捨てたとき：ランダムな敵の行動順を1つ遅らせる。",
                        Damage(Enemy, Phy, 80),
                        Simple(CardActionKind.Delay, Enemy, 1),
                        Simple(CardActionKind.Draw, Nobody, 1),
                        Simple(CardActionKind.Delay, Random, 1).WhenDiscarded()
                    )
                ),
                Of(
                    "InsightArrow",
                    // 1本目の枝：見抜いた隙を広げる。
                    Stage(
                        "InsightArrow",
                        1,
                        "看破の矢・射抜",
                        CardKind.Debuff,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。その敵の[弱点]をすべて見抜き、60%で[防御ダウン]2ターン。[初手]。",
                        Damage(Enemy, Phy, 90),
                        Simple(CardActionKind.RevealWeakness, Enemy),
                        Apply(Enemy, CardStatus.Vulnerable, 2).WithChance(60)
                    ).Is(CardTrait.Innate),
                    // 2本目の枝：全員の弱点を一度に見抜く。
                    Stage(
                        "InsightArrow",
                        2,
                        "看破の矢・千里眼",
                        CardKind.Debuff,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。敵全体の[弱点]をすべて見抜く。[初手]。",
                        Damage(Enemies, Phy, 50),
                        Simple(CardActionKind.RevealWeakness, Enemies)
                    ).Is(CardTrait.Innate)
                ),
                Of(
                    "ArrowRain",
                    // 1本目の枝：自ら毒を撒いて次の雨を強くする。
                    Stage(
                        "ArrowRain",
                        1,
                        "アローレイン・毒",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ×2。[毒]がある敵には1.5倍。60%で[毒]を1つ重ねる。",
                        Damage(Enemies, Phy, 45, hits: 2).Bonus(CardCondition.OnPoisoned, 50),
                        Apply(Enemies, CardStatus.Poison, 0, 1).WithChance(60)
                    ),
                    // 2本目の枝：矢の数を増やす。
                    Stage(
                        "ArrowRain",
                        2,
                        "アローレイン・豪",
                        CardKind.Attack,
                        Enemies,
                        3,
                        "敵全体に{0}ダメージ×3。[毒]がある敵には1.5倍。[連携]毒針：先に敵全体へ[毒]を2つ重ねる。",
                        Damage(Enemies, Phy, 45, hits: 3).Bonus(CardCondition.OnPoisoned, 50),
                        Apply(Enemies, CardStatus.Poison, 0, 2).WithChance(100).OnCombo()
                    ).Combo("PoisonNeedle")
                ),
                Of(
                    "ShadowSnipe",
                    // 1本目の枝：見抜いてから撃つ。
                    Stage(
                        "ShadowSnipe",
                        1,
                        "シャドウスナイプ・必中",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "敵1体の[弱点]をすべて見抜き、{0}ダメージ。[弱点]を突くと威力+50%。[保持]。",
                        Simple(CardActionKind.RevealWeakness, Enemy),
                        Damage(Enemy, Phy, 380).Bonus(CardCondition.OnWeakness, 50)
                    ).Is(CardTrait.Retain | CardTrait.CutIn),
                    // 2本目の枝：仕留めた隙に次の手を打つ。
                    Stage(
                        "ShadowSnipe",
                        2,
                        "シャドウスナイプ・影",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "敵1体に{0}ダメージ。[弱点]を突くと威力+50%、次に使うカードのコストを0にする。[保持]。",
                        Damage(Enemy, Phy, 350).Bonus(CardCondition.OnWeakness, 50),
                        NextCardFree().Only(CardCondition.OnWeakness)
                    ).Is(CardTrait.Retain | CardTrait.CutIn)
                ),
                Of(
                    "LightningBolt",
                    // 1本目の枝：痺れた敵を強く打つ。
                    Stage(
                        "LightningBolt",
                        1,
                        "ライトニングボルト・痺",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。[麻痺]中の敵には1.5倍。60%で[麻痺]1ターン。[合体]アイスランス。",
                        Damage(Enemy, Mag, 100).Bonus(CardCondition.OnParalyzed, 50),
                        Apply(Enemy, CardStatus.Paralysis, 1).WithChance(60)
                    ).Fuses("Ice", "FrostSpear"),
                    // 2本目の枝：一瞬で撃って次の手へ。
                    Stage(
                        "LightningBolt",
                        2,
                        "ライトニングボルト・迅",
                        CardKind.Attack,
                        Enemy,
                        1,
                        "敵1体に{0}ダメージ。60%で[麻痺]1ターン。カードを1枚引く。[合体]アイスランス。",
                        Damage(Enemy, Mag, 90),
                        Apply(Enemy, CardStatus.Paralysis, 1).WithChance(60),
                        Simple(CardActionKind.Draw, Nobody, 1)
                    ).Fuses("Ice", "FrostSpear")
                ),
                Of(
                    "ChainLightning",
                    // 1本目の枝：痺れた敵で強く跳ねる。
                    Stage(
                        "ChainLightning",
                        1,
                        "チェインライトニング・痺",
                        CardKind.Attack,
                        Chain,
                        2,
                        "敵1体に{0}ダメージ×3。1回ごとに別の敵へ移る。[麻痺]中の敵には1.5倍。",
                        Damage(Chain, Mag, 90, hits: 3).Bonus(CardCondition.OnParalyzed, 50)
                    ),
                    // 2本目の枝：跳ねる回数を増やす。
                    Stage(
                        "ChainLightning",
                        2,
                        "チェインライトニング・連鎖",
                        CardKind.Attack,
                        Chain,
                        3,
                        "敵1体に{0}ダメージ×5。1回ごとに別の敵へ移る。",
                        Damage(Chain, Mag, 80, hits: 5)
                    )
                ),
                Of(
                    "Thundercloud",
                    // 1本目の枝：呼んだ瞬間にも雷を落とす。
                    Stage(
                        "Thundercloud",
                        1,
                        "雷雲・嵐",
                        CardKind.Attack,
                        Enemies,
                        3,
                        "敵全体に{0}ダメージ。3ターンの間、毎ターンランダムな敵1体に{1}ダメージ。[消滅]。",
                        Damage(Enemies, Mag, 60),
                        Apply(Nobody, CardStatus.Thundercloud, 3, Mag, 100)
                    ).Is(CardTrait.Exhaust),
                    // 2本目の枝：雲を呼びながら次の手を整える。
                    Stage(
                        "Thundercloud",
                        2,
                        "雷雲・充",
                        CardKind.Attack,
                        Nobody,
                        2,
                        "3ターンの間、毎ターンランダムな敵1体に{0}ダメージ。カードを1枚引く。[消滅]。",
                        Apply(Nobody, CardStatus.Thundercloud, 3, Mag, 100),
                        Simple(CardActionKind.Draw, Nobody, 1)
                    ).Is(CardTrait.Exhaust)
                ),
                Of(
                    "ThunderSpear",
                    // 1本目の枝：痺れた敵を貫く。
                    Stage(
                        "ThunderSpear",
                        1,
                        "雷神の槍・神鳴",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "手札を1枚[捨てる]。敵1体に{0}ダメージ。[麻痺]中の敵には1.5倍。さらに敵全体に{1}ダメージ。",
                        Discard(1),
                        Damage(Enemy, Mag, 360).Bonus(CardCondition.OnParalyzed, 50),
                        Damage(Enemies, Mag, 60)
                    ).Is(CardTrait.CutIn),
                    // 2本目の枝：手札2枚を雷に変えて重くする。
                    Stage(
                        "ThunderSpear",
                        2,
                        "雷神の槍・轟",
                        CardKind.Attack,
                        Enemy,
                        3,
                        "手札を2枚[捨てる]。敵1体に{0}、さらに敵全体に{1}ダメージ。敵全体に60%で[麻痺]1ターン。",
                        Discard(2),
                        Damage(Enemy, Mag, 420),
                        Damage(Enemies, Mag, 80),
                        Apply(Enemies, CardStatus.Paralysis, 1).WithChance(60)
                    ).Is(CardTrait.CutIn)
                ),
                Of(
                    "Gale",
                    // 1本目の枝：速さで次の手を引き寄せる。
                    Stage(
                        "Gale",
                        1,
                        "疾風迅雷・迅",
                        CardKind.Buff,
                        Allies,
                        1,
                        "味方全体の速度を2ターン上げる。カードを2枚引く。[初手]。",
                        Apply(Allies, CardStatus.SpeedUp, 2, 30),
                        Simple(CardActionKind.Draw, Nobody, 2)
                    ).Is(CardTrait.Innate),
                    // 2本目の枝：速さに雷の力を乗せる。
                    Stage(
                        "Gale",
                        2,
                        "疾風迅雷・雷纏",
                        CardKind.Buff,
                        Allies,
                        1,
                        "味方全体の速度を2ターン上げ、攻撃力を2ターン15%上げる。",
                        Apply(Allies, CardStatus.SpeedUp, 2, 30),
                        Apply(Allies, CardStatus.AttackUp, 2, 15)
                    )
                ),
                Of(
                    "Scout",
                    // 1本目の枝：敵の弱点まで見て帰る。
                    Stage(
                        "Scout",
                        1,
                        "偵察・看破",
                        CardKind.Support,
                        Nobody,
                        0,
                        "カードを2枚引き、手札を1枚[捨てる]。敵全体の[弱点]をすべて見抜く。",
                        Simple(CardActionKind.Draw, Nobody, 2),
                        Discard(1),
                        Simple(CardActionKind.RevealWeakness, Enemies)
                    ),
                    // 2本目の枝：敵の足を止めて先手を取る。
                    Stage(
                        "Scout",
                        2,
                        "偵察・待ち伏せ",
                        CardKind.Support,
                        Enemy,
                        0,
                        "カードを2枚引き、手札を1枚[捨てる]。敵1体の行動順を1つ遅らせる。",
                        Simple(CardActionKind.Draw, Nobody, 2),
                        Discard(1),
                        Simple(CardActionKind.Delay, Enemy, 1)
                    )
                ),
                // --- ミーナ：癒やし・守り・聖なる打撃 ---
                Of(
                    "StepPrayer",
                    // 1本目の枝：歩いた分だけ清める。
                    Stage(
                        "StepPrayer",
                        1,
                        "万歩の祈り・清",
                        CardKind.Heal,
                        Allies,
                        2,
                        "味方全体のHPを{0}回復し、[状態異常]を消す。[歩み]。",
                        Heal(Allies, Mag, 70).WithAct(10, 100),
                        Simple(CardActionKind.Cleanse, Allies)
                    ),
                    // 2本目の枝：歩いた分だけ守る。
                    Stage(
                        "StepPrayer",
                        2,
                        "万歩の祈り・守",
                        CardKind.Heal,
                        Allies,
                        2,
                        "味方全体のHPを{0}回復。味方全体に[ブロック]{1}。[歩み]。",
                        Heal(Allies, Mag, 60).WithAct(10, 100),
                        Block(Allies, Def, 20)
                    )
                ),
                Of(
                    "Regen",
                    // 1本目の枝：芽吹きを全員に広げる。
                    Stage(
                        "Regen",
                        1,
                        "リジェネ・広",
                        CardKind.Heal,
                        Allies,
                        2,
                        "味方全体を、3ターンの間、毎ターン{0}回復する。[眠り札]。",
                        Apply(Allies, CardStatus.Regen, 3, Mag, 20)
                    ).Is(CardTrait.Sleeper),
                    // 2本目の枝：すぐの回復を足す。
                    Stage(
                        "Regen",
                        2,
                        "リジェネ・即",
                        CardKind.Heal,
                        Ally,
                        1,
                        "味方1体のHPを{0}回復し、3ターンの間、毎ターン{1}回復する。[眠り札]。",
                        Heal(Ally, Mag, 40),
                        Apply(Ally, CardStatus.Regen, 3, Mag, 30)
                    ).Is(CardTrait.Sleeper)
                ),
                Of(
                    "Resurrection",
                    // 1本目の枝：起こした仲間を守る。
                    Stage(
                        "Resurrection",
                        1,
                        "リザレクション・加護",
                        CardKind.Heal,
                        Ally,
                        3,
                        "味方1体のHPを最大の半分回復。倒れていれば復活。受けるダメージを2ターン25%減らす。[消滅]。",
                        Simple(CardActionKind.Revive, Ally, 50),
                        Apply(Ally, CardStatus.Protect, 2, 25)
                    ).Is(CardTrait.Exhaust | CardTrait.CutIn),
                    // 2本目の枝：起こした仲間を癒やし続ける。
                    Stage(
                        "Resurrection",
                        2,
                        "リザレクション・息吹",
                        CardKind.Heal,
                        Ally,
                        2,
                        "味方1体のHPを最大の半分回復。倒れていれば復活。3ターンの間、毎ターン{0}回復する。[消滅]。",
                        Simple(CardActionKind.Revive, Ally, 50),
                        Apply(Ally, CardStatus.Regen, 3, Mag, 25)
                    ).Is(CardTrait.Exhaust | CardTrait.CutIn)
                ),
                Of(
                    "HolyHammer",
                    // 1本目の枝：裁いた敵の守りを砕く。
                    Stage(
                        "HolyHammer",
                        1,
                        "聖なる鉄槌・裁",
                        CardKind.Attack,
                        Enemy,
                        2,
                        "敵1体に{0}ダメージ。[強化]を1つ消し、60%で[防御ダウン]2ターン。",
                        Damage(Enemy, Mag, 210),
                        Simple(CardActionKind.Dispel, Enemy, 1),
                        Apply(Enemy, CardStatus.Vulnerable, 2).WithChance(60)
                    ),
                    // 2本目の枝：打った光で仲間を癒やす。
                    Stage(
                        "HolyHammer",
                        2,
                        "聖なる鉄槌・祝",
                        CardKind.Attack,
                        Enemy,
                        2,
                        "敵1体に{0}ダメージ。[強化]を1つ消す。味方全体のHPを{1}回復。",
                        Damage(Enemy, Mag, 190),
                        Simple(CardActionKind.Dispel, Enemy, 1),
                        Heal(Allies, Mag, 20)
                    )
                ),
                Of(
                    "Protect",
                    // 1本目の枝：障壁で穢れも払う。
                    Stage(
                        "Protect",
                        1,
                        "プロテクト・清",
                        CardKind.Guard,
                        Allies,
                        1,
                        "味方全体の受けるダメージを、2ターン25%減らす。味方全体の[状態異常]を消す。[保持]。",
                        Apply(Allies, CardStatus.Protect, 2, 25),
                        Simple(CardActionKind.Cleanse, Allies)
                    ).Is(CardTrait.Retain),
                    // 2本目の枝：障壁の中で癒やす。
                    Stage(
                        "Protect",
                        2,
                        "プロテクト・祈",
                        CardKind.Guard,
                        Allies,
                        2,
                        "味方全体の受けるダメージを、2ターン25%減らす。2ターンの間、毎ターン{0}回復する。[保持]。",
                        Apply(Allies, CardStatus.Protect, 2, 25),
                        Apply(Allies, CardStatus.Regen, 2, Mag, 15)
                    ).Is(CardTrait.Retain)
                ),
                Of(
                    "Purify",
                    // 1本目の枝：清めたあとを守る。
                    Stage(
                        "Purify",
                        1,
                        "浄化の光・聖",
                        CardKind.Heal,
                        Allies,
                        0,
                        "味方全体の[状態異常]を消し、HPを{0}回復。受けるダメージを1ターン15%減らす。[霧散]。",
                        Simple(CardActionKind.Cleanse, Allies),
                        Heal(Allies, Mag, 25),
                        Apply(Allies, CardStatus.Protect, 1, 15)
                    ).Is(CardTrait.Ethereal),
                    // 2本目の枝：清めて次の手を巡らせる。
                    Stage(
                        "Purify",
                        2,
                        "浄化の光・巡",
                        CardKind.Heal,
                        Allies,
                        0,
                        "味方全体の[状態異常]を消し、HPを{0}回復。カードを1枚引く。[霧散]。",
                        Simple(CardActionKind.Cleanse, Allies),
                        Heal(Allies, Mag, 25),
                        Simple(CardActionKind.Draw, Nobody, 1)
                    ).Is(CardTrait.Ethereal)
                ),
                Of(
                    "ManaPrayer",
                    // 1本目の枝：魔力とともに手札も満たす。
                    Stage(
                        "ManaPrayer",
                        1,
                        "マナの祈り・満",
                        CardKind.Support,
                        Nobody,
                        0,
                        "エネルギー+2。カードを1枚引く。[消滅]。",
                        Simple(CardActionKind.Energy, Nobody, 2),
                        Simple(CardActionKind.Draw, Nobody, 1)
                    ).Is(CardTrait.Exhaust),
                    // 2本目の枝：魔力を仲間の癒やしにも分ける。
                    Stage(
                        "ManaPrayer",
                        2,
                        "マナの祈り・共鳴",
                        CardKind.Support,
                        Nobody,
                        0,
                        "エネルギー+2。味方全体のHPを{0}回復。[消滅]。",
                        Simple(CardActionKind.Energy, Nobody, 2),
                        Heal(Allies, Mag, 20)
                    ).Is(CardTrait.Exhaust)
                ),
                Of(
                    "Revelation",
                    // 1本目の枝：閃きで拾った手を軽くする。
                    Stage(
                        "Revelation",
                        1,
                        "天啓・閃",
                        CardKind.Support,
                        Nobody,
                        1,
                        "捨て札からカードを1枚選び、手札に加える。そのカードのコストを、このターンだけ0にする。[消滅]。",
                        TakeFromDiscard(1, free: true)
                    ).Is(CardTrait.Exhaust),
                    // 2本目の枝：拾い直しながら次の手も引く。
                    Stage(
                        "Revelation",
                        2,
                        "天啓・導",
                        CardKind.Support,
                        Nobody,
                        1,
                        "捨て札からカードを1枚選び、手札に加える。カードを1枚引く。[消滅]。",
                        TakeFromDiscard(1),
                        Simple(CardActionKind.Draw, Nobody, 1)
                    ).Is(CardTrait.Exhaust)
                ),
                Of(
                    "HolyLight",
                    // 1本目の枝：光で仲間も清める。
                    Stage(
                        "HolyLight",
                        1,
                        "ホーリーライト・浄",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。味方全体のHPを{1}回復し、[状態異常]を消す。",
                        Damage(Enemies, Mag, 70),
                        Heal(Allies, Mag, 30),
                        Simple(CardActionKind.Cleanse, Allies)
                    ),
                    // 2本目の枝：光の温もりを残す。
                    Stage(
                        "HolyLight",
                        2,
                        "ホーリーライト・陽",
                        CardKind.Attack,
                        Enemies,
                        2,
                        "敵全体に{0}ダメージ。味方全体を、2ターンの間、毎ターン{1}回復する。",
                        Damage(Enemies, Mag, 70),
                        Apply(Allies, CardStatus.Regen, 2, Mag, 20)
                    )
                ),
                Of(
                    "DivineShield",
                    // 1本目の枝：守りを全員に広げる。
                    Stage(
                        "DivineShield",
                        1,
                        "ディバインシールド・聖域",
                        CardKind.Guard,
                        Allies,
                        3,
                        "味方全体は、次のターンまでダメージを受けない。[消滅]。",
                        Apply(Allies, CardStatus.Invincible, 1)
                    ).Is(CardTrait.Exhaust),
                    // 2本目の枝：守りに癒やしを添える。
                    Stage(
                        "DivineShield",
                        2,
                        "ディバインシールド・祝福",
                        CardKind.Guard,
                        Ally,
                        2,
                        "味方1体は、次のターンまでダメージを受けない。HPを{0}回復。[消滅]。",
                        Apply(Ally, CardStatus.Invincible, 1),
                        Heal(Ally, Mag, 50)
                    ).Is(CardTrait.Exhaust)
                ),
            }
        );

        /// <summary>The awakening of the card skill with <paramref name="skillId"/>, or of the card skill a stage grew from; null for none.</summary>
        public static CardSkillAwakening Of(string skillId)
        {
            string id = BaseIdOf(skillId);
            foreach (var awakening in All)
                if (awakening.Skill.Id == id)
                    return awakening;
            return null;
        }

        /// <summary>The awakened stage with <paramref name="id"/> ("Fire.1"), or null.</summary>
        public static CardSkill Find(string id)
        {
            var awakening = Of(id);
            if (awakening == null)
                return null;
            foreach (var stage in awakening.Branches)
                if (stage.Id == id)
                    return stage;
            return null;
        }

        /// <summary>
        /// The stages a card can awaken into: the two branches of a card skill, none of an
        /// awakened stage (it awakens once) or of a card that is no card skill.
        /// </summary>
        public static IReadOnlyList<CardSkill> NextStages(string skillId)
        {
            var awakening = Of(skillId);
            return awakening != null && awakening.Skill.Id == skillId
                ? awakening.Branches
                : Array.Empty<CardSkill>();
        }

        /// <summary>Whether <paramref name="skillId"/> is an awakened stage of a card skill.</summary>
        public static bool IsAwakened(string skillId) => Find(skillId) != null;

        // "Fire.1" grew from "Fire".
        private static string BaseIdOf(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
                return skillId;
            int dot = skillId.IndexOf('.');
            return dot < 0 ? skillId : skillId.Substring(0, dot);
        }

        private static CardSkillAwakening Of(string skillId, CardSkill first, CardSkill second)
        {
            var skill =
                CardSkills.Find(skillId)
                ?? throw new ArgumentException($"No card skill {skillId}.", nameof(skillId));
            foreach (var stage in new[] { first, second })
            {
                stage.User = skill.User;
                stage.Element = skill.Element;
                stage.MaxMultiplier = skill.MaxMultiplier;
                stage.FoilStyle = skill.FoilStyle;
                // 挿絵は段階ごとの専用（"Fire.1" は CardFire1）。ファイル名に「.」を入れない。
                stage.BorrowedArt = stage.Id.Replace(".", "");
            }
            return new CardSkillAwakening
            {
                Skill = skill,
                Branches = Array.AsReadOnly(new[] { first, second }),
            };
        }

        // 段階に、消滅・保持・霧散・初手・眠り札とカットインを付ける。
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

        private static CardSkill Stage(
            string skillId,
            int branch,
            string name,
            CardKind kind,
            CardTarget target,
            int cost,
            string text,
            params CardAction[] actions
        ) =>
            new()
            {
                Id = skillId + "." + branch,
                Name = name,
                Kind = kind,
                Target = target,
                Cost = cost,
                Text = text,
                Actions = Array.AsReadOnly(actions),
            };
    }
}
