using Baryonyx.Economy;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class RuneBalanceTests
    {
        [SetUp]
        [TearDown]
        public void ResetBalance() => RuneBalance.Reset();

        [Test]
        public void UnknownUntilSetAndNotifiesOnlyOnChange()
        {
            int changes = 0;
            RuneBalance.Changed += () => changes++;
            Assert.That(RuneBalance.Known, Is.Null);
            Assert.That(RuneBalance.Text(RuneBalance.Known), Is.EqualTo("--"));

            RuneBalance.Set(12345);
            RuneBalance.Set(12345);
            Assert.That(RuneBalance.Known, Is.EqualTo(12345));
            Assert.That(RuneBalance.Text(RuneBalance.Known), Is.EqualTo("12,345"));
            Assert.That(changes, Is.EqualTo(1));

            RuneBalance.Set(-5);
            Assert.That(RuneBalance.Known, Is.EqualTo(0));
            Assert.That(changes, Is.EqualTo(2));
        }
    }
}
