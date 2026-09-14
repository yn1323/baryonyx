#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Health;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class HealthScreenPreviewStartupTests
    {
        private GameObject application;
        private HealthScreenView screen;

        [UnityTest]
        public IEnumerator EditorPlayStartsWithSamplesEvenWithoutOAuthSettings()
        {
            yield return CreateFocusedPreview();

            Assert.That(
                screen.DayButtons.Count(button => button.gameObject.activeSelf),
                Is.EqualTo(7)
            );
            Assert.That(screen.DayLabels[0].text, Does.Contain("6,432"));
            Assert.That(screen.Progress.text, Does.Contain("サンプルデータ"));
            Assert.That(screen.Footnote.text, Does.Contain("架空"));
            Assert.That(screen.RefreshButton.interactable, Is.True);
            Assert.That(screen.SignInButton.gameObject.activeSelf, Is.True);
            Assert.That(screen.GoogleStatus.text, Does.Contain("未接続"));
            Assert.That(screen.DetailsOverlay.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator FocusLossClearsDataUntilFocusAndPauseBothAllowForeground()
        {
            yield return CreateFocusedPreview();
            screen.DayButtons[0].onClick.Invoke();
            Assert.That(screen.DetailsOverlay.activeSelf, Is.True);
            application.SendMessage(
                "OnApplicationFocus",
                true,
                SendMessageOptions.DontRequireReceiver
            );
            application.SendMessage("OnApplicationPause", false);
            Assert.That(screen.DetailsOverlay.activeSelf, Is.True);

            application.SendMessage(
                "OnApplicationFocus",
                false,
                SendMessageOptions.DontRequireReceiver
            );
            Assert.That(screen.DayButtons.Any(button => button.gameObject.activeSelf), Is.False);
            Assert.That(screen.DetailsOverlay.activeSelf, Is.False);
            application.SendMessage("OnApplicationPause", false);
            Assert.That(screen.DayButtons.Any(button => button.gameObject.activeSelf), Is.False);
            application.SendMessage(
                "OnApplicationFocus",
                true,
                SendMessageOptions.DontRequireReceiver
            );
            Assert.That(
                screen.DayButtons.Count(button => button.gameObject.activeSelf),
                Is.EqualTo(7)
            );

            application.SendMessage("OnApplicationPause", true);
            application.SendMessage(
                "OnApplicationFocus",
                false,
                SendMessageOptions.DontRequireReceiver
            );
            application.SendMessage(
                "OnApplicationFocus",
                true,
                SendMessageOptions.DontRequireReceiver
            );
            Assert.That(screen.DayButtons.Any(button => button.gameObject.activeSelf), Is.False);
            application.SendMessage("OnApplicationPause", false);
            Assert.That(
                screen.DayButtons.Count(button => button.gameObject.activeSelf),
                Is.EqualTo(7)
            );
        }

        private IEnumerator CreateFocusedPreview()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Baryonyx/Features/Health/UI/HealthScreen.prefab"
            );
            Assert.That(prefab, Is.Not.Null);
            screen = Object.Instantiate(prefab).GetComponent<HealthScreenView>();
            application = new GameObject("Preview startup test");
            var bootstrap = application.AddComponent<HealthScreenBootstrap>();
            bootstrap.Screen = screen;
            bootstrap.Settings = null;
            yield return null;
            yield return null;
            // Give this test-owned app focus independently of the Editor window.
            application.SendMessage(
                "OnApplicationFocus",
                true,
                SendMessageOptions.DontRequireReceiver
            );
        }

        [UnityTearDown]
        public IEnumerator DestroyPreview()
        {
            if (application != null)
                Object.Destroy(application);
            if (screen != null)
                Object.Destroy(screen.gameObject);
            yield return null;
        }
    }
}
#endif
