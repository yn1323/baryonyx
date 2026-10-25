using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class EnemyLabSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/EnemyLab.unity";
        private static readonly string[] Keys =
        {
            EnemyLab.EnemyKey,
            EnemyLab.ActionKey,
            EnemyLab.RepeatKey,
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

        // 開発用の画面なので、読み込みと、選んだ敵がボタンで動くことだけを確かめる。行動の決まりは
        // EnemyBrainTests・BattleBlowsTests（EditMode）と、戦闘画面のテストが確かめる。反射はここだけで見る。
        [UnityTest]
        public IEnumerator TheChosenEnemyActsOnItsButtons()
        {
            var (lab, view) = (default(EnemyLab), default(BattleInspectView));
            yield return Load((l, v) => (lab, view) = (l, v));

            Assert.That(
                view.Hand.gameObject.activeInHierarchy,
                Is.False,
                "The cards are put away."
            );
            Assert.That(view.EndTurnButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(
                lab.ActionButtons,
                Has.Length.EqualTo(Enum.GetValues(typeof(EnemyLabAction)).Length)
            );
            Assert.That(
                lab.EnemyNames,
                Is.EqualTo(new[] { "苔スライム", "苔むした狼", "森の守り手" })
            );
            Assert.That(lab.Repeat, Is.False);
            var slime = view.Enemies[0];
            Assert.That(lab.Enemy, Is.EqualTo(0));
            Assert.That(
                slime.Marker.gameObject.activeSelf,
                Is.True,
                "A cursor marks the chosen enemy."
            );
            Assert.That(lab.EnemyLabel.text, Does.Contain("苔スライム"));
            Assert.That(lab.EnemyLabel.text, Does.Contain($"HP {slime.MaxHp}/{slime.MaxHp}"));
            var controls = lab.ActionButtons.Concat(
                new[] { lab.PreviousButton, lab.NextButton, lab.RepeatButton, lab.ResetButton }
            );
            foreach (var button in controls)
            {
                SceneTests.AssertTappable(loadedScene, button);
                SceneTests.AssertTouchSize(button.transform);
            }

            int blow = view.LabBlowPower(0);
            yield return Press(lab, EnemyLabAction.Hit);
            Assert.That(slime.Hp, Is.EqualTo(slime.MaxHp - blow));

            int party = PartyHp(view);
            int before = slime.Hp;
            yield return Press(lab, EnemyLabAction.Reflect);
            Assert.That(PartyHp(view), Is.EqualTo(party), "The mirror takes the blow.");
            Assert.That(
                slime.Hp,
                Is.EqualTo(
                    before - CombatFormula.Defend(slime.Power, slime.PhysicalDefense, slime.Level)
                ),
                "It hits itself, through its own defense."
            );
            for (int i = 0; i < view.Allies.Length; i++)
                Assert.That(view.HasStatus(true, i, CardStatus.Reflect), Is.False);

            lab.Select(lab.Enemy + 1);
            Assert.That(lab.Enemy, Is.EqualTo(1));
            Assert.That(view.Enemies[1].Marker.gameObject.activeSelf, Is.True);

            lab.ResetButton.onClick.Invoke();
            Assert.That(view.Enemies.All(enemy => enemy.Hp == enemy.MaxHp), Is.True);
            Assert.That(view.Allies.All(ally => ally.Hp == ally.StartHp), Is.True);
        }

        /// <summary>Presses the action's button and waits for the battle to finish it.</summary>
        private static IEnumerator Press(EnemyLab lab, EnemyLabAction action, float seconds = 5f)
        {
            var button = lab.ActionButtons[(int)action];
            Assert.That(button.interactable, Is.True, action + " can be pressed.");
            button.onClick.Invoke();
            Assert.That(lab.Busy, Is.True, action + " starts at once.");
            yield return SceneTests.WaitUntil(() => !lab.Busy, seconds, action + " ends.");
            // The lab's buttons come back on its next frame.
            yield return null;
        }

        private static int PartyHp(BattleInspectView view) => view.Allies.Sum(ally => ally.Hp);

        private IEnumerator Load(Action<EnemyLab, BattleInspectView> found)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            var lab = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EnemyLab>(true))
                .Single();
            Assert.That(lab.View, Is.Not.Null, "The lab moves the battle screen's enemies.");
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
