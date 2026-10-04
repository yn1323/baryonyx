using System;
using System.Collections.Generic;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Who each action of a card skill lands on, in waves, for the battle mock and the effects'
    /// preview: each round of damage is a wave, and an action on the same characters as the one
    /// before lands with it (a debuff with its blow), while one on others is a new wave (the
    /// party's block after a bash).
    /// </summary>
    public static class BattleCardPlan
    {
        /// <summary>One landing: on an ally or an enemy (by index), in a wave, with its actions.</summary>
        public sealed class Hit
        {
            public bool Ally;
            public int Index;
            public int Wave;
            public readonly List<CardAction> Actions = new();
        }

        /// <summary>
        /// Plans a card played on <paramref name="picked"/> (an enemy when
        /// <paramref name="onEnemies"/>, else an ally; -1 for none) by <paramref name="user"/>,
        /// among the enemies still standing and the allies standing. <paramref name="random"/>
        /// picks a number below the one given.
        /// </summary>
        public static List<Hit> Plan(
            CardSkill skill,
            IReadOnlyList<int> enemies,
            IReadOnlyList<int> allies,
            int picked,
            bool onEnemies,
            int user,
            Func<int, int> random
        )
        {
            var plan = new List<Hit>();
            int wave = -1;
            List<Hit> last = null;
            foreach (var action in skill.Actions)
            {
                var rounds = TargetsOf(action, enemies, allies, picked, onEnemies, user, random);
                if (rounds.Count == 0)
                    continue;
                if (action.Kind != CardActionKind.Damage && last != null && Same(last, rounds[0]))
                {
                    foreach (var hit in last)
                        hit.Actions.Add(action);
                    continue;
                }
                foreach (var round in rounds)
                {
                    wave++;
                    last = new List<Hit>();
                    foreach (var (ally, index) in round)
                    {
                        var hit = new Hit
                        {
                            Ally = ally,
                            Index = index,
                            Wave = wave,
                        };
                        hit.Actions.Add(action);
                        plan.Add(hit);
                        last.Add(hit);
                    }
                }
            }
            return plan;
        }

        private static bool Same(List<Hit> hits, List<(bool ally, int index)> targets)
        {
            if (hits.Count != targets.Count)
                return false;
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].Ally != targets[i].ally || hits[i].Index != targets[i].index)
                    return false;
            return true;
        }

        /// <summary>The characters an action reaches, as rounds (one for each hit of damage on a side).</summary>
        private static List<List<(bool ally, int index)>> TargetsOf(
            CardAction action,
            IReadOnlyList<int> enemies,
            IReadOnlyList<int> allies,
            int picked,
            bool onEnemies,
            int user,
            Func<int, int> random
        )
        {
            var rounds = new List<List<(bool, int)>>();
            int hits = Math.Max(1, action.Hits);
            int rounds1 = action.Kind == CardActionKind.Damage ? hits : 1;
            switch (action.Target)
            {
                case CardTarget.OneEnemy:
                {
                    int enemy =
                        onEnemies && Contains(enemies, picked) ? picked
                        : enemies.Count > 0 ? enemies[random(enemies.Count)]
                        : -1;
                    if (enemy < 0)
                        break;
                    for (int h = 0; h < rounds1; h++)
                        rounds.Add(new List<(bool, int)> { (false, enemy) });
                    break;
                }
                case CardTarget.AllEnemies:
                    if (enemies.Count == 0)
                        break;
                    for (int h = 0; h < rounds1; h++)
                    {
                        var round = new List<(bool, int)>();
                        foreach (int enemy in enemies)
                            round.Add((false, enemy));
                        rounds.Add(round);
                    }
                    break;
                case CardTarget.RandomEnemies:
                {
                    if (enemies.Count == 0)
                        break;
                    var round = new List<(bool, int)>();
                    for (int h = 0; h < hits; h++)
                        round.Add((false, enemies[random(enemies.Count)]));
                    rounds.Add(round);
                    break;
                }
                case CardTarget.ChainEnemies:
                {
                    if (enemies.Count == 0)
                        break;
                    int first = onEnemies ? IndexOf(enemies, picked) : -1;
                    if (first < 0)
                        first = random(enemies.Count);
                    var round = new List<(bool, int)>();
                    foreach (int at in CardRules.Chain(first, enemies.Count, hits, random))
                        round.Add((false, enemies[at]));
                    rounds.Add(round);
                    break;
                }
                case CardTarget.OneAlly:
                    rounds.Add(
                        new List<(bool, int)> { (true, !onEnemies && picked >= 0 ? picked : user) }
                    );
                    break;
                case CardTarget.AllAllies:
                {
                    var round = new List<(bool, int)>();
                    foreach (int ally in allies)
                        round.Add((true, ally));
                    if (round.Count > 0)
                        rounds.Add(round);
                    break;
                }
                default:
                    // On the user: itself, or what works on no one (the hand, the energy, a cloud).
                    rounds.Add(new List<(bool, int)> { (true, user) });
                    break;
            }
            return rounds;
        }

        private static bool Contains(IReadOnlyList<int> list, int value) =>
            IndexOf(list, value) >= 0;

        private static int IndexOf(IReadOnlyList<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == value)
                    return i;
            return -1;
        }
    }
}
