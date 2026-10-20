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
            // --- The mock's first six -------------------------------------------------------
            Card(
                "Slash",
                "斬り払い",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Attack,
                Enemy,
                1,
                2.4f,
                "敵1体に{0}ダメージ。[出血]中の敵には1.5倍。",
                Damage(Enemy, Phy, 100).Bonus(CardCondition.OnBleeding, 50)
            ),
            Card(
                "Fire",
                "ファイア",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemy,
                2,
                2.4f,
                "敵1体に{0}ダメージ。[弱点]を突くと[やけど]2ターン。",
                Damage(Enemy, Mag, 100),
                Apply(Enemy, CardStatus.Burn, 2).Only(CardCondition.OnWeakness)
            ),
            Card(
                "Ice",
                "アイスランス",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Attack,
                Enemy,
                2,
                2.2f,
                "敵1体に{0}ダメージ。[凍え]2ターン。",
                Damage(Enemy, Mag, 85),
                Apply(Enemy, CardStatus.Chill, 2)
            ),
            Card(
                "Thunder",
                "サンダー",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Enemies,
                3,
                2.2f,
                "敵全体に{0}ダメージ。[麻痺]中の敵には1.5倍。",
                Damage(Enemies, Mag, 70).Bonus(CardCondition.OnParalyzed, 50)
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
                Heal(Ally, Mag, 90)
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
                "味方全体に[ブロック]{0}。",
                Block(Allies, Def, 60)
            ),
            // --- Aria: slash, blunt, guarding the party -------------------------------------
            Card(
                "Whirlwind",
                "旋風斬",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Attack,
                Enemies,
                3,
                2f,
                "敵全体に{0}ダメージ。[出血]2ターン。",
                Damage(Enemies, Phy, 60),
                Apply(Enemies, CardStatus.Bleed, 2)
            ),
            Card(
                "Iai",
                "居合一閃",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Attack,
                Enemy,
                5,
                2.8f,
                "敵1体に{0}ダメージ。HPが半分以下の敵には1.5倍。",
                Damage(Enemy, Phy, 260).Bonus(CardCondition.OnWounded, 50)
            ),
            Card(
                "BladeDance",
                "千刃乱舞",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Attack,
                Random,
                6,
                1.6f,
                "ランダムな敵に{0}ダメージ×7。",
                Damage(Random, Phy, 45, hits: 7)
            ),
            Card(
                "Hone",
                "研ぎ澄ます",
                CardUser.Aria,
                CardElement.Slash,
                CardKind.Support,
                Nobody,
                0,
                2f,
                "手札の斬属性カードのコストをすべて1下げる。",
                CostDown(1, CardElement.Slash)
            ),
            Card(
                "ShieldBash",
                "シールドバッシュ",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Attack,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。味方全体に[ブロック]{1}。",
                Damage(Enemy, Def, 90),
                Block(Allies, Def, 30)
            ),
            Card(
                "ArmorBreak",
                "鎧砕き",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Debuff,
                Enemy,
                2,
                2.2f,
                "敵1体に{0}ダメージ。[防御ダウン]2ターン。",
                Damage(Enemy, Phy, 80),
                Apply(Enemy, CardStatus.Vulnerable, 2)
            ),
            Card(
                "EarthSplitter",
                "大地割り",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Attack,
                Enemies,
                4,
                2.4f,
                "敵全体に{0}ダメージ。[防御ダウン]中の敵には1.5倍。",
                Damage(Enemies, Phy, 85).Bonus(CardCondition.OnVulnerable, 50)
            ),
            Card(
                "GiantImpact",
                "ギガントインパクト",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Attack,
                Enemy,
                7,
                2.8f,
                "敵1体に{0}ダメージ。自分に[ブロック]があれば2倍。",
                Damage(Enemy, Phy, 400).Bonus(CardCondition.OnGuarded, 100)
            ),
            Card(
                "StrideStrike",
                "健脚の一撃",
                CardUser.Aria,
                CardElement.Blunt,
                CardKind.Attack,
                Enemy,
                3,
                2.4f,
                "敵1体に{0}ダメージ。今日のACTが多いほど強い。",
                Damage(Enemy, Phy, 110).WithAct(10, 100)
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
                "自分に[ブロック]{0}。2ターン、敵の攻撃を引きつける。",
                Block(Self, Def, 150),
                Apply(Self, CardStatus.Taunt, 2)
            ),
            Card(
                "WarCry",
                "鬨の声",
                CardUser.Aria,
                CardElement.None,
                CardKind.Buff,
                Allies,
                2,
                2f,
                "味方全体の攻撃力を2ターン25%上げる。",
                Apply(Allies, CardStatus.AttackUp, 2, 25)
            ),
            // --- Toma: fire and ice ---------------------------------------------------------
            Card(
                "Embers",
                "火の粉",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。[やけど]3ターン。",
                Damage(Enemy, Mag, 45),
                Apply(Enemy, CardStatus.Burn, 3)
            ),
            Card(
                "FlamePillar",
                "フレイムピラー",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemy,
                3,
                2.4f,
                "敵1体に{0}ダメージ。[やけど]中の敵には1.5倍。",
                Damage(Enemy, Mag, 120).Bonus(CardCondition.OnBurning, 50)
            ),
            Card(
                "FireStorm",
                "ファイアストーム",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemies,
                4,
                2.2f,
                "敵全体に{0}ダメージ。[やけど]2ターン。",
                Damage(Enemies, Mag, 70),
                Apply(Enemies, CardStatus.Burn, 2)
            ),
            Card(
                "BlastSigil",
                "爆炎の刻印",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemy,
                3,
                2.4f,
                "敵1体に[刻印]。次のターン、敵全体に{0}ダメージ。",
                Apply(Enemy, CardStatus.BlastSigil, 1, Mag, 150)
            ),
            Card(
                "Meteor",
                "メテオ",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Attack,
                Enemies,
                9,
                3f,
                "敵全体に{0}ダメージ。[やけど]中の敵には2倍。",
                Damage(Enemies, Mag, 450).Bonus(CardCondition.OnBurning, 100)
            ),
            Card(
                "FlameEnchant",
                "フレイムエンチャント",
                CardUser.Toma,
                CardElement.Fire,
                CardKind.Buff,
                Ally,
                2,
                2f,
                "味方1体の攻撃に、3ターン炎属性を加える。",
                Apply(Ally, CardStatus.FireBlade, 3)
            ),
            Card(
                "Icicles",
                "つらら落とし",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Attack,
                Random,
                2,
                1.7f,
                "ランダムな敵に{0}ダメージ×4。",
                Damage(Random, Mag, 35, hits: 4)
            ),
            Card(
                "Blizzard",
                "ブリザード",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Attack,
                Enemies,
                4,
                2.2f,
                "敵全体に{0}ダメージ。[凍え]2ターン。",
                Damage(Enemies, Mag, 90),
                Apply(Enemies, CardStatus.Chill, 2)
            ),
            Card(
                "IceMirror",
                "アイスミラー",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Guard,
                Ally,
                3,
                1.6f,
                "味方1体が次に受ける単体攻撃を、1回はね返す。",
                Apply(Ally, CardStatus.Reflect, 1)
            ),
            Card(
                "AbsoluteZero",
                "絶対零度",
                CardUser.Toma,
                CardElement.Ice,
                CardKind.Attack,
                Enemy,
                8,
                2.4f,
                "敵1体に{0}ダメージ。[凍え]中の敵には1.5倍。[凍結]させる。",
                Damage(Enemy, Mag, 340).Bonus(CardCondition.OnChilled, 50),
                Apply(Enemy, CardStatus.Freeze, 1)
            ),
            Card(
                "QuickCast",
                "詠唱短縮",
                CardUser.Toma,
                CardElement.None,
                CardKind.Support,
                Nobody,
                0,
                2f,
                "手札の炎・氷・雷カードのコストをすべて1下げる。",
                CostDown(1, Magic)
            ),
            // --- Luka: pierce, thunder, tricks ----------------------------------------------
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
                Damage(Enemy, Phy, 90),
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
                "敵1体に{0}ダメージ。[毒]を2つ重ねる。",
                Damage(Enemy, Phy, 35),
                Apply(Enemy, CardStatus.Poison, 0, 2)
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
                "敵1体に{0}ダメージ。その敵の行動順を1つ遅らせる。",
                Damage(Enemy, Phy, 60),
                Simple(CardActionKind.Delay, Enemy, 1)
            ),
            Card(
                "InsightArrow",
                "看破の矢",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Debuff,
                Enemy,
                2,
                2.2f,
                "敵1体に{0}ダメージ。その敵の[弱点]をすべて見抜く。",
                Damage(Enemy, Phy, 70),
                Simple(CardActionKind.RevealWeakness, Enemy)
            ),
            Card(
                "ArrowRain",
                "アローレイン",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Attack,
                Enemies,
                3,
                1.8f,
                "敵全体に{0}ダメージ×2。[毒]がある敵には1.5倍。",
                Damage(Enemies, Phy, 45, hits: 2).Bonus(CardCondition.OnPoisoned, 50)
            ),
            Card(
                "ShadowSnipe",
                "シャドウスナイプ",
                CardUser.Luka,
                CardElement.Pierce,
                CardKind.Attack,
                Enemy,
                5,
                2.6f,
                "敵1体に{0}ダメージ。[弱点]を突くと威力+50%。",
                Damage(Enemy, Phy, 260).Bonus(CardCondition.OnWeakness, 50)
            ),
            Card(
                "LightningBolt",
                "ライトニングボルト",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Enemy,
                1,
                2.2f,
                "敵1体に{0}ダメージ。[麻痺]1ターン。",
                Damage(Enemy, Mag, 70),
                Apply(Enemy, CardStatus.Paralysis, 1)
            ),
            Card(
                "ChainLightning",
                "チェインライトニング",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Chain,
                3,
                1.8f,
                "敵1体に{0}ダメージ×3。1回ごとに別の敵へ移る。",
                Damage(Chain, Mag, 65, hits: 3)
            ),
            Card(
                "Thundercloud",
                "雷雲",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Nobody,
                4,
                2f,
                "3ターンの間、毎ターンランダムな敵1体に{0}ダメージ。",
                Apply(Nobody, CardStatus.Thundercloud, 3, Mag, 80)
            ),
            Card(
                "ThunderSpear",
                "雷神の槍",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Attack,
                Enemy,
                6,
                2.6f,
                "敵1体に{0}、さらに敵全体に{1}ダメージ。",
                Damage(Enemy, Mag, 280),
                Damage(Enemies, Mag, 50)
            ),
            Card(
                "Gale",
                "疾風迅雷",
                CardUser.Luka,
                CardElement.Thunder,
                CardKind.Buff,
                Allies,
                2,
                2f,
                "味方全体の速度を2ターン上げる。",
                Apply(Allies, CardStatus.SpeedUp, 2, 30)
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
                "カードを2枚引く。",
                Simple(CardActionKind.Draw, Nobody, 2)
            ),
            // --- Mina: healing, protecting, holy blows --------------------------------------
            Card(
                "StepPrayer",
                "万歩の祈り",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Allies,
                3,
                2.2f,
                "味方全体のHPを{0}回復。今日のACTが多いほど回復。",
                Heal(Allies, Mag, 50).WithAct(10, 100)
            ),
            Card(
                "Regen",
                "リジェネ",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Ally,
                2,
                2.2f,
                "味方1体を、3ターンの間、毎ターン{0}回復する。",
                Apply(Ally, CardStatus.Regen, 3, Mag, 40)
            ),
            Card(
                "Resurrection",
                "リザレクション",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Ally,
                5,
                2f,
                "味方1体のHPを最大の半分回復。倒れていれば復活。",
                Simple(CardActionKind.Revive, Ally, 50)
            ),
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
                Damage(Enemy, Mag, 140),
                Simple(CardActionKind.Dispel, Enemy, 1)
            ),
            Card(
                "Protect",
                "プロテクト",
                CardUser.Mina,
                CardElement.None,
                CardKind.Guard,
                Allies,
                2,
                2f,
                "味方全体の受けるダメージを、2ターン25%減らす。",
                Apply(Allies, CardStatus.Protect, 2, 25)
            ),
            Card(
                "Purify",
                "浄化の光",
                CardUser.Mina,
                CardElement.None,
                CardKind.Heal,
                Allies,
                1,
                2.2f,
                "味方全体の[状態異常]を消し、HPを{0}回復。",
                Simple(CardActionKind.Cleanse, Allies),
                Heal(Allies, Mag, 25)
            ),
            Card(
                "ManaPrayer",
                "マナの祈り",
                CardUser.Mina,
                CardElement.None,
                CardKind.Support,
                Nobody,
                1,
                1.5f,
                "エネルギーを2回復する。",
                Simple(CardActionKind.Energy, Nobody, 2)
            ),
            Card(
                "Revelation",
                "天啓",
                CardUser.Mina,
                CardElement.None,
                CardKind.Support,
                Nobody,
                0,
                2f,
                "次に使うカードのコストを0にする。",
                Simple(CardActionKind.NextCardFree)
            ),
            Card(
                "HolyLight",
                "ホーリーライト",
                CardUser.Mina,
                CardElement.None,
                CardKind.Attack,
                Enemies,
                3,
                2.2f,
                "敵全体に{0}ダメージ。味方全体のHPを{1}回復。",
                Damage(Enemies, Mag, 50),
                Heal(Allies, Mag, 30)
            ),
            Card(
                "DivineShield",
                "ディバインシールド",
                CardUser.Mina,
                CardElement.None,
                CardKind.Guard,
                Ally,
                3,
                1.6f,
                "味方1体は、次のターンまでダメージを受けない。",
                Apply(Ally, CardStatus.Invincible, 1)
            )
        );

        /// <summary>
        /// Cards tried only in the battle mock, not among the card skills (<see cref="All"/>): not
        /// counted with them, not offered in the loadout, and without art of their own.
        /// 蛇牙の連撃 has many effects, to try the keyword notes stacked beside the raised card
        /// (doc/features/combat.md, card keywords).
        /// </summary>
        public static readonly IReadOnlyList<CardSkill> Prototypes = Styled(
            FoilStyles + 8,
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
                    "敵1体に{0}ダメージ×2。[出血]・[毒]・[防御ダウン]。[弱点]でエネルギー+1。",
                    Damage(Enemy, Phy, 45, hits: 2),
                    Apply(Enemy, CardStatus.Bleed, 2),
                    Apply(Enemy, CardStatus.Poison, 0, 2),
                    Apply(Enemy, CardStatus.Vulnerable, 2),
                    Simple(CardActionKind.Energy, Nobody, 1).Only(CardCondition.OnWeakness)
                )
            )
        );

        /// <summary>The card (or prototype) with <paramref name="id"/>, or null.</summary>
        public static CardSkill Find(string id)
        {
            foreach (var card in All)
                if (card.Id == id)
                    return card;
            foreach (var card in Prototypes)
                if (card.Id == id)
                    return card;
            return null;
        }

        // カードに、first から順に光り方を割り当てる。
        private static IReadOnlyList<CardSkill> Styled(int first, params CardSkill[] cards)
        {
            for (int i = 0; i < cards.Length; i++)
                cards[i].FoilStyle = first + i;
            return Array.AsReadOnly(cards);
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
