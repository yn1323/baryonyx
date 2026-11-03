using System.Collections.Generic;
using static Baryonyx.Combat.BattleCondition;

namespace Baryonyx.Combat
{
    /// <summary>
    /// What a status does while it is on someone, as an effect (doc/features/combat/cards.md,
    /// カードの状態の仮設定): the battle asks the effect of each status on a standing character
    /// as it asks the passives. A status whose working is the turn order, the targeting or a
    /// tick of its own (凍結・凍え・速度アップ・挑発・炎の加護・やけど・毒・出血・リジェネ・刻印・雷雲)
    /// has none here; the battle does it where it happens.
    /// </summary>
    public static class BattleStatuses
    {
        /// <summary>防御ダウン: the damage of the party's cards on the holder grows by this.</summary>
        public const int VulnerablePercent = 25;

        /// <summary>プロテクト: the damage the holder takes goes down by this.</summary>
        public const int ProtectPercent = 25;

        /// <summary>麻痺 on an ally: the damage of its cards goes down by this (as an enemy's blows do).</summary>
        public const int ParalysisPercent = 25;

        // 掛ける順：攻撃力アップのあとに防御ダウン（2026-11-03より前の計算と同じ順）。
        private const int AttackUpOrder = 1;
        private const int VulnerableOrder = 2;
        private const int ParalysisOrder = 3;

        // 反射：攻撃の威力を、攻撃した敵の物防とLvで弱めて返し、反射を使い切る。
        private static readonly BattleAct Reflect = BattleAct.Custom(m =>
        {
            m.World.RemoveStatus(m.Owner, CardStatus.Reflect);
            int power = CombatFormula.Defend(
                m.Amount,
                m.World.Defense(m.Actor, CardStat.PhysicalAttack),
                m.World.Level(m.Actor)
            );
            m.World.Strike(
                new BattleEvent
                {
                    Actor = m.Owner,
                    Target = m.Actor,
                    Kind = BattleDamageKind.Status,
                    Status = CardStatus.Reflect,
                },
                power,
                BattleStrikeLook.Plain
            );
            return BattleActResult.Did();
        });

        private static readonly Dictionary<CardStatus, BattleEffect> effects = Build();

        /// <summary>The effect of <paramref name="status"/> while it is on, or null for one the battle works out itself.</summary>
        public static BattleEffect EffectOf(CardStatus status) =>
            effects.TryGetValue(status, out var effect) ? effect : null;

        private static Dictionary<CardStatus, BattleEffect> Build()
        {
            var all = new Dictionary<CardStatus, BattleEffect>();
            void Add(
                CardStatus status,
                int priority,
                System.Func<BattleEffect, BattleEffect> rules
            ) =>
                all[status] = rules(
                    new BattleEffect("status-" + status, CardRules.StatusName(status)).OfStatus(
                        status,
                        priority
                    )
                );

            // 攻撃力アップ：持ち主のカードのダメージを、付いた割合だけ掛けて上げる。
            Add(
                CardStatus.AttackUp,
                0,
                e =>
                    e.Multiply(
                            BattleValue.Damage,
                            BattleAmount.OwnerStatusAmount(CardStatus.AttackUp),
                            AttackUpOrder
                        )
                        .When(ActorIsOwner & KindIs(BattleDamageKind.Card))
            );
            // 防御ダウン：持ち主が受ける、味方のカードのダメージ+25%。味方に付いたら、敵の攻撃で受けるダメージ+25%。
            Add(
                CardStatus.Vulnerable,
                0,
                e =>
                    e.Multiply(BattleValue.Damage, VulnerablePercent, VulnerableOrder)
                        .When(TargetIsOwner & KindIs(BattleDamageKind.Card))
                        .Multiply(BattleValue.DamageTaken, VulnerablePercent)
                        .When(TargetIsOwner & TargetIsAlly & KindIs(BattleDamageKind.Enemy))
            );
            // 麻痺：味方に付いたら、そのカードのダメージ-25%（敵の麻痺は、敵の攻撃の計算で弱める）。
            Add(
                CardStatus.Paralysis,
                0,
                e =>
                    e.Multiply(BattleValue.Damage, -ParalysisPercent, ParalysisOrder)
                        .When(ActorIsOwner & ActorIsAlly & KindIs(BattleDamageKind.Card))
            );
            // プロテクト：持ち主が受けるダメージ-25%。敵は弱点のあと、味方は防御の前に掛ける。
            Add(
                CardStatus.Protect,
                0,
                e =>
                    e.Multiply(BattleValue.DamageTaken, -ProtectPercent)
                        .When(TargetIsOwner & TargetIsEnemy)
                        .Multiply(BattleValue.Incoming, -ProtectPercent)
                        .When(TargetIsOwner & TargetIsAlly)
            );
            // 無敵：持ち主への敵の攻撃を止める。反射より先に働く。
            Add(
                CardStatus.Invincible,
                -1,
                e => e.On(BattleTrigger.BlowLanding, BattleAct.Cancel).When(TargetIsOwner)
            );
            // 反射：持ち主への敵の攻撃を1回、攻撃した敵へはね返す。敵に付いたら、味方の単体攻撃のカードを使った味方へ返す。
            Add(
                CardStatus.Reflect,
                0,
                e =>
                    e.On(BattleTrigger.BlowLanding, Reflect, BattleAct.Cancel)
                        .When(TargetIsOwner & ActorIsEnemy)
                        .On(BattleTrigger.BlowLanding, Reflect, BattleAct.Cancel)
                        .When(TargetIsOwner & TargetIsEnemy & ActorIsAlly & CardOnOneEnemy)
            );
            // 呪い：持ち主が得るブロックを半分にする。
            Add(CardStatus.Curse, 0, e => e.Multiply(BattleValue.Block, -50).When(TargetIsOwner));
            return all;
        }
    }
}
