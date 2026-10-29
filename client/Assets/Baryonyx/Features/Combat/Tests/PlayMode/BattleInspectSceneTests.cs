using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.UI;
using Baryonyx.Vfx.Hd2d;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class BattleInspectSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity";
        private Scene loadedScene;

        private FixedRandom random;

        // 手札を配る順、狙う相手、敵の行動を、いつも同じ乱数で決める。
        [SetUp]
        public void FixRandom() => random = FixedRandom.Use();

        [UnityTest]
        public IEnumerator SceneShowsPartyEnemiesAndHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);

            Assert.That(view.IdleActors, Has.Length.EqualTo(7), "4 allies and 3 enemies");
            Assert.That(view.Allies, Has.Length.EqualTo(4));
            Assert.That(view.Enemies, Has.Length.EqualTo(3));
            Assert.That(view.Cards, Has.Length.EqualTo(16), "The deck is 16 cards.");
            Assert.That(view.HandCards, Has.Count.EqualTo(5), "The opening hand is 5 cards.");
            Assert.That(view.DeckLabel.text, Is.EqualTo("11"));
            Assert.That(view.Enemies.Select(enemy => enemy.Sprite.texture), Has.None.Null);
            Assert.That(view.MaxEnergy, Is.EqualTo(3), "Every turn begins with 3 energy.");
            Assert.That(view.EnergyLabel.text, Is.EqualTo("3"), "Only the energy left shows.");
            Assert.That(view.HeldCard, Is.EqualTo(-1));

            foreach (var card in view.Cards)
            {
                var face = card.Body.GetComponent<BattleInspectCardView>();
                Assert.That(face.Owner.text, Is.Not.Empty);
                Assert.That(face.Kind.text, Does.Match("攻撃|回復|防御|強化|弱体|支援"));
                // A card with a power shows it as a part of a stat ("属攻の100%"), not the
                // ally's power; a card such as 偵察 has none.
                var first = CardSkills
                    .Find(view.Deck[card.DeckIndex].Skill)
                    ?.Powered()
                    .FirstOrDefault();
                if (first != null)
                {
                    Assert.That(face.Description.text, Does.Contain(CardRules.PartName(first)));
                    Assert.That(face.Description.text, Does.Not.Contain(">" + card.Power + "<"));
                }
                Assert.That(
                    face.Element.enabled,
                    Is.EqualTo(card.Element != BattleInspectElement.None),
                    "Cards without an element show no element icon."
                );
            }
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
            Assert.That(view.Enemies.All(enemy => enemy.Marker.gameObject.activeSelf), Is.True);
            Assert.That(view.Allies.Any(ally => ally.Marker.gameObject.activeSelf), Is.False);
            view.DragCard(Center(wolf.TargetArea));
            Assert.That(view.Armed, Is.True, "Moving up to the wolf is a swipe up.");
            Assert.That(view.Targets, Is.EqualTo(new[] { 1 }));
            Assert.That(view.IsGlowing(wolf.Sprite), Is.True);
            Assert.That(view.IsGlowing(view.Enemies[0].Sprite), Is.False);
            yield return null;
            Assert.That(view.Aim.gameObject.activeSelf, Is.True, "Dots run to the finger.");
            Assert.That(view.AimDots[0].gameObject.activeSelf, Is.True);

            view.ReleaseCard(Center(wolf.TargetArea));
            Assert.That(view.HeldCard, Is.EqualTo(-1));
            Assert.That(view.IsGlowing(wolf.Sprite), Is.False);
            Assert.That(view.Enemies.Any(enemy => enemy.Marker.gameObject.activeSelf), Is.False);
            yield return null;
            Assert.That(view.Aim.gameObject.activeSelf, Is.False);
            Assert.That(slash.Place, Is.EqualTo(BattleInspectCardPlace.Discard));
            Assert.That(view.Energy, Is.EqualTo(view.MaxEnergy - slash.Cost));
            yield return WaitActions(view);
            int dealt = Dealt(view, slash, wolf);
            Assert.That(dealt, Is.LessThan(slash.Power), "The wolf's defense softens the blow.");
            Assert.That(wolf.Hp, Is.EqualTo(wolf.MaxHp - dealt));
            Assert.That(
                wolf.Gauge.Ratio,
                Is.EqualTo(wolf.Hp / (float)wolf.MaxHp).Within(1f / wolf.Gauge.InnerDots)
            );
            Assert.That(Rising(view.DamageNumber), Is.EqualTo(new[] { dealt.ToString() }));
            Assert.That(Rising(view.WeakNumber), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ADropJustOutsideAnEnemyStillCountsOnIt()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var guardian = view.Enemies[2];
            var area = guardian.TargetArea;
            // Just past the right edge of the guardian's drawn body, where no other enemy is.
            var outside = BattleInspectView.ScreenPointOf(
                area,
                new Vector2(area.rect.xMax + 40f, area.rect.center.y)
            );

            view.PressCard(0, CardPoint(view.Cards[0]));
            view.DragCard(outside);
            Assert.That(view.Targets, Is.EqualTo(new[] { 2 }));
            view.ReleaseCard(outside);
            yield return WaitActions(view);

            Assert.That(guardian.Hp, Is.LessThan(guardian.MaxHp));
        }

        [UnityTest]
        public IEnumerator PlayingACardClosesTheGapAroundTheMiddle()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);

            Play(view, 0, view.Enemies[1].TargetArea);

            // The four left take the four places of a four-card fan, in their order; those the
            // energy left cannot pay for rest lower.
            int count = view.HandCards.Count;
            Assert.That(count, Is.EqualTo(4));
            for (int slot = 0; slot < count; slot++)
            {
                var expected = FanPosition(view, slot);
                expected.y -= BattleInspectView.RestDrop(
                    view.Settings,
                    view.Cards[view.HandCards[slot]].Cost <= view.Energy
                );
                Assert.That(
                    Vector2.Distance(view.RestPosition(view.HandCards[slot]), expected),
                    Is.LessThan(0.01f)
                );
            }
            float sum = view.HandCards.Sum(i => view.RestPosition(i).x);
            Assert.That(sum, Is.EqualTo(0f).Within(0.5f), "The fan is centred.");
        }

        [UnityTest]
        public IEnumerator ReleasingWithoutASwipeReturnsTheCard()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var slash = view.Cards[0];
            var point = CardPoint(slash);

            // Moved further than a tap, but not far enough up to aim.
            view.PressCard(0, point);
            view.ReleaseCard(point + Vector2.up * 70f);

            Assert.That(slash.InHand, Is.True);
            Assert.That(view.HeldCard, Is.EqualTo(-1));
            Assert.That(view.Energy, Is.EqualTo(view.MaxEnergy));
            Assert.That(view.Enemies.All(enemy => enemy.Hp == enemy.MaxHp), Is.True);
        }

        [UnityTest]
        public IEnumerator ATappedCardStaysUpUntilItsTargetIsTapped()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            var slash = view.Cards[0];
            var point = CardPoint(slash);

            view.PressCard(0, point);
            view.ReleaseCard(point);
            Assert.That(view.Selected, Is.True, "A tap leaves the card up.");
            Assert.That(view.HeldCard, Is.EqualTo(0));
            Assert.That(view.Targets, Is.Empty, "Nothing glows until a target is tapped.");
            Assert.That(view.Enemies.All(enemy => enemy.Marker.gameObject.activeSelf), Is.True);
            yield return null;
            Assert.That(view.TapArea.activeSelf, Is.True);
            Assert.That(view.Aim.gameObject.activeSelf, Is.False);

            view.TapScreen(Center(wolf.TargetArea));
            Assert.That(view.HeldCard, Is.EqualTo(-1));
            Assert.That(slash.InHand, Is.False);
            Assert.That(view.Enemies.Any(enemy => enemy.Marker.gameObject.activeSelf), Is.False);
            yield return null;
            Assert.That(
                view.TapArea.activeSelf,
                Is.True,
                "With no card up, the battlefield takes taps for the status list."
            );
            Assert.That(view.StatusListOpen, Is.False, "Playing the card lists no statuses.");
            yield return WaitActions(view);
            Assert.That(wolf.Hp, Is.EqualTo(wolf.MaxHp - Dealt(view, slash, wolf)));
        }

        [UnityTest]
        public IEnumerator TheKeywordNotesStandAwayFromTheCardsTargets()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var tips = (RectTransform)view.KeywordTips.transform;
            var checkedSides = new HashSet<bool>();
            // ガード (for the party, with [ブロック]) in hand with the cards for the enemies.
            DealInto(view, "Guard");

            foreach (int index in view.HandCards.ToArray())
            {
                var card = view.Cards[index];
                var skill = CardSkills.Find(view.Deck[card.DeckIndex].Skill);
                if (skill == null || CardKeywords.In(skill.Text).Count == 0)
                    continue;
                var point = CardPoint(card);
                view.PressCard(index, point);
                view.ReleaseCard(point);
                yield return null;
                Assert.That(view.KeywordTips.gameObject.activeSelf, Is.True);
                // The card stands in the middle; the notes keep to the side its targets are not on.
                float left = RectTransformUtility
                    .WorldToScreenPoint(null, tips.TransformPoint(tips.rect.min))
                    .x;
                float right = RectTransformUtility
                    .WorldToScreenPoint(null, tips.TransformPoint(tips.rect.max))
                    .x;
                float middle = Screen.width * 0.5f;
                if (card.TargetsEnemies)
                    Assert.That(right, Is.LessThan(middle), $"{skill.Name}: left of the card");
                else
                    Assert.That(left, Is.GreaterThan(middle), $"{skill.Name}: right of the card");
                checkedSides.Add(card.TargetsEnemies);
            }
            Assert.That(checkedSides, Is.EquivalentTo(new[] { true, false }), "Both kinds held.");
        }

        [UnityTest]
        public IEnumerator ATappedCardGoesBackOnATapOnNoTarget()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var point = CardPoint(view.Cards[0]);

            view.PressCard(0, point);
            view.ReleaseCard(point);
            // Top middle of the screen, far from every character.
            view.TapScreen(new Vector2(Screen.width * 0.5f, Screen.height - 4f));
            Assert.That(view.HeldCard, Is.EqualTo(-1));

            // Tapping another card while one waits swaps them.
            var second = CardPoint(view.Cards[1]);
            view.PressCard(0, point);
            view.ReleaseCard(point);
            view.PressCard(1, second);
            view.ReleaseCard(second);
            Assert.That(view.HeldCard, Is.EqualTo(1));

            Assert.That(view.HandCards, Has.Count.EqualTo(5), "Nothing was played.");
            Assert.That(view.Energy, Is.EqualTo(view.MaxEnergy));
            Assert.That(view.Enemies.All(enemy => enemy.Hp == enemy.MaxHp), Is.True);
        }

        [UnityTest]
        public IEnumerator ASecondTapOnAOneTargetCardPlaysItOnOneTargetAtRandom()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var slash = view.Cards[0];
            var point = CardPoint(slash);

            view.PressCard(0, point);
            view.ReleaseCard(point);
            yield return TapRaisedCard(view, slash);
            Assert.That(view.HeldCard, Is.EqualTo(-1));
            Assert.That(slash.InHand, Is.False, "A second tap plays the card.");
            Assert.That(view.Energy, Is.EqualTo(view.MaxEnergy - slash.Cost));
            yield return WaitActions(view);
            Assert.That(view.Enemies.Count(enemy => enemy.Hp < enemy.MaxHp), Is.EqualTo(1));

            int healIndex = System.Array.FindIndex(
                view.Cards,
                card => card.InHand && card.Effect == BattleInspectCardEffect.Heal
            );
            Assert.That(healIndex, Is.GreaterThanOrEqualTo(0), "A heal is in the hand.");
            var heal = view.Cards[healIndex];
            point = CardPoint(heal);
            view.PressCard(healIndex, point);
            view.ReleaseCard(point);
            yield return TapRaisedCard(view, heal);
            yield return WaitActions(view);
            Assert.That(heal.InHand, Is.False);
            Assert.That(
                Rising(view.HealNumber),
                Is.EqualTo(new[] { heal.Power.ToString() }),
                "One ally is healed."
            );
        }

        [UnityTest]
        public IEnumerator ASecondTapOnAWholeSideCardPlaysItOnTheWholeSide()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int thunderIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Effect == BattleInspectCardEffect.DamageAll
            );
            var point = CardPoint(view.Cards[thunderIndex]);

            view.PressCard(thunderIndex, point);
            view.ReleaseCard(point);
            yield return TapRaisedCard(view, view.Cards[thunderIndex]);
            // The bolts strike one enemy after another, so the numbers are counted as they rise.
            var numbers = new HashSet<TMP_Text>();
            for (float t = 0f; view.Acting && t < 5f; t += Time.unscaledDeltaTime)
            {
                numbers.UnionWith(RisingLabels(view.DamageNumber));
                numbers.UnionWith(RisingLabels(view.WeakNumber));
                yield return null;
            }
            Assert.That(view.Acting, Is.False, "The actions end.");
            Assert.That(view.Cards[thunderIndex].InHand, Is.False);
            Assert.That(view.Enemies.All(enemy => enemy.Hp < enemy.MaxHp), Is.True);
            Assert.That(numbers, Has.Count.EqualTo(view.Enemies.Length), "Each enemy is hit once.");
        }

        [UnityTest]
        public IEnumerator ATappedWholeSideCardIsPlayedByTappingAnyOfItsSide()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int thunderIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Effect == BattleInspectCardEffect.DamageAll
            );
            var point = CardPoint(view.Cards[thunderIndex]);

            view.PressCard(thunderIndex, point);
            view.ReleaseCard(point);
            Assert.That(view.Selected, Is.True);
            Assert.That(view.Enemies.All(enemy => view.IsGlowing(enemy.Sprite)), Is.True);

            view.TapScreen(Center(view.Enemies[0].TargetArea));
            yield return WaitActions(view);
            Assert.That(view.Enemies.All(enemy => enemy.Hp < enemy.MaxHp), Is.True);
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

            // Its targets glow from the press, before any swipe.
            view.PressCard(thunderIndex, point);
            Assert.That(view.Armed, Is.False);
            Assert.That(view.Enemies.All(enemy => view.IsGlowing(enemy.Sprite)), Is.True);

            // Straight up, not onto any enemy.
            view.DragCard(point + Vector2.up * 400f);
            Assert.That(view.Targets, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(view.Enemies.All(enemy => view.IsGlowing(enemy.Sprite)), Is.True);
            view.ReleaseCard(point + Vector2.up * 400f);
            yield return WaitActions(view);

            Assert.That(view.Enemies.All(enemy => enemy.Hp < enemy.MaxHp), Is.True);
        }

        [UnityTest]
        public IEnumerator AWholeSideCardMovedWithoutASwipeStopsGlowingAndIsNotPlayed()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int guardIndex = DealInto(view, "Guard");
            Assert.That(view.Cards[guardIndex].Effect, Is.EqualTo(BattleInspectCardEffect.Guard));
            var point = CardPoint(view.Cards[guardIndex]);
            int energy = view.Energy;

            view.PressCard(guardIndex, point);
            Assert.That(view.TargetsEnemies, Is.False);
            Assert.That(view.Allies.All(ally => view.IsGlowing(ally.Sprite)), Is.True);
            // Moved sideways: more than a tap, but not a swipe up.
            view.ReleaseCard(point + Vector2.right * 70f);

            Assert.That(view.Allies.Any(ally => view.IsGlowing(ally.Sprite)), Is.False);
            Assert.That(view.Energy, Is.EqualTo(energy));
            Assert.That(view.Cards[guardIndex].InHand, Is.True);
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
            yield return WaitActions(view);

            Assert.That(ally.Hp, Is.EqualTo(Mathf.Min(ally.MaxHp, before + heal.Power)));
            Assert.That(Rising(view.HealNumber), Is.EqualTo(new[] { heal.Power.ToString() }));
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
            for (float t = 0f; !hidden.Revealed && t < 5f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(hidden.Revealed, Is.True, "The blow reveals the weakness.");
            yield return WaitActions(view);

            int weak = Mathf.RoundToInt(Dealt(view, fire, wolf) * 1.5f);
            Assert.That(wolf.Hp, Is.EqualTo(wolf.MaxHp - weak));
            Assert.That(
                Rising(view.WeakNumber),
                Is.EqualTo(new[] { weak.ToString() }),
                "A weakness shows only in the number's look, without words."
            );
            Assert.That(Rising(view.DamageNumber), Is.Empty);
            Assert.That(hidden.Known.activeSelf, Is.True);
            Assert.That(hidden.Unknown.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ACardCostingMoreThanTheEnergyIsNotPlayed()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var guardian = view.Enemies[2];
            // The meteor (cost 3) costs more than the energy the fire leaves, to show how such a
            // card looks.
            int iceIndex = DealInto(view, "Meteor");
            int fireIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Fire
            );

            // Spend some energy with the fire, then try the card the energy cannot pay for.
            Play(view, fireIndex, guardian.TargetArea);
            int left = view.MaxEnergy - view.Cards[fireIndex].Cost;
            Assert.That(view.Energy, Is.EqualTo(left));
            Assert.That(view.Cards[iceIndex].Cost, Is.GreaterThan(left));
            Play(view, iceIndex, guardian.TargetArea);

            Assert.That(view.Cards[iceIndex].InHand, Is.True);
            Assert.That(view.Energy, Is.EqualTo(left));
            // The warning takes the place of the skill name.
            Assert.That(view.SkillBanner.gameObject.activeSelf, Is.True);
            Assert.That(view.SkillBanner.Label.text, Is.EqualTo("エネルギー不足"));
        }

        [UnityTest]
        public IEnumerator EndTurnRefillsTheEnergyFlowsTheHandAndDrawsFive()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            Play(view, 0, view.Enemies[1].TargetArea);

            view.EndTurnButton.onClick.Invoke();
            Assert.That(view.EnemyTurn, Is.True);
            Assert.That(view.EndTurnButton.interactable, Is.False);
            yield return WaitEnemyTurn(view);
            Assert.That(view.Turn, Is.EqualTo(view.StartTurn + 1));
            Assert.That(view.Energy, Is.EqualTo(BattleInspectView.EnergyPerTurn));
            yield return WaitDealt(view);
            // The hand flowed away; new cards come in at the left end, the side of the deck.
            Assert.That(DeckCards(view), Is.EqualTo(new[] { 9, 8, 7, 6, 5 }));
            Assert.That(view.DeckLabel.text, Is.EqualTo("6"));
            Assert.That(view.DiscardLabel.text, Is.EqualTo("5"), "The played card and the four that flowed.");
            // The played card's place on screen shows a newly drawn card of the deck.
            Assert.That(view.Cards[0].InHand, Is.True);
            Assert.That(view.Cards[0].DeckIndex, Is.Not.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SpendingTheLastEnergyEndsTheTurnOnceTheCardsHaveActed()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var slime = view.Enemies[0];
            var wolf = view.Enemies[1];
            int fire = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Fire
            );
            int thunder = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Thunder
            );
            Assert.That(view.Cards[fire].Cost + view.Cards[thunder].Cost, Is.EqualTo(view.Energy));

            // With energy left, the turn goes on.
            Play(view, fire, wolf.TargetArea);
            yield return WaitActions(view);
            Assert.That(view.Energy, Is.GreaterThan(0));
            Assert.That(view.EnemyTurn, Is.False);

            // The last of it is spent, but the card acts before the turn ends.
            Play(view, thunder, wolf.TargetArea);
            Assert.That(view.Energy, Is.EqualTo(0));
            Assert.That(view.EnemyTurn, Is.False);
            for (float t = 0f; !view.EnemyTurn && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.EnemyTurn, Is.True, "The spent energy ends the turn.");
            Assert.That(slime.Hp, Is.LessThan(slime.MaxHp), "The thunder landed first.");
            Assert.That(view.EndTurnButton.interactable, Is.False);

            yield return WaitEnemyTurn(view);
            Assert.That(view.Turn, Is.EqualTo(view.StartTurn + 1));
            Assert.That(view.Energy, Is.EqualTo(BattleInspectView.EnergyPerTurn));
        }

        [UnityTest]
        public IEnumerator ACardThatCostsNothingKeepsTheTurnWithNoEnergy()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            var slash = view.Cards[0];
            // As if its cost had been lowered to nothing.
            slash.Cost = 0;
            int fire = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Fire
            );
            int thunder = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Thunder
            );

            Play(view, fire, wolf.TargetArea);
            Play(view, thunder, wolf.TargetArea);
            Assert.That(view.Energy, Is.EqualTo(0));
            yield return WaitActions(view);
            Assert.That(view.EnemyTurn, Is.False, "The free card can still be played.");

            Play(view, 0, wolf.TargetArea);
            Assert.That(slash.InHand, Is.False);
            for (float t = 0f; !view.EnemyTurn && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.EnemyTurn, Is.True, "With no card left to pay for, the turn ends.");
            yield return WaitEnemyTurn(view);
        }

        [UnityTest]
        public IEnumerator EnergyLeftOverForNoCardInHandEndsTheTurn()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            int slash = System.Array.FindIndex(view.Cards, card => card.InHand && card.DeckIndex == 0);
            // Every other card in hand costs more than the energy the slash leaves.
            foreach (int i in view.HandCards)
                view.Cards[i].Cost = i == slash ? 1 : view.Energy;
            Assert.That(view.EndTurnButton.interactable, Is.True);

            Play(view, slash, wolf.TargetArea);
            Assert.That(view.Energy, Is.GreaterThan(0));
            Assert.That(view.EnemyTurn, Is.False, "The card acts before the turn ends.");
            Assert.That(
                view.EndTurnButton.interactable,
                Is.False,
                "With no card to pay for, the button cannot be pressed."
            );
            for (float t = 0f; !view.EnemyTurn && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.EnemyTurn, Is.True, "The energy left pays for no card.");
            yield return WaitEnemyTurn(view);
        }

        [UnityTest]
        public IEnumerator PlayModeStartsWithNoCardsAndDealsTheOpeningHandOneByOne()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value, dealt: false);
            Assert.That(view.Dealing, Is.True);
            Assert.That(view.HandCards, Is.Empty, "No cards before the deal.");
            Assert.That(view.DeckLabel.text, Is.EqualTo("16"));
            Assert.That(view.Cards.All(card => card.Group.alpha == 0f), Is.True);

            // Ending the turn waits for the deal.
            view.EndTurnButton.onClick.Invoke();
            Assert.That(view.EnemyTurn, Is.False);
            Assert.That(view.Turn, Is.EqualTo(view.StartTurn));

            // One card leaves the deck at a time, face down, and the count drops with it. Cards
            // already dealt stay where they landed.
            int seen = 0;
            bool sawFaceDown = false;
            var landed = new System.Collections.Generic.Dictionary<int, Vector2>();
            for (float t = 0f; view.Dealing && t < 5f; t += Time.deltaTime)
            {
                int count = view.HandCards.Count;
                Assert.That(count, Is.LessThanOrEqualTo(seen + 1), "One card at a time.");
                Assert.That(view.DeckLabel.text, Is.EqualTo((16 - count).ToString()));
                // A card is first seen on its back.
                if (count > seen)
                    Assert.That(view.Cards[view.HandCards[0]].Back.activeSelf, Is.True);
                foreach (int index in view.HandCards)
                {
                    if (view.IsFlying(index))
                    {
                        if (view.IsFaceDown(index))
                        {
                            sawFaceDown = true;
                            Assert.That(view.Cards[index].Back.activeSelf, Is.True);
                        }
                    }
                    else if (landed.TryGetValue(index, out var at))
                        Assert.That(
                            Vector2.Distance(view.RestPosition(index), at),
                            Is.LessThan(0.01f),
                            "A dealt card stays put."
                        );
                    else
                        landed[index] = view.RestPosition(index);
                }
                seen = count;
                yield return null;
            }
            Assert.That(view.Dealing, Is.False);
            Assert.That(sawFaceDown, Is.True, "Cards fly in on their backs.");
            Assert.That(view.HandCards, Has.Count.EqualTo(5));
            Assert.That(view.DeckLabel.text, Is.EqualTo("11"));
            yield return null;
            foreach (int index in view.HandCards)
            {
                Assert.That(view.IsFaceDown(index), Is.False);
                Assert.That(view.Cards[index].Back.activeSelf, Is.False);
                Assert.That(view.Cards[index].Group.alpha, Is.GreaterThan(0f));
            }
        }

        [UnityTest]
        public IEnumerator DrawingStopsAtTheHandLimitAndLeavesTheRestInTheDeck()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            // A copy of the settings for this battle only, with a hand of three.
            view.Settings = Object.Instantiate(view.Settings);
            view.Settings.HandLimit = 3;

            view.RestartDeal(Enumerable.Range(0, view.Cards.Length).ToArray(), instant: true);
            Assert.That(view.HandCards, Has.Count.EqualTo(3), "A full hand draws nothing more.");
            Assert.That(view.DeckLabel.text, Is.EqualTo("13"));
        }

        [UnityTest]
        public IEnumerator TheHandFlowsAwayAndADryDeckIsMadeAnewFromTheDiscardPile()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);

            // 5 dealt, 11 left; each turn the hand flows away and 5 are drawn.
            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            yield return WaitDealt(view);
            Assert.That(view.HandCards, Has.Count.EqualTo(5));
            Assert.That(view.DeckLabel.text, Is.EqualTo("6"));
            Assert.That(view.DiscardLabel.text, Is.EqualTo("5"));

            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            yield return WaitDealt(view);
            // 居合一閃 (保持) stays in the hand; the other four flowed away.
            Assert.That(view.DeckLabel.text, Is.EqualTo("1"));
            Assert.That(view.DiscardLabel.text, Is.EqualTo("9"));
            Assert.That(view.HandCards.Select(i => view.Cards[i].DeckIndex), Has.Member(DeckIndexOf(view, "Iai")));

            // Only the one card is left: the discard pile is shuffled into a new deck, and the
            // cards in hand are not in it, so none is in the hand twice.
            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            yield return WaitDealt(view);
            var inHand = view.HandCards.Select(i => view.Cards[i].DeckIndex).ToArray();
            Assert.That(inHand, Is.Unique);
            Assert.That(inHand.Concat(view.DrawPile).Concat(view.DiscardPile), Is.Unique);
            Assert.That(view.DiscardLabel.text, Is.EqualTo(view.DiscardPile.Count.ToString()));
        }

        [UnityTest]
        public IEnumerator AFallenAllysCardsCrumbleOutOfTheHandAndComeBackWhenItStandsUp()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int healer = view.Deck[DeckIndexOf(view, "Heal")].Caster;
            int fallen = view
                .HandCards.Select(i => view.Cards[i].Caster)
                .First(caster => caster != healer);
            var theirs = Enumerable
                .Range(0, view.Deck.Length)
                .Where(i => view.Deck[i].Caster == fallen)
                .ToArray();
            int inHand = view.HandCards.Count(i => view.Cards[i].Caster == fallen);
            int handBefore = view.HandCards.Count;

            view.Allies[fallen].Hp = 0;
            yield return null;

            Assert.That(view.HandCards.Select(i => view.Cards[i].Caster), Has.No.Member(fallen));
            Assert.That(view.HandCards, Has.Count.EqualTo(handBefore - inHand));
            Assert.That(view.DrawPile.Intersect(theirs), Is.Empty, "Its cards leave the deck.");
            Assert.That(view.DeckLabel.text, Is.EqualTo(view.DrawPile.Count.ToString()));
            // A copy of each card crumbles over the hand, then is gone.
            var layer = view.Hand.parent.Find("VanishingCards");
            Assert.That(layer, Is.Not.Null);
            Assert.That(layer.childCount, Is.GreaterThanOrEqualTo(inHand));
            for (float t = 0f; layer.childCount > 0 && t < 3f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(layer.childCount, Is.Zero, "The crumbling copies and motes are gone.");

            // A deck made anew leaves them out.
            view.RestartDeal(instant: true);
            Assert.That(
                view.HandCards.Select(i => view.Cards[i].DeckIndex).Concat(view.DrawPile),
                Is.EquivalentTo(Enumerable.Range(0, view.Deck.Length).Except(theirs))
            );

            // A heal is for those standing.
            int heal = DealInto(view, "Heal");
            Assert.That(heal, Is.GreaterThanOrEqualTo(0));
            var target = view.Allies[fallen];
            view.PressCard(heal, CardPoint(view.Cards[heal]));
            view.DragCard(Center(target.TargetArea));
            Assert.That(view.Targets, Has.No.Member(fallen));
            Assert.That(target.Marker.gameObject.activeSelf, Is.False);
            Assert.That(view.Allies[healer].Marker.gameObject.activeSelf, Is.True);
            view.ReleaseCard(Center(target.TargetArea));
            yield return WaitActions(view);
            Assert.That(target.Down, Is.True);

            // Up again in the party's turn, its cards are shuffled back into the deck and it
            // draws two of them.
            view.Allies[fallen].Hp = view.Allies[fallen].MaxHp;
            yield return null;
            yield return WaitDealt(view);
            var drawn = view.HandCards.Select(i => view.Cards[i].DeckIndex).Intersect(theirs).ToArray();
            Assert.That(drawn, Has.Length.EqualTo(System.Math.Min(BattleInspectView.RisingDraw, theirs.Length)));
            Assert.That(view.DrawPile.Concat(drawn).Intersect(theirs), Is.EquivalentTo(theirs));
            Assert.That(view.DeckLabel.text, Is.EqualTo(view.DrawPile.Count.ToString()));
        }

        [UnityTest]
        public IEnumerator AnAllyFelledByAnEnemyLosesItsCardsAtOnce()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            // Any blow fells any of them.
            foreach (var ally in view.Allies)
                ally.Hp = 1;

            view.EndTurnButton.onClick.Invoke();
            for (float t = 0f; view.EnemyTurn && t < 10f; t += Time.deltaTime)
            {
                foreach (var down in Enumerable.Range(0, 4).Where(i => view.Allies[i].Down))
                {
                    Assert.That(
                        view.HandCards.Select(i => view.Cards[i].Caster),
                        Has.No.Member(down)
                    );
                    Assert.That(
                        view.DrawPile.Select(i => view.Deck[i].Caster),
                        Has.No.Member(down)
                    );
                }
                yield return null;
            }
            yield return WaitDealt(view);
            var fallen = Enumerable.Range(0, 4).Where(i => view.Allies[i].Down).ToArray();
            Assert.That(fallen, Is.Not.Empty, "The enemies felled someone.");
            Assert.That(fallen, Has.Length.LessThan(4), "Someone still stands.");
            Assert.That(
                view.HandCards.Select(i => view.Cards[i].Caster).Intersect(fallen),
                Is.Empty,
                "The cards drawn are of those standing."
            );
        }

        /// <summary>The cards of the deck in hand, left to right.</summary>
        private static int[] DeckCards(BattleInspectView view) =>
            view.HandCards.Select(i => view.Cards[i].DeckIndex).ToArray();

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

            // Ending the turn goes through the enemies' turns to the next party turn.
            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            Assert.That(view.UpcomingTurns(6), Is.EqualTo(new[] { party, party, 2, party, 1, 0 }));
            Assert.That(view.TurnSlots[1].Party.activeSelf, Is.True);
            yield return WaitDealt(view);

            // A defeated enemy leaves the order.
            var slime = view.Enemies[0];
            slime.Hp = 1;
            int blow = view.HandCards.First(i =>
                view.Cards[i].TargetsEnemies && view.Cards[i].Cost <= view.Energy
            );
            Play(view, blow, slime.TargetArea);
            yield return WaitActions(view);
            Assert.That(slime.Alive, Is.False);
            Assert.That(view.UpcomingTurns(6), Has.No.Member(0));
        }

        [UnityTest]
        public IEnumerator ThePlayedCardsNameIsShownWhileItActsAndItsUserComesBack()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var slash = view.Cards[0];
            var user = view.Allies[slash.Caster];
            var home = user.Body.anchoredPosition;
            var barHome = user.HpBar.anchoredPosition;
            Assert.That(view.SkillBanner.gameObject.activeSelf, Is.False);

            Play(view, 0, view.Enemies[1].TargetArea);
            Assert.That(view.Acting, Is.True);
            Assert.That(view.SkillBanner.gameObject.activeSelf, Is.True);
            Assert.That(view.SkillBanner.Label.text, Is.EqualTo("斬り払い"));

            // The blow lands as the skill's effect strikes.
            var wolf = view.Enemies[1];
            for (float t = 0f; wolf.Hp == wolf.MaxHp && t < 2f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(wolf.Hp, Is.LessThan(wolf.MaxHp));

            yield return WaitActions(view);
            Assert.That(user.Body.anchoredPosition, Is.EqualTo(home));
            Assert.That(user.HpBar.anchoredPosition, Is.EqualTo(barHome));
            for (
                float t = 0f;
                view.SkillBanner.gameObject.activeSelf && t < 1f;
                t += Time.deltaTime
            )
                yield return null;
            Assert.That(view.SkillBanner.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ARaisedCardsUserIsShownWithARingAndItsName()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var slash = view.Cards[0];
            var user = view.Allies[slash.Caster];
            var homes = view.Allies.Select(ally => ally.Body.anchoredPosition).ToArray();
            var barHome = user.HpBar.anchoredPosition;
            Assert.That(
                view.Allies.Any(ally => ally.CasterRing.activeSelf || ally.NameTag.activeSelf),
                Is.False
            );

            var point = CardPoint(slash);
            view.PressCard(0, point);
            view.ReleaseCard(point);
            yield return WaitStep(view);
            for (int i = 0; i < view.Allies.Length; i++)
            {
                var ally = view.Allies[i];
                bool isUser = i == slash.Caster;
                Assert.That(ally.CasterRing.activeSelf, Is.EqualTo(isUser));
                Assert.That(ally.NameTag.activeSelf, Is.EqualTo(isUser));
            }
            Assert.That(
                user.NameTag.GetComponent<TranslucentTextPanel>().Label.text,
                Is.EqualTo(slash.Body.GetComponent<BattleInspectCardView>().Owner.text),
                "The tag shows the owner named on the card."
            );

            // Played, the ring and the name go.
            view.TapScreen(Center(view.Enemies[1].TargetArea));
            yield return null;
            Assert.That(user.CasterRing.activeSelf, Is.False);
            Assert.That(user.NameTag.activeSelf, Is.False);
            yield return WaitActions(view);
            Assert.That(user.Body.anchoredPosition, Is.EqualTo(homes[slash.Caster]));
            Assert.That(user.HpBar.anchoredPosition, Is.EqualTo(barHome));
        }

        [UnityTest]
        public IEnumerator PuttingTheCardBackSendsItsUserHome()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var heal = view.Cards.First(card => card.Effect == BattleInspectCardEffect.Heal);
            int index = System.Array.IndexOf(view.Cards, heal);
            var user = view.Allies[heal.Caster];
            var home = user.Body.anchoredPosition;

            var point = CardPoint(heal);
            view.PressCard(index, point);
            view.ReleaseCard(point);
            yield return WaitStep(view);
            Assert.That(user.CasterRing.activeSelf, Is.True);
            Assert.That(user.Marker.gameObject.activeSelf, Is.True, "The user may heal itself.");

            // Top middle of the screen, far from every character.
            view.TapScreen(new Vector2(Screen.width * 0.5f, Screen.height - 4f));
            yield return WaitStep(view);
            Assert.That(view.HeldCard, Is.EqualTo(-1));
            Assert.That(user.CasterRing.activeSelf, Is.False);
            Assert.That(user.NameTag.activeSelf, Is.False);
            Assert.That(user.Body.anchoredPosition, Is.EqualTo(home));
        }

        [UnityTest]
        public IEnumerator CardsPlayedInARowActOneAfterAnother()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            var slash = view.Cards[0];
            int fireIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Fire
            );
            var fire = view.Cards[fireIndex];

            Play(view, 0, wolf.TargetArea);
            Play(view, fireIndex, wolf.TargetArea);
            // Both are paid for at once; their effects land in turn.
            Assert.That(view.Energy, Is.EqualTo(view.MaxEnergy - slash.Cost - fire.Cost));
            Assert.That(view.HandCards, Has.Count.EqualTo(3));
            yield return WaitActions(view);
            // Fire is one of the wolf's weaknesses.
            Assert.That(
                wolf.Hp,
                Is.EqualTo(
                    wolf.MaxHp
                        - Dealt(view, slash, wolf)
                        - Mathf.RoundToInt(Dealt(view, fire, wolf) * 1.5f)
                )
            );
        }

        [UnityTest]
        public IEnumerator OnTheEnemyTurnEachEnemyUpToThePartyAttacksAnAlly()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            var slime = view.Enemies[0];
            var wolfHome = wolf.Body.anchoredPosition;
            int partyHp = view.Allies.Sum(ally => ally.Hp);
            Assert.That(wolf.SkillName, Is.Not.Empty);
            Assert.That(wolf.Power, Is.GreaterThan(0));

            int first = view.HandCards[0];
            view.EndTurnButton.onClick.Invoke();
            // Cards cannot be pressed while the enemies act.
            view.PressCard(first, CardPoint(view.Cards[first]));
            Assert.That(view.HeldCard, Is.EqualTo(-1));

            // The wolf acts first: its turn is at the head of the order, and it steps toward the
            // party with its skill's name up.
            bool wolfStepped = false;
            for (float t = 0f; view.EnemyTurn && t < 10f; t += Time.deltaTime)
            {
                if (wolf.Body.anchoredPosition.x < wolfHome.x - 1f)
                {
                    wolfStepped = true;
                    Assert.That(view.UpcomingTurns(1)[0], Is.EqualTo(1));
                    Assert.That(view.SkillBanner.Label.text, Is.EqualTo(wolf.SkillName));
                }
                yield return null;
            }
            Assert.That(view.EnemyTurn, Is.False);
            Assert.That(wolfStepped, Is.True);
            Assert.That(wolf.Body.anchoredPosition, Is.EqualTo(wolfHome));
            // The wolf and the slime come before the next party turn; the guardian waits. Each
            // hits an ally at random, softened by that ally's defense.
            var both = Blows(view, wolf).SelectMany(_ => Blows(view, slime), (w, s) => w + s);
            Assert.That(both, Has.Member(partyHp - view.Allies.Sum(ally => ally.Hp)));
            Assert.That(view.Turn, Is.EqualTo(view.StartTurn + 1));
        }

        [UnityTest]
        public IEnumerator DefeatedEnemiesDoNotAttack()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            view.Enemies[0].Hp = 0;
            view.Enemies[1].Hp = 0;
            int partyHp = view.Allies.Sum(ally => ally.Hp);

            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            Assert.That(view.Allies.Sum(ally => ally.Hp), Is.EqualTo(partyHp));
            Assert.That(view.Turn, Is.EqualTo(view.StartTurn + 1));
        }

        [UnityTest]
        public IEnumerator ABandTellsWhoseTurnBeginsAndTheEnemiesWaitForIt()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var banner = view.TurnBanner;
            var wolf = view.Enemies[1];
            var wolfHome = wolf.Body.anchoredPosition;
            Assert.That(view.TurnBannerGroup.blocksRaycasts, Is.False, "Taps go through the band.");

            // The enemies' band comes up, and the wolf waits for it before stepping out.
            view.EndTurnButton.onClick.Invoke();
            yield return null;
            Assert.That(banner.gameObject.activeSelf, Is.True);
            Assert.That(banner.Label.text, Is.EqualTo(BattleInspectView.EnemyTurnText));
            for (float t = 0f; t < view.Settings.TurnBannerHold * 0.8f; t += Time.deltaTime)
            {
                Assert.That(wolf.Body.anchoredPosition, Is.EqualTo(wolfHome));
                yield return null;
            }

            // The party's band comes up with the turn's number, over the deal.
            yield return WaitEnemyTurn(view);
            Assert.That(view.Dealing, Is.True);
            Assert.That(banner.gameObject.activeSelf, Is.True);
            Assert.That(
                banner.Label.text,
                Is.EqualTo(BattleInspectView.PartyTurnText(view.StartTurn + 1, again: false))
            );
            yield return WaitDealt(view);

            // The next turn is the party's again: no enemy acts, so no enemies' band, and the
            // party's band says the party acts again.
            view.EndTurnButton.onClick.Invoke();
            bool enemiesBand = false;
            for (float t = 0f; view.EnemyTurn && t < 5f; t += Time.deltaTime)
            {
                enemiesBand |= banner.Label.text == BattleInspectView.EnemyTurnText;
                yield return null;
            }
            Assert.That(view.EnemyTurn, Is.False);
            Assert.That(enemiesBand, Is.False);
            Assert.That(banner.gameObject.activeSelf, Is.True);
            Assert.That(
                banner.Label.text,
                Is.EqualTo(BattleInspectView.PartyTurnText(view.StartTurn + 2, again: true))
            );

            // The band goes away by itself.
            float shown = view.Settings.TurnBannerHold + view.Settings.TurnBannerFade;
            for (float t = 0f; banner.gameObject.activeSelf && t < shown + 1f; t += Time.deltaTime)
                yield return null;
            Assert.That(banner.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator APlayedSkillPlaysItsEffectAndTheStageSettles()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var vfx = view.Vfx;
            Assert.That(vfx, Is.Not.Null);
            var wolf = view.Enemies[1];
            int fire = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Fire
            );

            Play(view, fire, wolf.TargetArea);
            for (float t = 0f; view.Acting && t < 5f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(view.Acting, Is.False, "The action ends.");
            Assert.That(wolf.Hp, Is.LessThan(wolf.MaxHp));

            // The embers and smoke drift away and the stage lights up and comes to rest.
            for (float t = 0f; vfx.LiveCount > 0 && t < 3f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(vfx.LiveCount, Is.EqualTo(0));
            Assert.That(vfx.DimAlpha, Is.EqualTo(0f));
            Assert.That(vfx.Stage.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(vfx.Flash.enabled, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator OnTheThreeDStageTheActorsStandAsBoardsWhereTheUiPutsThem()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            yield return null;
            yield return null;

            var stageCamera = Hd2dStageCamera.Active;
            Assert.That(stageCamera, Is.Not.Null, "The battle has a 3D stage.");
            Assert.That(stageCamera.gameObject.scene, Is.EqualTo(loadedScene));

            var bodies = view
                .Allies.Select(ally => (ally.Body, Sprite: ally.Sprite))
                .Concat(view.Enemies.Select(enemy => (enemy.Body, Sprite: enemy.Sprite)));
            float tolerance = Screen.height * 0.02f;
            foreach (var (body, sprite) in bodies)
            {
                var board = body.GetComponent<Hd2dUiBillboard>();
                Assert.That(board, Is.Not.Null, body.name);
                Assert.That(board.Staged, Is.True, body.name);
                Assert.That(sprite.canvasRenderer.cull, Is.True, "The UI picture is not drawn.");
                Assert.That(board.VisualRenderer.enabled, Is.True);

                // The board covers on screen where the UI picture is.
                var mesh = board.VisualRenderer.GetComponent<MeshFilter>().sharedMesh;
                var drawn = mesh
                    .vertices.Select(v => (Vector2)stageCamera.Camera.WorldToScreenPoint(v))
                    .Aggregate(Vector2.zero, (sum, v) => sum + v / 4f);
                var corners = new Vector3[4];
                sprite.rectTransform.GetWorldCorners(corners);
                var ui = RectTransformUtility.WorldToScreenPoint(
                    sprite.canvas.rootCanvas.worldCamera,
                    (corners[0] + corners[2]) * 0.5f
                );
                Assert.That(Vector2.Distance(drawn, ui), Is.LessThan(tolerance), body.name);
            }
        }

        [UnityTest]
        public IEnumerator TheCameraCirclesTheBattlefieldButNotTheControlsThroughAHitStop()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var vfx = view.Vfx;
            var drift = (RectTransform)vfx.Stage.parent;
            Assert.That(drift.GetComponent<BattleStageDrift>(), Is.Not.Null);
            Assert.That(view.Enemies[0].TargetArea.IsChildOf(vfx.Stage), Is.True);
            Assert.That(view.Hand.IsChildOf(drift), Is.False, "The controls stay still.");

            Play(view, 0, view.Enemies[1].TargetArea);
            var last = drift.anchoredPosition;
            int frames = 0;
            int still = 0;
            int stopped = 0;
            for (float t = 0f; (view.Acting || t < 0.2f) && t < 5f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                frames++;
                if (vfx.HitStopping)
                    stopped++;
                if (drift.anchoredPosition == last)
                    still++;
                last = drift.anchoredPosition;
            }
            Assert.That(view.Acting, Is.False, "The action ends.");
            Assert.That(stopped, Is.GreaterThan(0), "The cut lands with a hit stop.");
            Assert.That(
                still,
                Is.EqualTo(0),
                $"The camera moves on in every one of {frames} frames."
            );
            Assert.That(
                ActorMaterials(view),
                Has.All.SameAs(view.ActorMaterial),
                "The flashes give the sprites their own material back."
            );
        }

        private static IEnumerable<Material> ActorMaterials(BattleInspectView view) =>
            view
                .Allies.Select(ally => ally.Sprite.material)
                .Concat(view.Enemies.Select(enemy => enemy.Sprite.material));

        [UnityTest]
        public IEnumerator ABlowLightsItsTargetAndTheLightFades()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var vfx = view.Vfx;
            var wolf = view.Enemies[1];
            int fire = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Fire
            );

            Play(view, fire, wolf.TargetArea);
            for (float t = 0f; wolf.Hp == wolf.MaxHp && t < 3f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(wolf.Hp, Is.LessThan(wolf.MaxHp));
            yield return null;
            Assert.That(vfx.LightOf(wolf.Sprite).Lit, Is.True, "The blast lights its target.");

            yield return WaitActions(view);
            for (float t = 0f; vfx.LightOf(wolf.Sprite).Lit && t < 2f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(view.Enemies.Any(enemy => vfx.LightOf(enemy.Sprite).Lit), Is.False);
            Assert.That(
                view.Allies.Any(ally => vfx.LightOf(ally.Sprite).Lit),
                Is.False,
                "The light fades away."
            );
        }

        [UnityTest]
        public IEnumerator EffectsAreWorkedOutWithNoPicture()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var vfx = view.Vfx;
            int ice = System.Array.FindIndex(
                view.Cards,
                card => card.Element == BattleInspectElement.Ice
            );
            int heal = System.Array.FindIndex(
                view.Cards,
                card => card.Effect == BattleInspectCardEffect.Heal
            );
            typeof(BattleInspectView).GetProperty("Energy").SetValue(view, 9);

            // The heal reuses the boards the ice lance left; each shows its own shape whole.
            Play(view, ice, view.Enemies[1].TargetArea);
            yield return WaitActions(view);
            for (float t = 0f; vfx.LiveCount > 0 && t < 3f; t += Time.unscaledDeltaTime)
                yield return null;
            Play(view, heal, view.Allies[0].TargetArea);
            yield return WaitActions(view);
            var shapes = vfx
                .BackLayer.GetComponentsInChildren<BattleVfxImage>(false)
                .Concat(vfx.FrontLayer.GetComponentsInChildren<BattleVfxImage>(false))
                .ToArray();
            Assert.That(shapes, Is.Not.Empty);
            Assert.That(shapes.Select(image => image.texture), Has.All.Null, "No picture.");
            // A shape's own material, or the copy of it that draws its rich look.
            bool OfShape(Material material, Material shape) =>
                shape != null
                && (
                    material == shape
                    || (material.shader == shape.shader && material.name.StartsWith(shape.name + " (Rich"))
                );
            Assert.That(
                shapes.Select(image => image.material),
                Has.All.Matches<Material>(material => vfx.Shapes.Any(shape => OfShape(material, shape)))
            );
            Assert.That(
                shapes.Select(image => image.uvRect),
                Has.All.EqualTo(new Rect(0f, 0f, 1f, 1f))
            );
            Assert.That(
                shapes.Any(image => OfShape(image.material, vfx.MaterialOf(BattleVfxShape.Pillar))),
                Is.True,
                "The heal raises its pillar of light."
            );
        }

        [UnityTest]
        public IEnumerator UnderAnotherCanvasTheBattlefieldFillsIt()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var stage = view.Enemies[0].TargetArea.GetComponentInParent<BattleStageCamera>();
            var stageRect = (RectTransform)stage.transform;

            // The showcase previews the screen under its own camera canvas.
            var holder = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(holder, loadedScene);
            var holderCanvas = holder.GetComponent<Canvas>();
            holderCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            holderCanvas.worldCamera = stage.GetComponent<Canvas>().worldCamera;
            stageRect.SetParent(holder.transform, false);
            yield return null;

            Assert.That(stageRect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(stageRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(stageRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(
                stageRect.rect.size,
                Is.EqualTo(((RectTransform)holder.transform).rect.size)
            );
        }

        [UnityTest]
        public IEnumerator TheBlowHoldsTheBattleStillForAMomentThenTimeGoesOn()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var vfx = view.Vfx;
            var wolf = view.Enemies[1];

            Play(view, 0, wolf.TargetArea);
            for (float t = 0f; !vfx.HitStopping && t < 2f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(vfx.HitStopping, Is.True, "The cut lands with a hit stop.");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(wolf.Hp, Is.LessThan(wolf.MaxHp), "The stop comes with the blow.");

            float stopped = Time.realtimeSinceStartup;
            while (vfx.HitStopping && Time.realtimeSinceStartup - stopped < 1f)
                yield return null;
            Assert.That(vfx.HitStopping, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return WaitActions(view);
        }

        [UnityTest]
        public IEnumerator LeavingTheBattleDuringAHitStopGivesTimeBack()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            view.Vfx.HitStop(0.2f);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            view.Vfx.enabled = false;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ACostlySkillOpensWithACutInOfItsUser()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var vfx = view.Vfx;
            // 居合一閃 is one of the big moves that cut in, whatever they cost.
            int index = DealInto(view, "Iai");
            var card = view.Cards[index];
            Assert.That(CardSkills.Find("Iai").Has(CardTrait.CutIn), Is.True);
            Assert.That(card.Cost, Is.LessThanOrEqualTo(view.Energy));
            // Energy left over keeps the turn from ending by itself after the card.
            typeof(BattleInspectView).GetProperty("Energy").SetValue(view, card.Cost + 1);

            Play(view, index, view.Enemies[1].TargetArea);
            // The user steps forward first, then the cut-in sweeps in.
            for (
                float t = 0f;
                !vfx.CutIn.gameObject.activeSelf && t < 1f;
                t += Time.unscaledDeltaTime
            )
                yield return null;
            Assert.That(vfx.CutIn.gameObject.activeSelf, Is.True);
            Assert.That(vfx.CutInName.text, Is.EqualTo("居合一閃"));
            Assert.That(vfx.CutInActor.texture, Is.SameAs(view.Allies[card.Caster].Sprite.texture));
            yield return WaitActions(view);
            Assert.That(vfx.CutIn.gameObject.activeSelf, Is.False);
            Assert.That(view.Enemies[1].Hp, Is.LessThan(view.Enemies[1].MaxHp));
        }

        /// <summary>Deals the card skill <paramref name="skill"/> into the opening hand in place of the guard, and returns its card.</summary>
        private static int DealInto(BattleInspectView view, string skill)
        {
            int deck = DeckIndexOf(view, skill);
            view.RestartDeal(
                new[] { 0, 1, 2, 3, deck }
                    .Concat(Enumerable.Range(0, view.Deck.Length).Where(i => i > 3 && i != deck))
                    .ToArray(),
                instant: true
            );
            return System.Array.FindIndex(
                view.Cards,
                card => card.InHand && card.DeckIndex == deck
            );
        }

        [UnityTest]
        public IEnumerator ABurnLeftByACardHurtsAtThePartysNextTurn()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            int embers = DealInto(view, "Embers");
            var skill = Baryonyx.Combat.CardSkills.Find("Embers");
            int caster = view.Cards[embers].Caster;
            int blow = Baryonyx.Combat.CardRules.Power(
                skill.Actions[0],
                view.StatOf(caster, skill.Actions[0].Stat),
                view.TodayAct
            );
            var (stat, percent) = Baryonyx.Combat.CardRules.TickOf(Baryonyx.Combat.CardStatus.Burn);
            int burn = Mathf.RoundToInt(view.StatOf(caster, stat) * percent / 100f);

            Play(view, embers, wolf.TargetArea);
            yield return WaitActions(view);
            Assert.That(view.HasStatus(false, 1, Baryonyx.Combat.CardStatus.Burn), Is.True);
            // The wolf's 属防 softens the blow, and fire is one of its weaknesses. The burn
            // goes through no defense.
            int softened = Baryonyx.Combat.CombatFormula.Defend(
                blow,
                wolf.MagicDefense,
                view.Allies[caster].Level
            );
            int afterBlow = wolf.MaxHp - Mathf.RoundToInt(softened * 1.5f);
            Assert.That(wolf.Hp, Is.EqualTo(afterBlow));

            view.EndTurnButton.onClick.Invoke();
            for (float t = 0f; view.EnemyTurn && t < 15f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.EnemyTurn, Is.False);
            Assert.That(
                wolf.Hp,
                Is.EqualTo(afterBlow - burn),
                "The burn works as the party's turn comes."
            );
        }

        [UnityTest]
        public IEnumerator BlockFromACardSoftensTheEnemiesBlows()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var slime = view.Enemies[0];
            var wolf = view.Enemies[1];
            int guard = DealInto(view, "Guard");
            Play(view, guard, view.Allies[0].TargetArea);
            yield return WaitActions(view);
            Assert.That(view.BlockOf(0), Is.GreaterThan(0), "Every ally takes the block.");
            Assert.That(view.BlockOf(3), Is.EqualTo(view.BlockOf(0)));
            // The block shows over the HP bar.
            Assert.That(view.Allies.All(ally => ally.Gauge.Block.gameObject.activeSelf), Is.True);
            int[] hpBefore = view.Allies.Select(ally => ally.Hp).ToArray();

            view.EndTurnButton.onClick.Invoke();
            for (float t = 0f; view.EnemyTurn && t < 15f; t += Time.deltaTime)
                yield return null;
            int lost = hpBefore.Sum() - view.Allies.Sum(ally => ally.Hp);
            Assert.That(lost, Is.LessThan(wolf.Power + slime.Power));
            // The block stays into the party's next turn, halved; the blows wear it down first.
            Assert.That(
                Enumerable.Range(0, view.Allies.Length).Sum(i => view.BlockOf(i)),
                Is.GreaterThan(0),
                "The block lasts past the party's next turn."
            );
            for (int i = 0; i < view.Allies.Length; i++)
            {
                if (view.Allies[i].Hp < hpBefore[i])
                    Assert.That(view.BlockOf(i), Is.EqualTo(0), "A blow takes the block first.");
                Assert.That(
                    view.Allies[i].Gauge.Block.gameObject.activeSelf,
                    Is.EqualTo(view.BlockOf(i) > 0)
                );
            }
        }

        [UnityTest]
        public IEnumerator BlockGrowsNoFurtherThanTheAllysMaxHp()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var ally = view.Allies[1];
            ally.MaxHp = 1;
            ally.Hp = 1;
            int guard = DealInto(view, "Guard");
            Play(view, guard, view.Allies[0].TargetArea);
            yield return WaitActions(view);
            Assert.That(view.BlockOf(0), Is.GreaterThan(1));
            Assert.That(view.BlockOf(1), Is.EqualTo(1), "The block stops at the ally's max HP.");
        }

        [UnityTest]
        public IEnumerator LostHpTrailsBehindTheBarAndThenDrains()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var gauge = view.Enemies[0].Gauge;
            int max = view.Enemies[0].MaxHp;
            Assert.That(gauge.Ratio, Is.EqualTo(1f));

            gauge.SetHp(max / 2, max);
            Assert.That(gauge.Ratio, Is.EqualTo(0.5f).Within(1f / gauge.InnerDots));
            Assert.That(gauge.TrailRatio, Is.EqualTo(1f), "The lost HP stays as a trail.");
            Assert.That(gauge.Trail.gameObject.activeSelf, Is.True);

            yield return null;
            Assert.That(gauge.TrailRatio, Is.EqualTo(1f), "The trail holds a moment first.");

            yield return SceneTests.WaitUntil(
                () => gauge.TrailRatio == gauge.Ratio,
                gauge.TrailHold + gauge.TrailDrain + 1f,
                "Then it drains to the fill."
            );
            Assert.That(gauge.Trail.gameObject.activeSelf, Is.False);

            gauge.SetHp(max, max);
            Assert.That(gauge.Ratio, Is.EqualTo(1f), "Healing fills the bar at once.");
            Assert.That(gauge.Trail.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator EveryBarWritesItsHpInTheMiddle()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            foreach (var enemy in view.Enemies)
                Assert.That(enemy.Gauge.Number.text, Is.EqualTo(enemy.Hp.ToString()));
            foreach (var ally in view.Allies)
                Assert.That(ally.Gauge.Number.text, Is.EqualTo(ally.Hp.ToString()));

            var gauge = view.Enemies[0].Gauge;
            int max = view.Enemies[0].MaxHp;
            gauge.SetHp(max / 3, max);
            Assert.That(gauge.Number.text, Is.EqualTo((max / 3).ToString()));
            gauge.SetHp(-20, max);
            Assert.That(gauge.Number.text, Is.EqualTo("0"), "Overkill still reads 0.");
        }

        [UnityTest]
        public IEnumerator ARaisedCardForecastsItsBlowOnEveryEnemyItCanHit()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int embers = DealInto(view, "Embers");
            // A weakness still hidden is left out of the forecast, so the blow lands on an enemy
            // whose fire weakness, if it has one, is known.
            int target = System.Array.FindIndex(
                view.Enemies,
                enemy =>
                    enemy.Weaknesses.All(weakness =>
                        weakness.Element != BattleInspectElement.Fire || weakness.Revealed
                    )
            );
            Assert.That(target, Is.GreaterThanOrEqualTo(0));
            var area = view.Enemies[target].TargetArea;

            view.PressCard(embers, CardPoint(view.Cards[embers]));
            view.DragCard(Center(area));
            yield return null;
            for (int i = 0; i < view.Enemies.Length; i++)
            {
                var enemy = view.Enemies[i];
                Assert.That(view.EnemyForecasts[i].Shown, Is.True, $"Enemy {i} shows the blow.");
                Assert.That(view.EnemyForecasts[i].Hp, Is.LessThan(enemy.Hp));
                Assert.That(view.EnemyForecasts[i].Defeat, Is.False);
                Assert.That(enemy.Gauge.ForecastBand.gameObject.activeSelf, Is.True);
                Assert.That(enemy.Gauge.Number.text, Is.EqualTo(enemy.Hp.ToString()));
            }
            Assert.That(view.AllyForecasts.Any(forecast => forecast.Shown), Is.False);
            int forecast = view.EnemyForecasts[target].Hp;

            view.ReleaseCard(Center(area));
            Assert.That(
                view.Enemies.Any(enemy => enemy.Gauge.Forecasting),
                Is.False,
                "Playing the card takes the forecast away."
            );
            yield return WaitActions(view);
            Assert.That(view.Enemies[target].Hp, Is.EqualTo(forecast), "The blow is as forecast.");
        }

        [UnityTest]
        public IEnumerator HealingAndBlockAreForecastOnTheAllies()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int heal = DealInto(view, "Heal");
            var hurt = view.Allies[0];
            hurt.Hp = hurt.MaxHp / 3;

            view.PressCard(heal, CardPoint(view.Cards[heal]));
            view.DragCard(Center(hurt.TargetArea));
            yield return null;
            var healed = view.AllyForecasts[0];
            Assert.That(healed.Shown, Is.True);
            Assert.That(healed.Hp, Is.GreaterThan(hurt.Hp).And.AtMost(hurt.MaxHp));
            Assert.That(hurt.Gauge.ForecastBand.gameObject.activeSelf, Is.True);
            Assert.That(view.EnemyForecasts.Any(forecast => forecast.Shown), Is.False);
            view.ReleaseCard(Center(hurt.TargetArea));
            yield return WaitActions(view);
            Assert.That(hurt.Hp, Is.EqualTo(healed.Hp), "The healing is as forecast.");

            int guard = DealInto(view, "Guard");
            view.PressCard(guard, CardPoint(view.Cards[guard]));
            view.DragCard(Center(view.Allies[0].TargetArea));
            yield return null;
            var blocks = new int[view.Allies.Length];
            for (int i = 0; i < view.Allies.Length; i++)
            {
                var forecast = view.AllyForecasts[i];
                Assert.That(forecast.Shown, Is.True, $"Ally {i} shows the block.");
                Assert.That(forecast.Block, Is.GreaterThan(view.BlockOf(i)));
                Assert.That(view.Allies[i].Gauge.BlockForecast.gameObject.activeSelf, Is.True);
                blocks[i] = forecast.Block;
            }
            view.ReleaseCard(Center(view.Allies[0].TargetArea));
            yield return WaitActions(view);
            for (int i = 0; i < view.Allies.Length; i++)
                Assert.That(
                    view.BlockOf(i),
                    Is.EqualTo(blocks[i]),
                    $"Ally {i}'s block is as forecast."
                );
        }

        [UnityTest]
        public IEnumerator TheForecastPromisesADefeatButKeepsAHiddenWeaknessHidden()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int thunder = DealInto(view, "Thunder");
            var card = view.Cards[thunder];
            int weak = System.Array.FindIndex(
                view.Enemies,
                enemy =>
                    enemy.Weaknesses.Any(weakness =>
                        weakness.Element == BattleInspectElement.Thunder && !weakness.Revealed
                    )
            );
            Assert.That(weak, Is.GreaterThanOrEqualTo(0), "An enemy hides a thunder weakness.");
            var enemy = view.Enemies[weak];

            int hidden = enemy.Hp - view.ForecastOf(card).enemies[weak].Hp;
            foreach (var weakness in enemy.Weaknesses)
                if (weakness.Element == BattleInspectElement.Thunder)
                    weakness.Show(true);
            int shown = enemy.Hp - view.ForecastOf(card).enemies[weak].Hp;
            Assert.That(
                shown,
                Is.EqualTo(Mathf.RoundToInt(hidden * 1.5f)),
                "Only a revealed weakness grows the forecast."
            );

            enemy.Hp = 1;
            view.PressCard(thunder, CardPoint(card));
            yield return null;
            Assert.That(view.EnemyForecasts[weak].Defeat, Is.True);
            Assert.That(enemy.Gauge.DefeatTag.activeSelf, Is.True);
            for (int i = 0; i < view.Enemies.Length; i++)
                if (i != weak)
                    Assert.That(view.Enemies[i].Gauge.DefeatTag.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TappingACharacterWithNoCardUpListsItsStatuses()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            // A burn from 火の粉 gives the wolf a status to list.
            int embers = DealInto(view, "Embers");
            var wolf = view.Enemies[1];
            Play(view, embers, wolf.TargetArea);
            yield return WaitActions(view);
            Assert.That(view.HasStatus(false, 1, CardStatus.Burn), Is.True);
            yield return null;
            Assert.That(view.TapArea.activeSelf, Is.True);

            var list = view.StatusList;
            view.TapScreen(Center(wolf.TargetArea));
            Assert.That(view.StatusListOpen, Is.True);
            Assert.That(view.Listed, Is.EqualTo((false, 1)));
            Assert.That(list.gameObject.activeSelf, Is.True);
            Assert.That(list.Titles[0].text, Is.EqualTo(CombatEnemies.Find(wolf.Id).Name));
            Assert.That(
                list.Bodies[0].text,
                Is.EqualTo(BattleInspectView.HpLine(wolf.Hp, wolf.MaxHp))
            );
            Assert.That(list.Titles[1].text, Does.StartWith("やけど　残り"));
            Assert.That(list.Bodies[1].text, Is.EqualTo(CardRules.StatusNote(CardStatus.Burn)));

            // Another character moves the list to it; an ally with no status says so.
            var ally = view.Allies[0];
            view.TapScreen(Center(ally.TargetArea));
            Assert.That(view.Listed, Is.EqualTo((true, 0)));
            Assert.That(list.Titles[0].text, Is.EqualTo(ally.Name));
            Assert.That(list.Titles[1].text, Is.EqualTo(BattleInspectView.NoStatusText));

            view.TapScreen(Center(ally.TargetArea));
            Assert.That(view.StatusListOpen, Is.False, "A second tap closes the list.");
            Assert.That(list.gameObject.activeSelf, Is.False);

            view.TapScreen(Center(wolf.TargetArea));
            view.TapScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.75f));
            Assert.That(view.StatusListOpen, Is.False, "A tap on no one closes the list.");

            view.TapScreen(Center(wolf.TargetArea));
            int card = System.Array.FindIndex(view.Cards, c => c.InHand);
            view.PressCard(card, CardPoint(view.Cards[card]));
            Assert.That(view.StatusListOpen, Is.False, "Pressing a card closes the list.");
            view.ReleaseCard(CardPoint(view.Cards[card]));
        }

        [UnityTest]
        public IEnumerator ACardThatDrawsAddsCardsToTheHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            // 見本のデッキには引くだけのカードがないため、同じルカの「蛇牙の連撃」を「疾風迅雷」（1枚引く）に替えてから配る。
            var skill = CardSkills.Find("Gale");
            int slot = DeckIndexOf(view, "SerpentFang");
            var serpent = view.Deck[slot];
            view.Deck[slot] = new BattleInspectCardData
            {
                Name = skill.Name,
                Owner = serpent.Owner,
                Skill = skill.Id,
                Kind = Baryonyx.UI.Cards.CardText.KindLine(skill),
                Description = Baryonyx.UI.Cards.CardText.Description(skill),
                Art = serpent.Art,
                Frame = serpent.Frame,
                Cost = skill.Cost,
                Element = serpent.Element,
                Effect = BattleCardText.EffectOf(skill),
                Caster = serpent.Caster,
            };
            int gale = DealInto(view, "Gale");
            Assert.That(
                view.Cards[gale].TargetsWholeSide,
                Is.True,
                "A card for the whole party needs no exact target."
            );
            int before = view.HandCards.Count;

            Play(view, gale, view.Allies[0].TargetArea);
            yield return WaitActions(view);
            for (float t = 0f; view.Dealing && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.HandCards, Has.Count.EqualTo(before - 1 + 1));
        }

        /// <summary>The index in the deck of the card skill <paramref name="skill"/>.</summary>
        private static int DeckIndexOf(BattleInspectView view, string skill)
        {
            int index = System.Array.FindIndex(view.Deck, data => data.Skill == skill);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), skill + " is in the deck.");
            return index;
        }

        [UnityTest]
        public IEnumerator ACheapSkillGoesStraightToItsEffect()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            Play(view, 0, view.Enemies[1].TargetArea);
            for (float t = 0f; view.Acting && t < 5f; t += Time.unscaledDeltaTime)
            {
                Assert.That(view.Vfx.CutIn.gameObject.activeSelf, Is.False);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ABeatenEnemyCrumblesAwayAndComesBackUntinted()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            wolf.Hp = 1;

            Play(view, 0, wolf.TargetArea);
            for (float t = 0f; wolf.Alive && t < 3f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(wolf.Alive, Is.False);
            for (float t = 0f; wolf.Group.alpha > 0f && t < 3f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(wolf.Group.alpha, Is.EqualTo(0f), "The wolf is gone once the card is over.");

            // Its picture is drawn again where it stood, to be eaten away.
            var body = view
                .Vfx.FrontLayer.GetComponentsInChildren<RawImage>()
                .Single(image => image.texture == wolf.Sprite.texture);

            // The board is used again by later effects once the crumble is over.
            bool Crumbling() => body.isActiveAndEnabled && body.texture == wolf.Sprite.texture;
            for (float t = 0f; Crumbling() && t < 3f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(Crumbling(), Is.False, "It is eaten away to nothing.");
            Assert.That(wolf.Sprite.color, Is.EqualTo(Color.white), "It comes back untinted.");
            yield return WaitActions(view);
        }

        /// <summary>
        /// Taps the middle of the raised card the way a finger does: the tap goes to whatever the
        /// event system hits first there, which must be the card itself, not the cards of the
        /// hand under it nor the battlefield.
        /// </summary>
        private static IEnumerator TapRaisedCard(BattleInspectView view, BattleInspectCard card)
        {
            // The card's raycast setting follows in the frame after it is selected.
            yield return null;
            var point = RectTransformUtility.WorldToScreenPoint(
                null,
                card.Body.TransformPoint(card.Body.rect.center)
            );
            var tap = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(tap, hits);
            Assert.That(hits, Is.Not.Empty);
            var input = hits[0].gameObject.GetComponentInParent<BattleInspectCardInput>();
            Assert.That(input, Is.Not.Null, "The tap lands on a card.");
            Assert.That(view.Cards[input.Index], Is.SameAs(card), "The raised card takes the tap.");
            input.OnPointerDown(tap);
            input.OnPointerUp(tap);
        }

        /// <summary>Presses a card, swipes it onto the target and lets go.</summary>
        private static void Play(BattleInspectView view, int card, RectTransform target)
        {
            view.PressCard(card, CardPoint(view.Cards[card]));
            view.DragCard(Center(target));
            view.ReleaseCard(Center(target));
        }

        /// <summary>Where the card at <paramref name="slot"/> of the hand stands in the fan.</summary>
        private static Vector2 FanPosition(BattleInspectView view, int slot)
        {
            int count = view.HandCards.Count;
            var (position, _) = BattleInspectView.FanPose(
                slot,
                count,
                BattleInspectView.FanStepFor(
                    count,
                    view.Settings.FanStep,
                    view.Settings.FanMaxSpread
                ),
                view.Settings.FanRadius,
                view.Settings.FanDrop,
                view.Settings.RestBottom
            );
            return position;
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

        /// <summary>
        /// A card's blow on <paramref name="enemy"/> after its defense: 属防 for a spell (worked
        /// out from 属攻), 物防 for the others, at the level of the card's user.
        /// </summary>
        private static int Dealt(
            BattleInspectView view,
            BattleInspectCard card,
            BattleInspectEnemy enemy
        )
        {
            var damage = CardSkills
                .Find(view.Deck[card.DeckIndex].Skill)
                .Actions.First(action => action.Kind == CardActionKind.Damage);
            int defense =
                damage.Stat == CardStat.MagicAttack ? enemy.MagicDefense : enemy.PhysicalDefense;
            return CombatFormula.Defend(card.Power, defense, view.Allies[card.Caster].Level);
        }

        /// <summary>The enemy's blow as each ally would take it, after that ally's defense.</summary>
        private static IEnumerable<int> Blows(BattleInspectView view, BattleInspectEnemy enemy) =>
            view.Allies.Select(ally =>
                CombatFormula.Defend(enemy.Power, ally.Stats.PhysicalDefense, enemy.Level)
            );

        /// <summary>The texts of the numbers now rising from a template.</summary>
        // ダメージと状態を同時に与えると、数字の下に状態の名前が並び、上がる途中でも重ならない。
        [UnityTest]
        public IEnumerator ADamageNumberAndTheStatusPutOnWithItDoNotOverlap()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int embers = DeckIndexOf(view, "Embers");
            view.RestartDeal(
                new[] { embers }
                    .Concat(Enumerable.Range(0, view.Deck.Length).Where(i => i != embers))
                    .ToArray(),
                instant: true
            );
            int index = System.Array.FindIndex(
                view.Cards,
                card => card.InHand && card.DeckIndex == embers
            );
            var target = view.Enemies[0];
            Play(view, index, target.TargetArea);

            TMP_Text number = null;
            TMP_Text name = null;
            for (
                float t = 0f;
                (number == null || name == null) && t < 5f;
                t += Time.unscaledDeltaTime
            )
            {
                number = RisingLabels(view.DamageNumber)
                    .Concat(RisingLabels(view.WeakNumber))
                    .FirstOrDefault();
                name = RisingLabels(view.PopupTemplate)
                    .FirstOrDefault(text => text.text.Contains("やけど"));
                yield return null;
            }
            Assert.That(number, Is.Not.Null, "The damage number rises.");
            Assert.That(name, Is.Not.Null, "The status name rises.");

            // 大きく弾ける最初の一瞬を過ぎてから、消えるまでのあいだを見る。
            float since = Time.unscaledTime;
            int checkedFrames = 0;
            while (number != null && name != null && Time.unscaledTime - since < 1.1f)
            {
                if (Time.unscaledTime - since >= 0.2f)
                {
                    float scale = number.rectTransform.lossyScale.y;
                    float gap = number.rectTransform.position.y - name.rectTransform.position.y;
                    float needed = (number.fontSize + name.fontSize) * 0.5f * scale;
                    Assert.That(
                        gap,
                        Is.GreaterThanOrEqualTo(needed),
                        $"The number stays a line above the name ({Time.unscaledTime - since:0.00}s)."
                    );
                    checkedFrames++;
                }
                yield return null;
            }
            Assert.That(checkedFrames, Is.GreaterThan(5));
        }

        private static string[] Rising(TMP_Text template) =>
            RisingLabels(template).Select(text => text.text).ToArray();

        /// <summary>The numbers now rising from a template.</summary>
        private static IEnumerable<TMP_Text> RisingLabels(TMP_Text template) =>
            template
                .transform.parent.GetComponentsInChildren<TMP_Text>()
                .Where(text => text != template && text.name == template.name + "(Clone)");

        private static Vector2 Center(RectTransform rect) =>
            BattleInspectView.ScreenPointOf(rect, rect.rect.center);

        /// <summary>Waits for the played cards to finish acting.</summary>
        private static IEnumerator WaitActions(BattleInspectView view)
        {
            for (float t = 0f; view.Acting && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.Acting, Is.False, "The actions end.");
        }

        /// <summary>Waits for an ally's step for a raised or put back card to end.</summary>
        private static IEnumerator WaitStep(BattleInspectView view)
        {
            for (float t = 0f; t < view.Settings.StepTime + 0.1f; t += Time.deltaTime)
                yield return null;
        }

        /// <summary>Waits for the enemies to act and the party's next turn to begin.</summary>
        private static IEnumerator WaitEnemyTurn(BattleInspectView view)
        {
            for (float t = 0f; view.EnemyTurn && t < 10f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.EnemyTurn, Is.False, "The enemy turn ends.");
        }

        /// <summary>Waits for the cards being dealt to land.</summary>
        private static IEnumerator WaitDealt(BattleInspectView view)
        {
            for (float t = 0f; view.Dealing && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.Dealing, Is.False, "The deal ends.");
        }

        /// <summary>
        /// Loads the scene. When <paramref name="dealt"/>, the deck is put in card order and the
        /// opening hand (one of each of the six cards, 0 to 5) laid down at once, so the tests
        /// know the hand; otherwise the shuffled deal runs as in play.
        /// </summary>
        private IEnumerator Load(System.Action<BattleInspectView> found, bool dealt = true)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            var view = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BattleInspectView>(true))
                .Single();
            if (dealt)
                view.RestartDeal(Enumerable.Range(0, view.Cards.Length).ToArray(), instant: true);
            found(view);
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Time.timeScale = 1f;
            random?.Dispose();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
