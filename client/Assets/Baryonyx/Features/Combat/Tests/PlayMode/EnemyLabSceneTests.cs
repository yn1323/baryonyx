using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public IEnumerator TheChosenEnemyActsAndReactsAsInTheBattle()
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
                AssertTappable(button);
                SceneTests.AssertTouchSize(button.transform);
            }

            int blow = view.LabBlowPower(0);
            yield return Press(lab, EnemyLabAction.Hit);
            Assert.That(slime.Hp, Is.EqualTo(slime.MaxHp - blow));

            Assert.That(slime.Weaknesses[0].Revealed, Is.False);
            yield return Press(lab, EnemyLabAction.WeakHit);
            Assert.That(slime.Weaknesses[0].Revealed, Is.True, "The hidden weakness is revealed.");
            Assert.That(slime.Hp, Is.EqualTo(slime.MaxHp - blow - Mathf.RoundToInt(blow * 1.5f)));

            yield return Press(lab, EnemyLabAction.Defeat);
            Assert.That(slime.Alive, Is.False);
            Assert.That(view.UpcomingTurns(6), Has.No.Member(0), "It leaves the turn order.");

            int party = PartyHp(view);
            yield return Press(lab, EnemyLabAction.Attack);
            Assert.That(slime.Hp, Is.EqualTo(slime.MaxHp), "A beaten enemy stands to act again.");
            Assert.That(PartyHp(view), Is.EqualTo(party - slime.Power), "It hits one ally.");

            party = PartyHp(view);
            yield return Press(lab, EnemyLabAction.Freeze);
            Assert.That(PartyHp(view), Is.EqualTo(party), "A frozen enemy does not move.");
            Assert.That(
                view.HasStatus(false, 0, CardStatus.Freeze),
                Is.False,
                "Its turn thaws it."
            );

            yield return Press(lab, EnemyLabAction.Reflect);
            Assert.That(PartyHp(view), Is.EqualTo(party), "The mirror takes the blow.");
            Assert.That(slime.Hp, Is.EqualTo(slime.MaxHp - slime.Power), "It hits itself.");
            for (int i = 0; i < view.Allies.Length; i++)
                Assert.That(view.HasStatus(true, i, CardStatus.Reflect), Is.False);

            yield return Press(lab, EnemyLabAction.Paralysis);
            Assert.That(
                PartyHp(view),
                Is.EqualTo(party - Mathf.RoundToInt(slime.Power * 0.75f)),
                "A paralysed enemy hits a quarter weaker."
            );
            Assert.That(lab.EnemyLabel.text, Does.Contain("麻痺"));

            lab.Select(lab.Enemy + 1);
            Assert.That(lab.Enemy, Is.EqualTo(1));
            Assert.That(view.Enemies[1].Marker.gameObject.activeSelf, Is.True);
            int turn = view.Turn;
            yield return Press(lab, EnemyLabAction.EnemyTurn, seconds: 10f);
            Assert.That(view.Turn, Is.EqualTo(turn + 1), "The party's next turn begins.");

            lab.ResetButton.onClick.Invoke();
            Assert.That(view.Enemies.All(enemy => enemy.Hp == enemy.MaxHp), Is.True);
            Assert.That(view.StatusesNow(false, 0), Is.Empty);
            Assert.That(slime.Weaknesses[0].Revealed, Is.False, "The weakness is hidden again.");
            Assert.That(view.Allies.All(ally => ally.Hp == ally.StartHp), Is.True);
        }

        [UnityTest]
        public IEnumerator RepeatingPlaysTheActionAgainFromTheStart()
        {
            var (lab, view) = (default(EnemyLab), default(BattleInspectView));
            yield return Load((l, v) => (lab, view) = (l, v));
            var slime = view.Enemies[0];

            // One blow now, then the same blow over and over.
            lab.Run(EnemyLabAction.Hit);
            lab.SetRepeat(true);
            Assert.That(PlayerPrefs.GetInt(EnemyLab.RepeatKey), Is.EqualTo(1), "Remembered.");
            Assert.That(
                PlayerPrefs.GetInt(EnemyLab.ActionKey),
                Is.EqualTo((int)EnemyLabAction.Hit)
            );
            yield return SceneTests.WaitUntil(() => !lab.Busy, 5f);
            yield return SceneTests.WaitUntil(() => lab.Busy, message: "It plays again by itself.");
            yield return SceneTests.WaitUntil(() => !lab.Busy, 5f);
            Assert.That(
                slime.Hp,
                Is.EqualTo(slime.MaxHp - view.LabBlowPower(0)),
                "Each run starts from full HP, so the blows do not add up."
            );
            lab.SetRepeat(false);
        }

        /// <summary>Nothing lies over the button's middle to take its tap.</summary>
        private void AssertTappable(Button button)
        {
            var events = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                .Single();
            var data = new PointerEventData(events)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    ((RectTransform)button.transform).TransformPoint(
                        ((RectTransform)button.transform).rect.center
                    )
                ),
            };
            var hits = new List<RaycastResult>();
            events.RaycastAll(data, hits);
            Assert.That(hits, Is.Not.Empty, button.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
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
