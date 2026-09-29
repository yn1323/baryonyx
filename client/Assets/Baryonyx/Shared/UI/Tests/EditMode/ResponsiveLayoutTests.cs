using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class ResponsiveLayoutTests
    {
        [TestCase(1920f, 1f)]
        [TestCase(2400f, 1f)]
        [TestCase(1440f, 0.75f)]
        [TestCase(0f, 1f)]
        public void WorldShrinksOnlyOnNarrowScreens(float width, float expected)
        {
            Assert.That(WorldLayerFit.ScaleFor(width, 1920f), Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(1f, 4)]
        [TestCase(2f / 3f, 3)]
        [TestCase(4f / 3f, 5)]
        [TestCase(0.1f, 1)]
        public void PixelDotsCoverWholeScreenPixels(float scale, int expected)
        {
            Assert.That(PixelPerfectRawImage.DotPixels(4f, scale), Is.EqualTo(expected));
        }

        [TestCase(1920, 1080, 16f / 9f, 1920, 1080)]
        [TestCase(2400, 1080, 16f / 9f, 2400, 1350)]
        [TestCase(1024, 768, 16f / 9f, 1365.333f, 768)]
        public void BackgroundCoversParentWithoutChangingItsAspect(
            float parentWidth,
            float parentHeight,
            float aspect,
            float expectedWidth,
            float expectedHeight
        )
        {
            var parentObject = new GameObject("Parent", typeof(RectTransform));
            var backgroundObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(ResponsiveBackground)
            );
            try
            {
                var parent = parentObject.GetComponent<RectTransform>();
                parent.sizeDelta = new Vector2(parentWidth, parentHeight);
                var background = backgroundObject.GetComponent<RectTransform>();
                background.SetParent(parent, false);
                var fitter = backgroundObject.GetComponent<ResponsiveBackground>();
                fitter.AspectRatio = aspect;

                fitter.Apply();
                Assert.That(background.rect.width, Is.EqualTo(expectedWidth).Within(.01f));
                Assert.That(background.rect.height, Is.EqualTo(expectedHeight).Within(.01f));
                Assert.That(background.anchorMin, Is.EqualTo(Vector2.one * .5f));
                Assert.That(background.anchorMax, Is.EqualTo(Vector2.one * .5f));
            }
            finally
            {
                Object.DestroyImmediate(backgroundObject);
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void SafeAreaFollowerCopiesNormalizedSourceBounds()
        {
            var sourceObject = new GameObject("Source", typeof(RectTransform));
            var targetObject = new GameObject(
                "Target",
                typeof(RectTransform),
                typeof(SafeAreaFollower)
            );
            try
            {
                var source = sourceObject.GetComponent<RectTransform>();
                source.anchorMin = new Vector2(.05f, .08f);
                source.anchorMax = new Vector2(.94f, .92f);
                var target = targetObject.GetComponent<RectTransform>();
                var follower = targetObject.GetComponent<SafeAreaFollower>();
                follower.Source = source;

                Assert.That(follower.Apply(), Is.True);
                Assert.That(target.anchorMin, Is.EqualTo(source.anchorMin));
                Assert.That(target.anchorMax, Is.EqualTo(source.anchorMax));
                Assert.That(target.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(target.offsetMax, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(sourceObject);
            }
        }

        [Test]
        public void SafeAreaFollowerConvertsAnAsymmetricViewportToNormalizedAnchors()
        {
            var targetObject = new GameObject(
                "Target",
                typeof(RectTransform),
                typeof(SafeAreaFollower)
            );
            try
            {
                var target = targetObject.GetComponent<RectTransform>();
                var follower = targetObject.GetComponent<SafeAreaFollower>();

                Assert.That(
                    follower.ApplyViewport(new Vector2(1024, 768), new Rect(24, 48, 960, 680)),
                    Is.True
                );
                Assert.That(target.anchorMin, Is.EqualTo(new Vector2(24f / 1024, 48f / 768)));
                Assert.That(target.anchorMax, Is.EqualTo(new Vector2(984f / 1024, 728f / 768)));
                Assert.That(target.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(target.offsetMax, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }
    }
}
