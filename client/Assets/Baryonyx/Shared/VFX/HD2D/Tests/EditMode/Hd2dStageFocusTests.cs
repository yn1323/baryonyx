using Baryonyx.Vfx.Hd2d;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class Hd2dStageFocusTests
    {
        private static float Radius(float depth, float intensity = 1f) =>
            Hd2dStageFocus.EvaluateBlurRadius(depth, 10f, 2f, 3f, 12f, 20f, 6f, intensity);

        [Test]
        public void TheCharactersDepthStaysSharp()
        {
            Assert.That(Radius(10f), Is.EqualTo(0f));
            Assert.That(Radius(8f), Is.EqualTo(0f), "The near edge of the sharp range.");
            Assert.That(Radius(12f), Is.EqualTo(0f), "The far edge of the sharp range.");
        }

        [Test]
        public void TheDistanceBlursUpToTheFarRadius()
        {
            Assert.That(Radius(18f), Is.EqualTo(3f).Within(1e-4f), "Half way through the falloff.");
            Assert.That(Radius(24f), Is.EqualTo(6f));
            Assert.That(Radius(100f), Is.EqualTo(6f));
        }

        [Test]
        public void TheForegroundBlursMoreThanTheDistance()
        {
            Assert.That(Radius(6.5f), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(Radius(5f), Is.EqualTo(20f));
            Assert.That(Radius(0.5f), Is.EqualTo(20f));
        }

        [Test]
        public void TheBlurCreepsInAndSettlesWithoutACrease()
        {
            // A quarter of the way through the falloff the blur has barely started, and a
            // quarter before its end it has almost settled (a smooth step, not a straight line).
            Assert.That(Radius(15f), Is.EqualTo(6f * 0.15625f).Within(1e-4f));
            Assert.That(Radius(21f), Is.EqualTo(6f * 0.84375f).Within(1e-4f));
            Assert.That(Radius(7.25f), Is.EqualTo(20f * 0.15625f).Within(1e-4f));
        }

        [Test]
        public void IntensityScalesTheBlurAndZeroTurnsItOff()
        {
            Assert.That(Radius(24f, 0.5f), Is.EqualTo(3f));
            Assert.That(Radius(24f, 0f), Is.EqualTo(0f));
            var focus = UnityEngine.ScriptableObject.CreateInstance<Hd2dStageFocus>();
            try
            {
                Assert.That(focus.IsActive(), Is.False, "Off until a look turns it on.");
                focus.intensity.Override(1f);
                Assert.That(focus.IsActive(), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(focus);
            }
        }
    }
}
