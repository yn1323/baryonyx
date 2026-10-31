using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 戦闘後の強化（doc/features/stage-progression.md の戦闘後の強化の中身）。
    public sealed class AdventureBoonsTests
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

        private static BattlePartyMember Member(string name) =>
            new()
            {
                Name = name,
                Stats = new CharacterStats(1000, 1000, 1000, 1000, 1000, 1000, 100, 100),
            };

        private static BattlePartyMember[] Party() =>
            new[] { Member("トーマ"), Member("ルカ"), Member("アリア"), Member("ミナ") };

        // 同じ冒険の同じマスなら、何度作っても同じ候補になる（中断して再開しても同じ候補）。
        [Test]
        public void TheSameWinOffersTheSameBoons()
        {
            var party = Party();
            for (int seed = 1; seed < 40; seed++)
            {
                var first = AdventureBoons.Offer(seed, "f3-1", AdventureBoonBattle.Early, 0, party);
                var again = AdventureBoons.Offer(seed, "f3-1", AdventureBoonBattle.Early, 0, party);
                Assert.That(first.Count, Is.EqualTo(party.Length * AdventureBoons.PerMember));
                Assert.That(again, Is.EqualTo(first));
            }
        }

        // 1人に2つずつ、その仲間の列に並ぶ。1人の2つは別の種類で、全員の強化と、代償つき・修行は
        // それぞれ1つまで。白には上の系統が出ない。
        [Test]
        public void TheBoonsFollowTheRules()
        {
            var party = Party();
            foreach (
                var battle in new[]
                {
                    AdventureBoonBattle.Early,
                    AdventureBoonBattle.Late,
                    AdventureBoonBattle.Strong,
                    AdventureBoonBattle.TrialMet,
                }
            )
                for (int seed = 0; seed < 300; seed++)
                {
                    var offers = AdventureBoons.Offer(seed, "f4-2", battle, 0, party);
                    for (int column = 0; column < party.Length; column++)
                    {
                        var mine = offers.Skip(column * AdventureBoons.PerMember).Take(AdventureBoons.PerMember).ToArray();
                        Assert.That(mine.Select(boon => boon.Id).Distinct().Count(), Is.EqualTo(mine.Length));
                        foreach (var boon in mine.Where(boon => !boon.Everyone))
                            Assert.That(boon.Slot, Is.EqualTo(column));
                    }
                    Assert.That(offers.Count(boon => boon.Everyone), Is.LessThanOrEqualTo(1));
                    Assert.That(
                        offers.Count(boon =>
                            boon.Template.Family
                                is AdventureBoonFamily.Cost
                                    or AdventureBoonFamily.Training
                        ),
                        Is.LessThanOrEqualTo(1)
                    );
                    var plain = new[]
                    {
                        AdventureBoonFamily.Single,
                        AdventureBoonFamily.Party,
                        AdventureBoonFamily.Pair,
                    };
                    foreach (var boon in offers.Where(boon => boon.Rank == AdventureBoonRank.White))
                        Assert.That(plain, Does.Contain(boon.Template.Family));
                    // 段階を引くのは3回だけで、残りはその戦闘の一番下の段階になる。
                    var lowest = battle == AdventureBoonBattle.TrialMet
                        ? AdventureBoonRank.Gold
                        : AdventureBoonRank.White;
                    Assert.That(
                        offers.Count(boon => boon.Rank != lowest),
                        Is.LessThanOrEqualTo(AdventureBoons.RankDraws)
                    );
                }
        }

        // 仲間が欠けていれば、いる仲間の分だけ出す。
        [Test]
        public void OnlyTheMembersThereGetBoons()
        {
            var party = new[] { Member("トーマ"), null, Member("アリア"), null };
            for (int seed = 0; seed < 100; seed++)
            {
                var offers = AdventureBoons.Offer(seed, "f2-0", AdventureBoonBattle.Early, 0, party);
                Assert.That(offers.Count, Is.EqualTo(2 * AdventureBoons.PerMember));
                Assert.That(
                    offers.Where(boon => !boon.Everyone).Select(boon => boon.Slot).Distinct(),
                    Is.SubsetOf(new[] { 0, 2 })
                );
            }
        }

        // 強い魔物の後は1つが金以上、条件を満たした試練の後は1つが虹。最奥の間の後は出ない。
        [Test]
        public void HarderBattlesOfferHigherSteps()
        {
            var party = Party();
            for (int seed = 0; seed < 300; seed++)
            {
                Assert.That(
                    AdventureBoons
                        .Offer(seed, "f5-1", AdventureBoonBattle.Strong, 0, party)
                        .Any(boon => boon.Rank != AdventureBoonRank.White)
                );
                Assert.That(
                    AdventureBoons
                        .Offer(seed, "f5-1", AdventureBoonBattle.TrialMet, 0, party)
                        .Any(boon => boon.Rank == AdventureBoonRank.Rainbow)
                );
            }
            Assert.That(
                AdventureBoons.Offer(1, "f10-0", AdventureBoonBattle.None, 0, party),
                Is.Empty
            );
        }

        // 魔物の後の虹はおよそ5%で、虹が出ない候補が続くほど出やすくなる。
        [Test]
        public void ARunWithoutRainbowRaisesItsChance()
        {
            double Rate(AdventureBoonBattle battle, int pity)
            {
                int rainbow = 0;
                const int Draws = 4000;
                for (int seed = 0; seed < Draws; seed++)
                    rainbow += AdventureBoons
                        .Ranks(new RouteRandom(seed * 7919 + 13), battle, pity)
                        .Count(rank => rank == AdventureBoonRank.Rainbow);
                return rainbow / (Draws * (double)AdventureBoons.RankDraws);
            }
            Assert.That(Rate(AdventureBoonBattle.Early, 0), Is.InRange(0.035, 0.065));
            Assert.That(Rate(AdventureBoonBattle.Late, 0), Is.InRange(0.06, 0.1));
            Assert.That(Rate(AdventureBoonBattle.Early, 5), Is.InRange(0.17, 0.23));
        }

        // 同じ項目の上昇は、休憩の鍛えると足し算でまとめて1回掛ける。どの項目も元の半分より下げない。
        [Test]
        public void BoonsAddUpWithTheTrainingAndKeepHalfAtLeast()
        {
            var party = Party();
            var boons = new[]
            {
                new AdventureBoon("attack", AdventureBoonRank.White, 0, CardStat.PhysicalAttack),
                new AdventureBoon("attack", AdventureBoonRank.Gold, 0, CardStat.PhysicalAttack),
                new AdventureBoon("party-hp", AdventureBoonRank.White, -1, CardStat.PhysicalAttack),
                new AdventureBoon(
                    "glass-blade",
                    AdventureBoonRank.Rainbow,
                    1,
                    CardStat.MagicAttack
                ),
                new AdventureBoon(
                    "glass-blade",
                    AdventureBoonRank.Rainbow,
                    1,
                    CardStat.MagicAttack
                ),
            };
            AdventureBoons.Apply(party, boons, new[] { 1, 0, 0, 0 }, 0.15f, false);
            // トーマ：物攻 12+20+15=47%、属攻は鍛えた15%だけ、最大HPは全員の5%。
            Assert.That(party[0].Stats.PhysicalAttack, Is.EqualTo(1470));
            Assert.That(party[0].Stats.MagicAttack, Is.EqualTo(1150));
            Assert.That(party[0].Stats.Hp, Is.EqualTo(1050));
            // ルカ：属攻 +120%、最大HP 5-30-30=-55% は半分で止める。
            Assert.That(party[1].Stats.MagicAttack, Is.EqualTo(2200));
            Assert.That(party[1].Stats.Hp, Is.EqualTo(500));
            Assert.That(party[1].Stats.PhysicalAttack, Is.EqualTo(1000));
        }

        // 育つ強化は選んだあと勝つたびに上がり、修行は2戦のあいだ下がってから上がる。強敵狩りは強い戦闘でだけ。
        [Test]
        public void GrowthTrainingAndGiantSlayerWorkWhenTheyShould()
        {
            float PhysicalAttack(AdventureBoon[] boons, bool strong) =>
                AdventureBoons.PercentsOf(1, boons, null, 0.15f, strong)[0][1];
            float PhysicalDefense(AdventureBoon[] boons) =>
                AdventureBoons.PercentsOf(1, boons, null, 0.15f, false)[0][2];
            var filler = new AdventureBoon(
                "hp",
                AdventureBoonRank.White,
                0,
                CardStat.PhysicalAttack
            );
            var seasoned = new AdventureBoon(
                "seasoned",
                AdventureBoonRank.Gold,
                0,
                CardStat.PhysicalAttack
            );
            Assert.That(PhysicalAttack(new[] { seasoned }, false), Is.EqualTo(0));
            Assert.That(PhysicalAttack(new[] { seasoned, filler, filler }, false), Is.EqualTo(8));
            Assert.That(
                PhysicalAttack(Enumerable.Repeat(filler, 9).Prepend(seasoned).ToArray(), false),
                Is.EqualTo(20)
            );
            var training = new AdventureBoon(
                "training",
                AdventureBoonRank.Gold,
                0,
                CardStat.PhysicalAttack
            );
            Assert.That(PhysicalDefense(new[] { training, filler }), Is.EqualTo(-20));
            Assert.That(PhysicalAttack(new[] { training, filler }, false), Is.EqualTo(0));
            Assert.That(PhysicalDefense(new[] { training, filler, filler }), Is.EqualTo(0));
            Assert.That(PhysicalAttack(new[] { training, filler, filler }, false), Is.EqualTo(30));
            var slayer = new AdventureBoon(
                "giant-slayer",
                AdventureBoonRank.Rainbow,
                0,
                CardStat.PhysicalAttack
            );
            Assert.That(PhysicalAttack(new[] { slayer }, false), Is.EqualTo(0));
            Assert.That(PhysicalAttack(new[] { slayer }, true), Is.EqualTo(40));
        }

        // 背水・先手・弔いは戦闘の前には掛けず、戦闘の途中で効く強化として、対象の仲間に渡す。
        [Test]
        public void BoonsThatWorkDuringTheBattleGoToTheMembersAsBoosts()
        {
            var party = Party();
            var boons = new[]
            {
                new AdventureBoon("backwater", AdventureBoonRank.Gold, 1, CardStat.PhysicalAttack),
                new AdventureBoon(
                    "vanguard",
                    AdventureBoonRank.Rainbow,
                    -1,
                    CardStat.PhysicalAttack
                ),
                new AdventureBoon("attack", AdventureBoonRank.White, 1, CardStat.PhysicalAttack),
            };
            AdventureBoons.Apply(party, boons, null, 0.15f, false);
            Assert.That(party[1].Stats.PhysicalAttack, Is.EqualTo(1120));
            Assert.That(party[1].Stats.Speed, Is.EqualTo(1000));
            Assert.That(party[1].Unboosted.PhysicalAttack, Is.EqualTo(1000));
            Assert.That(party[1].Percents[BattleStatBoosts.PhysicalAttack], Is.EqualTo(12));
            Assert.That(
                party[1].Boosts.Select(boost => (boost.Condition, boost.Stats, boost.Percent)),
                Is.EqualTo(
                    new[]
                    {
                        (BattleBoostCondition.HalfHpOrLess, BattleBoostStats.Attacks, 30),
                        (BattleBoostCondition.FirstTurns, BattleBoostStats.Speed, 40),
                    }
                )
            );
            Assert.That(party[0].Boosts.Select(boost => boost.Name), Is.EqualTo(new[] { "先手" }));
        }

        // 選ぶ強化を待つマスは、選ぶと消え、選んだ強化と虹の出ない回数が残る。HPを持ち越しても消えない。
        [Test]
        public void TheBoonWaitedForAndTheBoonsChosenAreKept()
        {
            AdventureVitals.AwaitBoon("run-1", "f3-1", AdventureBoonBattle.Strong);
            Assert.That(
                AdventureVitals.PendingBoon("run-1"),
                Is.EqualTo(("f3-1", AdventureBoonBattle.Strong))
            );
            AdventureVitals.Keep("run-1", new[] { 0.5f, 1f, 1f, 1f });
            Assert.That(AdventureVitals.PendingBoon("run-1"), Is.Not.Null);
            var boon = new AdventureBoon("valor", AdventureBoonRank.Gold, 2, CardStat.MagicAttack);
            AdventureVitals.ChooseBoon("run-1", boon, false);
            Assert.That(AdventureVitals.PendingBoon("run-1"), Is.Null);
            Assert.That(AdventureVitals.Boons("run-1"), Is.EqualTo(new[] { boon }));
            Assert.That(AdventureVitals.BoonPity("run-1"), Is.EqualTo(1));
            AdventureVitals.HoneSlot("run-1", 0);
            AdventureVitals.ChooseBoon("run-1", boon, true);
            Assert.That(AdventureVitals.Boons("run-1").Count, Is.EqualTo(2));
            Assert.That(AdventureVitals.BoonPity("run-1"), Is.EqualTo(0));
            Assert.That(AdventureVitals.HonedSlots("run-1")[0], Is.EqualTo(1));
            Assert.That(AdventureVitals.Of("run-1")[0], Is.EqualTo(0.5f));
            // 別の冒険には残らない。
            Assert.That(AdventureVitals.Boons("run-2"), Is.Empty);
            Assert.That(AdventureVitals.PendingBoon("run-2"), Is.Null);
        }
    }
}
