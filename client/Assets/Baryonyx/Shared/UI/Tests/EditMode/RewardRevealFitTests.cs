using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 手に入れた物の札（RewardReveal）を何枚も出すとき、組が画面の外へはみ出さないよう上下にずらす。
    public sealed class RewardRevealFitTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private RewardReveal Reveal(float placeY)
        {
            root = new GameObject("Host", typeof(RectTransform));
            var host = (RectTransform)root.transform;
            host.sizeDelta = new Vector2(1920f, 1080f);
            var reveal = root.AddComponent<RewardReveal>();
            var plate = new GameObject("Plate", typeof(RectTransform)).GetComponent<RectTransform>();
            plate.SetParent(host, false);
            plate.sizeDelta = new Vector2(600f, 236f);
            plate.anchoredPosition = new Vector2(0f, placeY);
            reveal.Plate = plate;
            return reveal;
        }

        [Test]
        public void ManyPlatesFromHighUpMoveDownToStayOnTheScreen()
        {
            var reveal = Reveal(250f);
            Assert.That(reveal.FitOf(1), Is.EqualTo(0f), "One plate fits where it is.");
            // 6枚は2列3行：一番上の行が画面の上の縁より下に来るまで下げる。
            float shift = reveal.FitOf(6);
            Assert.That(shift, Is.LessThan(0f));
            float top = 250f + shift + reveal.OffsetOf(0, 6).y + 118f;
            Assert.That(top, Is.LessThanOrEqualTo(540f));
            float bottom = 250f + shift + reveal.OffsetOf(5, 6).y - 118f;
            Assert.That(bottom, Is.GreaterThanOrEqualTo(-540f));
        }
    }
}
