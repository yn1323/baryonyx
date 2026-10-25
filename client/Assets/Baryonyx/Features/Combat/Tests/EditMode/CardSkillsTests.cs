using System;
using System.Linq;
using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class CardSkillsTests
    {
        [Test]
        public void IdsNamesAndArtAreEachUsedOnce()
        {
            Assert.That(CardSkills.All.Select(card => card.Id), Is.Unique);
            Assert.That(CardSkills.All.Select(card => card.Name), Is.Unique);
            Assert.That(CardSkills.All.Select(card => card.Art), Is.Unique);
        }

        [Test]
        public void EveryCardCanBePaidForAndDoesSomething()
        {
            foreach (var card in CardSkills.All)
            {
                Assert.That(card.Cost, Is.InRange(0, 9), card.Name);
                Assert.That(card.Actions, Is.Not.Empty, card.Name);
                Assert.That(card.Text, Is.Not.Empty, card.Name);
            }
        }

        [Test]
        public void EachDescriptionHasANumberForEveryPoweredAction()
        {
            foreach (var card in CardSkills.All.Concat(CardSkills.Prototypes))
            {
                int powered = card.Powered().Count();
                for (int i = 0; i < powered; i++)
                    StringAssert.Contains("{" + i + "}", card.Text, card.Name);
                StringAssert.DoesNotContain("{" + powered + "}", card.Text, card.Name);
                Assert.DoesNotThrow(() => CardRules.Describe(card), card.Name);
            }
        }

        [Test]
        public void TheMockFirstSixCardsComeFirst()
        {
            Assert.That(
                CardSkills.All.Take(6).Select(card => card.Id),
                Is.EqualTo(new[] { "Slash", "Fire", "Ice", "Thunder", "Heal", "Guard" })
            );
        }

        [Test]
        public void AFoundCardIsTheOneWithItsId()
        {
            Assert.That(CardSkills.Find("Meteor").Name, Is.EqualTo("メテオ"));
            Assert.That(CardSkills.Find("Nothing"), Is.Null);
        }

        [TestCase(243, 100, 243)]
        [TestCase(318, 85, 270)]
        [TestCase(260, 45, 117)]
        public void PowerIsAPartOfTheStat(int stat, int percent, int expected)
        {
            var action = CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, percent);
            Assert.That(CardRules.Power(action, stat), Is.EqualTo(expected));
        }

        [TestCase(0, 0)]
        [TestCase(999, 0)]
        [TestCase(3500, 30)]
        [TestCase(10000, 100)]
        [TestCase(25000, 100)]
        public void TodaysActGrowsThePowerUpToItsCap(int act, int expectedPercent)
        {
            var action = CardAction
                .Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 100)
                .WithAct(10, 100);
            Assert.That(CardRules.ActBonusPercent(action, act), Is.EqualTo(expectedPercent));
            Assert.That(CardRules.Power(action, 200, act), Is.EqualTo(200 + 2 * expectedPercent));
        }

        [Test]
        public void ActDoesNotGrowACardThatDoesNotWalkWithIt()
        {
            var action = CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 100);
            Assert.That(CardRules.Power(action, 200, 9000), Is.EqualTo(200));
        }

        [Test]
        public void ABonusGrowsThePowerByItsPart()
        {
            var action = CardAction
                .Damage(CardTarget.OneEnemy, CardStat.MagicAttack, 120)
                .Bonus(CardCondition.OnBurning, 50);
            Assert.That(CardRules.WithBonus(action, 382), Is.EqualTo(573));
        }

        [Test]
        public void ADescriptionShowsThePartsOfTheStatsInOrder()
        {
            var card = CardSkills.Find("HolyLight");
            var text = CardRules.Describe(card, (action, part) => $"[{part}]");
            Assert.That(
                text,
                Is.EqualTo("敵全体に[属攻の70%]ダメージ。味方全体のHPを[属攻の35%]回復。")
            );
        }

        [Test]
        public void ADescriptionLeavesOutTodaysAct()
        {
            // 健脚の一撃はACTで威力が上がるが、説明文は割合だけを書き、ACTの増分は足さない。
            var card = CardSkills.Find("StrideStrike");
            var action = card.Powered().Single();
            Assert.That(action.ActStepPercent, Is.GreaterThan(0));
            StringAssert.Contains(CardRules.PartName(action), CardRules.Describe(card));
        }

        [Test]
        public void EveryKeywordInATextIsExplained()
        {
            foreach (var card in CardSkills.All.Concat(CardSkills.Prototypes))
            foreach (var name in CardKeywords.Names(card.Text))
                Assert.That(CardKeywords.Find(name), Is.Not.Null, $"{card.Name}: [{name}]");
        }

        [Test]
        public void ADescriptionPaintsItsKeywordsOrShowsThemPlain()
        {
            var card = CardSkills.Find("Ice");
            Assert.That(
                CardRules.Describe(card),
                Is.EqualTo("敵1体に属攻の120%ダメージ。60%で凍え2ターン。合体ライトニングボルト。")
            );
            Assert.That(
                CardRules.Describe(card, keyword: name => $"<{name}>"),
                Is.EqualTo("敵1体に属攻の120%ダメージ。60%で<凍え>2ターン。<合体>ライトニングボルト。")
            );
            Assert.That(
                CardKeywords.In(card.Text).Select(k => k.Name),
                Is.EqualTo(new[] { "凍え", "合体" })
            );
        }

        [Test]
        public void APrototypeIsFoundButNotAmongTheCardSkills()
        {
            foreach (var prototype in CardSkills.Prototypes)
            {
                Assert.That(CardSkills.Find(prototype.Id), Is.SameAs(prototype));
                Assert.That(CardSkills.All.Select(card => card.Id), Has.No.Member(prototype.Id));
                Assert.That(
                    CardSkills.All.Select(card => card.Name),
                    Has.No.Member(prototype.Name)
                );
            }
            var fang = CardSkills.Find("SerpentFang");
            Assert.That(fang.Art, Is.EqualTo("CardVitalThrust"));
            Assert.That(
                CardKeywords.In(fang.Text).Select(keyword => keyword.Name),
                Is.EqualTo(new[] { "出血", "毒", "防御ダウン", "弱点" })
            );
        }

        [Test]
        public void KeywordsAreListedOnceInTheirOrderAndAnOpenBracketIsLeft()
        {
            Assert.That(
                CardKeywords.Names("[毒]と[やけど]、また[毒]。"),
                Is.EqualTo(new[] { "毒", "やけど" })
            );
            Assert.That(
                CardKeywords.Plain("[凍え]2ターン。[未完"),
                Is.EqualTo("凍え2ターン。[未完")
            );
        }

        [Test]
        public void ACardForNoOneIsNamedByWhatItWorksOn()
        {
            Assert.That(CardRules.ScopeName(CardSkills.Find("Hone")), Is.EqualTo("次のカード"));
            Assert.That(CardRules.ScopeName(CardSkills.Find("Scout")), Is.EqualTo("山札"));
            Assert.That(
                CardRules.ScopeName(CardSkills.Find("ManaPrayer")),
                Is.EqualTo("エネルギー")
            );
            Assert.That(
                CardRules.ScopeName(CardSkills.Find("Revelation")),
                Is.EqualTo("捨て札")
            );
            Assert.That(
                CardRules.ScopeName(CardSkills.Find("Thundercloud")),
                Is.EqualTo("敵ランダム")
            );
        }

        [Test]
        public void AChainNeverHitsTheSameTargetTwiceInARow()
        {
            var random = new Random(7);
            for (int round = 0; round < 200; round++)
            {
                var order = CardRules.Chain(1, 3, 3, random.Next);
                Assert.That(order[0], Is.EqualTo(1));
                for (int i = 1; i < order.Length; i++)
                {
                    Assert.That(order[i], Is.InRange(0, 2));
                    Assert.That(order[i], Is.Not.EqualTo(order[i - 1]));
                }
            }
        }

        [Test]
        public void AChainOnALoneTargetHitsItEachTime()
        {
            Assert.That(CardRules.Chain(0, 1, 3, _ => 0), Is.EqualTo(new[] { 0, 0, 0 }));
        }
    }
}
