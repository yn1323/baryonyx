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

        [UnityTest]
        public IEnumerator EachMomentOfTheBattlePlaysOnItsButton()
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
            AssertEnemiesStanding(view);

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

            // The drop is S at first.
            Assert.That(lab.Quality, Is.EqualTo(Inventory.ItemQuality.S));
            yield return Press(lab, BattleMoment.Drop);
            Assert.That(lab.Reveal.Shown, Is.True, "The drop opens on its plate.");
            Assert.That(lab.Reveal.Quality.text, Is.EqualTo("S"));
            Assert.That(
                lab.Reveal.Kind.text,
                Is.EqualTo(lab.Samples[0].Kind).And.StartsWith("武器")
            );
            Assert.That(lab.Reveal.Name.text, Is.EqualTo(lab.Samples[0].Name));
            Assert.That(lab.Reveal.Nothing.gameObject.activeSelf, Is.False);

            // Past S comes nothing found.
            lab.QualityButton.onClick.Invoke();
            Assert.That(lab.Quality, Is.Null);
            Assert.That(lab.QualityLabel.text, Does.Contain("なし"));
            yield return Press(lab, BattleMoment.Drop);
            Assert.That(lab.Reveal.Shown, Is.True);
            Assert.That(lab.Reveal.Nothing.gameObject.activeSelf, Is.True);
            Assert.That(lab.Reveal.Name.gameObject.activeSelf, Is.False);

            lab.QualityButton.onClick.Invoke();
            Assert.That(lab.Quality, Is.EqualTo(Inventory.ItemQuality.C));
            yield return Press(lab, BattleMoment.VictoryAndDrop, seconds: 10f);
            Assert.That(view.Enemies.All(enemy => !enemy.Alive), Is.True);
            Assert.That(lab.Reveal.Quality.text, Is.EqualTo("C"));

            yield return Press(lab, BattleMoment.Wipeout);
            Assert.That(view.Allies.All(ally => ally.Down), Is.True, "The party is wiped out.");
            Assert.That(lab.Reveal.Shown, Is.False, "A new moment takes the plate away.");
            yield return Press(lab, BattleMoment.Revive);
            Assert.That(
                view.Allies.All(ally => ally.Hp == ally.MaxHp),
                Is.True,
                "The party stands again."
            );

            yield return Press(lab, BattleMoment.BossEncounter, seconds: 8f);
            AssertEnemiesStanding(view);
            Assert.That(view.Enemies.Any(enemy => enemy.Boss), Is.True, "One of them is the boss.");

            yield return Press(lab, BattleMoment.PartyTurn);
            yield return Press(lab, BattleMoment.PartyTurnAgain);
            yield return Press(lab, BattleMoment.NoEnergy);
            int turn = view.Turn;
            yield return Press(lab, BattleMoment.EnemyTurn, seconds: 10f);
            Assert.That(view.Turn, Is.EqualTo(turn + 1), "The party's next turn has begun.");

            Assert.That(
                PlayerPrefs.GetInt(BattleMomentLab.MomentKey),
                Is.EqualTo((int)BattleMoment.EnemyTurn),
                "The moment last pressed is remembered."
            );
            Assert.That(PlayerPrefs.GetInt(BattleMomentLab.QualityKey), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheRememberedMomentPlaysAgainAndRepeats()
        {
            PlayerPrefs.SetInt(BattleMomentLab.MomentKey, (int)BattleMoment.Wipeout);
            PlayerPrefs.SetInt(BattleMomentLab.RepeatKey, 1);
            var (lab, view) = (default(BattleMomentLab), default(BattleInspectView));
            yield return Load((l, v) => (lab, view) = (l, v));

            Assert.That(lab.Moment, Is.EqualTo(BattleMoment.Wipeout));
            Assert.That(lab.Repeat, Is.True);
            Assert.That(lab.RepeatLabel.text, Is.EqualTo("くり返し：入"));
            yield return SceneTests.WaitUntil(
                () => view.Allies.All(ally => ally.Down),
                8f,
                "The remembered moment plays."
            );
            // Each run starts from the party standing; the first ally falls again at once, so
            // the others are what show it.
            yield return SceneTests.WaitUntil(
                () => view.Allies.Any(ally => !ally.Down),
                8f,
                "The next run stands the party up first."
            );
            lab.RepeatButton.onClick.Invoke();
            Assert.That(lab.Repeat, Is.False);
            yield return SceneTests.WaitUntil(() => !lab.Busy, 8f, "The last run ends.");
        }

        private static void AssertEnemiesStanding(BattleInspectView view)
        {
            for (int i = 0; i < view.Enemies.Length; i++)
            {
                var enemy = view.Enemies[i];
                Assert.That(enemy.Alive, Is.True, enemy.Body.name);
                Assert.That(enemy.Group.alpha, Is.EqualTo(1f), enemy.Body.name + " shows.");
                Assert.That(
                    enemy.Sprite.color,
                    Is.EqualTo(Color.white),
                    enemy.Body.name + " is lit."
                );
            }
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
