namespace Baryonyx.Combat
{
    /// <summary>
    /// What one hit of a card does (doc/plans/2026-11-03-battle-effect-system.md): each of its
    /// actions on the character, in order, when its condition holds. The same for the battle,
    /// its forecast and a battle without a screen, through <see cref="ICardWorld"/>. A card's
    /// blow is worked out in the order of 数を変える順番: the power, the block a bash adds,
    /// the card's own bonus, the effects (<see cref="BattleValue.Damage"/>: the passives, the
    /// items, 攻撃力アップ and 防御ダウン), the enemy's defense, and a critical hit; the world
    /// then lands it with the weakness, protection and block.
    /// </summary>
    public static class CardHits
    {
        /// <summary>
        /// Lands <paramref name="hit"/> of <paramref name="skill"/> played by the ally at
        /// <paramref name="user"/>; <paramref name="first"/> when it is the first landing of its
        /// wave. Tells how hard its blows hit.
        /// </summary>
        public static BattleBlowWeight Land(
            ICardWorld world,
            BattleEffects effects,
            BattleCardPlay play,
            CardSkill skill,
            BattleCardPlan.Hit hit,
            int user,
            bool first
        )
        {
            var weight = BattleBlowWeight.Normal;
            bool weak = false;
            var by = BattleUnit.Ally(user);
            var on = hit.Ally ? BattleUnit.Ally(hit.Index) : BattleUnit.Enemy(hit.Index);
            foreach (var action in hit.Actions)
            {
                if (
                    action.HasCondition
                    && !Holds(world, effects, action.Condition, skill, by, on, weak, play)
                )
                    continue;
                // 倒れた敵には、ダメージ（当たらずに終わる）のほかは働かない。
                if (!hit.Ally && !world.Stands(on) && action.Kind != CardActionKind.Damage)
                    continue;
                switch (action.Kind)
                {
                    case CardActionKind.Damage:
                    {
                        if (hit.Ally)
                            break;
                        weak = world.IsWeak(on, skill.Element, by);
                        var dealt = Blow(
                            world,
                            effects,
                            play,
                            skill,
                            action,
                            by,
                            on,
                            weak,
                            out bool stopped
                        );
                        if (dealt != BattleBlowWeight.Normal || weight == BattleBlowWeight.Normal)
                            weight = dealt;
                        // かわされた・はね返された当たりは、ほかの働き（状態など）も届かない。
                        if (stopped)
                            return weight;
                        break;
                    }
                    case CardActionKind.Heal:
                        world.LandCardHeal(
                            by,
                            on,
                            HealPower(
                                world,
                                effects,
                                play,
                                skill,
                                world.CardPower(action, by),
                                by,
                                on
                            )
                        );
                        break;
                    case CardActionKind.Revive:
                        world.LandCardRevive(on, action.Amount);
                        break;
                    case CardActionKind.Block:
                        world.LandCardBlock(
                            by,
                            on,
                            BlockPower(world, effects, play, skill, action, by, on),
                            action.Target == CardTarget.AllAllies,
                            first
                        );
                        break;
                    case CardActionKind.Status:
                        world.LandCardStatus(by, on, action);
                        break;
                    default:
                        world.LandCardSupport(by, on, action, first);
                        break;
                }
            }
            return weight;
        }

        /// <summary>A card's blow: its power up to the defense, a critical hit, and its landing.</summary>
        public static BattleBlowWeight Blow(
            ICardWorld world,
            BattleEffects effects,
            BattleCardPlay play,
            CardSkill skill,
            CardAction action,
            BattleUnit by,
            BattleUnit on,
            bool weak,
            out bool stopped
        )
        {
            int power = BlowPower(world, effects, play, skill, action, by, on, weak);
            // 敵の効果が当たる前に止めることがある（すり抜け・身かわし・反射）。
            stopped = effects
                .Fire(
                    world,
                    BattleTrigger.BlowLanding,
                    new BattleEvent
                    {
                        Actor = by,
                        Target = on,
                        Card = skill,
                        Action = action,
                        Element = skill.Element,
                        Kind = BattleDamageKind.Card,
                        Play = play,
                        Amount = power,
                    }
                )
                .Cancelled;
            if (stopped)
                return BattleBlowWeight.Normal;
            // 1回ごとに、使い手の会心で会心になることがある（doc/features/combat.md）。
            bool critical = world.RollCritical(by);
            if (critical)
                power = CombatFormula.Critical(power);
            return world.LandCardBlow(
                new BattleEvent
                {
                    Actor = by,
                    Target = on,
                    Card = skill,
                    Action = action,
                    Element = skill.Element,
                    Kind = BattleDamageKind.Card,
                    Play = play,
                    Weak = weak,
                },
                power,
                weak,
                critical
            );
        }

