namespace Baryonyx.Combat
{
    /// <summary>
    /// The battle as an enemy's blow sees it (<see cref="EnemyBlows"/>): a
    /// <see cref="ICardWorld"/> that also lands an enemy's blow on an ally. The battle screen and
    /// a battle without a screen are each one.
    /// </summary>
    public interface IFightWorld : ICardWorld
    {
        /// <summary>Takes away <paramref name="ally"/>'s block (ブロックを砕かれた).</summary>
        void BreakBlock(BattleUnit ally);

        /// <summary>
        /// Lands an enemy's blow of <paramref name="power"/> on <paramref name="target"/>: its
        /// block takes what it can unless <paramref name="pastBlock"/>, and its HP the rest.
        /// </summary>
        void LandEnemyBlow(
            BattleUnit attacker,
            BattleUnit target,
            int power,
            bool magic,
            bool pastBlock,
            bool critical,
            EnemyMove move
        );
    }

    /// <summary>
    /// How an enemy's blow lands on an ally (doc/plans/2026-11-03-battle-effect-system.md, 数を
    /// 変える順番): the effects may stop it (無敵・反射), then what comes before the ally's
    /// defense (プロテクト), the defense against its stat, a broken block, a critical hit and
    /// what the ally takes (<see cref="BattleValue.DamageTaken"/>); then the effects are told it
    /// landed (切り返し).
    /// </summary>
    public static class EnemyBlows
    {
        public static void Strike(
            IFightWorld world,
            BattleEffects effects,
            BattleUnit attacker,
            BattleUnit target,
            int power,
            bool magic,
            EnemyMove move = null
        )
        {
            var landing = effects.Fire(
                world,
                BattleTrigger.BlowLanding,
                new BattleEvent
                {
                    Actor = attacker,
                    Target = target,
                    Amount = power,
                    Kind = BattleDamageKind.Enemy,
                }
            );
            if (landing.Cancelled)
                return;
            power = effects.Modify(
                world,
                BattleValue.Incoming,
                power,
                new BattleEvent
                {
                    Actor = attacker,
                    Target = target,
                    Kind = BattleDamageKind.Enemy,
                }
            );
            // The ally's defense softens the blow: magic defense against an enemy's spell,
            // physical defense against its blow.
            if (!attacker.IsNone && move is not { IgnoreDefense: true })
                power = CombatFormula.Defend(
                    power,
                    world.Defense(target, magic ? CardStat.MagicAttack : CardStat.PhysicalAttack),
                    world.Level(attacker)
                );
            if (move is { BreakBlock: true } && world.Block(target) > 0)
                world.BreakBlock(target);
            // An enemy's blow may land as a critical hit, by its critical stat (doc/features/combat.md).
            bool critical = !attacker.IsNone && world.RollCritical(attacker);
            if (critical)
                power = CombatFormula.Critical(power);
            var blow = new BattleEvent
            {
                Actor = attacker,
                Target = target,
                Kind = BattleDamageKind.Enemy,
            };
            power = effects.Modify(world, BattleValue.DamageTaken, power, blow);
            int before = world.Hp(target);
            world.LandEnemyBlow(
                attacker,
                target,
                power,
                magic,
                move is { IgnoreBlock: true },
                critical,
                move
            );
            // 切り返しなど、攻撃を受けたことに働く効果に伝える。
            blow.Amount = before - world.Hp(target);
            if (before > 0 && !world.Stands(target))
                effects.Fire(world, BattleTrigger.AllyFell, blow);
            effects.Fire(world, BattleTrigger.TookBlow, blow);
        }
    }
}
