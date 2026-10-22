using System.Linq;
using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class CombatPassivesTests
    {
        [Test]
        public void EveryCharacterHasTwoPassivesUnlockedAtLevelOneAndTen()
        {
            var characters = new[]
            {
                "toma",
                "luka",
                "aria",
                "mina",
                "anselm",
                "greta",
                "lutz",
                "rita",
                "ritsu",
            };
            foreach (string id in characters)
            {
                var passives = CombatPassives.Of(id);
                Assert.That(passives.Select(p => p.UnlockLevel), Is.EqualTo(new[] { 1, 10 }), id);
                Assert.That(CombatPassives.UnlockedAt(id, 9), Has.Count.EqualTo(1), id);
                Assert.That(CombatPassives.UnlockedAt(id, 10), Has.Count.EqualTo(2), id);
            }
            Assert.That(CombatPassives.All.Count, Is.EqualTo(characters.Length * 2));
            Assert.That(CombatPassives.Of("nobody"), Is.Empty);
        }

        [Test]
        public void EachKindIsOnePassiveWithItsOwnIdAndIcon()
        {
            var kinds = System.Enum.GetValues(typeof(CombatPassiveKind)).Cast<CombatPassiveKind>();
            Assert.That(CombatPassives.All.Select(p => p.Kind), Is.EquivalentTo(kinds));
            Assert.That(CombatPassives.All.Select(p => p.Id), Is.Unique);
            Assert.That(CombatPassives.All.Select(p => p.Icon), Is.Unique);
            Assert.That(CombatPassives.All.Select(p => p.Id), Has.All.StartsWith("passive-"));
        }

        [Test]
        public void TheTextsUseOnlyKnownKeywordsAndReadPlainWithoutBrackets()
        {
            foreach (var passive in CombatPassives.All)
            {
                foreach (string keyword in CardKeywords.Names(passive.Text))
                    Assert.That(CardKeywords.Find(keyword), Is.Not.Null, passive.Name);
                Assert.That(passive.PlainText, Does.Not.Contain("[").And.Not.Contain("]"));
            }
        }

        [Test]
        public void StarChartCarriesAtMostOneEnergy()
        {
            Assert.That(CombatPassiveRules.Carry(0), Is.EqualTo(0));
            Assert.That(CombatPassiveRules.Carry(1), Is.EqualTo(1));
            Assert.That(CombatPassiveRules.Carry(4), Is.EqualTo(1));
        }

        [Test]
        public void CoverStepsInOnceForALowAllyWhileAriaIsNotLow()
        {
            // HP 30 of 100 is low; 31 is not.
            Assert.That(CombatPassiveRules.Covers(0, 30, 100, 2, 500, 500, false), Is.True);
            Assert.That(CombatPassiveRules.Covers(0, 31, 100, 2, 500, 500, false), Is.False);
            Assert.That(CombatPassiveRules.Covers(0, 30, 100, 2, 500, 500, true), Is.False);
            Assert.That(CombatPassiveRules.Covers(0, 30, 100, 2, 150, 500, false), Is.False);
            Assert.That(CombatPassiveRules.Covers(2, 30, 100, 2, 30, 100, false), Is.False);
            Assert.That(CombatPassiveRules.Covers(0, 0, 100, 2, 500, 500, false), Is.False);
        }

        [Test]
        public void WalkersBlessingGrowsByFivePercentPerThousandActUpToFifty()
        {
            Assert.That(CombatPassiveRules.WalkersBonusPercent(0), Is.EqualTo(0));
            Assert.That(CombatPassiveRules.WalkersBonusPercent(999), Is.EqualTo(0));
            Assert.That(CombatPassiveRules.WalkersBonusPercent(6000), Is.EqualTo(30));
            Assert.That(CombatPassiveRules.WalkersBonusPercent(25000), Is.EqualTo(50));
            Assert.That(CombatPassiveRules.Grow(200, 30), Is.EqualTo(260));
        }

        [Test]
        public void CopycatGrowsTenPercentAPieceUpToFiftyWhenItIsFull()
        {
            Assert.That(CombatPassiveRules.CopycatPercent(0), Is.EqualTo(0));
            Assert.That(CombatPassiveRules.CopycatPercent(3), Is.EqualTo(30));
            Assert.That(CombatPassiveRules.CopycatPercent(9), Is.EqualTo(50));
            Assert.That(CombatPassiveRules.CopycatFull(4), Is.False);
            Assert.That(CombatPassiveRules.CopycatFull(5), Is.True);
            Assert.That(CombatPassiveRules.IsCopycatElement(CardElement.Ice), Is.True);
            Assert.That(CombatPassiveRules.IsCopycatElement(CardElement.Fire), Is.False);
        }

        [Test]
        public void OverflowAndManaShieldTurnIntoBlock()
        {
            Assert.That(CombatPassiveRules.Overflow(90, 100, 30), Is.EqualTo(20));
            Assert.That(CombatPassiveRules.Overflow(50, 100, 30), Is.EqualTo(0));
            Assert.That(CombatPassiveRules.ManaShieldBlock(0), Is.EqualTo(0));
            Assert.That(CombatPassiveRules.ManaShieldBlock(3), Is.EqualTo(1));
            Assert.That(CombatPassiveRules.ManaShieldBlock(250), Is.EqualTo(50));
        }

        [Test]
        public void ChainedArrowsAddsAHitToRepeatedDamageOnACopy()
        {
            var repeated = CardSkills.All.First(CombatPassiveRules.HitsMoreThanOnce);
            var more = CombatPassiveRules.WithExtraHit(repeated);
            Assert.That(more, Is.Not.SameAs(repeated));
            Assert.That(more.Id, Is.EqualTo(repeated.Id));
            for (int i = 0; i < repeated.Actions.Count; i++)
            {
                var before = repeated.Actions[i];
                bool grows = before.Kind == CardActionKind.Damage && before.Hits > 1;
                Assert.That(more.Actions[i].Hits, Is.EqualTo(before.Hits + (grows ? 1 : 0)));
            }
            Assert.That(
                repeated.Actions.Where(a => a.Kind == CardActionKind.Damage).Max(a => a.Hits),
                Is.GreaterThan(1),
                "The definition keeps its own hits."
            );

            var single = CardSkills.All.First(skill => !CombatPassiveRules.HitsMoreThanOnce(skill));
            Assert.That(CombatPassiveRules.WithExtraHit(single), Is.SameAs(single));
        }
    }
}
