using Baryonyx.Combat.Presentation;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattleInspectTurnBannerTests
    {
        [Test]
        public void APartyTurnBandShowsTheTurnsNumberSmaller() =>
            Assert.That(
                BattleInspectView.PartyTurnText(4, again: false),
                Is.EqualTo("味方のターン<size=62%>　ターン4</size>")
            );

        [Test]
        public void APartyTurnRightAfterThePartysSaysItActsAgain()
        {
            string text = BattleInspectView.PartyTurnText(5, again: true);
            Assert.That(text, Does.Contain("もう一度 味方のターン"));
            Assert.That(text, Does.Not.Contain("ターン5"));
        }
    }
}
