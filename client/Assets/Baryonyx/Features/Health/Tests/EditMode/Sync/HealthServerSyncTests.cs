using Baryonyx.Health;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthServerSyncTests
    {
        [TestCase("2026-09-24T00:00:00+09:00", "2026-09-23T15:00:00.000Z")]
        [TestCase("2026-09-23T15:00:00.123456Z", "2026-09-23T15:00:00.123Z")]
        [TestCase("2026-09-23T15:00:00Z", "2026-09-23T15:00:00.000Z")]
        public void TimestampsAreSentInUtc(string source, string expected)
        {
            Assert.That(HealthServerSync.Utc(source), Is.EqualTo(expected));
        }
    }
}
