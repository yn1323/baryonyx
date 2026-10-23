using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Temple;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TempleSummonLabSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/TempleSummonLab.unity";
        private static readonly string[] Keys =
        {
            TempleSummonLab.TierKey,
            TempleSummonLab.UpgradeKey,
            TempleSummonLab.RepeatKey,
            TempleSummonLab.TenKey,
        };

        private Scene loadedScene;

        // The lab remembers its choices on this machine; the test starts from none and gives
        // the developer's back after.
        private readonly Dictionary<string, int> saved = new();

        [SetUp]
        public void ForgetChoices()
        {
            saved.Clear();
            foreach (var key in Keys)
            {
                if (PlayerPrefs.HasKey(key))
                    saved[key] = PlayerPrefs.GetInt(key);
                PlayerPrefs.DeleteKey(key);
            }
        }

        // 開くと虹（昇格あり）の1回召喚を再生し、光を替えると見本を引き直して最初から再生する。
        [UnityTest]
        public IEnumerator TheSummoningPlaysAndEachLightCanBeChosen()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            var lab = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TempleSummonLab>(true))
                .Single();
            Assert.That(lab.Show, Is.Not.Null, "The lab plays the summoning presentation.");
            var canvas = lab.Show.GetComponent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(canvas.worldCamera, Is.Not.Null, "A camera draws it for Bloom.");

            Assert.That(lab.Plays, Is.EqualTo(1));
            Assert.That(lab.Tier, Is.EqualTo(TempleSummonTier.Rainbow));
            Assert.That(lab.Show.Playing, Is.True);
            Assert.That(lab.Show.CardFace.Name.text, Is.Not.Empty, "The card shows a sample.");

            lab.TierButton.onClick.Invoke();
            Assert.That(lab.Tier, Is.EqualTo(TempleSummonTier.White));
            Assert.That(lab.UpgradeButton.interactable, Is.False, "White has nothing below it.");
            Assert.That(lab.Plays, Is.EqualTo(2));
            lab.TierButton.onClick.Invoke();
            Assert.That(lab.Tier, Is.EqualTo(TempleSummonTier.Gold));
            Assert.That(
                PlayerPrefs.GetInt(TempleSummonLab.TierKey),
                Is.EqualTo((int)TempleSummonTier.Gold)
            );

            // 最後のコマ：窓は砕け、札が降りて止まっている。
            lab.Show.Skip();
            Assert.That(lab.Show.Playing, Is.False);
            Assert.That(lab.Show.Window.gameObject.activeSelf, Is.False);
            Assert.That(lab.Show.Hole.gameObject.activeSelf, Is.True);
            Assert.That(lab.Show.Card.gameObject.activeSelf, Is.True);
            Assert.That(lab.Show.Card.anchoredPosition, Is.EqualTo(lab.Show.CardRest));

            // 10回：窓の壁に切り替え、押すと伏せた札まで飛び、もう一度押すとめくって終わる。
            lab.PlayTenButton.onClick.Invoke();
            Assert.That(lab.Ten, Is.True);
            var ten = lab.TenShow;
            Assert.That(ten.gameObject.activeInHierarchy, Is.True);
            Assert.That(lab.Show.gameObject.activeInHierarchy, Is.False);
            Assert.That(ten.Panes, Has.Length.EqualTo(TempleSummon.TenCount));
            Assert.That(ten.Playing, Is.True);
            ten.Tap();
            yield return null;
            Assert.That(ten.Flipped, Is.False, "The cards wait face down for a tap.");
            Assert.That(ten.Panes.All(pane => pane.CardBack.activeSelf), Is.True);
            ten.Tap();
            float waited = 0f;
            while (ten.Playing && waited < 5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(ten.Playing, Is.False);
            Assert.That(ten.Panes.All(pane => !pane.CardBack.activeSelf), Is.True);
            Assert.That(lab.Plays, Is.EqualTo(4));
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            foreach (var key in Keys)
            {
                if (saved.TryGetValue(key, out int value))
                    PlayerPrefs.SetInt(key, value);
                else
                    PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
