using System.Collections;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class SceneTransitionPlayModeTests
    {
        private GameObject root;
        private SceneTransitionController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("SceneTransitionTest", typeof(RectTransform));
            root.SetActive(false);
            root.AddComponent<Canvas>();
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<CanvasGroup>();
            root.AddComponent<SceneTransitionController>();
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            AddImage("TransitionBlocker", Color.clear);
            AddImage("FadePanel", Color.black);
            AddImage("WipePanel", Color.black);
            AddImage("ShutterFirst", Color.black);
            AddImage("ShutterSecond", Color.black);
            controller = root.GetComponent<SceneTransitionController>();
            root.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null)
                Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllTransitionTypesCoverAndRevealWithTheirConfiguredDirection()
        {
            var settings = new SceneTransitionSettings
            {
                CoverDuration = 0.02f,
                RevealDuration = 0.02f,
                Color = Color.black,
            };

            settings.Type = SceneTransitionType.Fade;
            yield return AssertCoverAndReveal(settings);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f).Within(0.001f));

            settings.Type = SceneTransitionType.Wipe;
            settings.WipeDirection = SceneTransitionWipeDirection.RightToLeft;
            yield return AssertCoverAndReveal(settings);
            var wipe = root.transform.Find("WipePanel").GetComponent<Image>();
            Assert.That(wipe.rectTransform.anchorMin.x, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(wipe.rectTransform.anchorMax.x, Is.EqualTo(0f).Within(0.001f));

            settings.Type = SceneTransitionType.Shutter;
            settings.ShutterAxis = SceneTransitionShutterAxis.Vertical;
            yield return AssertCoverAndReveal(settings);
            var top = root.transform.Find("ShutterFirst").GetComponent<Image>();
            Assert.That(top.rectTransform.anchorMin.y, Is.EqualTo(1f).Within(0.001f));

            settings.ShutterAxis = SceneTransitionShutterAxis.Horizontal;
            yield return AssertCoverAndReveal(settings);
            var left = root.transform.Find("ShutterFirst").GetComponent<Image>();
            Assert.That(left.rectTransform.anchorMax.x, Is.EqualTo(0f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PlayInUsesTheSingleEnterSettingsByDefault()
        {
            controller.DefaultSettings.Type = SceneTransitionType.Wipe;
            controller.DefaultSettings.WipeDirection = SceneTransitionWipeDirection.RightToLeft;
            controller.EnterSettings.Type = SceneTransitionType.Shutter;
            controller.EnterSettings.ShutterAxis = SceneTransitionShutterAxis.Horizontal;
            controller.DefaultSettings.CoverDuration = 0.02f;
            controller.EnterSettings.RevealDuration = 0.02f;

            Assert.That(controller.PlayOut(), Is.True);
            yield return WaitUntilStopped();
            Assert.That(root.transform.Find("WipePanel").gameObject.activeSelf, Is.True);

            Assert.That(controller.PlayIn(), Is.True);
            yield return WaitUntilStopped();
            Assert.That(root.transform.Find("WipePanel").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("ShutterFirst").gameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("ShutterSecond").gameObject.activeSelf, Is.True);
        }

        private IEnumerator AssertCoverAndReveal(SceneTransitionSettings settings)
        {
            Assert.That(controller.PlayOut(settings), Is.True);
            yield return WaitUntilStopped();
            Assert.That(controller.IsCovered, Is.True);

            Assert.That(controller.PlayIn(settings), Is.True);
            yield return WaitUntilStopped();
            Assert.That(controller.IsCovered, Is.False);
            Assert.That(controller.IsPlaying, Is.False);
        }

        private IEnumerator WaitUntilStopped()
        {
            var deadline = Time.realtimeSinceStartup + 1f;
            while (controller.IsPlaying && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(controller.IsPlaying, Is.False);
        }

        private void AddImage(string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = imageObject.GetComponent<Image>();
            image.color = color;
        }
    }
}
