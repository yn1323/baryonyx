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
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                HealthScreenTestAssets.Source
            );
            Assert.That(prefab, Is.Not.Null);
            screen = Object.Instantiate(prefab).GetComponent<HealthScreenView>();
            application = new GameObject("Preview startup test");
            var bootstrap = application.AddComponent<HealthScreenBootstrap>();
            bootstrap.Screen = screen;
            bootstrap.Settings = null;
            yield return null;
            yield return null;

            Assert.That(
                screen.DayButtons.Count(button => button.gameObject.activeSelf),
                Is.EqualTo(7)
            );
            Assert.That(screen.DayLabels[0].text, Does.Contain("6,432"));
            Assert.That(screen.Progress.text, Does.Contain("サンプルデータ"));
            Assert.That(screen.Footnote.text, Does.Contain("架空"));
            Assert.That(screen.RefreshButton.interactable, Is.True);
            Assert.That(screen.SignInButton.gameObject.activeSelf, Is.False);
            Assert.That(screen.DetailsOverlay.activeSelf, Is.False);
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
