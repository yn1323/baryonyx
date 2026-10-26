using Baryonyx.UI;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class MapPawnHopTests
    {
        private const float None = float.PositiveInfinity;

        [Test]
        public void TakesOffAtOnceAndPeaksHalfwayThroughTheAir()
        {
            var start = MapPawnHop.At(0f, None);
            var rising = MapPawnHop.At(MapPawnHop.AirSeconds * 0.1f, None);
            var top = MapPawnHop.At(MapPawnHop.AirSeconds * 0.5f, None);

            Assert.That(start.Height, Is.EqualTo(0f));
            Assert.That(rising.Height, Is.GreaterThan(0f), "No crouch before taking off");
            Assert.That(rising.Scale.y, Is.GreaterThan(1f), "Stretches while rising");
            Assert.That(top.Height, Is.EqualTo(MapPawnHop.Height).Within(0.01f));
            Assert.That(top.Lean, Is.GreaterThan(0f));
            Assert.That(top.Lift, Is.GreaterThan(start.Lift));
        }

        [Test]
        public void SquashesOnLandingKeepingItsVolumeThenRests()
        {
            var landing = MapPawnHop.At(MapPawnHop.AirSeconds + 0.05f, 0.05f);
            var rest = MapPawnHop.At(
                MapPawnHop.AirSeconds + MapPawnHop.LandSeconds,
                MapPawnHop.LandSeconds
            );

            Assert.That(landing.Height, Is.EqualTo(0f));
            Assert.That(landing.Scale.y, Is.LessThan(1f));
            Assert.That(landing.Scale.x * landing.Scale.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rest.Height, Is.EqualTo(0f));
            Assert.That(rest.Scale.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rest.Scale.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rest.Lean, Is.EqualTo(0f));
        }
    }
}
