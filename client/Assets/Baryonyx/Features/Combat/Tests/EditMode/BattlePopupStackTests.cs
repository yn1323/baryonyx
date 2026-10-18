using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattlePopupStackTests
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
        public void NamesPutOnTogetherLineUpUnderOneAnother()
        {
            var stack = new BattlePopupStack();

            Assert.That(stack.Place(enemy, 46f, 0f, 1.3f), Is.EqualTo(0f), "出血");
            Assert.That(stack.Place(enemy, 46f, 0f, 1.3f), Is.EqualTo(46f), "毒");
            Assert.That(stack.Place(enemy, 46f, 0f, 1.3f), Is.EqualTo(92f), "防御ダウン");
        }

        [Test]
        public void EachCharacterHasItsOwnLines()
        {
            var stack = new BattlePopupStack();
            stack.Place(enemy, 46f, 0f, 1.3f);

            Assert.That(stack.Place(ally, 46f, 0f, 1.3f), Is.EqualTo(0f));
        }

        [Test]
        public void APopupGoesUnderTheOnesStillShowingAndStartsAtTheTopOnceTheyHaveGone()
        {
            var stack = new BattlePopupStack();
            stack.Place(enemy, 46f, 0f, 1.3f);

            Assert.That(
                stack.Place(enemy, 50f, 1f, 1.3f),
                Is.EqualTo(46f),
                "The first still shows."
            );
            Assert.That(stack.Place(enemy, 46f, 1.5f, 1.3f), Is.EqualTo(96f), "under the second");
            Assert.That(stack.Place(enemy, 46f, 3f, 1.3f), Is.EqualTo(0f), "All have gone.");
        }
    }
}
