using System;
using System.Collections.Generic;
using static Baryonyx.Combat.CardStat;
using static Baryonyx.Combat.EnemyAim;
using static Baryonyx.Combat.EnemyCare;
using static Baryonyx.Combat.EnemyCue;
using static Baryonyx.Combat.EnemyMove;
using static Baryonyx.Combat.EnemyPattern;

namespace Baryonyx.Combat
{
    /// <summary>
    /// Every enemy's skills and patterns (doc/catalog/enemies: 技 and 行動パターン, with the
    /// skills of doc/catalog/enemy-skills), one behaviour per kind of <see cref="CombatEnemies"/>.
    /// Written from the documents; keep them in step when a document changes. Effects the battle
    /// does not have yet are left out as the comments tell: statuses on the party, calls for help
    /// and the like. The kinds are split over files by where they first show up.
    /// </summary>
    public static partial class CombatEnemyBehaviors
    {
        private static IReadOnlyList<EnemyBehavior> all;

        public static IReadOnlyList<EnemyBehavior> All => all ??= Gather();

        public static EnemyBehavior Find(string id)
        {
            foreach (var behavior in All)
                if (behavior.Id == id)
                    return behavior;
            return null;
        }

        private static IReadOnlyList<EnemyBehavior> Gather()
        {
            var list = new List<EnemyBehavior>();
            list.AddRange(Mock());
            list.AddRange(Forest());
            list.AddRange(Coast());
            list.AddRange(Depths());
            list.AddRange(Tomb());
            list.AddRange(Peaks());
            return list;
        }

        // --- 状態を付ける技（doc/catalog/enemy-skills）：効果の部品で書く ----------------------

        // 使った敵が持ち主になり、使ったときに1回働く。名前は技の名前として出るので、効果の名前は出さない。
        private static EnemyMove Afflict(string name, BattleAct act) =>
            EnemyMove.Hex(
                name,
                new BattleEffect("enemy-skill-" + name, name)
                    .On(BattleTrigger.Used, act)
                    .When(BattleCondition.ActorIsOwner)
                    .Shows(BattleShow.Quiet)
            );

        /// <summary>にらみつけ：味方単体に麻痺2ターン。</summary>
        private static EnemyMove Glare() =>
            Afflict(
                "にらみつけ",
                BattleAct.ApplyStatus(BattleWho.RandomAlly, CardStatus.Paralysis, 2)
            );

        /// <summary>金切り声：味方全体に麻痺1ターン。</summary>
        private static EnemyMove Screech() =>
            Afflict(
                "金切り声",
                BattleAct.ApplyStatus(BattleWho.AllAllies, CardStatus.Paralysis, 1)
            );

        /// <summary>墨吐き：味方全体に麻痺1ターン。予告を伏せる分は、予告を出さない戦闘なので入れない。</summary>
        private static EnemyMove InkSpray() =>
            Afflict("墨吐き", BattleAct.ApplyStatus(BattleWho.AllAllies, CardStatus.Paralysis, 1));

        /// <summary>胞子ばらまき：味方全体に毒を2重ねる。</summary>
        private static EnemyMove SporeScatter() =>
            Afflict(
                "胞子ばらまき",
                BattleAct.ApplyStatus(BattleWho.AllAllies, CardStatus.Poison, 0, 0, 2)
            );

        /// <summary>甘い蜜の香り：味方全体に防御ダウン1ターン（受けるダメージ+25%）。</summary>
        private static EnemyMove SweetScent() =>
            Afflict(
                "甘い蜜の香り",
                BattleAct.ApplyStatus(BattleWho.AllAllies, CardStatus.Vulnerable, 1)
            );

        /// <summary>胞子の芽吹き：毒が付いている味方それぞれの毒を、今の重なりと同じ数だけ増やす。</summary>
        private static EnemyMove SporeBloom() =>
            Afflict(
                "胞子の芽吹き",
                BattleAct.Custom(m =>
                {
                    bool any = false;
                    foreach (var ally in BattleWho.AllAllies.In(m))
                    {
                        int stacks = m.World.StatusAmount(ally, CardStatus.Poison);
                        if (stacks > 0)
                            any |= m.World.ApplyStatus(
                                m.Owner,
                                ally,
                                CardStatus.Poison,
                                0,
                                0,
                                stacks
                            );
                    }
                    return any ? BattleActResult.Did() : BattleActResult.Nothing;
                })
            );

