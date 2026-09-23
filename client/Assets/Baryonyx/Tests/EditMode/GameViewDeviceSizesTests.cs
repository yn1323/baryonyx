using Baryonyx.Editor;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class GameViewDeviceSizesTests
    {
        [Test]
        public void MotoEdge50ProIsRegisteredWithoutDuplicates()
        {
            GameViewDeviceSizes.Register();
            var count = GameViewDeviceSizes.CountRegistered();
            GameViewDeviceSizes.Register();

            Assert.That(count, Is.GreaterThan(0));
            Assert.That(GameViewDeviceSizes.CountRegistered(), Is.EqualTo(count));
        }
    }
}
