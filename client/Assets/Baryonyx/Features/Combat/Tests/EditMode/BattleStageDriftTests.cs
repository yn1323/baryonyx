using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattleStageDriftTests
    {
        [Test]
        public void TheStageGoesOnceRoundACircleEachPeriod()
        {
            AssertNear(BattleStageDrift.Offset(0f, 12f, 40f), new Vector2(12f, 0f));
            AssertNear(BattleStageDrift.Offset(10f, 12f, 40f), new Vector2(0f, 12f));
            AssertNear(BattleStageDrift.Offset(20f, 12f, 40f), new Vector2(-12f, 0f));
            AssertNear(BattleStageDrift.Offset(30f, 12f, 40f), new Vector2(0f, -12f));
            AssertNear(
                BattleStageDrift.Offset(40f, 12f, 40f),
                BattleStageDrift.Offset(0f, 12f, 40f)
            );
            for (float t = 0f; t < 40f; t += 0.37f)
                Assert.That(
                    BattleStageDrift.Offset(t, 12f, 40f).magnitude,
                    Is.EqualTo(12f).Within(0.001f),
                    "The centre of the view keeps on the circle."
                );
        }

        [Test]
        public void TheStageGlidesByFractionsOfAPixel()
        {
            // One frame at 60 fps moves it far less than a pixel, and never jumps a whole one.
            for (float t = 0f; t < 40f; t += 0.37f)
            {
                float step = Vector2.Distance(
                    BattleStageDrift.Offset(t, 12f, 40f),
                    BattleStageDrift.Offset(t + 1f / 60f, 12f, 40f)
                );
                Assert.That(step, Is.GreaterThan(0f).And.LessThan(0.1f));
            }
        }

        [Test]
        public void ANoughtRadiusHoldsTheStageStill()
        {
            Assert.That(BattleStageDrift.Offset(5f, 0f, 40f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TheDefaultCircleIsSlowAndStaysInsideTheRoomLaidForIt()
        {
            var drift = new GameObject(
                "Drift",
                typeof(RectTransform)
            ).AddComponent<BattleStageDrift>();
            try
            {
                float speed = 2f * Mathf.PI * drift.Radius / drift.Period;
                Assert.That(drift.Radius, Is.GreaterThan(0f));
                Assert.That(drift.Radius, Is.LessThanOrEqualTo(BattleStageDrift.MaxRadius));
                Assert.That(speed, Is.LessThan(2.5f), "Slower than 2.5 px a second.");
            }
            finally
            {
                Object.DestroyImmediate(drift.gameObject);
            }
        }

        private static void AssertNear(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        }
    }
}
