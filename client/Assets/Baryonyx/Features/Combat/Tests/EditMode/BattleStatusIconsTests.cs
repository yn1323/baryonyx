using System;
using System.IO;
using System.Linq;
using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattleStatusIconsTests
    {
        private const string Folder = "Assets/Baryonyx/Features/Combat/UI/Art/Statuses";

        [Test]
        public void StatsShowTheirPictureWithARisingOrFallingTriangle()
        {
            Assert.That(
                BattleStatusIcons.Of(CardStatus.AttackUp),
                Is.EqualTo(BattleStatusIcon.AttackUp)
            );
            Assert.That(
                BattleStatusIcons.Of(CardStatus.Protect),
                Is.EqualTo(BattleStatusIcon.DefenseUp),
                "プロテクト counts as defense up."
            );
            Assert.That(
                BattleStatusIcons.Of(CardStatus.Vulnerable),
                Is.EqualTo(BattleStatusIcon.DefenseDown)
            );
            Assert.That(
                BattleStatusIcons.Of(CardStatus.SpeedUp),
                Is.EqualTo(BattleStatusIcon.SpeedUp)
            );
        }

        [Test]
        public void EveryIconInTheRowHasAStatusAndAPicture()
        {
            var reached = Enum.GetValues(typeof(CardStatus))
                .Cast<CardStatus>()
                .Select(BattleStatusIcons.Of)
                .ToHashSet();
            foreach (var icon in BattleStatusIcons.Order)
            {
                Assert.That(reached, Does.Contain(icon), $"{icon} is shown for some status.");
                Assert.That(
                    File.Exists($"{Folder}/{BattleStatusIcons.ArtName(icon)}.aseprite"),
                    Is.True,
                    $"{icon} has its picture."
                );
            }
            Assert.That(BattleStatusIcons.Order, Has.No.Member(BattleStatusIcon.None));
            Assert.That(
                BattleStatusIcons.Order.Distinct().Count(),
                Is.EqualTo(BattleStatusIcons.Order.Length)
            );
        }
    }
}
