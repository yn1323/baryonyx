using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.EditMode
{
    public sealed class GuidePanelLoadTests
    {
        // 名前を渡した読み込みは、描き終えるまでの時間をログに出す（Editorは開発ビルドとして扱われる）。
        [Test]
        public void ANamedLoadLogsTheTimeUntilItIsDrawn()
        {
            var load = new GuidePanelLoad();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[計測\] 画面 TrainingView \d+ms$"));

            load.Start(_ => Task.CompletedTask, _ => Assert.Fail(), "TrainingView");

            Assert.That(load.Task.IsCompleted, Is.True);
        }

        // 名前のない読み込みと、読めなかった読み込みは、時間を出さない。
        [Test]
        public void AnUnnamedOrFailedLoadLogsNoTime()
        {
            var load = new GuidePanelLoad();
            bool failed = false;

            load.Start(_ => Task.CompletedTask, _ => Assert.Fail());
            load.Start(
                _ => Task.FromException(new System.InvalidOperationException()),
                _ => failed = true,
                "TrainingView"
            );

            Assert.That(failed, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheLineRoundsToWholeMilliseconds()
        {
            Assert.That(
                GuidePanelLoad.TimingLine("CardLoadoutView", 530.6),
                Is.EqualTo("[計測] 画面 CardLoadoutView 531ms")
            );
        }
    }
}
