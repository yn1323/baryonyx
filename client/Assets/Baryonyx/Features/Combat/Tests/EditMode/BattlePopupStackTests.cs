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
                Is.EqualTo(48f),
                "The first still shows."
            );
            Assert.That(stack.Place(enemy, 46f, 1.5f, 1.3f), Is.EqualTo(96f), "under the second");
            Assert.That(stack.Place(enemy, 46f, 3f, 1.3f), Is.EqualTo(0f), "All have gone.");
        }

        [Test]
        public void ANameGoesUnderTheDamageNumberShownWithIt()
        {
            var stack = new BattlePopupStack();
            // 数字は頭の上（y=100）で止まり36上がる。状態の名前は胴の上（y=100）で止まり90上がる。
            Assert.That(stack.Place(enemy, 100f, 46f, 36f, 0f, 1.3f), Is.EqualTo(0f), "number");
            Assert.That(
                stack.Place(enemy, 100f, 46f, 90f, 0f, 1.3f),
                Is.EqualTo(46f),
                "A name rising further stays under the number on its way up."
            );
        }

        [Test]
        public void ANumberAfterANameRisingFurtherGoesLowerStillUntilTheNameHasStopped()
        {
            var stack = new BattlePopupStack();
            stack.Place(enemy, 100f, 46f, 90f, 0f, 1.3f);

            // 名前のほうが54多く上がるため、上がっている間は数字が追いつかないよう54さらに下げる。
            Assert.That(stack.Place(enemy, 100f, 46f, 36f, 0f, 1.3f), Is.EqualTo(100f));

            var later = new BattlePopupStack();
            later.Place(enemy, 100f, 46f, 90f, 0f, 1.3f);
            Assert.That(
                later.Place(enemy, 100f, 46f, 36f, 0.6f, 1.3f),
                Is.EqualTo(46f),
                "The name has stopped."
            );
        }

        [Test]
        public void APopupThatStopsLowerThanTheOthersStaysWhereItIs()
        {
            var stack = new BattlePopupStack();
            stack.Place(enemy, 200f, 46f, 36f, 0f, 1.3f);

            Assert.That(stack.Place(enemy, 100f, 46f, 90f, 0f, 1.3f), Is.EqualTo(0f));
        }
    }
}
