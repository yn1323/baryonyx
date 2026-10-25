using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 敵の一撃の狙いと弱め方：麻痺、ブロック、挑発。
    public sealed class BattleBlowsTests
    {
        [Test]
        public void AParalysedEnemyHitsAQuarterWeaker()
        {
            Assert.That(BattleBlows.Weakened(100, paralysed: true), Is.EqualTo(75));
            Assert.That(BattleBlows.Weakened(10, paralysed: true), Is.EqualTo(8), "Rounded.");
            Assert.That(BattleBlows.Weakened(100, paralysed: false), Is.EqualTo(100));
        }

        [Test]
        public void ABlockTakesWhatItCanOfABlow()
        {
            Assert.That(BattleBlows.Soaked(block: 30, damage: 50), Is.EqualTo(30));
            Assert.That(BattleBlows.Soaked(block: 80, damage: 50), Is.EqualTo(50));
        }

        [Test]
        public void ATauntingAllyDrawsTheBlow()
        {
            var standing = new[] { 0, 2, 3 };
            Assert.That(BattleBlows.Taunted(2, standing, ally => ally == 3), Is.EqualTo(3));
            Assert.That(BattleBlows.Taunted(2, standing, _ => false), Is.EqualTo(2));
            // Two taunting: the first standing draws it.
            Assert.That(BattleBlows.Taunted(0, standing, ally => ally >= 2), Is.EqualTo(2));
        }
    }
}
