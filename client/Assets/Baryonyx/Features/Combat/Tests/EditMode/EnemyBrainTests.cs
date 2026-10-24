using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using NUnit.Framework;
using static Baryonyx.Combat.CardStat;
using static Baryonyx.Combat.EnemyCue;
using static Baryonyx.Combat.EnemyMove;
using static Baryonyx.Combat.EnemyPattern;

namespace Baryonyx.Tests.EditMode
{
    public sealed class EnemyBrainTests
    {
        private static EnemySituation At(
            int turn,
            float hp = 1f,
            bool others = true,
            float othersLowest = 1f,
            float partyLowest = 1f,
            bool partyGuarded = false
        ) => new(turn, hp, others, othersLowest, partyLowest, partyGuarded);

        /// <summary>The skills it uses over turns 1 to <paramref name="turns"/>, gathering shown as "…".</summary>
        private static List<string> Run(EnemyBrain brain, int turns, float hp = 1f)
        {
            var used = new List<string>();
            for (int turn = 1; turn <= turns; turn++)
            {
                var step = brain.Next(At(turn, hp));
                used.Add(step.Charging ? step.Move.Name + "…" : step.Move?.Name);
            }
            return used;
        }

        [Test]
        public void ItOpensWithItsOpeningThenGoesRoundItsOrder()
        {
            var brain = new EnemyBrain(CombatEnemyBehaviors.Find("forest-guardian"), _ => 0);
            Assert.That(
                Run(brain, 5),
                Is.EqualTo(new[] { "大地の拳", "身を固める", "根縛り", "地響き", "大地の拳" }),
                "The order begins with the opening, so it goes on from its second skill."
            );
        }

        [Test]
        public void ASkillTheBattleCannotDoIsPassedOver()
        {
            // 胞子ばらまき (poison on the party) is never chosen, so the slime keeps to its tackle.
            var brain = new EnemyBrain(CombatEnemyBehaviors.Find("moss-slime"), n => n - 1);
            Assert.That(Run(brain, 4), Has.All.EqualTo("たいあたり"));
        }

        [Test]
        public void AWeightedChoiceKeepsItsRuns()
        {
            var behavior = new EnemyBehavior
            {
                Id = "test",
                Moves = new[] { Attack("a", PhysicalAttack, 100), Attack("b", PhysicalAttack, 60) },
                Early = new EnemyPattern
                {
                    Choices = new[] { Weight("a", 90), Weight("b", 10) },
                    Once = new[] { "b" },
                },
            };
            // Always the first choice open: a, a, then b (a may not come three times), then a.
            var brain = new EnemyBrain(behavior, _ => 0);
            Assert.That(Run(brain, 5), Is.EqualTo(new[] { "a", "a", "b", "a", "a" }));
        }

        [Test]
        public void ASkillThatLandsNoBlowKeepsLessOfItsWeight()
        {
            var behavior = new EnemyBehavior
            {
                Id = "test",
                Moves = new[] { Attack("a", PhysicalAttack, 100), Guard("g", 100) },
                Early = new EnemyPattern { Choices = new[] { Weight("a", 50), Weight("g", 50) } },
            };
            var brain = new EnemyBrain(behavior, _ => 0);
            Assert.That(brain.WeightOf(new EnemyChoice("a", 50)), Is.EqualTo(50));
            Assert.That(
                brain.WeightOf(new EnemyChoice("g", 50)),
                Is.EqualTo(50 * EnemyBrain.QuietWeightPercent / 100)
            );
            // 抽選の合計も、減らした重みで数える。
            int total = 0;
            Run(
                new EnemyBrain(
                    behavior,
                    n =>
                    {
                        total = n;
                        return 0;
                    }
                ),
                1
            );
            Assert.That(total, Is.EqualTo(50 + 50 * EnemyBrain.QuietWeightPercent / 100));
        }

        [Test]
        public void ItsHpRunningLowCallsForItsRuleOnceAndThenWhatFollows()
        {
            var behavior = new EnemyBehavior
            {
                Id = "test",
                Moves = new[]
                {
                    Attack("bite", PhysicalAttack, 100),
                    Focus("gather"),
                    Mend("lick", 100),
                },
                Early = new EnemyPattern
                {
                    Choices = new[] { Weight("bite", 1) },
                    Rules = new[] { EnemyRule.When(Hurt, "gather").Times(1).Next("bite") },
                },
            };
            var brain = new EnemyBrain(behavior, _ => 0);
            Assert.That(brain.Next(At(1)).Move.Name, Is.EqualTo("bite"));
            Assert.That(brain.Next(At(2, hp: 0.4f)).Move.Name, Is.EqualTo("gather"));
            Assert.That(brain.Next(At(3, hp: 0.4f)).Move.Name, Is.EqualTo("bite"));
            Assert.That(brain.Next(At(4, hp: 0.3f)).Move.Name, Is.EqualTo("bite"), "Only once.");
        }

