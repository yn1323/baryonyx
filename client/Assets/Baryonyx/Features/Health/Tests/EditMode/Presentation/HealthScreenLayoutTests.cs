using Baryonyx.Health;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthScreenLayoutTests
    {
        private RectTransform root;
        private HealthScreenLayout layout;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject(
                "LayoutTest",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            layout = root.gameObject.AddComponent<HealthScreenLayout>();
            layout.SafeArea = Child("SafeArea", root);
            layout.DetailsSafeArea = Child("DetailsSafeArea", root);
            layout.MainPanel = Child("Main", layout.SafeArea);
            layout.DetailsPanel = Child("Details", layout.DetailsSafeArea);
            foreach (var panel in new[] { layout.MainPanel, layout.DetailsPanel })
            {
                panel.anchorMin = new Vector2(0.5f, 0);
                panel.anchorMax = new Vector2(0.5f, 1);
                panel.sizeDelta = new Vector2(0, -80);
            }
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root.gameObject);

        [TestCase(720, 1280)]
        [TestCase(1080, 2340)]
        [TestCase(1080, 2400)]
        [TestCase(1536, 2048)]
        [TestCase(2048, 1536)]
        public void PanelsFitAsymmetricSafeAreaAcrossAspectRatios(int width, int height)
        {
            float scale = Mathf.Min(width / 800f, height / 1100f);
            root.sizeDelta = new Vector2(width / scale, height / scale);
            var safe = new Rect(24, 72, width - 64, height - 168);
            Assert.That(
                layout.ApplyViewport(new Vector2(width, height), safe, root.rect.size),
                Is.True
            );
            Assert.That(layout.SafeArea.anchorMin.x, Is.EqualTo(24f / width).Within(0.0001f));
            Assert.That(layout.SafeArea.anchorMin.y, Is.EqualTo(72f / height).Within(0.0001f));
            Assert.That(
                layout.SafeArea.anchorMax.y,
                Is.EqualTo((height - 96f) / height).Within(0.0001f)
            );
            Assert.That(layout.DetailsSafeArea.anchorMin, Is.EqualTo(layout.SafeArea.anchorMin));
            Assert.That(layout.DetailsSafeArea.anchorMax, Is.EqualTo(layout.SafeArea.anchorMax));
            Assert.That(layout.MainPanel.rect.width, Is.LessThanOrEqualTo(860));
            Assert.That(layout.DetailsPanel.rect.width, Is.LessThanOrEqualTo(980));
            AssertInside(layout.MainPanel, layout.SafeArea);
            AssertInside(layout.DetailsPanel, layout.DetailsSafeArea);
        }

        [Test]
        public void SameResolutionWithDifferentSafeAreaOrCanvasSizeIsApplied()
        {
            var screen = new Vector2(720, 1280);
            root.sizeDelta = new Vector2(800, 1422);
            var full = new Rect(0, 0, 720, 1280);
            Assert.That(layout.ApplyViewport(screen, full, root.rect.size), Is.True);
            Assert.That(layout.ApplyViewport(screen, full, root.rect.size), Is.False);
            Assert.That(
                layout.ApplyViewport(screen, new Rect(0, 60, 720, 1100), root.rect.size),
                Is.True
            );
            Assert.That(layout.SafeArea.anchorMin.y, Is.GreaterThan(0));
            root.sizeDelta = new Vector2(700, 1300);
            Assert.That(layout.ApplyViewport(screen, full, root.rect.size), Is.True);
            Assert.That(layout.MainPanel.rect.width, Is.EqualTo(620).Within(0.01f));
        }

        [Test]
        public void InvalidDimensionsCanRecover()
        {
            var full = new Rect(0, 0, 720, 1280);
            root.sizeDelta = new Vector2(800, 1422);
            Assert.That(layout.ApplyViewport(Vector2.zero, full, root.rect.size), Is.False);
            Assert.That(
                layout.ApplyViewport(new Vector2(720, 1280), Rect.zero, root.rect.size),
                Is.False
            );
            Assert.That(layout.ApplyViewport(new Vector2(720, 1280), full, Vector2.zero), Is.False);
            Assert.That(
                layout.ApplyViewport(new Vector2(720, 1280), full, root.rect.size),
                Is.True
            );
        }

        [Test]
        public void AvailableWidthSmallerThanMarginsNeverBecomesNegative()
        {
            root.sizeDelta = new Vector2(50, 1100);
            layout.ApplyViewport(new Vector2(50, 1100), new Rect(0, 0, 50, 1100), root.rect.size);
            Assert.That(layout.MainPanel.rect.width, Is.Zero);
            Assert.That(layout.DetailsPanel.rect.width, Is.Zero);
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent, false);
            return child;
        }

        private static void AssertInside(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = parent.InverseTransformPoint(corner);
                Assert.That(
                    point.x,
                    Is.InRange(parent.rect.xMin - 0.01f, parent.rect.xMax + 0.01f)
                );
                Assert.That(
                    point.y,
                    Is.InRange(parent.rect.yMin - 0.01f, parent.rect.yMax + 0.01f)
                );
            }
        }
    }
}
