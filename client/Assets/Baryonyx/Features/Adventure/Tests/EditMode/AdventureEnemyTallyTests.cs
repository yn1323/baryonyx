using Baryonyx.Adventure;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 戦闘を終えたときにライブラリへ送る、遭遇した敵と倒した体数（doc/features/library.md の敵の記録）。
    public sealed class AdventureEnemyTallyTests
    {
        [Test]
        public void CountsEachKindOnceInOrderWithTheBeatenOnes()
        {
            var tally = AdventureEnemyTally.Count(
                new[]
                {
                    ("moss-slime", true),
                    ("moss-wolf", false),
                    ("moss-slime", true),
                    ("moss-slime", false),
                    ("", true),
                }
            );
            Assert.That(tally.Count, Is.EqualTo(2));
            Assert.That(tally[0].EnemyId, Is.EqualTo("moss-slime"));
            Assert.That(tally[0].Defeated, Is.EqualTo(2));
            // 倒していなくても、遭遇した敵として0体で送る。
            Assert.That(tally[1].EnemyId, Is.EqualTo("moss-wolf"));
            Assert.That(tally[1].Defeated, Is.EqualTo(0));
        }
    }
}
