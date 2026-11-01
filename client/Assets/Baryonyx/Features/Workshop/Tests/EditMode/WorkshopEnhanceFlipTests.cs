using Baryonyx.Inventory;
using Baryonyx.Workshop;
using NUnit.Framework;
using UnityEngine;
using Look = Baryonyx.Workshop.WorkshopEnhanceFlip.Look;

namespace Baryonyx.Tests.EditMode
{
    // 品質強化の演出（doc/features/enhancement.md 品質強化の演出）：段階と拍、回ったカードの向き。
    public sealed class WorkshopEnhanceFlipTests
    {
        // 段階は強化した後の品質で決める。Sのまま上げる強化も虹になる。
        [TestCase(ItemQuality.C, Look.Plain)]
        [TestCase(ItemQuality.B, Look.Plain)]
        [TestCase(ItemQuality.A, Look.Gold)]
        [TestCase(ItemQuality.S, Look.Rainbow)]
        public void TheQualityAfterSetsHowRichItIs(ItemQuality after, Look look) =>
            Assert.That(WorkshopEnhanceFlip.LookOf(after), Is.EqualTo(look));

        // 拍は順に進み、普通がいちばん短く、金・虹は溜める分だけ長い。普通は裏で止まらない。
        [Test]
        public void TheBeatsGoInOrderAndRicherLooksLast()
        {
            foreach (var look in new[] { Look.Plain, Look.Gold, Look.Rainbow })
            {
                var beats = WorkshopEnhanceFlip.Beats.Of(look);
                Assert.That(beats.SpinEnd, Is.GreaterThan(WorkshopEnhanceFlip.Beats.FlyEnd), look.ToString());
                Assert.That(beats.Turn, Is.GreaterThanOrEqualTo(beats.SpinEnd), look.ToString());
                Assert.That(beats.Burst, Is.GreaterThan(beats.Turn), look.ToString());
                Assert.That(beats.ReturnStart, Is.GreaterThan(beats.Burst), look.ToString());
                Assert.That(beats.Seconds, Is.GreaterThan(beats.ReturnStart), look.ToString());
            }
            var plain = WorkshopEnhanceFlip.Beats.Of(Look.Plain);
            var gold = WorkshopEnhanceFlip.Beats.Of(Look.Gold);
            var rainbow = WorkshopEnhanceFlip.Beats.Of(Look.Rainbow);
            Assert.That(plain.Turn, Is.EqualTo(plain.SpinEnd));
            Assert.That(plain.Seconds, Is.LessThan(1.6f));
            Assert.That(gold.Seconds, Is.GreaterThan(plain.Seconds));
            Assert.That(rainbow.Seconds, Is.GreaterThan(gold.Seconds));
        }

        // 裏を見せて溜める間は裏が見え、表を向いたあとは強化した後の表が見える。初めは強化する前の表。
        [Test]
        public void TheCardShowsTheBackWhileItChargesAndTheCardAfterOnceTurned()
        {
            var root = new GameObject("Flip", typeof(RectTransform));
            try
            {
                var flip = root.AddComponent<WorkshopEnhanceFlip>();
                flip.Card = Child(root.transform, "Card");
                flip.BeforeFace = Child(flip.Card, "BeforeFace");
                flip.AfterFace = Child(flip.Card, "AfterFace");
                var back = Child(flip.Card, "Back");
                flip.Back = back.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                var plan = new WorkshopEnhanceFlip.Plan { Look = Look.Gold };
                var beats = WorkshopEnhanceFlip.Beats.Of(Look.Gold);

                flip.Show(0f, plan);
                Assert.That(flip.BeforeFace.gameObject.activeSelf, Is.True);
                Assert.That(flip.AfterFace.gameObject.activeSelf, Is.False);

                flip.Show((beats.SpinEnd + beats.Turn) / 2f, plan);
                Assert.That(back.gameObject.activeSelf, Is.True);
                Assert.That(flip.BeforeFace.gameObject.activeSelf, Is.False);
                Assert.That(flip.AfterFace.gameObject.activeSelf, Is.False);

                flip.Show(beats.Burst + 0.05f, plan);
                Assert.That(back.gameObject.activeSelf, Is.False);
                Assert.That(flip.AfterFace.gameObject.activeSelf, Is.True);
                Assert.That(flip.Card.localScale.x, Is.GreaterThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }
    }
}
