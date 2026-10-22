using System.Collections.Generic;
using Baryonyx.Adventure;
using Baryonyx.SkillEvolution;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 冒険の戦闘での固有スキルの進化（doc/features/skill-evolution.md）：部屋の種類ごとの確率と、部屋を終えたときに残すこと。
    public sealed class AdventureEvolutionTests
    {
        [SetUp]
        [TearDown]
        public void Forget() => UniqueSkillSession.Reset();

        [Test]
        public void TheBaseChanceRisesWithTheRoomsEnemies()
        {
            Assert.That(AdventureEvolution.BasePercentOf(AdventureRoomKind.Battle), Is.EqualTo(4f));
            Assert.That(AdventureEvolution.BasePercentOf(AdventureRoomKind.Elite), Is.EqualTo(8f));
            Assert.That(AdventureEvolution.BasePercentOf(AdventureRoomKind.Trial), Is.EqualTo(8f));
            Assert.That(AdventureEvolution.BasePercentOf(AdventureRoomKind.Boss), Is.EqualTo(10f));
            Assert.That(AdventureEvolution.BasePercentOf(AdventureRoomKind.Treasure), Is.Zero);
            var rules = new AdventureEvolution().Rules(AdventureRoomKind.Boss, 10);
            Assert.That(rules.Chance(10), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(rules.Chance(12), Is.EqualTo(0.08f).Within(1e-6f));
        }

        [Test]
        public void TheRoomsEvolutionsAreKeptOnlyWhenTheRoomEnds()
        {
            var saved = new List<AdventureEvolution.Step>();
            var evolution = new AdventureEvolution(saved.Add);
            var rules = evolution.Rules(AdventureRoomKind.Battle, 10);
            UniqueSkillSession.ProgressOf("toma", 0, null);
            rules.Evolved("toma", 0, 5);
            Assert.That(evolution.Pending, Has.Count.EqualTo(1));
            Assert.That(
                UniqueSkillSession.ProgressOf("toma", 0, null).SetStage,
                Is.Zero,
                "Not kept yet."
            );
            Assert.That(rules.Acquired("toma", 0, 5), Is.False);

            evolution.Keep();
            Assert.That(evolution.Pending, Is.Empty);
            Assert.That(UniqueSkillSession.ProgressOf("toma", 0, null).SetStage, Is.EqualTo(5));
            Assert.That(rules.Acquired("toma", 0, 5), Is.True);
            Assert.That(saved, Has.Count.EqualTo(1));
            Assert.That(saved[0].Person, Is.EqualTo("toma"));
            Assert.That(saved[0].Unique, Is.Zero);
            Assert.That(saved[0].Stage, Is.EqualTo(5));
            evolution.Keep();
            Assert.That(saved, Has.Count.EqualTo(1), "Kept once.");
        }
    }
}
