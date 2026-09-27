using System.Collections;
using System.Linq;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class BattleInspectSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/BattleInspect.unity";
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator SceneShowsPartyEnemiesAndHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);

            Assert.That(view.IdleActors, Has.Length.EqualTo(7), "4 allies and 3 enemies");
            Assert.That(view.Enemies, Has.Length.EqualTo(3));
            Assert.That(view.Cards, Has.Length.EqualTo(6));
            Assert.That(view.Enemies.Select(enemy => enemy.Sprite.texture), Has.None.Null);
            Assert.That(view.Turn, Is.EqualTo(3));
            Assert.That(view.MaxEnergy, Is.EqualTo(5), "Energy grows from 3 by one each turn.");
            Assert.That(view.EnergyLabel.text, Is.EqualTo("5/5"));
            Assert.That(view.Target, Is.EqualTo(0));
            Assert.That(view.TargetFrame.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator CardThenEnemyPlaysTheCardOnThatEnemy()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            var slash = view.Cards[0];

            slash.Button.onClick.Invoke();
            Assert.That(view.SelectedCard, Is.EqualTo(0));
            wolf.Button.onClick.Invoke();

            Assert.That(view.Target, Is.EqualTo(1));
            Assert.That(view.SelectedCard, Is.EqualTo(-1));
            Assert.That(slash.Used, Is.True);
            Assert.That(view.Energy, Is.EqualTo(view.MaxEnergy - slash.Cost));
            Assert.That(wolf.Hp, Is.EqualTo(wolf.MaxHp - slash.Power));
            Assert.That(wolf.HpLabel.text, Is.EqualTo($"{wolf.Hp}/{wolf.MaxHp}"));
        }

        [UnityTest]
        public IEnumerator WeaknessMultipliesDamageAndEndTurnRefillsTheHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            var ice = view.Cards.Single(card => card.Element == BattleInspectElement.Ice);
            Assert.That(wolf.Weakness, Is.EqualTo(BattleInspectElement.Ice));

            ice.Button.onClick.Invoke();
            wolf.Button.onClick.Invoke();
            Assert.That(wolf.Hp, Is.EqualTo(wolf.MaxHp - Mathf.RoundToInt(ice.Power * 1.5f)));

            view.EndTurnButton.onClick.Invoke();
            Assert.That(view.Turn, Is.EqualTo(4));
            Assert.That(view.Energy, Is.EqualTo(6));
            Assert.That(view.Cards.Any(card => card.Used), Is.False);
        }

        [UnityTest]
        public IEnumerator CardCostingMoreThanTheEnergyIsNotSelected()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var thunder = view.Cards.Single(card =>
                card.Effect == BattleInspectCardEffect.DamageAll
            );

            // Spend down to 1 energy with two cost-2 cards, then try the cost-3 card.
            foreach (var card in view.Cards.Where(card => card.Cost == 2))
            {
                card.Button.onClick.Invoke();
                view.Enemies[2].Button.onClick.Invoke();
            }
            Assert.That(view.Energy, Is.EqualTo(1));
            thunder.Button.onClick.Invoke();
            Assert.That(view.SelectedCard, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator TurnOrderStartsWithThePartyAndAdvancesToTheNextPartyTurn()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            const int party = BattleInspectView.PartyTurn;

            Assert.That(view.TurnSlots, Has.Length.EqualTo(6));
            Assert.That(
                view.UpcomingTurns(6),
                Is.EqualTo(new[] { party, 1, 0, party, party, 2 }),
                "Party, wolf, slime, then two party turns in a row, then the guardian."
            );
            Assert.That(view.TurnSlots[0].Party.activeSelf, Is.True);
            Assert.That(view.TurnSlots[0].Now.activeSelf, Is.True);
            Assert.That(view.TurnSlots[1].Enemy.texture, Is.SameAs(view.Enemies[1].Sprite.texture));

            // Ending the turn passes the enemy turns (they do not act in the mock).
            view.EndTurnButton.onClick.Invoke();
            Assert.That(view.UpcomingTurns(6), Is.EqualTo(new[] { party, party, 2, party, 1, 0 }));
            Assert.That(view.TurnSlots[1].Party.activeSelf, Is.True);

            // A defeated enemy leaves the order.
            var slime = view.Enemies[0];
            slime.Hp = 1;
            view.Cards[0].Button.onClick.Invoke();
            slime.Button.onClick.Invoke();
            Assert.That(slime.Alive, Is.False);
            Assert.That(view.UpcomingTurns(6), Has.No.Member(0));
        }

        private IEnumerator Load(System.Action<BattleInspectView> found)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            var view = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BattleInspectView>(true))
                .Single();
            found(view);
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
