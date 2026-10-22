using Baryonyx.Adventure;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // マスの間で持ち越すHP（doc/features/stage-progression.md の HPの持ち越し）と、休憩の回復。
    public sealed class AdventureVitalsTests
    {
        private MemoryAdventureVitalsStore store;

        [SetUp]
        public void UseMemory()
        {
            store = new MemoryAdventureVitalsStore();
            AdventureVitals.UseStore(store);
        }

        [TearDown]
        public void UseDevice() => AdventureVitals.UseStore(null);

        [Test]
        public void AnAdventureStartsAtFullHp()
        {
            Assert.That(AdventureVitals.Of("run-1"), Is.EqualTo(new[] { 1f, 1f, 1f, 1f }));
            Assert.That(AdventureVitals.Of(""), Is.EqualTo(new[] { 1f, 1f, 1f, 1f }));
        }

        // 勝った戦闘のHPを次の戦闘へ持ち越し、別の冒険は満タンから始める。
        [Test]
        public void TheHpOfABattleWonIsCarriedOnInTheSameAdventureOnly()
        {
            AdventureVitals.Keep("run-1", new[] { 0.5f, 0f, 1f, 0.25f });
            Assert.That(AdventureVitals.Of("run-1"), Is.EqualTo(new[] { 0.5f, 0f, 1f, 0.25f }));
            Assert.That(AdventureVitals.Of("run-2"), Is.EqualTo(new[] { 1f, 1f, 1f, 1f }));
            // 足りない枠は満タン、はみ出す値は0〜1に収める。
            AdventureVitals.Keep("run-1", new[] { 2f, -1f });
            Assert.That(AdventureVitals.Of("run-1"), Is.EqualTo(new[] { 1f, 0f, 1f, 1f }));
        }

        // 休憩は、倒れた味方も含め、全員を最大HPの40%回復する。満タンを超えない。
        [Test]
        public void ARestHealsEveryoneByFortyPercentAndStandsTheFallenUp()
        {
            AdventureVitals.Keep("run-1", new[] { 0.5f, 0f, 0.9f, 0.1f });
            var rested = AdventureVitals.Rest("run-1");
            Assert.That(rested, Is.EqualTo(new[] { 0.9f, 0.4f, 1f, 0.5f }).Within(0.0001f));
            Assert.That(AdventureVitals.Of("run-1"), Is.EqualTo(rested).Within(0.0001f));
        }

        // 全員が倒れたままの記録は使わない（全滅は復活か冒険の終わりになる）。
        [Test]
        public void APartyAllFallenIsNotCarriedOn()
        {
            AdventureVitals.Keep("run-1", new[] { 0f, 0f, 0f, 0f });
            Assert.That(AdventureVitals.Of("run-1"), Is.EqualTo(new[] { 1f, 1f, 1f, 1f }));
        }

        // 戦闘を始めるHP：倒れていれば0、そうでなければ1以上で最大HPを超えない。
        [Test]
        public void TheBattleStartsFromTheCarriedHp()
        {
            Assert.That(BattleInspectView.StartHpOf(200, 1f), Is.EqualTo(200));
            Assert.That(BattleInspectView.StartHpOf(200, 0.5f), Is.EqualTo(100));
            Assert.That(BattleInspectView.StartHpOf(200, 0.001f), Is.EqualTo(1));
            Assert.That(BattleInspectView.StartHpOf(200, 0f), Is.Zero);
            Assert.That(BattleInspectView.StartHpOf(200, 3f), Is.EqualTo(200));
        }
    }
}
