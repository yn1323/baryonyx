using Baryonyx.Combat;
using NUnit.Framework;

// 名前空間の外に置き、このアセンブリのすべてのテストに効かせる。
// 戦闘の会心と状態異常の確率を、会心なし・状態異常は必ず付くに固定し、同じ戦闘がいつも同じに進むようにする。
// 確率を確かめるテストは BattleOdds.Fix で変え、終わったらこの固定へ戻す。
[SetUpFixture]
public sealed class BattleOddsSetUp
{
    [OneTimeSetUp]
    public void FixOdds() => BattleOdds.Fix(critical: false, lands: true);

    [OneTimeTearDown]
    public void UnfixOdds() => BattleOdds.Unfix();
}
