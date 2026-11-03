using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattleCardPlanTests
    {
        // 「[弱点]を突くとエネルギー+1」は、突いた当たりで確かめる。使い手への別の当たりにしない。
        [TestCase("VitalThrust")]
        [TestCase("SerpentFang")]
        public void AnEffectOnAWeaknessRidesTheBlowThatHitsIt(string id)
        {
            var card = CardSkills.Find(id);
            var plan = BattleCardPlan.Plan(
                card,
                new[] { 0, 1 },
                new[] { 0, 1, 2, 3 },
                1,
                true,
                3,
                _ => 0
            );

            Assert.That(plan.Any(hit => hit.Ally), Is.False);
            var energy = plan.Single(hit =>
                hit.Actions.Any(action => action.Kind == CardActionKind.Energy)
            );
            Assert.That(energy, Is.SameAs(plan[^1]));
            Assert.That(
                energy.Actions.Any(action => action.Kind == CardActionKind.Damage),
                Is.True
            );
        }

        // 条件のない働き（マナの祈りのエネルギー+2など）は、これまでどおり使い手への当たりにする。
        [Test]
        public void AnEffectWithoutAConditionStaysOnTheUser()
        {
            var plan = BattleCardPlan.Plan(
                CardSkills.Find("ManaPrayer"),
                new[] { 0 },
                new[] { 0, 1, 2, 3 },
                -1,
                false,
                2,
                _ => 0
            );

            Assert.That(plan.Single().Ally, Is.True);
            Assert.That(plan.Single().Index, Is.EqualTo(2));
        }

        // 順に当たる連撃は、最後の1発だけが「最後の1発」になる。
        [Test]
        public void OnlyTheLastBlowOfSeveralFinishes()
        {
            var plan = BattleCardPlan.Plan(
                CardSkills.Find("Icicles"),
                new[] { 0, 1 },
                new[] { 0, 1, 2, 3 },
                -1,
                true,
                1,
                _ => 0
            );

            Assert.That(plan, Has.Count.EqualTo(4));
            Assert.That(
                Enumerable
                    .Range(0, plan.Count)
                    .Where(i => BattleCardPlan.Finishes(plan, i))
                    .ToArray(),
                Is.EqualTo(new[] { 3 })
            );
        }

        // 1体に2回当てる連撃も、2発目が最後の1発になる。
        [Test]
        public void TheSecondOfTwoBlowsOnOneEnemyFinishes()
        {
            var plan = BattleCardPlan.Plan(
                CardSkills.Find("SerpentFang"),
                new[] { 0, 1 },
                new[] { 0, 1, 2, 3 },
                1,
                true,
                2,
                _ => 0
            );

            Assert.That(
                Enumerable
                    .Range(0, plan.Count)
                    .Where(i => BattleCardPlan.Finishes(plan, i))
                    .ToArray(),
                Is.EqualTo(new[] { plan.Count - 1 })
            );
        }

        // 全体に1度だけ当てるカードには、最後の1発がない。
        [Test]
        public void ABlowOnAllAtOnceHasNoFinish()
        {
            var plan = BattleCardPlan.Plan(
                CardSkills.Find("Blizzard"),
                new[] { 0, 1, 2 },
                new[] { 0, 1, 2, 3 },
                -1,
                true,
                1,
                _ => 0
            );

            Assert.That(plan.Count, Is.GreaterThan(1));
            Assert.That(
                Enumerable.Range(0, plan.Count).Any(i => BattleCardPlan.Finishes(plan, i)),
                Is.False
            );
        }
    }
}