        /// <summary>
        /// 毒まわし：毒の重なりが最も多い味方へ、毒が尽きるまでに与えるはずのダメージの合計を一度に
        /// 与えて毒を消す。
        /// </summary>
        private static EnemyMove PoisonRush() =>
            Afflict(
                "毒まわし",
                BattleAct.Custom(m =>
                {
                    var most = BattleUnit.None;
                    int stacks = 0;
                    foreach (var ally in BattleWho.AllAllies.In(m))
                    {
                        int here = m.World.StatusAmount(ally, CardStatus.Poison);
                        if (here > stacks)
                        {
                            stacks = here;
                            most = ally;
                        }
                    }
                    if (most.IsNone)
                        return BattleActResult.Nothing;
                    int power = m.World.StatusPower(most, CardStatus.Poison);
                    m.World.RemoveStatus(most, CardStatus.Poison);
                    m.World.Strike(
                        new BattleEvent
                        {
                            Actor = m.Owner,
                            Target = most,
                            Kind = BattleDamageKind.Status,
                            Status = CardStatus.Poison,
                        },
                        power * stacks * (stacks + 1) / 2,
                        BattleStrikeLook.Plain
                    );
                    return BattleActResult.Did();
                })
            );

        // 自分の身を守る技・味方を苦しめる技（doc/catalog/enemy-skills）。
        private static BattleEffect Guise(string name) => new("enemy-skill-" + name, name);

        private static EnemyMove Hold(
            string name,
            BattleHoldSpan span,
            int turns,
            BattleEffect effect
        ) => Afflict(name, BattleAct.Hold(effect, span, turns));

        /// <summary>水晶角の照り返し：次に受ける単体攻撃のカードを1回、使った味方へはね返す。</summary>
        private static EnemyMove CrystalGlare() =>
            Afflict(
                "水晶角の照り返し",
                BattleAct.ApplyStatus(BattleWho.Owner, CardStatus.Reflect, 1)
            );

        /// <summary>すり抜け：次の自分の行動まで、物理（斬・打・貫）の攻撃のダメージを受けない。</summary>
        private static EnemyMove SlipThrough() =>
            Hold(
                "すり抜け",
                BattleHoldSpan.UntilOwnerActs,
                0,
                Guise("すり抜け")
                    .On(BattleTrigger.BlowLanding, BattleAct.Cancel)
                    .When(
                        BattleCondition.TargetIsOwner
                            & BattleCondition.ActorIsAlly
                            & BattleCondition.Physical
                    )
            );

        /// <summary>耳ざとい身かわし：次の自分の行動まで、コスト3以上の単体攻撃のカードをかわす。</summary>
        private static EnemyMove KeenDodge() =>
            Hold(
                "耳ざとい身かわし",
                BattleHoldSpan.UntilOwnerActs,
                0,
                Guise("身かわし")
                    .On(BattleTrigger.BlowLanding, BattleAct.Cancel)
                    .When(
                        BattleCondition.TargetIsOwner
                            & BattleCondition.ActorIsAlly
                            & BattleCondition.CardOnOneEnemy
                            & BattleCondition.CardCostAtLeast(3)
                    )
            );

        /// <summary>霧隠れ：2ターン、単体攻撃のカードから受けるダメージ-50%。</summary>
        private static EnemyMove MistVeil() =>
            Hold(
                "霧隠れ",
                BattleHoldSpan.Turns,
                2,
                Guise("霧隠れ")
                    .Multiply(BattleValue.Damage, -50, 4)
                    .When(
                        BattleCondition.TargetIsOwner
                            & BattleCondition.KindIs(BattleDamageKind.Card)
                            & BattleCondition.CardOnOneEnemy
                    )
            );

