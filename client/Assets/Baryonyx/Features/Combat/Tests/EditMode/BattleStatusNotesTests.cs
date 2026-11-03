using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    /// <summary>The scattered numbers of blows one after another, and the status notes after them.</summary>
    public sealed class BattleStatusNotesTests
    {
        private GameObject enemy;
        private GameObject ally;

        [SetUp]
        public void SetUp()
        {
            enemy = new GameObject("Enemy");
            ally = new GameObject("Ally");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(ally);
        }

        [Test]
        public void TheNumbersOfBlowsOneAfterAnotherGoAroundTheFirst()
        {
            var scatter = new BattleNumberScatter();

            Assert.That(
                scatter.Place(enemy, 0f, 1f),
                Is.EqualTo(Vector2.zero),
                "The first starts in the middle."
            );
            var second = scatter.Place(enemy, 0.14f, 1f);
            var third = scatter.Place(enemy, 0.28f, 1f);
            var fourth = scatter.Place(enemy, 0.42f, 1f);
            Assert.That(second.x, Is.GreaterThan(50f), "Upper right.");
            Assert.That(second.y, Is.GreaterThan(0f));
            Assert.That(third.x, Is.LessThan(-50f), "Lower left.");
            Assert.That(third.y, Is.LessThan(0f));
            // HPバーの下で上へ行けなくても、横か下にずれて前の数字に重ならない。
            Assert.That(fourth.y, Is.LessThan(-30f), "Under the first, clear of the second.");
            Assert.That(
                scatter.Place(ally, 0.3f, 1f),
                Is.EqualTo(Vector2.zero),
                "Each character has its own."
            );
        }

        [Test]
        public void TheNumbersStartInTheMiddleAgainOnceTheOthersHaveGone()
        {
            var scatter = new BattleNumberScatter();
            scatter.Place(enemy, 0f, 1f);
            scatter.Place(enemy, 0.2f, 1f);

            Assert.That(scatter.Place(enemy, 1.5f, 1f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ManyNumbersStayNearTheirStart()
        {
            var scatter = new BattleNumberScatter();
            float farthest = 0f;
            for (int i = 0; i < 9; i++)
                farthest = Mathf.Max(farthest, scatter.Place(enemy, i * 0.1f, 2f).magnitude);

            Assert.That(farthest, Is.LessThan(100f));
        }

        [Test]
        public void TheNotesComeAfterTheNumberOneGapApart()
        {
            var notes = new BattleStatusNotes();

            Assert.That(notes.Start(enemy, 1f), Is.EqualTo(1f + BattleStatusNotes.Lead), "出血");
            Assert.That(
                notes.Start(enemy, 1f),
                Is.EqualTo(1f + BattleStatusNotes.Lead + BattleStatusNotes.Gap),
                "毒"
            );
            Assert.That(
                notes.Start(ally, 1f),
                Is.EqualTo(1f + BattleStatusNotes.Lead),
                "Each character has its own."
            );
            Assert.That(
                notes.Start(enemy, 5f),
                Is.EqualTo(5f + BattleStatusNotes.Lead),
                "Long after."
            );
        }

        [Test]
        public void ANoteGlidesUpAndToTheRightAtOneSpeed()
        {
            var early = BattleStatusNotes.Offset(0.1f);
            var later = BattleStatusNotes.Offset(0.3f) - BattleStatusNotes.Offset(0.2f);

            Assert.That(early.y, Is.EqualTo(later.y).Within(0.01f), "The same speed all the way.");
            Assert.That(early.x, Is.EqualTo(early.y * BattleStatusNotes.Lean).Within(0.01f));
            Assert.That(
                BattleStatusNotes.Gap * BattleStatusNotes.Speed,
                Is.GreaterThanOrEqualTo(36f),
                "One note has risen past the height of a note before the next comes out."
            );
        }

        [Test]
        public void ANoteFadesInAndOut()
        {
            Assert.That(BattleStatusNotes.Alpha(0f), Is.EqualTo(0f));
            Assert.That(
                BattleStatusNotes.Alpha(BattleStatusNotes.FadeIn * 0.5f),
                Is.EqualTo(0.5f).Within(0.01f)
            );
            Assert.That(BattleStatusNotes.Alpha(BattleStatusNotes.Life * 0.5f), Is.EqualTo(1f));
            Assert.That(BattleStatusNotes.Alpha(BattleStatusNotes.Life), Is.EqualTo(0f));
        }
    }
}
