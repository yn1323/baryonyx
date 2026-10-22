using System.Linq;
using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class CombatEnemyBehaviorsTests
    {
        [Test]
        public void EveryKindOfEnemyHasItsSkillsOnce()
        {
            var ids = CombatEnemyBehaviors.All.Select(behavior => behavior.Id).ToArray();
            Assert.That(ids, Is.Unique);
            Assert.That(ids, Is.EquivalentTo(CombatEnemies.All.Select(enemy => enemy.Id)));
        }

        [Test]
        public void EverySkillAPatternNamesIsOneOfItsOwn()
        {
            foreach (var behavior in CombatEnemyBehaviors.All)
            {
                var names = behavior.Moves.Select(move => move.Name).ToArray();
                Assert.That(names, Is.Unique, behavior.Id);
                foreach (string name in behavior.Named())
                    Assert.That(names, Has.Member(name), $"{behavior.Id}: {name}");
            }
        }

        [Test]
        public void EveryEnemyHasABlowTheBattleCanDo()
        {
            foreach (var behavior in CombatEnemyBehaviors.All)
                Assert.That(
                    behavior.Moves.Any(move => move.Supported && move.Strikes),
                    behavior.Id
                );
        }

        [Test]
        public void TheSkillsHaveSensibleNumbers()
        {
            foreach (var behavior in CombatEnemyBehaviors.All)
            foreach (var move in behavior.Moves)
            {
                string at = $"{behavior.Id}: {move.Name}";
                Assert.That(move.Hits, Is.GreaterThanOrEqualTo(1), at);
                Assert.That(move.Percent, Is.InRange(0, 400), at);
                Assert.That(move.ChargeTurns, Is.InRange(0, 3), at);
                if (move.Strikes)
                    Assert.That(
                        new[]
                        {
                            CardStat.PhysicalAttack,
                            CardStat.MagicAttack,
                            CardStat.PhysicalDefense,
                        },
                        Has.Member(move.Stat),
                        at
                    );
                // The skill that starts the second part may do nothing more than start it.
                if (move.Supported && move.Name != behavior.LateMove)
                    Assert.That(
                        move.Strikes
                            || move.BlockPercent > 0
                            || move.HealPercent > 0
                            || move.RegenTurns > 0
                            || move.AttackUpTurns > 0
                            || move.ProtectTurns > 0
                            || move.PowerUpPercent > 0,
                        Is.True,
                        $"{at} does something"
                    );
            }
        }

        [Test]
        public void ABrainOfEveryEnemyPicksASkillForManyTurns()
        {
            foreach (var behavior in CombatEnemyBehaviors.All)
            {
                var brain = new EnemyBrain(behavior, n => n / 2);
                for (int turn = 1; turn <= 20; turn++)
                {
                    float hp = 1f - turn * 0.04f;
                    var step = brain.Next(
                        new EnemySituation(turn, hp, turn < 10, 0.4f, 0.6f, turn % 3 == 0)
                    );
                    Assert.That(step.Move, Is.Not.Null, $"{behavior.Id} on turn {turn}");
                    Assert.That(step.Move.Supported, Is.True, $"{behavior.Id} on turn {turn}");
                }
            }
        }
    }
}
