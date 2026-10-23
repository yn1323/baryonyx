using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using static Baryonyx.Combat.CardAction;

namespace Baryonyx.SkillEvolution
{
    /// <summary>
    /// The unique skills' evolution trees (provisional mock data: doc/features/skill-evolution.md).
    /// Every tree has all seven stages: the card the unique skill starts as, and three branches
    /// of two steps each, every branch growing the skill a way of its own. Each stage of the first
    /// two unique skills has art of its own ("CardUnique" + tree + stage); the third's stages
    /// borrow a card skill's art for now.
    /// </summary>
    public static class UniqueSkillTrees
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

        public static readonly IReadOnlyList<UniqueSkillTree> All = Styled(
            // --- The mage (トーマ・ルッツ) ---------------------------------------------------
            Tree(
                "ManaBurst",
                2.2f,
                Stage(
                    "ManaBurst",
                    "マナバースト",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    2,
                    "UniqueManaBurst0",
                    "敵全体に{0}ダメージ。",
                    Damage(Enemies, Mag, 80)
                ),
                // 1本目の枝：威力を上げる。
                Stage(
                    "ManaBurst.1",
                    "マナバースト・烈",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    2,
                    "UniqueManaBurst1",
                    "敵全体に{0}ダメージ。",
                    Damage(Enemies, Mag, 100)
                ),
                Stage(
                    "ManaBurst.2",
                    "マナバースト・極",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "UniqueManaBurst2",
                    "敵全体に{0}ダメージ。",
                    Damage(Enemies, Mag, 150)
                ),
                // 2本目の枝：魔力の弾を散らす。
                Stage(
                    "ManaBurst.3",
                    "マナバースト・散",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Random,
                    1,
                    "UniqueManaBurst3",
                    "ランダムな敵に{0}ダメージ×4。",
                    Damage(Random, Mag, 35, hits: 4)
                ),
                Stage(
                    "ManaBurst.4",
                    "マナバースト・嵐",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Random,
                    2,
                    "UniqueManaBurst4",
                    "ランダムな敵に{0}ダメージ×6。[弱点]を突くと威力+50%。",
                    Damage(Random, Mag, 35, hits: 6).Bonus(CardCondition.OnWeakness, 50)
                ),
                // 3本目の枝：魔力を手元へ巡らせる。
                Stage(
                    "ManaBurst.5",
                    "マナバースト・環",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    2,
                    "UniqueManaBurst5",
                    "敵全体に{0}ダメージ。エネルギー+1。",
                    Damage(Enemies, Mag, 60),
                    Simple(CardActionKind.Energy, Nobody, 1)
                ),
                Stage(
                    "ManaBurst.6",
                    "マナバースト・還流",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    2,
                    "UniqueManaBurst6",
                    "敵全体に{0}ダメージ。エネルギー+1。カードを1枚引く。",
                    Damage(Enemies, Mag, 70),
                    Simple(CardActionKind.Energy, Nobody, 1),
                    Simple(CardActionKind.Draw, Nobody, 1)
                )
            ),
            Tree(
                "StarFall",
                1.7f,
                Stage(
                    "StarFall",
                    "星降り",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Random,
                    3,
                    "UniqueStarFall0",
                    "ランダムな敵に{0}ダメージ×5。",
                    Damage(Random, Mag, 40, hits: 5)
                ),
                // 1本目の枝：炎の星にする。
                Stage(
                    "StarFall.1",
                    "星降り・灼",
                    CardUser.Toma,
                    CardElement.Fire,
                    CardKind.Attack,
                    Random,
                    3,
                    "UniqueStarFall1",
                    "ランダムな敵に{0}ダメージ×5。敵全体に[やけど]2ターン。",
                    Damage(Random, Mag, 40, hits: 5),
                    Apply(Enemies, CardStatus.Burn, 2)
                ),
                Stage(
                    "StarFall.2",
                    "星降り・焔獄",
                    CardUser.Toma,
                    CardElement.Fire,
                    CardKind.Attack,
                    Random,
                    5,
                    "UniqueStarFall2",
                    "敵全体に[やけど]2ターン。ランダムな敵に{0}ダメージ×6。[やけど]中は1.5倍。",
                    Apply(Enemies, CardStatus.Burn, 2),
                    Damage(Random, Mag, 40, hits: 6).Bonus(CardCondition.OnBurning, 50)
                ),
                // 2本目の枝：氷の星にする。
                Stage(
                    "StarFall.3",
                    "星降り・氷晶",
                    CardUser.Toma,
                    CardElement.Ice,
                    CardKind.Attack,
                    Random,
                    3,
                    "UniqueStarFall3",
                    "ランダムな敵に{0}ダメージ×5。敵全体に[凍え]2ターン。",
                    Damage(Random, Mag, 40, hits: 5),
                    Apply(Enemies, CardStatus.Chill, 2)
                ),
                Stage(
                    "StarFall.4",
                    "星降り・凍星",
                    CardUser.Toma,
                    CardElement.Ice,
                    CardKind.Attack,
                    Random,
                    4,
                    "UniqueStarFall4",
                    "ランダムな敵に{0}ダメージ×6。[凍え]中は1.5倍。敵全体に[凍え]2ターン。",
                    Damage(Random, Mag, 45, hits: 6).Bonus(CardCondition.OnChilled, 50),
                    Apply(Enemies, CardStatus.Chill, 2)
                ),
                // 3本目の枝：1体に集める。
                Stage(
                    "StarFall.5",
                    "星降り・墜星",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemy,
                    3,
                    "UniqueStarFall5",
                    "敵1体に{0}ダメージ×5。",
                    Damage(Enemy, Mag, 45, hits: 5)
                ),
                Stage(
                    "StarFall.6",
                    "星降り・天墜",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemy,
                    4,
                    "UniqueStarFall6",
                    "敵1体に{0}ダメージ×5。HPが半分以下の敵には1.5倍。",
                    Damage(Enemy, Mag, 50, hits: 5).Bonus(CardCondition.OnWounded, 50)
                )
            ),
            // --- The archer (ルカ・グレタ・リツ) ----------------------------------------------
            Tree(
                "ThunderArrow",
                2.2f,
                Stage(
                    "ThunderArrow",
                    "雷撃の矢",
                    CardUser.Luka,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Enemy,
                    2,
                    "UniqueThunderArrow0",
                    "敵1体に{0}ダメージ。[麻痺]1ターン。",
                    Damage(Enemy, Phy, 120),
                    Apply(Enemy, CardStatus.Paralysis, 1)
                ),
                // 1本目の枝：麻痺で縛る。
                Stage(
                    "ThunderArrow.1",
                    "雷撃の矢・痺",
                    CardUser.Luka,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Enemy,
                    2,
                    "UniqueThunderArrow1",
                    "敵1体に{0}ダメージ。[麻痺]中の敵には1.5倍。[麻痺]2ターン。",
                    Damage(Enemy, Phy, 120).Bonus(CardCondition.OnParalyzed, 50),
                    Apply(Enemy, CardStatus.Paralysis, 2)
                ),
                Stage(
                    "ThunderArrow.2",
                    "雷撃の矢・縛雷",
                    CardUser.Luka,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Enemy,
                    3,
                    "UniqueThunderArrow2",
                    "敵1体に{0}ダメージ。[麻痺]中の敵には1.5倍。[麻痺]2ターン。行動順を1つ遅らせる。",
                    Damage(Enemy, Phy, 140).Bonus(CardCondition.OnParalyzed, 50),
                    Apply(Enemy, CardStatus.Paralysis, 2),
                    Simple(CardActionKind.Delay, Enemy, 1)
                ),
                // 2本目の枝：敵から敵へ渡る。
                Stage(
                    "ThunderArrow.3",
                    "雷撃の矢・連",
                    CardUser.Luka,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Chain,
                    2,
                    "UniqueThunderArrow3",
                    "敵1体に{0}ダメージ×3。1回ごとに別の敵へ移る。",
                    Damage(Chain, Phy, 60, hits: 3)
                ),
                Stage(
                    "ThunderArrow.4",
                    "雷撃の矢・迅雷",
                    CardUser.Luka,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Chain,
                    3,
                    "UniqueThunderArrow4",
                    "敵1体に{0}ダメージ×5。1回ごとに別の敵へ移る。",
                    Damage(Chain, Phy, 60, hits: 5)
                ),
                // 3本目の枝：弱点を射て次の手につなぐ。
                Stage(
                    "ThunderArrow.5",
                    "雷撃の矢・閃",
                    CardUser.Luka,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Enemy,
                    1,
                    "UniqueThunderArrow5",
                    "敵1体に{0}ダメージ。[麻痺]1ターン。[弱点]を突くとエネルギー+1。",
                    Damage(Enemy, Phy, 100),
                    Apply(Enemy, CardStatus.Paralysis, 1),
                    Simple(CardActionKind.Energy, Nobody, 1).Only(CardCondition.OnWeakness)
                ),
                Stage(
                    "ThunderArrow.6",
                    "雷撃の矢・閃光",
                    CardUser.Luka,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "UniqueThunderArrow6",
                    "敵1体に{0}ダメージ。[麻痺]1ターン。[弱点]を突くとエネルギー+1、カードを1枚引く。",
                    Damage(Enemy, Phy, 100),
                    Apply(Enemy, CardStatus.Paralysis, 1),
                    Simple(CardActionKind.Energy, Nobody, 1).Only(CardCondition.OnWeakness),
                    Simple(CardActionKind.Draw, Nobody, 1).Only(CardCondition.OnWeakness)
                )
            ),
            Tree(
                "ArrowStorm",
                1.7f,
                Stage(
                    "ArrowStorm",
                    "矢の雨",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "UniqueArrowStorm0",
                    "敵全体に{0}ダメージ×3。",
                    Damage(Enemies, Phy, 45, hits: 3)
                ),
                // 1本目の枝：毒を重ねる。
                Stage(
                    "ArrowStorm.1",
                    "矢の雨・毒",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "UniqueArrowStorm1",
                    "敵全体に{0}ダメージ×3。[毒]を2つ重ねる。",
                    Damage(Enemies, Phy, 40, hits: 3),
                    Apply(Enemies, CardStatus.Poison, 0, 2)
                ),
                Stage(
                    "ArrowStorm.2",
                    "矢の雨・蠱毒",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemies,
                    4,
                    "UniqueArrowStorm2",
                    "敵全体に{0}ダメージ×3。[毒]がある敵には1.5倍。[毒]を3つ重ねる。",
                    Damage(Enemies, Phy, 40, hits: 3).Bonus(CardCondition.OnPoisoned, 50),
                    Apply(Enemies, CardStatus.Poison, 0, 3)
                ),
                // 2本目の枝：鎧を穿つ。
                Stage(
                    "ArrowStorm.3",
                    "矢の雨・穿",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "UniqueArrowStorm3",
                    "敵全体に{0}ダメージ×3。[防御ダウン]2ターン。",
                    Damage(Enemies, Phy, 40, hits: 3),
                    Apply(Enemies, CardStatus.Vulnerable, 2)
                ),
                Stage(
                    "ArrowStorm.4",
                    "矢の雨・穿天",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemies,
                    5,
                    "UniqueArrowStorm4",
                    "敵全体に{0}ダメージ×4。[防御ダウン]中は1.5倍。[防御ダウン]2ターン。",
                    Damage(Enemies, Phy, 40, hits: 4).Bonus(CardCondition.OnVulnerable, 50),
                    Apply(Enemies, CardStatus.Vulnerable, 2)
                ),
                // 3本目の枝：速く射て手を回す。
                Stage(
                    "ArrowStorm.5",
                    "矢の雨・速",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemies,
                    2,
                    "UniqueArrowStorm5",
                    "敵全体に{0}ダメージ×3。カードを1枚引く。",
                    Damage(Enemies, Phy, 40, hits: 3),
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                Stage(
                    "ArrowStorm.6",
                    "矢の雨・疾風",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemies,
                    2,
                    "UniqueArrowStorm6",
                    "敵全体に{0}ダメージ×3。カードを1枚引く。味方全体の速度を2ターン上げる。",
                    Damage(Enemies, Phy, 40, hits: 3),
                    Simple(CardActionKind.Draw, Nobody, 1),
                    Apply(Allies, CardStatus.SpeedUp, 2, 30)
                )
            ),
            // --- The knight (アリア・アンセルム) ---------------------------------------------
            Tree(
                "ShieldStance",
                2.2f,
                Stage(
                    "ShieldStance",
                    "盾の構え",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Self,
                    1,
                    "UniqueShieldStance0",
                    "自分に[ブロック]{0}。",
                    Block(Self, Def, 80)
                ),
                // 1本目の枝：自分の守りを固める。
                Stage(
                    "ShieldStance.1",
                    "盾の構え・堅",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Self,
                    1,
                    "UniqueShieldStance1",
                    "自分に[ブロック]{0}。2ターン、受けるダメージを25%減らす。",
                    Block(Self, Def, 100),
                    Apply(Self, CardStatus.Protect, 2, 25)
                ),
                Stage(
                    "ShieldStance.2",
                    "盾の構え・不動",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Self,
                    2,
                    "UniqueShieldStance2",
                    "自分に[ブロック]{0}。2ターン、受けるダメージを50%減らす。",
                    Block(Self, Def, 150),
                    Apply(Self, CardStatus.Protect, 2, 50)
                ),
                // 2本目の枝：攻撃をはね返す。
                Stage(
                    "ShieldStance.3",
                    "盾の構え・返",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Self,
                    1,
                    "UniqueShieldStance3",
                    "自分に[ブロック]{0}。次に受ける単体攻撃を1回はね返す。",
                    Block(Self, Def, 80),
                    Apply(Self, CardStatus.Reflect, 1)
                ),
                Stage(
                    "ShieldStance.4",
                    "盾の構え・鏡盾",
                    CardUser.Aria,
                    CardElement.Blunt,
                    CardKind.Attack,
                    Enemy,
                    2,
                    "UniqueShieldStance4",
                    "敵1体に{0}ダメージ。自分に[ブロック]{1}。単体攻撃を1回反射。",
                    Damage(Enemy, Def, 80),
                    Block(Self, Def, 100),
                    Apply(Self, CardStatus.Reflect, 1)
                ),
                // 3本目の枝：仲間も守る。
                Stage(
                    "ShieldStance.5",
                    "盾の構え・陣",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Self,
                    1,
                    "UniqueShieldStance5",
                    "自分に[ブロック]{0}。味方全体に[ブロック]{1}。",
                    Block(Self, Def, 80),
                    Block(Allies, Def, 30)
                ),
                Stage(
                    "ShieldStance.6",
                    "盾の構え・城壁",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Allies,
                    2,
                    "UniqueShieldStance6",
                    "味方全体に[ブロック]{0}。2ターン、敵の攻撃を引きつける。",
                    Block(Allies, Def, 60),
                    Apply(Self, CardStatus.Taunt, 2)
                )
            ),
            Tree(
                "Issen",
                2.6f,
                Stage(
                    "Issen",
                    "一閃",
                    CardUser.Aria,
                    CardElement.Slash,
                    CardKind.Attack,
                    Enemy,
                    3,
                    "UniqueIssen0",
                    "敵1体に{0}ダメージ。",
                    Damage(Enemy, Phy, 200)
                ),
                // 1本目の枝：出血を咲かせる。
                Stage(
                    "Issen.1",
                    "一閃・血華",
                    CardUser.Aria,
                    CardElement.Slash,
                    CardKind.Attack,
                    Enemy,
                    3,
                    "UniqueIssen1",
                    "敵1体に{0}ダメージ。[出血]中の敵には1.5倍。[出血]2ターン。",
                    Damage(Enemy, Phy, 200).Bonus(CardCondition.OnBleeding, 50),
                    Apply(Enemy, CardStatus.Bleed, 2)
                ),
                Stage(
                    "Issen.2",
                    "一閃・散華",
                    CardUser.Aria,
                    CardElement.Slash,
                    CardKind.Attack,
                    Enemy,
                    4,
                    "UniqueIssen2",
                    "敵1体に{0}ダメージ。[出血]中の敵には1.5倍。[出血]3ターン。",
                    Damage(Enemy, Phy, 280).Bonus(CardCondition.OnBleeding, 50),
                    Apply(Enemy, CardStatus.Bleed, 3)
                ),
                // 2本目の枝：軽く速くする。
                Stage(
                    "Issen.3",
                    "一閃・疾",
                    CardUser.Aria,
                    CardElement.Slash,
                    CardKind.Attack,
                    Enemy,
                    2,
                    "UniqueIssen3",
                    "敵1体に{0}ダメージ。",
                    Damage(Enemy, Phy, 150)
                ),
                Stage(
                    "Issen.4",
                    "一閃・瞬",
                    CardUser.Aria,
                    CardElement.Slash,
                    CardKind.Attack,
                    Enemy,
                    1,
                    "UniqueIssen4",
                    "敵1体に{0}ダメージ。カードを1枚引く。",
                    Damage(Enemy, Phy, 150),
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                // 3本目の枝：横に薙ぐ。
                Stage(
                    "Issen.5",
                    "一閃・薙",
                    CardUser.Aria,
                    CardElement.Slash,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "UniqueIssen5",
                    "敵全体に{0}ダメージ。",
                    Damage(Enemies, Phy, 110)
                ),
                Stage(
                    "Issen.6",
                    "一閃・嵐薙",
                    CardUser.Aria,
                    CardElement.Slash,
                    CardKind.Attack,
                    Enemies,
                    4,
                    "UniqueIssen6",
                    "敵全体に{0}ダメージ。[防御ダウン]2ターン。",
                    Damage(Enemies, Phy, 110),
                    Apply(Enemies, CardStatus.Vulnerable, 2)
                )
            ),
            // --- The healer (ミナ・リタ) -------------------------------------------------------
            Tree(
                "HealingWind",
                2.2f,
                Stage(
                    "HealingWind",
                    "癒しの風",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    2,
                    "UniqueHealingWind0",
                    "味方全体のHPを{0}回復する。",
                    Heal(Allies, Mag, 60)
                ),
                // 1本目の枝：回復を続ける。
                Stage(
                    "HealingWind.1",
                    "癒しの風・息吹",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    2,
                    "UniqueHealingWind1",
                    "味方全体のHPを{0}回復。2ターンの間、毎ターン{1}回復する。",
                    Heal(Allies, Mag, 40),
                    Apply(Allies, CardStatus.Regen, 2, Mag, 20)
                ),
                Stage(
                    "HealingWind.2",
                    "癒しの風・春風",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    3,
                    "UniqueHealingWind2",
                    "味方全体のHPを{0}回復。3ターンの間、毎ターン{1}回復する。",
                    Heal(Allies, Mag, 50),
                    Apply(Allies, CardStatus.Regen, 3, Mag, 25)
                ),
                // 2本目の枝：守りと浄化を加える。
                Stage(
                    "HealingWind.3",
                    "癒しの風・守",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    2,
                    "UniqueHealingWind3",
                    "味方全体のHPを{0}回復。1ターン、受けるダメージを25%減らす。",
                    Heal(Allies, Mag, 45),
                    Apply(Allies, CardStatus.Protect, 1, 25)
                ),
                Stage(
                    "HealingWind.4",
                    "癒しの風・聖域",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    3,
                    "UniqueHealingWind4",
                    "味方全体の[状態異常]を消し、HPを{0}回復。2ターン、受けるダメージを25%減らす。",
                    Simple(CardActionKind.Cleanse, Allies),
                    Heal(Allies, Mag, 55),
                    Apply(Allies, CardStatus.Protect, 2, 25)
                ),
                // 3本目の枝：エネルギーと手札を巡らせる。
                Stage(
                    "HealingWind.5",
                    "癒しの風・巡り",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    2,
                    "UniqueHealingWind5",
                    "味方全体のHPを{0}回復。エネルギー+1。",
                    Heal(Allies, Mag, 45),
                    Simple(CardActionKind.Energy, Nobody, 1)
                ),
                Stage(
                    "HealingWind.6",
                    "癒しの風・恵み",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    2,
                    "UniqueHealingWind6",
                    "味方全体のHPを{0}回復。エネルギー+1。カードを1枚引く。",
                    Heal(Allies, Mag, 50),
                    Simple(CardActionKind.Energy, Nobody, 1),
                    Simple(CardActionKind.Draw, Nobody, 1)
                )
            ),
            Tree(
                "Revive",
                2f,
                Stage(
                    "Revive",
                    "蘇生",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    5,
                    "UniqueRevive0",
                    "倒れた味方1体を、HPを最大の半分にして復活させる。",
                    Simple(CardActionKind.Revive, Ally, 50)
                ),
                // 1本目の枝：軽くする。
                Stage(
                    "Revive.1",
                    "蘇生・祈",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    3,
                    "UniqueRevive1",
                    "倒れた味方1体を、HPを最大の30%にして復活させる。",
                    Simple(CardActionKind.Revive, Ally, 30)
                ),
                Stage(
                    "Revive.2",
                    "蘇生・奇跡",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    2,
                    "UniqueRevive2",
                    "倒れた味方1体を、HPを最大の半分にして復活させる。カードを1枚引く。",
                    Simple(CardActionKind.Revive, Ally, 50),
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                // 2本目の枝：守って起こす。
                Stage(
                    "Revive.3",
                    "蘇生・加護",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    6,
                    "UniqueRevive3",
                    "倒れた味方1体を、HPを最大の半分にして復活させる。次のターンまでダメージを受けない。",
                    Simple(CardActionKind.Revive, Ally, 50),
                    Apply(Ally, CardStatus.Invincible, 1)
                ),
                Stage(
                    "Revive.4",
                    "蘇生・不死鳥",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    7,
                    "UniqueRevive4",
                    "倒れた味方1体を、HP最大の半分で復活。1ターン無敵。3ターン毎ターン{0}回復。",
                    Simple(CardActionKind.Revive, Ally, 50),
                    Apply(Ally, CardStatus.Invincible, 1),
                    Apply(Ally, CardStatus.Regen, 3, Mag, 30)
                ),
                // 3本目の枝：奮い立たせて起こす。
                Stage(
                    "Revive.5",
                    "蘇生・活力",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    5,
                    "UniqueRevive5",
                    "倒れた味方1体を、HPを最大の半分にして復活させる。攻撃力を2ターン25%上げる。",
                    Simple(CardActionKind.Revive, Ally, 50),
                    Apply(Ally, CardStatus.AttackUp, 2, 25)
                ),
                Stage(
                    "Revive.6",
                    "蘇生・再起",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    5,
                    "UniqueRevive6",
                    "倒れた味方1体を、HP最大の60%で復活。攻撃力2ターン25%アップ。エネルギー+1。",
                    Simple(CardActionKind.Revive, Ally, 60),
                    Apply(Ally, CardStatus.AttackUp, 2, 25),
                    Simple(CardActionKind.Energy, Nobody, 1)
                )
            ),
            // --- 3つ目の固有スキル（Lv40で解放） ---------------------------------------------
            // 専用の挿絵ができるまで、枝ごとに近いスキルの挿絵を借りる（doc/features/skill-evolution.md）。
            // The mage: 敵全体の弱点を見抜き、魔法のカードにつなぐ。
            Tree(
                "ArcaneEye",
                2f,
                Stage(
                    "ArcaneEye",
                    "魔眼",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Support,
                    Enemies,
                    1,
                    "Scout",
                    "敵全体の[弱点]をすべて見抜く。カードを1枚引く。",
                    Simple(CardActionKind.RevealWeakness, Enemies),
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                // 1本目の枝：見抜いた敵を撃つ。
                Stage(
                    "ArcaneEye.1",
                    "魔眼・射",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemy,
                    2,
                    "LightningBolt",
                    "敵全体の[弱点]をすべて見抜く。敵1体に{0}ダメージ。",
                    Simple(CardActionKind.RevealWeakness, Enemies),
                    Damage(Enemy, Mag, 130)
                ),
                Stage(
                    "ArcaneEye.2",
                    "魔眼・滅光",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Attack,
                    Enemy,
                    3,
                    "LightningBolt",
                    "敵全体の[弱点]をすべて見抜く。敵1体に{0}ダメージ。[防御ダウン]2ターン。",
                    Simple(CardActionKind.RevealWeakness, Enemies),
                    Damage(Enemy, Mag, 170),
                    Apply(Enemy, CardStatus.Vulnerable, 2)
                ),
                // 2本目の枝：手札を増やす。
                Stage(
                    "ArcaneEye.3",
                    "魔眼・識",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Support,
                    Enemies,
                    1,
                    "ManaPrayer",
                    "敵全体の[弱点]をすべて見抜く。カードを2枚引く。",
                    Simple(CardActionKind.RevealWeakness, Enemies),
                    Simple(CardActionKind.Draw, Nobody, 2)
                ),
                Stage(
                    "ArcaneEye.4",
                    "魔眼・叡智",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Support,
                    Enemies,
                    1,
                    "ManaPrayer",
                    "敵全体の[弱点]をすべて見抜く。カードを2枚引く。エネルギー+1。",
                    Simple(CardActionKind.RevealWeakness, Enemies),
                    Simple(CardActionKind.Draw, Nobody, 2),
                    Simple(CardActionKind.Energy, Nobody, 1)
                ),
                // 3本目の枝：炎・氷・雷のカードを軽くする。
                Stage(
                    "ArcaneEye.5",
                    "魔眼・律",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Support,
                    Enemies,
                    1,
                    "QuickCast",
                    "敵全体の[弱点]をすべて見抜く。手札の炎・氷・雷カードのコストをすべて1下げる。",
                    Simple(CardActionKind.RevealWeakness, Enemies),
                    CostDown(1, CardElement.Fire, CardElement.Ice, CardElement.Thunder)
                ),
                Stage(
                    "ArcaneEye.6",
                    "魔眼・星律",
                    CardUser.Toma,
                    CardElement.None,
                    CardKind.Support,
                    Enemies,
                    2,
                    "QuickCast",
                    "敵全体の[弱点]をすべて見抜く。手札の炎・氷・雷カードのコストをすべて2下げる。",
                    Simple(CardActionKind.RevealWeakness, Enemies),
                    CostDown(2, CardElement.Fire, CardElement.Ice, CardElement.Thunder)
                )
            ),
            // The archer: 低いコストで素早く撃つ。
            Tree(
                "QuickShot",
                1.8f,
                Stage(
                    "QuickShot",
                    "速射",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    1,
                    "InsightArrow",
                    "敵1体に{0}ダメージ×2。",
                    Damage(Enemy, Phy, 45, hits: 2)
                ),
                // 1本目の枝：撃つ数を増やす。
                Stage(
                    "QuickShot.1",
                    "速射・連",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    1,
                    "ArrowRain",
                    "敵1体に{0}ダメージ×3。",
                    Damage(Enemy, Phy, 45, hits: 3)
                ),
                Stage(
                    "QuickShot.2",
                    "速射・乱れ撃ち",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Random,
                    2,
                    "ArrowRain",
                    "ランダムな敵に{0}ダメージ×6。",
                    Damage(Random, Phy, 45, hits: 6)
                ),
                // 2本目の枝：撃ちながら手札をつなぐ。
                Stage(
                    "QuickShot.3",
                    "速射・継",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    1,
                    "Gale",
                    "敵1体に{0}ダメージ×2。カードを1枚引く。",
                    Damage(Enemy, Phy, 45, hits: 2),
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                Stage(
                    "QuickShot.4",
                    "速射・流転",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    1,
                    "Gale",
                    "敵1体に{0}ダメージ×2。カードを1枚引く。エネルギー+1。",
                    Damage(Enemy, Phy, 45, hits: 2),
                    Simple(CardActionKind.Draw, Nobody, 1),
                    Simple(CardActionKind.Energy, Nobody, 1)
                ),
                // 3本目の枝：守りを崩す。
                Stage(
                    "QuickShot.5",
                    "速射・穿",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    1,
                    "ShadowSnipe",
                    "敵1体に{0}ダメージ×2。[防御ダウン]1ターン。",
                    Damage(Enemy, Phy, 45, hits: 2),
                    Apply(Enemy, CardStatus.Vulnerable, 1)
                ),
                Stage(
                    "QuickShot.6",
                    "速射・貫通",
                    CardUser.Luka,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    2,
                    "ShadowSnipe",
                    "敵1体に{0}ダメージ×3。[防御ダウン]中は1.5倍。[防御ダウン]2ターン。",
                    Damage(Enemy, Phy, 50, hits: 3).Bonus(CardCondition.OnVulnerable, 50),
                    Apply(Enemy, CardStatus.Vulnerable, 2)
                )
            ),
            // The knight: 味方全体を守りながら奮い立たせる。
            Tree(
                "Rally",
                2f,
                Stage(
                    "Rally",
                    "鼓舞",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Buff,
                    Allies,
                    2,
                    "WarCry",
                    "味方全体に[ブロック]{0}。味方全体の攻撃力を2ターン20%上げる。",
                    Block(Allies, Def, 40),
                    Apply(Allies, CardStatus.AttackUp, 2, 20)
                ),
                // 1本目の枝：自ら斬り込む。
                Stage(
                    "Rally.1",
                    "鼓舞・突撃",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    2,
                    "StrideStrike",
                    "敵全体に{0}ダメージ。味方全体の攻撃力を2ターン20%上げる。",
                    Damage(Enemies, Phy, 60),
                    Apply(Allies, CardStatus.AttackUp, 2, 20)
                ),
                Stage(
                    "Rally.2",
                    "鼓舞・総攻撃",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "StrideStrike",
                    "敵全体に{0}ダメージ。味方全体の攻撃力を3ターン30%上げる。",
                    Damage(Enemies, Phy, 80),
                    Apply(Allies, CardStatus.AttackUp, 3, 30)
                ),
                // 2本目の枝：号令で手札をつなぐ。
                Stage(
                    "Rally.3",
                    "鼓舞・号令",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Buff,
                    Allies,
                    2,
                    "Hone",
                    "味方全体に[ブロック]{0}。味方全体の攻撃力を2ターン20%上げる。カードを1枚引く。",
                    Block(Allies, Def, 40),
                    Apply(Allies, CardStatus.AttackUp, 2, 20),
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                Stage(
                    "Rally.4",
                    "鼓舞・凱歌",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Buff,
                    Allies,
                    2,
                    "Hone",
                    "味方全体に[ブロック]{0}。味方全体の攻撃力を2ターン20%上げる。カードを2枚引く。",
                    Block(Allies, Def, 40),
                    Apply(Allies, CardStatus.AttackUp, 2, 20),
                    Simple(CardActionKind.Draw, Nobody, 2)
                ),
                // 3本目の枝：守りを固める。
                Stage(
                    "Rally.5",
                    "鼓舞・堅陣",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Allies,
                    2,
                    "GuardianOath",
                    "味方全体に[ブロック]{0}。味方全体の受けるダメージを、2ターン25%減らす。",
                    Block(Allies, Def, 60),
                    Apply(Allies, CardStatus.Protect, 2, 25)
                ),
                Stage(
                    "Rally.6",
                    "鼓舞・鉄壁",
                    CardUser.Aria,
                    CardElement.None,
                    CardKind.Guard,
                    Allies,
                    3,
                    "GuardianOath",
                    "味方全体に[ブロック]{0}。味方全体の受けるダメージを、2ターン40%減らす。",
                    Block(Allies, Def, 80),
                    Apply(Allies, CardStatus.Protect, 2, 40)
                )
            ),
            // The healer: 敵を撃ち、その光で味方を癒やす。
            Tree(
                "Judgment",
                2f,
                Stage(
                    "Judgment",
                    "裁きの光",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "HolyLight",
                    "敵全体に{0}ダメージ。味方全体のHPを{1}回復する。",
                    Damage(Enemies, Mag, 60),
                    Heal(Allies, Mag, 25)
                ),
                // 1本目の枝：1体に集める。
                Stage(
                    "Judgment.1",
                    "裁きの光・断罪",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Attack,
                    Enemy,
                    3,
                    "HolyHammer",
                    "敵1体に{0}ダメージ。味方全体のHPを{1}回復する。",
                    Damage(Enemy, Mag, 140),
                    Heal(Allies, Mag, 25)
                ),
                Stage(
                    "Judgment.2",
                    "裁きの光・天罰",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Attack,
                    Enemy,
                    4,
                    "HolyHammer",
                    "敵1体に{0}ダメージ。HPが半分以下の敵には1.5倍。味方全体のHPを{1}回復する。",
                    Damage(Enemy, Mag, 160).Bonus(CardCondition.OnWounded, 50),
                    Heal(Allies, Mag, 30)
                ),
                // 2本目の枝：清める。
                Stage(
                    "Judgment.3",
                    "裁きの光・浄",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "Purify",
                    "敵全体に{0}ダメージ。味方全体の[状態異常]を消し、HPを{1}回復。",
                    Damage(Enemies, Mag, 60),
                    Simple(CardActionKind.Cleanse, Allies),
                    Heal(Allies, Mag, 25)
                ),
                Stage(
                    "Judgment.4",
                    "裁きの光・聖浄",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    4,
                    "Purify",
                    "敵全体の[強化]を1つ消し、{0}ダメージ。味方全体の[状態異常]を消し、HPを{1}回復。",
                    Simple(CardActionKind.Dispel, Enemies, 1),
                    Damage(Enemies, Mag, 70),
                    Simple(CardActionKind.Cleanse, Allies),
                    Heal(Allies, Mag, 30)
                ),
                // 3本目の枝：癒やしを残す。
                Stage(
                    "Judgment.5",
                    "裁きの光・陽光",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "Regen",
                    "敵全体に{0}ダメージ。味方全体のHPを{1}回復。2ターン毎ターン{2}回復。",
                    Damage(Enemies, Mag, 60),
                    Heal(Allies, Mag, 25),
                    Apply(Allies, CardStatus.Regen, 2, Mag, 15)
                ),
                Stage(
                    "Judgment.6",
                    "裁きの光・天照",
                    CardUser.Mina,
                    CardElement.None,
                    CardKind.Attack,
                    Enemies,
                    3,
                    "Regen",
                    "敵全体に{0}ダメージ。味方全体のHPを{1}回復。3ターン毎ターン{2}回復。",
                    Damage(Enemies, Mag, 60),
                    Heal(Allies, Mag, 25),
                    Apply(Allies, CardStatus.Regen, 3, Mag, 20)
                )
            )
        );

        public static UniqueSkillTree Find(string id) =>
            string.IsNullOrEmpty(id)
                ? null
                : Array.Find((UniqueSkillTree[])All, tree => tree.Id == id);

        /// <summary>Every stage made so far, of every tree.</summary>
        public static IEnumerable<UniqueSkillStage> Stages()
        {
            foreach (var tree in All)
            foreach (var stage in tree.Stages)
                if (stage != null)
                    yield return stage;
        }

        /// <summary>The stage whose card has the id, or null.</summary>
        public static UniqueSkillStage FindStage(string card)
        {
            if (string.IsNullOrEmpty(card))
                return null;
            foreach (var stage in Stages())
                if (stage.Card.Id == card)
                    return stage;
            return null;
        }

        // 固有スキルごとに、カードスキルの後の光り方を1つずつ割り当てる。どの進化の段階も同じ光り方にする。
        private static UniqueSkillTree[] Styled(params UniqueSkillTree[] trees)
        {
            for (int i = 0; i < trees.Length; i++)
                foreach (var stage in trees[i].Stages)
                    if (stage != null)
                        stage.Card.FoilStyle = CardSkills.FoilStyles + i;
            return trees;
        }

        // 最初の段階に続けて、取得した順（1本目の枝の1段目、2段目、2本目の枝の1段目…）に段階を並べる。
        // limit は、その固有スキルの倍率の上限。進化しても同じ上限を使う。
        private static UniqueSkillTree Tree(
            string id,
            float limit,
            params UniqueSkillStage[] stages
        )
        {
            var all = new UniqueSkillStage[UniqueSkillTreeShape.Count];
            Array.Copy(stages, all, Math.Min(stages.Length, all.Length));
            foreach (var stage in stages)
                stage.Card.MaxMultiplier = limit;
            // 固有スキルはレア度を持たず、品質C（×1.00）で始まる（doc/features/inventory.md の品質）。
            return new UniqueSkillTree { Id = id, Stages = Array.AsReadOnly(all) };
        }

        private static UniqueSkillStage Stage(
            string id,
            string name,
            CardUser user,
            CardElement element,
            CardKind kind,
            CardTarget target,
            int cost,
            string art,
            string text,
            params CardAction[] actions
        ) =>
            new()
            {
                Art = "Card" + art,
                Card = new CardSkill
                {
                    Id = id,
                    Name = name,
                    User = user,
                    Element = element,
                    Kind = kind,
                    Target = target,
                    Cost = cost,
                    Text = text,
                    Actions = Array.AsReadOnly(actions),
                },
            };
    }
}
