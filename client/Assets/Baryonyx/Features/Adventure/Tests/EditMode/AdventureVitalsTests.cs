using System.Linq;
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

        // 休憩で鍛えた仲間とカードは、その冒険のあいだ残り、HPを持ち越しても消えない。
        [Test]
        public void TheHoningLastsThroughTheAdventureOnly()
        {
            AdventureVitals.Keep("run-1", new[] { 0.5f, 1f, 1f, 1f });
            AdventureVitals.HoneSlot("run-1", 1);
            AdventureVitals.HoneSlot("run-1", 1);
            AdventureVitals.HoneCard("run-1", 2, "Fire");
            AdventureVitals.HoneCard("run-1", 2, "Fire");
            AdventureVitals.Keep("run-1", new[] { 0.25f, 1f, 1f, 1f });
            Assert.That(AdventureVitals.HonedSlots("run-1"), Is.EqualTo(new[] { 0, 2, 0, 0 }));
            Assert.That(AdventureVitals.HonedCards("run-1"), Is.EqualTo(new[] { "2:Fire" }));
            Assert.That(AdventureVitals.Of("run-1")[0], Is.EqualTo(0.25f));
            AdventureVitals.Rest("run-1");
            Assert.That(AdventureVitals.HonedSlots("run-1"), Is.EqualTo(new[] { 0, 2, 0, 0 }));
            // 別の冒険は、鍛えていない所から始める。
            Assert.That(AdventureVitals.HonedSlots("run-2"), Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(AdventureVitals.HonedCards("run-2"), Is.Empty);
            // HPを持ち越す前に鍛えても、HPは満タンのまま。
            AdventureVitals.HoneSlot("run-3", 0);
            Assert.That(AdventureVitals.Of("run-3"), Is.EqualTo(new[] { 1f, 1f, 1f, 1f }));
        }

        // 鍛えた仲間は物攻・属攻が1回ごとに15%上がり、鍛えたカードは威力が30%上がって名前に「+」が付く。
        // 固有スキルのカードは鍛えない。
        [Test]
        public void TheHonedMembersAndCardsGrowInBattle()
        {
            var fire = Baryonyx.Combat.CardSkills.All.First(skill =>
                skill.Actions.Any(action => action.HasPower)
            );
            var members = new[]
            {
                new BattlePartyMember
                {
                    Name = "A",
                    Stats = new Baryonyx.Combat.CharacterStats
                    {
                        PhysicalAttack = 100,
                        MagicAttack = 200,
                    },
                    Cards = new[]
                    {
                        new BattlePartyCard { Skill = fire },
                        new BattlePartyCard { Skill = fire, Unique = 0 },
                    },
                },
                null,
                new BattlePartyMember
                {
                    Name = "C",
                    Stats = new Baryonyx.Combat.CharacterStats
                    {
                        PhysicalAttack = 100,
                        MagicAttack = 100,
                    },
                    Cards = new[] { new BattlePartyCard { Skill = fire } },
                },
            };
            AdventureVitals.HoneSlot("run-1", 0);
            AdventureVitals.HoneSlot("run-1", 0);
            AdventureVitals.HoneCard("run-1", 0, fire.Id);
            AdventureParty.Hone(members, "run-1");
            Assert.That(members[0].Stats.PhysicalAttack, Is.EqualTo(130));
            Assert.That(members[0].Stats.MagicAttack, Is.EqualTo(260));
            Assert.That(members[2].Stats.PhysicalAttack, Is.EqualTo(100));
            var honed = members[0].Cards[0].Skill;
            Assert.That(honed.Name, Is.EqualTo(fire.Name + AdventureParty.HonedMark));
            Assert.That(
                honed.Powered().Select(action => action.Percent),
                Is.EqualTo(
                    fire.Powered().Select(action => (int)System.Math.Round(action.Percent * 1.3f))
                )
            );
            Assert.That(honed.Cost, Is.EqualTo(fire.Cost));
            // 元のカードの定義は変えない。
            Assert.That(fire.Name, Does.Not.EndWith(AdventureParty.HonedMark));
            // 固有スキルのカードと、ほかの枠の同じカードは鍛えない。
            Assert.That(members[0].Cards[1].Skill, Is.SameAs(fire));
            Assert.That(members[2].Cards[0].Skill, Is.SameAs(fire));
            Assert.That(
                AdventureParty.HonableCards(members[0]).Select(card => card.Skill.Id),
                Is.EqualTo(new[] { fire.Id })
            );
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
