using Baryonyx.Vfx.Hd2d;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class Hd2dStageMathTests
    {
        [Test]
        public void TheMiddleOfTheScreenLooksStraightAhead()
        {
            var direction = Hd2dStageMath.ViewDirection(new Vector2(0.5f, 0.5f), 30f, 16f / 9f);
            Assert.That(direction, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void TheTopEdgeOfTheScreenIsHalfTheFieldOfViewUp()
        {
            var direction = Hd2dStageMath.ViewDirection(new Vector2(0.5f, 1f), 30f, 16f / 9f);
            float angle = Mathf.Atan2(direction.y, direction.z) * Mathf.Rad2Deg;
            Assert.That(angle, Is.EqualTo(15f).Within(1e-3f));
        }

        [Test]
        public void ARayDownwardMeetsTheGround()
        {
            bool hit = Hd2dStageMath.GroundHit(
                new Vector3(0f, 6f, -8f),
                new Vector3(0f, -1f, 1f),
                0f,
                out var ground
            );
            Assert.That(hit, Is.True);
            Assert.That(ground.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(ground.z, Is.EqualTo(-2f).Within(1e-4f));
        }

        [Test]
        public void ARayLevelOrUpwardNeverMeetsTheGround()
        {
            Assert.That(Hd2dStageMath.GroundHit(Vector3.up, Vector3.forward, 0f, out _), Is.False);
            Assert.That(Hd2dStageMath.GroundHit(Vector3.up, Vector3.up, 0f, out _), Is.False);
        }

        [Test]
        public void OneCanvasUnitGrowsWithDepth()
        {
            // At 10 m a 60-degree lens sees 2 * 10 * tan(30) m from top to bottom.
            float unit = Hd2dStageMath.WorldPerCanvasUnit(10f, 60f, 1080f);
            Assert.That(
                unit * 1080f,
                Is.EqualTo(20f * Mathf.Tan(30f * Mathf.Deg2Rad)).Within(1e-4f)
            );
            Assert.That(
                Hd2dStageMath.WorldPerCanvasUnit(20f, 60f, 1080f),
                Is.EqualTo(unit * 2f).Within(1e-6f)
            );
        }

        [Test]
        public void TheCameraMovesAgainstTheUiSoTheFocusMovesWithIt()
        {
            var offset = new Vector2(12f, -6f);
            var shift = Hd2dStageMath.CameraShift(offset, 16f, 30f, 1080f);
            float unit = Hd2dStageMath.WorldPerCanvasUnit(16f, 30f, 1080f);
            // The camera goes the other way, so what lies at the focus slides the UI's way.
            Assert.That(shift.x, Is.EqualTo(-12f * unit).Within(1e-6f));
            Assert.That(shift.y, Is.EqualTo(6f * unit).Within(1e-6f));
        }

        [Test]
        public void AShadowBoardRunsAcrossTheLightAndKeepsTheCamerasRight()
        {
            var right = Hd2dStageMath.ShadowBoardRight(new Vector3(-1f, 1f, 1f), Vector3.right);
            Assert.That(right.y, Is.EqualTo(0f));
            Assert.That(right.magnitude, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(
                Vector3.Dot(right, new Vector3(-1f, 0f, 1f).normalized),
                Is.EqualTo(0f).Within(1e-5f),
                "The board faces the light, so its shadow shows the whole outline."
            );
            Assert.That(Vector3.Dot(right, Vector3.right), Is.GreaterThan(0f));

            var behind = Hd2dStageMath.ShadowBoardRight(new Vector3(1f, 1f, 1f), Vector3.right);
            Assert.That(Vector3.Dot(behind, Vector3.right), Is.GreaterThan(0f), "No flip.");
        }

        [Test]
        public void ALightStraightAboveLeavesTheBoardAlongTheCamera()
        {
            var right = Hd2dStageMath.ShadowBoardRight(Vector3.up, new Vector3(2f, 0.5f, 0f));
            Assert.That(right, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void TheIntroMoveStartsWholeAndSettlesWithoutAJolt()
        {
            Assert.That(Hd2dStageMath.IntroRemaining(0f, 3f), Is.EqualTo(1f));
            Assert.That(Hd2dStageMath.IntroRemaining(3f, 3f), Is.EqualTo(0f));
            Assert.That(Hd2dStageMath.IntroRemaining(10f, 3f), Is.EqualTo(0f));
            float early = Hd2dStageMath.IntroRemaining(0.5f, 3f);
            float late = Hd2dStageMath.IntroRemaining(2.5f, 3f);
            Assert.That(early, Is.LessThan(1f).And.GreaterThan(late));
            // Easing out: the last stretch moves far less than the first.
            Assert.That(1f - early, Is.GreaterThan(late * 10f));
            Assert.That(Hd2dStageMath.IntroRemaining(1f, 0f), Is.EqualTo(0f), "No move.");
        }

        [Test]
        public void FlameLightWobblesWithinItsRange()
        {
            for (float t = 0f; t < 20f; t += 0.37f)
            {
                float wave = Hd2dLightFlicker.Flicker(t, 2.4f, 3f);
                Assert.That(wave, Is.InRange(-1f, 1f));
            }
            Assert.That(
                Hd2dLightFlicker.Flicker(1f, 2.4f, 3f),
                Is.Not.EqualTo(Hd2dLightFlicker.Flicker(1f, 2.4f, 7f)),
                "Each light flickers on its own."
            );
        }
    }
}