        /// <summary>燃えるうろこ：2ターン、自分に単体の物理の攻撃をしてきた味方に、やけど1ターン。</summary>
        private static EnemyMove BurningScales() =>
            Hold(
                "燃えるうろこ",
                BattleHoldSpan.Turns,
                2,
                Guise("燃えるうろこ")
                    .On(
                        BattleTrigger.DealtDamage,
                        BattleAct.ApplyStatus(BattleWho.Actor, CardStatus.Burn, 1)
                    )
                    .When(
                        BattleCondition.TargetIsOwner
                            & BattleCondition.ActorIsAlly
                            & BattleCondition.KindIs(BattleDamageKind.Card)
                            & BattleCondition.CardOnOneEnemy
                            & BattleCondition.Physical
                    )
            );

        /// <summary>
        /// 畑の見張り番：2ターン、自分以外の敵が単体攻撃のカードを受けるたびに、そのカードを使った味方へ
        /// 物攻の40%を返す（1ターンに2回まで）。
        /// </summary>
        private static EnemyMove FieldWatch() =>
            Hold(
                "畑の見張り番",
                BattleHoldSpan.Turns,
                2,
                Guise("見張り")
                    .On(
                        BattleTrigger.DealtDamage,
                        BattleAct.Strike(
                            BattleWho.Actor,
                            CardStat.PhysicalAttack,
                            40,
                            CardElement.None
                        )
                    )
                    .When(
                        BattleCondition.TargetIsEnemy
                            & !BattleCondition.TargetIsOwner
                            & BattleCondition.ActorIsAlly
                            & BattleCondition.KindIs(BattleDamageKind.Card)
                            & BattleCondition.CardOnOneEnemy
                    )
                    .Limit(2, BattleLimitSpan.Turn)
            );

        /// <summary>熱風の羽ばたき：自分と仲間に速度アップ2ターン。</summary>
        private static EnemyMove HotGust() =>
            Afflict(
                "熱風の羽ばたき",
                BattleAct.ApplyStatus(BattleWho.OwnSide, CardStatus.SpeedUp, 2)
            );

        /// <summary>延焼：やけどが付いている味方はやけどの残りを2延ばし、付いていない味方にはやけど1ターン。</summary>
        private static EnemyMove Spread() =>
            Afflict(
                "延焼",
                BattleAct.Custom(m =>
                {
                    bool any = false;
                    foreach (var ally in BattleWho.AllAllies.In(m))
                    {
                        int left = m.World.StatusTurns(ally, CardStatus.Burn);
                        any |= m.World.ApplyStatus(
                            m.Owner,
                            ally,
                            CardStatus.Burn,
                            left > 0 ? left + 2 : 1,
                            0,
                            0
                        );
                    }
                    return any ? BattleActResult.Did() : BattleActResult.Nothing;
                })
            );

        /// <summary>朽ちの呪い：味方単体に呪い2ターン（得るブロックが半分になる）。</summary>
        private static EnemyMove DecayCurse() =>
            Afflict("朽ちの呪い", BattleAct.ApplyStatus(BattleWho.RandomAlly, CardStatus.Curse, 2));

        /// <summary>
        /// 丸のみ：自分以外の敵のうちHPの最も低い1体を倒し、その敵の残りHPだけ自分を回復する
        /// （属攻の300%まで）。
        /// </summary>
        private static EnemyMove Swallow() =>
            Afflict(
                "丸のみ",
                BattleAct.Custom(m =>
                {
                    var prey = BattleUnit.None;
                    int hp = int.MaxValue;
                    foreach (var enemy in BattleWho.OwnSide.In(m))
                        if (enemy != m.Owner && m.World.Hp(enemy) < hp)
                        {
                            hp = m.World.Hp(enemy);
                            prey = enemy;
                        }
                    if (prey.IsNone)
                        return BattleActResult.Nothing;
                    m.World.Strike(
                        new BattleEvent
                        {
                            Actor = m.Owner,
                            Target = prey,
                            Kind = BattleDamageKind.Status,
                            Pierce = true,
                        },
                        hp,
                        BattleStrikeLook.Plain
                    );
                    m.World.Heal(
                        m.Owner,
                        m.Owner,
                        Math.Min(hp, m.World.Stat(m.Owner, CardStat.MagicAttack) * 3)
                    );
                    return BattleActResult.Did();
                })
            );

