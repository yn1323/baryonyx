using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 戦闘の途中で条件を満たす間だけ効く強化（doc/features/stage-progression.md の背水・先手・弔い）。
    public sealed class BattleStatBoostsTests
    {
        private static readonly CharacterStats Base = new(1000, 1000, 1000, 1000, 1000, 1000, 0, 0);

        private static BattleStatBoost Boost(
            BattleBoostCondition condition,
            BattleBoostStats stats,
            int percent
        ) =>
            new()
            {
                Name = condition.ToString(),
                Condition = condition,
                Stats = stats,
                Percent = percent,
                Turns = 2,
            };

        // 背水はHPが半分以下で立っている間、先手は最初の2ターン、弔いは倒れた仲間がいる間だけ効く。
        [Test]
        public void EachBoostWorksOnlyWhileItsConditionHolds()
        {
            var backwater = Boost(BattleBoostCondition.HalfHpOrLess, BattleBoostStats.Attacks, 30);
            Assert.That(BattleStatBoosts.Works(backwater, 51, 100, 1, 0), Is.False);
            Assert.That(BattleStatBoosts.Works(backwater, 50, 100, 1, 0), Is.True);
            Assert.That(BattleStatBoosts.Works(backwater, 0, 100, 1, 0), Is.False);
            var vanguard = Boost(BattleBoostCondition.FirstTurns, BattleBoostStats.Speed, 25);
            Assert.That(BattleStatBoosts.Works(vanguard, 100, 100, 2, 0), Is.True);
            Assert.That(BattleStatBoosts.Works(vanguard, 100, 100, 3, 0), Is.False);
            var mourning = Boost(BattleBoostCondition.PerFallenAlly, BattleBoostStats.Attacks, 10);
            Assert.That(BattleStatBoosts.Works(mourning, 100, 100, 5, 0), Is.False);
            Assert.That(BattleStatBoosts.Works(mourning, 100, 100, 5, 1), Is.True);
        }

        // 効いている強化は、戦闘の前の強化の%に足して、強化の前のステータスから1回掛ける。
        [Test]
        public void WorkingBoostsAddToThePercentsBeforeTheBattle()
        {
            // 戦闘の前に物攻 +47%、速度 +0% だった。
            var percents = new float[] { 0, 47, 0, 0, 0, 0 };
            var stats = Base;
            stats.PhysicalAttack = 1470;
            var boosts = new[]
            {
                Boost(BattleBoostCondition.HalfHpOrLess, BattleBoostStats.Attacks, 30),
                Boost(BattleBoostCondition.FirstTurns, BattleBoostStats.Speed, 25),
                Boost(BattleBoostCondition.PerFallenAlly, BattleBoostStats.Attacks, 10),
            };
            // 何も効いていなければ、戦闘の始めのまま。
            Assert.That(
                BattleStatBoosts.Now(stats, Base, percents, boosts, 100, 100, 3, 0),
                Is.EqualTo(stats)
            );
            // HPが半分で2ターン目、2人倒れている：物攻 47+30+20=97%、属攻 30+20=50%、速度 +25%。
            var now = BattleStatBoosts.Now(stats, Base, percents, boosts, 50, 100, 2, 2);
            Assert.That(now.PhysicalAttack, Is.EqualTo(1970));
            Assert.That(now.MagicAttack, Is.EqualTo(1500));
            Assert.That(now.Speed, Is.EqualTo(1250));
            Assert.That(now.PhysicalDefense, Is.EqualTo(1000));
        }
    }
}
