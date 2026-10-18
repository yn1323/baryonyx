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

        [Test]
        public void ReservedRunesStayOffTheBalanceUntilReleased()
        {
            RuneBalance.Set(1000);
            RuneBalance.Reserve(300);
            Assert.That(RuneBalance.Known, Is.EqualTo(700));

            // 保存を待つ間に読み直した残高にも、先に使った分を引いて見せる。
            RuneBalance.Set(1000);
            Assert.That(RuneBalance.Known, Is.EqualTo(700));

            // 保存できたら、サーバーの残高にしてから取り置きを戻す。
            RuneBalance.Set(700);
            RuneBalance.Release(300);
            Assert.That(RuneBalance.Known, Is.EqualTo(700));
        }
    }
}
