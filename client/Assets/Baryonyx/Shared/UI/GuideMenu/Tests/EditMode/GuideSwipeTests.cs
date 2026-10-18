using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class GuideSwipeTests
    {
        [Test]
        public void AFlickToTheLeftShowsTheNextPerson()
        {
            // 左へ120（設計座標）以上動かすと次の人、右へで前の人。短い動きや縦の動きでは替えない。
            Assert.That(GuideSwipe.StepOf(new Vector2(-140, 20)), Is.EqualTo(1));
            Assert.That(GuideSwipe.StepOf(new Vector2(160, -30)), Is.EqualTo(-1));
            Assert.That(GuideSwipe.StepOf(new Vector2(-80, 0)), Is.EqualTo(0));
            Assert.That(GuideSwipe.StepOf(new Vector2(-150, 200)), Is.EqualTo(0));
        }
    }
}