        [Test]
        public void ARuleWaitsTheTurnsItIsGivenBetweenUses()
        {
            var brain = new EnemyBrain(CombatEnemyBehaviors.Find("moss-wolf"), _ => 0);
            var used = new List<string>();
            for (int turn = 1; turn <= 5; turn++)
                used.Add(brain.Next(At(turn)).Move.Name);
            // 遠吠え opens, and comes again three turns after.
            Assert.That(used[0], Is.EqualTo("遠吠え"));
            Assert.That(used.GetRange(1, 2), Has.No.Member("遠吠え"));
            Assert.That(used[3], Is.EqualTo("遠吠え"));
        }

        [Test]
        public void ARuleLooksAtTheParty()
        {
            var brain = new EnemyBrain(CombatEnemyBehaviors.Find("moss-wolf"), _ => 0);
            brain.Next(At(1));
            Assert.That(brain.Next(At(2, partyLowest: 0.3f)).Move.Name, Is.EqualTo("喉笛狙い"));
            Assert.That(
                brain.Next(At(3, partyLowest: 0.3f)).Move.Name,
                Is.Not.EqualTo("喉笛狙い"),
                "Not two turns in a row."
            );
        }

        [Test]
        public void ABigSkillIsGatheredForATurnBeforeItLands()
        {
            var behavior = new EnemyBehavior
            {
                Id = "test",
                Moves = new[]
                {
                    Attack("fist", PhysicalAttack, 100),
                    Attack("hammer", PhysicalAttack, 140, EnemyAim.All).Charged(1),
                    Attack("chant", MagicAttack, 150, EnemyAim.All).Charged(2),
                },
                Early = new EnemyPattern { Order = new[] { "fist", "hammer", "chant" } },
            };
            var brain = new EnemyBrain(behavior, _ => 0);
            Assert.That(
                Run(brain, 7),
                Is.EqualTo(
                    new[] { "fist", "hammer…", "hammer", "chant…", "chant…", "chant", "fist" }
                )
            );
        }

        [Test]
        public void TheGatheringStepTellsTheTurnsLeft()
        {
            var behavior = new EnemyBehavior
            {
                Id = "test",
                Moves = new[] { Attack("chant", MagicAttack, 150, EnemyAim.All).Charged(2) },
                Early = new EnemyPattern { Order = new[] { "chant" } },
            };
            var brain = new EnemyBrain(behavior, _ => 0);
            Assert.That(brain.Next(At(1)).ChargeLeft, Is.EqualTo(2));
            Assert.That(brain.Next(At(2)).ChargeLeft, Is.EqualTo(1));
            var lands = brain.Next(At(3));
            Assert.That(lands.Charging, Is.False);
            Assert.That(lands.Move.Name, Is.EqualTo("chant"));
        }

        [Test]
        public void TheSecondPartStartsOnItsTurnWithItsSkillAndThenItsCycle()
        {
            var brain = new EnemyBrain(CombatEnemyBehaviors.Find("forest-guardian"), _ => 0);
            Run(brain, 5);
            var awakening = brain.Next(At(6));
            Assert.That(awakening.Awakens, Is.True);
            Assert.That(awakening.Move.Name, Is.EqualTo("刻印の目覚め"));
            Assert.That(brain.Late, Is.True);
            var next = new List<string>();
            for (int turn = 7; turn <= 10; turn++)
            {
                var step = brain.Next(At(turn));
                next.Add(step.Charging ? step.Move.Name + "…" : step.Move.Name);
            }
            // Two choices (not the same twice), then the hammer gathered and landing.
            Assert.That(next[0], Is.Not.EqualTo(next[1]));
            Assert.That(next.GetRange(2, 2), Is.EqualTo(new[] { "森羅の鉄槌…", "森羅の鉄槌" }));
        }

        [Test]
        public void TheSecondPartStartsEarlierWhenItsHpIsHalfGone()
        {
            var brain = new EnemyBrain(CombatEnemyBehaviors.Find("forest-guardian"), _ => 0);
            brain.Next(At(1));
            var step = brain.Next(At(2, hp: 0.5f));
            Assert.That(step.Awakens, Is.True);
            Assert.That(step.Move.Name, Is.EqualTo("刻印の目覚め"));
        }

        [Test]
        public void FromItsEnrageItsSkillsAreStronger()
        {
            var brain = new EnemyBrain(CombatEnemyBehaviors.Find("moss-slime"), _ => 0);
            var steps = Enumerable.Range(1, 5).Select(turn => brain.Next(At(turn))).ToList();
            Assert.That(steps.Take(4).Select(step => step.Percent), Has.All.EqualTo(100));
            Assert.That(steps[4].Enrages, Is.True);
            Assert.That(steps[4].Percent, Is.EqualTo(150));
            Assert.That(brain.Next(At(6)).Enrages, Is.False, "It enrages once.");
        }
    }
}
