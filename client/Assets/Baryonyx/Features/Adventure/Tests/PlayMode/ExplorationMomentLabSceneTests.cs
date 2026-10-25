using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class ExplorationMomentLabSceneTests
    {
        private const string ScenePath =
            "Assets/Baryonyx/App/Scenes/Debug/ExplorationMomentLab.unity";
        private static readonly string[] Keys =
        {
            ExplorationMomentLab.MomentKey,
            ExplorationMomentLab.QualityKey,
            ExplorationMomentLab.RepeatKey,
        };

        private Scene loadedScene;

        // The lab remembers its choices on this machine; the test starts from none and gives
        // the developer's back after.
        private readonly Dictionary<string, int> saved = new();

        private FixedRandom random;

        [SetUp]
        public void ForgetChoices()
        {
            random = FixedRandom.Use();
            saved.Clear();
            foreach (var key in Keys)
            {
                if (PlayerPrefs.HasKey(key))
                    saved[key] = PlayerPrefs.GetInt(key);
                PlayerPrefs.DeleteKey(key);
            }
        }

        // 開発用の画面なので、読み込みと、ボタンで場面が再生されて残ることだけを確かめる。
        [UnityTest]
        public IEnumerator AMomentPlaysOnItsButtonAndIsRemembered()
        {
            var lab = default(ExplorationMomentLab);
            yield return Load(found => lab = found);
            var view = lab.View;

            Assert.That(view.ShownRun, Is.Not.Null, "The sample route is drawn.");
            Assert.That(
                lab.MomentButtons,
                Has.Length.EqualTo(Enum.GetValues(typeof(ExplorationMoment)).Length)
            );
            // The first Play opens on the monsters of a monster room.
            Assert.That(lab.Moment, Is.EqualTo(ExplorationMoment.Encounter));
            yield return SceneTests.WaitUntil(() => lab.Busy, 5f, "The opening moment starts.");
            yield return SceneTests.WaitUntil(() => !lab.Busy, 8f, "The opening moment ends.");
            yield return null;

            var controls = lab.MomentButtons.Concat(
                new[] { lab.QualityButton, lab.RepeatButton, lab.ResetButton, lab.OtherLabButton }
            );
            foreach (var button in controls)
            {
                SceneTests.AssertTappable(loadedScene, button);
                SceneTests.AssertTouchSize(button.transform);
            }

            yield return Press(lab, ExplorationMoment.Chest);
            Assert.That(view.ChestSprite.texture, Is.SameAs(view.ChestOpen), "The chest is open.");
            Assert.That(lab.Moments.Reveal.Shown, Is.True, "What it held shows on its plate.");
            Assert.That(
                PlayerPrefs.GetInt(ExplorationMomentLab.MomentKey),
                Is.EqualTo((int)ExplorationMoment.Chest),
                "The moment last pressed is remembered."
            );
        }

        private static IEnumerator Press(
            ExplorationMomentLab lab,
            ExplorationMoment moment,
            float seconds = 6f
        )
        {
            var button = lab.MomentButtons[(int)moment];
            Assert.That(button.interactable, Is.True, moment + " can be pressed.");
            button.onClick.Invoke();
            Assert.That(lab.Busy, Is.True, moment + " starts at once.");
            yield return SceneTests.WaitUntil(() => !lab.Busy, seconds, moment + " ends.");
            yield return null;
        }

        private IEnumerator Load(Action<ExplorationMomentLab> found)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            var lab = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ExplorationMomentLab>(true))
                .Single();
            Assert.That(lab.View, Is.Not.Null, "The lab plays over the exploration screen.");
            Assert.That(lab.Moments, Is.Not.Null);
            Assert.That(lab.Moments.View, Is.SameAs(lab.View));
            found(lab);
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Time.timeScale = 1f;
            random?.Dispose();
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
