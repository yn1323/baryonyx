using Baryonyx.SkillEvolution;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 固有スキルの進化先と、戦闘で進化する確率（doc/features/skill-evolution.md#確率の仮設定）。
    public sealed class UniqueSkillEvolutionTests
    {
        [Test]
        public void AStageEvolvesIntoItsChildrenInTheTree()
        {
            Assert.That(
                UniqueSkillEvolution.NextStages("ManaBurst"),
                Is.EqualTo(new[] { 1, 3, 5 })
            );
            Assert.That(UniqueSkillEvolution.NextStages("ManaBurst.3"), Is.EqualTo(new[] { 4 }));
            Assert.That(UniqueSkillEvolution.NextStages("ManaBurst.4"), Is.Empty);
            Assert.That(UniqueSkillEvolution.NextStages("Slash"), Is.Empty, "A card skill.");
            Assert.That(UniqueSkillEvolution.NextStages(null), Is.Empty);
            Assert.That(UniqueSkillEvolution.TryFind("StarFall.2", out var tree, out int stage));
            Assert.That(tree.Id, Is.EqualTo("StarFall"));
            Assert.That(stage, Is.EqualTo(2));
        }

        [Test]
        public void TheChanceFallsByATenthOfTheBaseForEachLevelOverTheRecommendedDownToAFifth()
        {
            Assert.That(UniqueSkillEvolution.Chance(10f, 10, 10), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(
                UniqueSkillEvolution.Chance(10f, 4, 10),
                Is.EqualTo(0.1f).Within(1e-6f),
                "Under it, all of it."
            );
            Assert.That(UniqueSkillEvolution.Chance(10f, 13, 10), Is.EqualTo(0.07f).Within(1e-6f));
            Assert.That(
                UniqueSkillEvolution.Chance(4f, 18, 10),
                Is.EqualTo(0.008f).Within(1e-6f),
                "A fifth at 8 over."
            );
            Assert.That(
                UniqueSkillEvolution.Chance(4f, 40, 10),
                Is.EqualTo(0.008f).Within(1e-6f),
                "Never under a fifth."
            );
            Assert.That(UniqueSkillEvolution.Chance(0f, 10, 10), Is.Zero);
        }

        [Test]
        public void AnEvolutionAcquiresAndSetsTheStage()
        {
            UniqueSkillSession.Reset();
            try
            {
                UniqueSkillSession.Evolve("toma", 0, 3, null);
                var progress = UniqueSkillSession.ProgressOf("toma", 0, null);
                Assert.That(progress.SetStage, Is.EqualTo(3));
                Assert.That(progress.Has(3), Is.True);
                UniqueSkillSession.SetStage("toma", 0, 0, null);
                UniqueSkillSession.Evolve("toma", 0, 1, null);
                Assert.That(progress.Acquired, Is.EqualTo(new[] { 1, 3 }));
                Assert.That(progress.SetStage, Is.EqualTo(1));
            }
            finally
            {
                UniqueSkillSession.Reset();
            }
        }
    }
}
