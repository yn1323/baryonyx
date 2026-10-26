using Baryonyx.UI;
using Baryonyx.UI.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 押した位置のキラキラの決まり（doc/features/screens/common.md#押した位置のキラキラ）を、入力と描画を外して確かめる。
    public sealed class TapSparkleTests
    {
        private static readonly Vector2 Point = new(960f, 540f);

        private static TapSparkleGestures NewGestures() => new(TapSparkleGoldDust.Duration);

        [Test]
        public void ATouchShowsASignAndLiftingBurstsWhereItTouched()
        {
            var gestures = NewGestures();
            gestures.Down(1, Point, 0f);
            Assert.That(gestures.Sparks[0].Phase, Is.EqualTo(TapSparkleGestures.Phase.Sign));

            gestures.Up(1, 0.12f);

            Assert.That(gestures.Sparks.Count, Is.EqualTo(1));
            Assert.That(gestures.Sparks[0].Phase, Is.EqualTo(TapSparkleGestures.Phase.Burst));
            Assert.That(gestures.Sparks[0].At, Is.EqualTo(Point));
            Assert.That(gestures.Sparks[0].Start, Is.EqualTo(0.12f));
        }

        [Test]
        public void MovingPastTheSlopCallsTheSignOffAndLiftingThenBurstsNothing()
        {
            var gestures = NewGestures();
            gestures.Down(1, Point, 0f);
            gestures.Move(1, Point + new Vector2(TapSparkleGestures.Slop, 0f), 0.05f);
            Assert.That(gestures.Sparks[0].Phase, Is.EqualTo(TapSparkleGestures.Phase.Sign));

            gestures.Move(1, Point + new Vector2(TapSparkleGestures.Slop + 1f, 0f), 0.1f);
            gestures.Up(1, 0.15f);

            Assert.That(gestures.Sparks[0].Phase, Is.EqualTo(TapSparkleGestures.Phase.Fading));
            Assert.That(
                TapSparkleGestures.SignLeft(gestures.Sparks[0], 0.15f),
                Is.EqualTo(0.5f).Within(1e-4)
            );
        }

        [Test]
        public void RestingCallsTheSignOff()
        {
            var gestures = NewGestures();
            gestures.Down(1, Point, 0f);
            gestures.Tick(TapSparkleGestures.Hold - 0.01f);
            Assert.That(gestures.Sparks[0].Phase, Is.EqualTo(TapSparkleGestures.Phase.Sign));

            gestures.Tick(TapSparkleGestures.Hold);
            gestures.Up(1, 0.6f);

            Assert.That(gestures.Sparks[0].Phase, Is.EqualTo(TapSparkleGestures.Phase.Fading));
        }

        [Test]
        public void ASecondFingerCallsTheSignOffAndShowsNothing()
        {
            var gestures = NewGestures();
            gestures.Down(1, Point, 0f);
            gestures.Down(2, Point + new Vector2(200f, 0f), 0.05f);
            gestures.Up(2, 0.1f);
            gestures.Up(1, 0.12f);

            Assert.That(gestures.Sparks.Count, Is.EqualTo(1));
            Assert.That(gestures.Sparks[0].Phase, Is.EqualTo(TapSparkleGestures.Phase.Fading));
        }

        [Test]
        public void AFifthBurstTakesTheOldestAway()
        {
            var gestures = NewGestures();
            for (int i = 0; i < TapSparkleGestures.MaxBursts + 1; i++)
            {
                var at = new Vector2(100f * (i + 1), 300f);
                gestures.Down(i, at, i * 0.05f);
                gestures.Up(i, i * 0.05f + 0.01f);
            }

            Assert.That(gestures.Sparks.Count, Is.EqualTo(TapSparkleGestures.MaxBursts));
            Assert.That(gestures.Sparks[0].At, Is.EqualTo(new Vector2(200f, 300f)));
        }

        [Test]
        public void FadedSignsAndFinishedBurstsGo()
        {
            var gestures = NewGestures();
            gestures.Down(1, Point, 0f);
            gestures.Up(1, 0.1f);
            gestures.Down(2, Point, 0.2f);
            gestures.Up(2, 0.25f, cancelled: true);

            gestures.Tick(0.26f + TapSparkleGestures.CancelFade);
            Assert.That(gestures.Sparks.Count, Is.EqualTo(1));

            gestures.Tick(0.11f + TapSparkleGoldDust.Duration);
            Assert.That(gestures.Sparks.Count, Is.EqualTo(0));
        }

        // 起動時に名前で探すため、どのビルドにもシェーダーが入っていなければならない。
        [Test]
        public void EveryBuildCarriesTheShader()
        {
            Assert.That(Shader.Find(TapSparkleLayer.ShaderName), Is.Not.Null);
            Assert.That(TapSparkleAssets.ShaderIncluded(), Is.True);
        }
    }
}
