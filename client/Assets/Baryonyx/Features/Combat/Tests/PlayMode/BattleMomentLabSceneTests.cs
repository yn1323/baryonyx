using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class BattleMomentLabSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleMomentLab.unity";
        private static readonly string[] Keys =
        {
            BattleMomentLab.MomentKey,
            BattleMomentLab.QualityKey,
            BattleMomentLab.RepeatKey,
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
            var (lab, view) = (default(BattleMomentLab), default(BattleInspectView));
            yield return Load((l, v) => (lab, view) = (l, v));

            Assert.That(
                view.Hand.gameObject.activeInHierarchy,
                Is.False,
                "The cards are put away."
            );
            Assert.That(
                lab.MomentButtons,
                Has.Length.EqualTo(Enum.GetValues(typeof(BattleMoment)).Length)
            );
            Assert.That(lab.Samples, Is.Not.Empty, "The drops hold pieces of equipment.");
            Assert.That(lab.Samples, Has.All.Matches<RewardItem>(drop => drop.Icon != null));

            // The first Play opens on the enemies appearing.
            Assert.That(lab.Moment, Is.EqualTo(BattleMoment.Encounter));
            yield return SceneTests.WaitUntil(() => lab.Busy, 5f, "The opening moment starts.");
            yield return SceneTests.WaitUntil(() => !lab.Busy, 8f, "The enemies have come on.");
            yield return null;
            Assert.That(view.Enemies.All(enemy => enemy.Alive), Is.True, "The enemies stand.");

            var controls = lab.MomentButtons.Concat(
                new[] { lab.QualityButton, lab.RepeatButton, lab.ResetButton, lab.OtherLabButton }
            );
            foreach (var button in controls)
            {
                SceneTests.AssertTappable(loadedScene, button);
                SceneTests.AssertTouchSize(button.transform);
            }

            yield return Press(lab, BattleMoment.Victory);
            Assert.That(view.Enemies.All(enemy => !enemy.Alive), Is.True, "The party has won.");

            Assert.That(
                PlayerPrefs.GetInt(BattleMomentLab.MomentKey),
                Is.EqualTo((int)BattleMoment.Victory),
                "The moment last pressed is remembered."
            );
        }

        private static IEnumerator Press(
            BattleMomentLab lab,
            BattleMoment moment,
            float seconds = 6f
        )
        {
            var button = lab.MomentButtons[(int)moment];
            Assert.That(button.interactable, Is.True, moment + " can be pressed.");
            button.onClick.Invoke();
            Assert.That(lab.Busy, Is.True, moment + " starts at once.");
            yield return SceneTests.WaitUntil(() => !lab.Busy, seconds, moment + " ends.");
            // The lab's buttons come back on its next frame.
            yield return null;
        }

        private IEnumerator Load(Action<BattleMomentLab, BattleInspectView> found)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            var lab = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BattleMomentLab>(true))
                .Single();
            Assert.That(lab.View, Is.Not.Null, "The lab plays the battle screen's moments.");
            // The opening hand at once, so the battle is not busy dealing.
            lab.View.RestartDeal(
                Enumerable.Range(0, lab.View.Deck.Length).ToArray(),
                instant: true
            );
            yield return null;
            found(lab, lab.View);
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
