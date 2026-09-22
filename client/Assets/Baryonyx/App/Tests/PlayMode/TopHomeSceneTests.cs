using System.Collections;
using System.Linq;
using Baryonyx.App;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TopHomeSceneTests
    {
        private const string TopScenePath = "Assets/Baryonyx/App/Scenes/Top.unity";
        private const string HomeScenePath = "Assets/Baryonyx/App/Scenes/Home.unity";
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator FullScreenTopTapLoadsHomeScene()
        {
            yield return SceneManager.LoadSceneAsync(TopScenePath, LoadSceneMode.Single);
            loadedScene = SceneManager.GetSceneByPath(TopScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);

            var roots = loadedScene.GetRootGameObjects();
            var canvas = roots
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Single(candidate => candidate.name == "TopCanvas");
            Assert.That(
                canvas.GetComponent<CanvasScaler>().referenceResolution,
                Is.EqualTo(new Vector2(1920, 1080))
            );
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(
                roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count(),
                Is.EqualTo(1)
            );

            var button = canvas.transform.Find("TopScreen").GetComponent<Button>();
            var rect = (RectTransform)button.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(button.GetComponent<Image>().color.a, Is.EqualTo(0f));
            Assert.That(button.GetComponent<TopSceneController>().NextSceneName, Is.EqualTo("Home"));

            var background = canvas.transform.Find("TopBackground").GetComponent<RawImage>();
            Assert.That(background.texture, Is.Not.Null);
            Assert.That(background.raycastTarget, Is.False);

            var panel = button.transform.Find("TopTitlePanel").GetComponent<RawImage>();
            var panelRect = (RectTransform)panel.transform;
            Assert.That(panel.texture, Is.Not.Null);
            Assert.That(panelRect.anchorMin, Is.EqualTo(new Vector2(0.1f, 0.5f)));
            Assert.That(panelRect.anchorMax, Is.EqualTo(new Vector2(0.9f, 0.5f)));
            Assert.That(panelRect.anchoredPosition, Is.EqualTo(new Vector2(0f, 120f)));
            Assert.That(panelRect.sizeDelta, Is.EqualTo(new Vector2(0f, 300f)));
            Assert.That(panel.raycastTarget, Is.False);

            var backdropObject = panel.transform.Find("TopTitleBackdropCanvas");
            Assert.That(backdropObject.GetComponent<Canvas>(), Is.Not.Null);
            var backdrop = backdropObject.GetComponent<RawImage>();
            var backdropRect = (RectTransform)backdrop.transform;
            Assert.That(backdropRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(backdropRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(backdropRect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(backdropRect.sizeDelta, Is.EqualTo(new Vector2(1320f, 260f)));
            Assert.That(backdrop.texture, Is.Not.Null);
            Assert.That(backdrop.color.r, Is.EqualTo(backdrop.color.g));
            Assert.That(backdrop.color.g, Is.EqualTo(backdrop.color.b));
            Assert.That(backdrop.color.a, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(backdrop.raycastTarget, Is.False);

            var title = panel.transform.Find("TopTitle").GetComponent<TextMeshProUGUI>();
            var titleRect = (RectTransform)title.transform;
            Assert.That(title.text, Is.EqualTo("てくてくダンジョン"));
            Assert.That(title.fontSize, Is.EqualTo(128f));
            Assert.That(titleRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(titleRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(title.raycastTarget, Is.False);

            button.onClick.Invoke();
            yield return null;
            yield return null;

            loadedScene = SceneManager.GetSceneByPath(HomeScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            Assert.That(
                loadedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                    .Any(canvasObject => canvasObject.name == "HomeCanvas"),
                Is.True
            );
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