        // The three enemies of the battle mock (2026-10-04).
        private static EnemyBehavior[] Mock() =>
            new[]
            {
                new EnemyBehavior
                {
                    Id = "moss-slime",
                    Moves = new[]
                    {
                        Attack("たいあたり", PhysicalAttack, 100),
                        SporeScatter(),
                        Mend("朝露すすり", 100),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "たいあたり",
                        Choices = new[] { Weight("たいあたり", 60), Weight("胞子ばらまき", 40) },
                        Once = new[] { "胞子ばらまき" },
                        Rules = new[] { EnemyRule.When(Hurt, "朝露すすり").Times(1) },
                    },
                    // 激化で胞子ばらまきの毒が3重ねになる分は、激化が攻撃の割合だけを変えるため入れない。
                    EnrageTurn = 5,
                    Enrage = new[] { new EnemyEnrage("たいあたり", 150) },
                },
                new EnemyBehavior
                {
                    Id = "moss-wolf",
                    Moves = new[]
                    {
                        Attack("かみつき", PhysicalAttack, 100),
                        // 出血2ターンは入れない。
                        Attack("引き裂き", PhysicalAttack, 60),
                        Boost("遠吠え", 2),
                        Guard("草陰に伏せる", 120).WithPowerUp(50),
                        Attack("喉笛狙い", PhysicalAttack, 140, Weakest),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "遠吠え",
                        OpeningAlone = "草陰に伏せる",
                        Choices = new[]
                        {
                            Weight("かみつき", 40),
                            Weight("引き裂き", 30),
                            Weight("草陰に伏せる", 30),
                        },
                        Once = new[] { "草陰に伏せる" },
                        Rules = new[]
                        {
                            EnemyRule.When(Together, "遠吠え").Apart(3),
                            EnemyRule.When(PartyHurt, "喉笛狙い").Apart(2),
                        },
                    },
                    EnrageTurn = 5,
                    Enrage = new[]
                    {
                        new EnemyEnrage("かみつき", 130),
                        new EnemyEnrage("引き裂き", 130),
                        new EnemyEnrage("喉笛狙い", 130),
                    },
                },
                new EnemyBehavior
                {
                    Id = "forest-guardian",
                    Moves = new[]
                    {
                        Attack("大地の拳", PhysicalAttack, 100),
                        Attack("地響き", PhysicalAttack, 50, EnemyAim.All),
                        // 絡みつき1ターンは入れない。
                        Attack("根縛り", PhysicalAttack, 30),
                        Guard("身を固める", 120),
                        Mend("森の恵み", 100, Others).WithRegen(2, 20),
                        Other("仲間を呼ぶ", "敵の呼び出し"),
                        Boost("刻印の目覚め", Forever, Self),
                        // 防御ダウン2ターンは入れない。溜めのターンに身を固めるも使う。
                        Attack("森羅の鉄槌", PhysicalAttack, 140, EnemyAim.All)
                            .Charged(1, "身を固める"),
                    },
                    Early = new EnemyPattern
                    {
                        Opening = "大地の拳",
                        Order = new[] { "大地の拳", "身を固める", "根縛り", "地響き" },
                        Rules = new[]
                        {
                            EnemyRule.When(OtherHurt, "森の恵み").Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                    LateTurn = 6,
                    LateMove = "刻印の目覚め",
                    Late = new EnemyPattern
                    {
                        Cycle = new[] { Pick, Pick, "森羅の鉄槌" },
                        Choices = new[]
                        {
                            Weight("大地の拳", 1),
                            Weight("地響き", 1),
                            Weight("根縛り", 1),
                        },
                        MaxRun = 1,
                        Rules = new[]
                        {
                            EnemyRule.When(OtherHurt, "森の恵み").Apart(3),
                            EnemyRule.When(Alone, "仲間を呼ぶ").Times(2),
                        },
                    },
                },
            };
    }
}
