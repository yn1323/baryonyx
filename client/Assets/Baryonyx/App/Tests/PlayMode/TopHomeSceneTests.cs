using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.UI;
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

            var reusablePanel = panel.GetComponent<TranslucentTextPanel>();
            Assert.That(reusablePanel, Is.Not.Null);
            Assert.That(reusablePanel.FontSize, Is.EqualTo(128f));
            Assert.That(reusablePanel.BackdropSize, Is.EqualTo(new Vector2(1320f, 260f)));
            Assert.That(reusablePanel.BackdropAlpha, Is.EqualTo(0.42f));
            var backdropObject = panel.transform.Find("BackdropCanvas");
            Assert.That(backdropObject.GetComponent<Canvas>(), Is.Not.Null);
            var backdrop = backdropObject.Find("Backdrop").GetComponent<RawImage>();
            var backdropRect = (RectTransform)backdropObject;
            Assert.That(backdropRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(backdropRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(backdropRect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(backdropRect.sizeDelta, Is.EqualTo(new Vector2(1320f, 260f)));
            Assert.That(backdrop.texture, Is.Not.Null);
            Assert.That(backdrop.color.r, Is.EqualTo(backdrop.color.g));
            Assert.That(backdrop.color.g, Is.EqualTo(backdrop.color.b));
            Assert.That(backdrop.color.a, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(backdrop.raycastTarget, Is.False);

            var title = panel.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            var titleRect = (RectTransform)title.transform;
            Assert.That(title.text, Is.EqualTo("てくてくダンジョン"));
            Assert.That(title.fontSize, Is.EqualTo(128f));
            Assert.That(titleRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(titleRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(title.raycastTarget, Is.False);
            Assert.That(reusablePanel.PulseEnabled, Is.False);
            Assert.That(reusablePanel.LabelGroup, Is.Not.Null);

            var tapPanel = canvas.transform.Find("TopScreen/TapToStartPanel")
                .GetComponent<TranslucentTextPanel>();
            Assert.That(tapPanel, Is.Not.Null);
            Assert.That(tapPanel.Label.text, Is.EqualTo("TAP TO START"));
            Assert.That(tapPanel.Label.fontSize, Is.EqualTo(48f));
            Assert.That(tapPanel.FontSize, Is.EqualTo(48f));
            Assert.That(tapPanel.BackdropSize, Is.EqualTo(new Vector2(760f, 92f)));
            Assert.That(tapPanel.BackdropAlpha, Is.EqualTo(0.42f));
            Assert.That(((RectTransform)tapPanel.transform).anchorMin, Is.EqualTo(new Vector2(0.5f, 0.16f)));
            Assert.That(tapPanel.Panel.raycastTarget, Is.False);
            Assert.That(tapPanel.PulseEnabled, Is.True);
            Assert.That(tapPanel.LabelGroup, Is.Not.Null);
            Assert.That(tapPanel.LabelGroup.blocksRaycasts, Is.False);
            Assert.That(tapPanel.PulseDurationSeconds, Is.EqualTo(2.4f));
            Assert.That(tapPanel.PulseMinimumAlpha, Is.EqualTo(0.35f));

            Assert.That(reusablePanel.Panel.raycastTarget, Is.False);
            Assert.That(reusablePanel.Backdrop.raycastTarget, Is.False);
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
