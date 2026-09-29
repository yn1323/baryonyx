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
            Assert.That(view.Allies, Has.Length.EqualTo(4));
            Assert.That(view.Enemies, Has.Length.EqualTo(3));
            Assert.That(view.Cards, Has.Length.EqualTo(6));
            Assert.That(view.Enemies.Select(enemy => enemy.Sprite.texture), Has.None.Null);
            Assert.That(view.MaxEnergy, Is.EqualTo(5), "Energy grows from 3 by one each turn.");
            Assert.That(view.EnergyLabel.text, Is.EqualTo("5/5"));
            Assert.That(view.HeldCard, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator DroppingACardOnAnEnemyAfterASwipePlaysItThere()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            var slash = view.Cards[0];

            view.PressCard(0, CardPoint(slash));
            Assert.That(view.HeldCard, Is.EqualTo(0));
            view.DragCard(Center(wolf.TargetArea));
            Assert.That(view.Armed, Is.True, "Moving up to the wolf is a swipe up.");
            Assert.That(view.Targets, Is.EqualTo(new[] { 1 }));
            Assert.That(view.IsGlowing(wolf.Sprite), Is.True);
            Assert.That(view.IsGlowing(view.Enemies[0].Sprite), Is.False);

            view.ReleaseCard(Center(wolf.TargetArea));
            Assert.That(view.HeldCard, Is.EqualTo(-1));
            Assert.That(view.IsGlowing(wolf.Sprite), Is.False);
            Assert.That(slash.Used, Is.True);
            Assert.That(view.Energy, Is.EqualTo(view.MaxEnergy - slash.Cost));
            Assert.That(wolf.Hp, Is.EqualTo(wolf.MaxHp - slash.Power));
            Assert.That(wolf.HpFill.anchorMax.x, Is.EqualTo(wolf.Hp / (float)wolf.MaxHp));
        }

        [UnityTest]
        public IEnumerator ADropJustOutsideAnEnemyStillCountsOnIt()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var guardian = view.Enemies[2];
            var area = guardian.TargetArea;
            // Just past the right edge of the guardian's drawn body, where no other enemy is.
            var outside = RectTransformUtility.WorldToScreenPoint(
                null,
                area.TransformPoint(new Vector2(area.rect.xMax + 40f, area.rect.center.y))
            );

            view.PressCard(0, CardPoint(view.Cards[0]));
            view.DragCard(outside);
            Assert.That(view.Targets, Is.EqualTo(new[] { 2 }));
            var card = view.Cards[0].Body;
            yield return null;
            Assert.That(
                card.localScale.x,
                Is.GreaterThan(0.9f * view.Settings.CardScale),
                "The card stays full size while aiming."
            );
            // After the spring settles, the pressed card is enlarged.
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                yield return null;
            Assert.That(
                card.localScale.x,
                Is.EqualTo(view.Settings.RaisedScale * view.Settings.CardScale).Within(0.02f)
            );
            view.ReleaseCard(outside);

            Assert.That(guardian.Hp, Is.LessThan(guardian.MaxHp));
        }

        [UnityTest]
        public IEnumerator PlayingACardClosesTheGapAroundTheMiddle()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);

            Play(view, 0, view.Enemies[1].TargetArea);

            // The five left take the five places of a five-card fan, in their order.
            int count = view.Cards.Length - 1;
            for (int slot = 0; slot < count; slot++)
            {
                var (expected, _) = BattleInspectView.FanPose(
                    slot,
                    count,
                    view.Settings.FanStep,
                    view.Settings.FanRadius,
                    view.Settings.FanDrop,
                    view.Settings.RestBottom
                );
                Assert.That(
                    Vector2.Distance(view.RestPosition(slot + 1), expected),
                    Is.LessThan(0.01f)
                );
            }
            float sum = Enumerable.Range(1, count).Sum(i => view.RestPosition(i).x);
            Assert.That(sum, Is.EqualTo(0f).Within(0.5f), "The fan is centred.");
        }

        [UnityTest]
        public IEnumerator ReleasingWithoutASwipeReturnsTheCard()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var slash = view.Cards[0];
            var point = CardPoint(slash);

            view.PressCard(0, point);
            view.ReleaseCard(point + Vector2.up * 40f);

            Assert.That(slash.Used, Is.False);
            Assert.That(view.HeldCard, Is.EqualTo(-1));
            Assert.That(view.Energy, Is.EqualTo(view.MaxEnergy));
            Assert.That(view.Enemies.All(enemy => enemy.Hp == enemy.MaxHp), Is.True);
        }

        [UnityTest]
        public IEnumerator AWholeSideCardNeedsOnlyASwipe()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int thunderIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Effect == BattleInspectCardEffect.DamageAll
            );
            var point = CardPoint(view.Cards[thunderIndex]);

            // Straight up, not onto any enemy.
            view.PressCard(thunderIndex, point);
            view.DragCard(point + Vector2.up * 400f);
            Assert.That(view.Targets, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(view.Enemies.All(enemy => view.IsGlowing(enemy.Sprite)), Is.True);
            view.ReleaseCard(point + Vector2.up * 400f);

            Assert.That(view.Enemies.All(enemy => enemy.Hp < enemy.MaxHp), Is.True);
        }

        [UnityTest]
        public IEnumerator HealDroppedOnAnAllyHealsThatAlly()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int healIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Effect == BattleInspectCardEffect.Heal
            );
            var heal = view.Cards[healIndex];
            var ally = view.Allies[0];
            int before = ally.Hp;

            view.PressCard(healIndex, CardPoint(heal));
            view.DragCard(Center(ally.TargetArea));
            Assert.That(view.TargetsEnemies, Is.False);
            Assert.That(view.Targets, Is.EqualTo(new[] { 0 }));
            Assert.That(view.IsGlowing(ally.Sprite), Is.True);
            view.ReleaseCard(Center(ally.TargetArea));

            Assert.That(ally.Hp, Is.EqualTo(Mathf.Min(ally.MaxHp, before + heal.Power)));
            Assert.That(view.Allies.Skip(1).All(other => other.Hp == other.StartHp), Is.True);
        }

        [UnityTest]
        public IEnumerator HittingAHiddenWeaknessRevealsIt()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            int fireIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Fire
            );
            var fire = view.Cards[fireIndex];
            var hidden = wolf.Weaknesses.Single(weakness =>
                weakness.Element == BattleInspectElement.Fire
            );
            Assert.That(hidden.Unknown.activeSelf, Is.True, "A hidden weakness shows ?.");
            Assert.That(hidden.Known.activeSelf, Is.False);

            Play(view, fireIndex, wolf.TargetArea);

            Assert.That(wolf.Hp, Is.EqualTo(wolf.MaxHp - Mathf.RoundToInt(fire.Power * 1.5f)));
            Assert.That(hidden.Known.activeSelf, Is.True);
            Assert.That(hidden.Unknown.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ACardCostingMoreThanTheEnergyIsNotPlayed()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var guardian = view.Enemies[2];
            int thunderIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Effect == BattleInspectCardEffect.DamageAll
            );

            // Spend down to 1 energy with the two cost-2 cards, then try the cost-3 card.
            for (int i = 0; i < view.Cards.Length; i++)
                if (view.Cards[i].Cost == 2)
                    Play(view, i, guardian.TargetArea);
            Assert.That(view.Energy, Is.EqualTo(1));
            Play(view, thunderIndex, guardian.TargetArea);

            Assert.That(view.Cards[thunderIndex].Used, Is.False);
            Assert.That(view.Energy, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EndTurnRefillsTheEnergyAndTheHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            Play(view, 0, view.Enemies[1].TargetArea);

            view.EndTurnButton.onClick.Invoke();
            Assert.That(view.Turn, Is.EqualTo(4));
            Assert.That(view.Energy, Is.EqualTo(6));
            Assert.That(view.Cards.Any(card => card.Used), Is.False);
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
            Assert.That(view.TurnSlots[1].Enemy.texture, Is.SameAs(view.Enemies[1].Sprite.texture));

            // Ending the turn passes the enemy turns (they do not act in the mock).
            view.EndTurnButton.onClick.Invoke();
            Assert.That(view.UpcomingTurns(6), Is.EqualTo(new[] { party, party, 2, party, 1, 0 }));
            Assert.That(view.TurnSlots[1].Party.activeSelf, Is.True);

            // A defeated enemy leaves the order.
            var slime = view.Enemies[0];
            slime.Hp = 1;
            Play(view, 0, slime.TargetArea);
            Assert.That(slime.Alive, Is.False);
            Assert.That(view.UpcomingTurns(6), Has.No.Member(0));
        }

        /// <summary>Presses a card, swipes it onto the target and lets go.</summary>
        private static void Play(BattleInspectView view, int card, RectTransform target)
        {
            view.PressCard(card, CardPoint(view.Cards[card]));
            view.DragCard(Center(target));
            view.ReleaseCard(Center(target));
        }

        /// <summary>A point on the card's name row, which stays on screen while the card rests.</summary>
        private static Vector2 CardPoint(BattleInspectCard card)
        {
            var rect = card.Body.rect;
            return RectTransformUtility.WorldToScreenPoint(
                null,
                card.Body.TransformPoint(new Vector2(rect.center.x, rect.yMax - 40f))
            );
        }

        private static Vector2 Center(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

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