        /// <summary>
        /// A card's blow on an enemy up to the enemy's defense: the power, the block a bash adds
        /// and any amount of the action's own, the card's bonus, the effects and the defense.
        /// </summary>
        public static int BlowPower(
            ICardWorld world,
            BattleEffects effects,
            BattleCardPlay play,
            CardSkill skill,
            CardAction action,
            BattleUnit by,
            BattleUnit on,
            bool weak
        )
        {
            int power = world.CardPower(action, by);
            var at = Bound(
                world,
                effects,
                new BattleEvent
                {
                    Actor = by,
                    Target = on,
                    Weak = weak,
                    Play = play,
                }
            );
            // シールドバッシュ：使い手のブロックの割合を足す。
            if (action.BlockPercent > 0)
                power += BattleAmount.ActorBlock(action.BlockPercent).For(at);
            if (action.Plus != null)
                power += action.Plus.For(at);
            if (
                action.HasBonus
                && Holds(world, effects, action.BonusCondition, skill, by, on, weak, play)
            )
                power = CardRules.WithBonus(action, power);
            power = effects.Modify(
                world,
                BattleValue.Damage,
                power,
                new BattleEvent
                {
                    Actor = by,
                    Target = on,
                    Card = skill,
                    Action = action,
                    Element = skill.Element,
                    Kind = BattleDamageKind.Card,
                    Play = play,
                    Weak = weak,
                }
            );
            return CombatFormula.Defend(power, world.Defense(on, action.Stat), world.Level(by));
        }

        /// <summary>A card's healing, grown by the effects (歩み手の加護).</summary>
        public static int HealPower(
            IBattleWorld world,
            BattleEffects effects,
            BattleCardPlay play,
            CardSkill skill,
            int amount,
            BattleUnit by,
            BattleUnit on
        ) =>
            effects.Modify(
                world,
                BattleValue.Heal,
                amount,
                new BattleEvent
                {
                    Actor = by,
                    Target = on,
                    Card = skill,
                    Element = skill?.Element ?? CardElement.None,
                    Play = play,
                }
            );

        /// <summary>A card's block, grown by the effects.</summary>
        public static int BlockPower(
            ICardWorld world,
            BattleEffects effects,
            BattleCardPlay play,
            CardSkill skill,
            CardAction action,
            BattleUnit by,
            BattleUnit on
        ) =>
            effects.Modify(
                world,
                BattleValue.Block,
                world.CardPower(action, by),
                new BattleEvent
                {
                    Actor = by,
                    Target = on,
                    Card = skill,
                    Action = action,
                    Element = skill?.Element ?? CardElement.None,
                    Play = play,
                }
            );

        /// <summary>True when <paramref name="condition"/> holds for a card of <paramref name="by"/> landing on <paramref name="on"/>.</summary>
        public static bool Holds(
            IBattleWorld world,
            BattleEffects effects,
            BattleCondition condition,
            CardSkill skill,
            BattleUnit by,
            BattleUnit on,
            bool weak,
            BattleCardPlay play
        ) =>
            condition.Holds(
                Bound(
                    world,
                    effects,
                    new BattleEvent
                    {
                        Actor = by,
                        Target = on,
                        Card = skill,
                        Element = skill?.Element ?? CardElement.None,
                        Weak = weak,
                        Play = play,
                    }
                )
            );

        /// <summary>
        /// A blow of the party landed: its damage counts for the card it came from, and the
        /// effects are told of the defeat it caused and of the blow.
        /// </summary>
        public static void Landed(
            IBattleWorld world,
            BattleEffects effects,
            BattleEvent blow,
            int dealt,
            bool defeated
        )
        {
            blow.Amount = dealt;
            if (blow.Kind == BattleDamageKind.Card && blow.Play != null)
                blow.Play.Dealt += dealt;
            if (defeated)
                effects.Fire(world, BattleTrigger.EnemyDefeated, blow);
            effects.Fire(world, BattleTrigger.DealtDamage, blow);
        }

        private static BattleEvent Bound(
            IBattleWorld world,
            BattleEffects effects,
            BattleEvent moment
        )
        {
            moment.World = world;
            moment.Effects = effects;
            return moment;
        }
    }
}
