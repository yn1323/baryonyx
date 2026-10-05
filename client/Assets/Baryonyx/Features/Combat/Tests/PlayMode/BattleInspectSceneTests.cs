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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class BattleInspectSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleInspect.unity";
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator SceneShowsPartyEnemiesAndHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);

            Assert.That(view.IdleActors, Has.Length.EqualTo(7), "4 allies and 3 enemies");
            Assert.That(view.Allies, Has.Length.EqualTo(4));
            Assert.That(view.Enemies, Has.Length.EqualTo(3));
            Assert.That(view.Cards, Has.Length.EqualTo(16), "The deck is 16 cards.");
            Assert.That(view.HandCards, Has.Count.EqualTo(6), "The opening hand is 6 cards.");
            Assert.That(view.DeckLabel.text, Is.EqualTo("10"));
            Assert.That(view.Enemies.Select(enemy => enemy.Sprite.texture), Has.None.Null);
            Assert.That(view.MaxEnergy, Is.EqualTo(5), "Energy grows from 3 by one each turn.");
            Assert.That(view.EnergyLabel.text, Is.EqualTo("5/5"));
            Assert.That(view.HeldCard, Is.EqualTo(-1));

            foreach (var card in view.Cards)
            {
                var face = card.Body.GetComponent<BattleInspectCardView>();
                Assert.That(face.Owner.text, Is.Not.Empty);
                Assert.That(face.Kind.text, Does.Match("攻撃|回復|防御|強化|弱体|支援"));
                // A card with a number shows it; a card such as 偵察 has none.
                if (card.Power > 0)
                    Assert.That(face.Description.text, Does.Contain(card.Power.ToString()));
                Assert.That(
                    face.Element.enabled,
                    Is.EqualTo(card.Element != BattleInspectElement.None),
                    "Cards without an element show no element icon."
                );
                foreach (var band in face.Bands)
                {
                    Assert.That(band.LineEnds, Has.Length.EqualTo(3), "owner, name and kind");
                    Assert.That(band.LineEnds, Has.All.GreaterThan(0f));
                    Assert.That(band.LineEnds, Has.All.LessThan(band.rectTransform.rect.width));
                }
            }
            var ice = view.Cards.First(card => card.Element == BattleInspectElement.Ice);
            var heal = view.Cards.First(card => card.Effect == BattleInspectCardEffect.Heal);
            Assert.That(
                NameBandEnd(ice),
                Is.GreaterThan(NameBandEnd(heal)),
                "The band under アイスランス runs further than under ヒール."
            );
        }

        private static float NameBandEnd(BattleInspectCard card) =>
            card.Body.GetComponent<BattleInspectCardView>().Bands[0].LineEnds[1];

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
            Assert.That(view.AimDots[0].color, Is.EqualTo(Color.white));

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
            Assert.That(wolf.HpFill.anchorMax.x, Is.EqualTo(wolf.Hp / (float)wolf.MaxHp));
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
            var card = view.Cards[0].Body;
            yield return null;
            Assert.That(
                card.localScale.x,
                Is.GreaterThan(0.9f * view.Settings.CardScale),
                "The card stays full size while aiming."
            );
            // After the spring settles, the pressed card is enlarged, in the middle from left to
            // right and standing near the bottom of the screen.
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                yield return null;
            Assert.That(
                card.localScale.x,
                Is.EqualTo(view.Settings.RaisedScale * view.Settings.CardScale).Within(0.02f)
            );
            var foot = RectTransformUtility.WorldToScreenPoint(
                null,
                card.TransformPoint(new Vector2(card.rect.center.x, card.rect.yMin))
            );
            float scaleFactor = view.Hand.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            Assert.That(foot.x, Is.EqualTo(Screen.width * 0.5f).Within(4f));
            Assert.That(
                foot.y,
                Is.EqualTo(view.Settings.RaisedBottom * scaleFactor).Within(4f),
                "A little room under the card."
            );
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

            // The five left take the five places of a five-card fan, in their order.
            int count = view.HandCards.Count;
            Assert.That(count, Is.EqualTo(5));
            for (int slot = 0; slot < count; slot++)
            {
                var (expected, _) = BattleInspectView.FanPose(
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
            Assert.That(view.TapArea.activeSelf, Is.False);
            yield return WaitActions(view);
            Assert.That(wolf.Hp, Is.EqualTo(wolf.MaxHp - Dealt(view, slash, wolf)));
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

            Assert.That(view.HandCards, Has.Count.EqualTo(6), "Nothing was played.");
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
            int guardIndex = System.Array.FindIndex(
                view.Cards,
                card => card.Effect == BattleInspectCardEffect.Guard
            );
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
            Assert.That(hidden.Glinting, Is.False);

            Play(view, fireIndex, wolf.TargetArea);
            for (float t = 0f; !hidden.Revealed && t < 5f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(hidden.Revealed, Is.True, "The blow reveals the weakness.");
            Assert.That(hidden.Glinting, Is.True, "The icon glints as it is revealed.");
            var slot = hidden.Known.transform.parent;
            for (float t = 0f; hidden.Glinting && t < 2f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(hidden.Glinting, Is.False, "The glint ends.");
            Assert.That(slot.localScale, Is.EqualTo(Vector3.one), "The icon settles to its size.");
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
        public IEnumerator ARevealedWeaknessTurnsIntoItsIconAtThePeakOfTheGlint()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var hidden = view.Enemies[0].Weaknesses.First(weakness => !weakness.StartsRevealed);
            var slot = hidden.Known.transform.parent;
            const float swell = BattleInspectWeakness.Swell;

            hidden.Pose(swell * 0.5f);
            Assert.That(hidden.Unknown.activeSelf, Is.True, "The ? swells first.");
            Assert.That(hidden.Glinting, Is.True);
            Assert.That(hidden.Glint.texture, Is.EqualTo(Picture(hidden.Unknown)));
            Assert.That(slot.localScale.x, Is.GreaterThan(1f));
            Assert.That(hidden.Glint.Shape.x, Is.GreaterThan(0f), "It whitens as it swells.");

            hidden.Pose(swell);
            Assert.That(hidden.Known.activeSelf, Is.True, "At the peak it is the element's icon.");
            Assert.That(hidden.Unknown.activeSelf, Is.False);
            Assert.That(hidden.Glint.texture, Is.EqualTo(Picture(hidden.Known)));
            Assert.That(hidden.Glint.Shape.x, Is.EqualTo(1f), "The new icon starts white.");

            hidden.Pose(swell + BattleInspectWeakness.Settle * 0.5f);
            Assert.That(hidden.Glint.Shape.x, Is.Zero, "The white has cleared.");
            Assert.That(
                hidden.Glint.Shape.y,
                Is.GreaterThan(0f).And.LessThan(1f),
                "The band of light is crossing the icon."
            );

            hidden.Pose(swell + BattleInspectWeakness.Settle);
            Assert.That(hidden.Glinting, Is.False);
            Assert.That(hidden.Known.activeSelf, Is.True);
            Assert.That(slot.localScale, Is.EqualTo(Vector3.one));
        }

        private static Texture Picture(GameObject icon) => icon.GetComponent<RawImage>().texture;

        [UnityTest]
        public IEnumerator ACardCostingMoreThanTheEnergyIsNotPlayed()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var guardian = view.Enemies[2];
            // The meteor costs more than the first turn's energy, to show how such a card looks:
            // it is dealt into the opening hand in place of the guard.
            int meteor = DeckIndexOf(view, "Meteor");
            view.RestartDeal(
                new[] { 0, 1, 2, 3, 4, meteor }
                    .Concat(Enumerable.Range(0, view.Deck.Length).Where(i => i > 4 && i != meteor))
                    .ToArray(),
                instant: true
            );
            int iceIndex = System.Array.FindIndex(
                view.Cards,
                card => card.InHand && card.Cost > view.MaxEnergy
            );
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
            // The warning takes the place of the skill name, in the red of an unpayable cost.
            Assert.That(view.SkillBanner.gameObject.activeSelf, Is.True);
            Assert.That(view.SkillBanner.Label.text, Is.EqualTo("エネルギー不足"));
            Assert.That(view.SkillBanner.Label.color, Is.EqualTo(BattleInspectCardView.ShortCost));

            // A card the energy cannot pay for is see-through and darkened, with a reddish cost.
            yield return null;
            var ice = view.Cards[iceIndex];
            Assert.That(ice.Group.alpha, Is.EqualTo(view.Settings.UnplayableAlpha));
            Assert.That(view.Settings.UnplayableAlpha, Is.LessThan(1f));
            // The see-through card does not thin its black: it is as dark as the settings say.
            Assert.That(
                ice.Face.Shade.color.a * ice.Group.alpha,
                Is.EqualTo(view.Settings.UnplayableDarkness).Within(0.001f)
            );
            Assert.That(ice.Face.Shade.enabled, Is.True);
            Assert.That(ice.Face.CostDigit.color, Is.EqualTo(BattleInspectCardView.ShortCost));
            var slash = view.Cards[0];
            Assert.That(slash.Face.Shade.enabled, Is.False, "Cost 1 is paid for.");
            Assert.That(slash.Face.CostDigit.color, Is.EqualTo(Color.white));
            Assert.That(slash.Group.alpha, Is.EqualTo(1f));

            // Raised, it is opaque again so its details can be read.
            view.PressCard(iceIndex, CardPoint(ice));
            yield return null;
            Assert.That(ice.Group.alpha, Is.EqualTo(1f));
            Assert.That(ice.Face.Shade.enabled, Is.True);
            Assert.That(
                ice.Face.Shade.color.a,
                Is.EqualTo(view.Settings.UnplayableDarkness).Within(0.001f)
            );
        }

        [UnityTest]
        public IEnumerator EndTurnRefillsTheEnergyAndDrawsThreeCards()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            Play(view, 0, view.Enemies[1].TargetArea);
            var kept = view.HandCards.ToArray();
            var before = kept.Select(i => view.RestPosition(i).x).ToArray();

            view.EndTurnButton.onClick.Invoke();
            Assert.That(view.EnemyTurn, Is.True);
            Assert.That(view.EndTurnButton.interactable, Is.False);
            yield return WaitEnemyTurn(view);
            Assert.That(view.Turn, Is.EqualTo(4));
            Assert.That(view.Energy, Is.EqualTo(6));
            // The cards in hand make room by moving right only, before any card flies.
            for (int i = 0; i < kept.Length; i++)
                Assert.That(view.RestPosition(kept[i]).x, Is.GreaterThan(before[i]));
            yield return WaitDealt(view);
            // New cards come in at the left end, the side of the deck.
            Assert.That(DeckCards(view), Is.EqualTo(new[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
            Assert.That(view.DeckLabel.text, Is.EqualTo("7"));
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
            Assert.That(view.Turn, Is.EqualTo(4));
            Assert.That(view.Energy, Is.EqualTo(6));
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

            // One card leaves the deck at a time, face down, and the count drops with it. Each
            // flies right only, and cards already dealt stay where they landed.
            int seen = 0;
            bool sawFaceDown = false;
            var lastX = Enumerable.Repeat(float.MinValue, view.Cards.Length).ToArray();
            // The right edge of the deck's counter (its picture and the number).
            var deck = view.DeckAnchor;
            float deckRight = view
                .Hand.InverseTransformPoint(
                    deck.TransformPoint(new Vector2(deck.rect.xMax, deck.rect.center.y))
                )
                .x;
            var corners = new Vector3[4];
            var landed = new System.Collections.Generic.Dictionary<int, Vector2>();
            for (float t = 0f; view.Dealing && t < 5f; t += Time.deltaTime)
            {
                int count = view.HandCards.Count;
                Assert.That(count, Is.LessThanOrEqualTo(seen + 1), "One card at a time.");
                Assert.That(view.DeckLabel.text, Is.EqualTo((16 - count).ToString()));
                if (count > seen)
                {
                    // First seen on its back just right of the deck's counter, clear of its
                    // number, and never for a frame in the fan.
                    var dealt = view.Cards[view.HandCards[0]];
                    Assert.That(dealt.Back.activeSelf, Is.True);
                    dealt.Body.GetWorldCorners(corners);
                    float cardLeft = corners.Min(c => view.Hand.InverseTransformPoint(c).x);
                    Assert.That(cardLeft, Is.GreaterThanOrEqualTo(deckRight));
                    Assert.That(cardLeft, Is.LessThan(deckRight + 200f));
                }
                foreach (int index in view.HandCards)
                {
                    float x = view.Cards[index].Body.anchoredPosition.x;
                    if (view.IsFlying(index))
                    {
                        Assert.That(
                            x,
                            Is.GreaterThanOrEqualTo(lastX[index] - 0.01f),
                            "Right only."
                        );
                        lastX[index] = x;
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
            Assert.That(view.HandCards, Has.Count.EqualTo(6));
            Assert.That(view.DeckLabel.text, Is.EqualTo("10"));
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

            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            yield return WaitDealt(view);
            Assert.That(view.HandCards, Has.Count.EqualTo(9));
            Assert.That(view.DeckLabel.text, Is.EqualTo("7"));

            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            yield return WaitDealt(view);
            Assert.That(view.HandCards, Has.Count.EqualTo(9), "A full hand draws nothing.");
            Assert.That(view.DeckLabel.text, Is.EqualTo("7"));
        }

        [UnityTest]
        public IEnumerator AnEmptyDeckIsDealtAnewWithTheWholeDeck()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value, dealt: false);
            // A deck left with one card after the opening hand.
            view.RestartDeal(new[] { 0, 1, 2, 3, 4, 5, 6 }, instant: true);
            Play(view, 0, view.Enemies[1].TargetArea);

            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            yield return WaitDealt(view);
            // The last card, then two from the whole 16 shuffled anew, even the cards in hand.
            var hand = DeckCards(view);
            Assert.That(hand, Has.Length.EqualTo(8));
            Assert.That(hand.Skip(2), Is.EqualTo(new[] { 6, 5, 4, 3, 2, 1 }));
            Assert.That(view.DrawPile, Has.Count.EqualTo(14));
            Assert.That(view.DeckLabel.text, Is.EqualTo("14"));
            Assert.That(
                hand.Take(2).Concat(view.DrawPile).OrderBy(i => i),
                Is.EqualTo(Enumerable.Range(0, 16)),
                "The new deck is all 16, so cards in hand come again."
            );
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
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

            // Gold for the current turn, then red for an enemy and blue for the party.
            Assert.That(view.TurnSlots[0].Frame.color, Is.EqualTo(view.CurrentTurnFrame));
            Assert.That(view.TurnSlots[1].Frame.color, Is.EqualTo(view.EnemyTurnFrame));
            Assert.That(view.TurnSlots[1].Fill.color, Is.EqualTo(view.EnemyTurnFill));
            Assert.That(view.TurnSlots[3].Frame.color, Is.EqualTo(view.PartyTurnFrame));
            Assert.That(view.TurnSlots[3].Fill.color, Is.EqualTo(view.PartyTurnFill));

            // It starts at the top left, clear of the skill name in the top middle.
            var bar = (RectTransform)view.TurnSlots[0].Layout.transform.parent;
            Assert.That(bar.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(
                WorldRect(bar).Overlaps(WorldRect(view.SkillBanner.BackdropCanvas)),
                Is.False
            );

            // Ending the turn goes through the enemies' turns to the next party turn.
            view.EndTurnButton.onClick.Invoke();
            yield return WaitEnemyTurn(view);
            Assert.That(view.UpcomingTurns(6), Is.EqualTo(new[] { party, party, 2, party, 1, 0 }));
            Assert.That(view.TurnSlots[1].Party.activeSelf, Is.True);
            Assert.That(view.TurnSlots[1].Frame.color, Is.EqualTo(view.PartyTurnFrame));
            Assert.That(view.TurnSlots[2].Frame.color, Is.EqualTo(view.EnemyTurnFrame));
            yield return WaitDealt(view);

            // A defeated enemy leaves the order.
            var slime = view.Enemies[0];
            slime.Hp = 1;
            Play(view, 0, slime.TargetArea);
            yield return WaitActions(view);
            Assert.That(slime.Alive, Is.False);
            Assert.That(view.UpcomingTurns(6), Has.No.Member(0));
        }

        [UnityTest]
        public IEnumerator ThePlayedCardsUserStepsForwardWithItsNameShownAndComesBack()
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

            // One step toward the enemies, the HP bar with it.
            float step = view.Settings.StepDistance;
            for (float t = 0f; t < view.Settings.StepTime + 0.1f; t += Time.deltaTime)
                yield return null;
            Assert.That(user.Body.anchoredPosition.x - home.x, Is.EqualTo(step).Within(0.01f));
            Assert.That(user.HpBar.anchoredPosition.x - barHome.x, Is.EqualTo(step).Within(0.01f));

            // The blow lands as the skill's effect strikes, while the user still stands forward.
            var wolf = view.Enemies[1];
            for (float t = 0f; wolf.Hp == wolf.MaxHp && t < 2f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(wolf.Hp, Is.LessThan(wolf.MaxHp));
            Assert.That(user.Body.anchoredPosition.x - home.x, Is.EqualTo(step).Within(0.01f));

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
        public IEnumerator ARaisedCardsUserStepsForwardWithARingAndItsName()
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
            float step = view.Settings.StepDistance;
            for (int i = 0; i < view.Allies.Length; i++)
            {
                var ally = view.Allies[i];
                bool isUser = i == slash.Caster;
                Assert.That(ally.CasterRing.activeSelf, Is.EqualTo(isUser));
                Assert.That(ally.NameTag.activeSelf, Is.EqualTo(isUser));
                Assert.That(
                    ally.Body.anchoredPosition.x - homes[i].x,
                    Is.EqualTo(isUser ? step : 0f).Within(0.01f),
                    "Only the user steps forward; the others stay put."
                );
            }
            Assert.That(user.HpBar.anchoredPosition.x - barHome.x, Is.EqualTo(step).Within(0.01f));
            Assert.That(
                user.NameTag.GetComponent<TranslucentTextPanel>().Label.text,
                Is.EqualTo(slash.Body.GetComponent<BattleInspectCardView>().Owner.text),
                "The tag shows the owner named on the card."
            );

            // Played, the user goes on from its step instead of going home first.
            view.TapScreen(Center(view.Enemies[1].TargetArea));
            yield return null;
            Assert.That(user.CasterRing.activeSelf, Is.False);
            Assert.That(user.NameTag.activeSelf, Is.False);
            Assert.That(
                user.Body.anchoredPosition.x - homes[slash.Caster].x,
                Is.EqualTo(step).Within(0.01f)
            );
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
            Assert.That(view.HandCards, Has.Count.EqualTo(4));
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

            view.EndTurnButton.onClick.Invoke();
            // Cards cannot be pressed while the enemies act.
            view.PressCard(view.HandCards[0], CardPoint(view.Cards[view.HandCards[0]]));
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

            // The enemies' band comes up in red, and the wolf waits for it before stepping out.
            view.EndTurnButton.onClick.Invoke();
            yield return null;
            Assert.That(banner.gameObject.activeSelf, Is.True);
            Assert.That(banner.Label.text, Is.EqualTo(BattleInspectView.EnemyTurnText));
            Assert.That(banner.Label.color, Is.EqualTo(view.EnemyBannerText));
            Assert.That(
                view.TurnBannerLines.All(line => line.color == view.EnemyBannerLine),
                Is.True
            );
            for (float t = 0f; t < view.Settings.TurnBannerHold * 0.8f; t += Time.deltaTime)
            {
                Assert.That(wolf.Body.anchoredPosition, Is.EqualTo(wolfHome));
                yield return null;
            }

            // The party's band comes up in blue with the turn's number, over the deal.
            yield return WaitEnemyTurn(view);
            Assert.That(view.Dealing, Is.True);
            Assert.That(banner.gameObject.activeSelf, Is.True);
            Assert.That(
                banner.Label.text,
                Is.EqualTo(BattleInspectView.PartyTurnText(view.StartTurn + 1, again: false))
            );
            Assert.That(banner.Label.color, Is.EqualTo(view.PartyBannerText));
            Assert.That(
                view.TurnBannerLines.All(line => line.color == view.PartyBannerLine),
                Is.True
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
        public IEnumerator APlayedSkillDarkensTheStagePlaysItsEffectAndSettles()
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
            float darkest = 0f;
            int most = 0;
            for (float t = 0f; view.Acting && t < 5f; t += Time.unscaledDeltaTime)
            {
                darkest = Mathf.Max(darkest, vfx.DimAlpha);
                most = Mathf.Max(most, vfx.LiveCount);
                yield return null;
            }
            Assert.That(view.Acting, Is.False, "The action ends.");
            Assert.That(darkest, Is.GreaterThan(0.3f), "The stage darkens behind the actors.");
            Assert.That(most, Is.GreaterThan(20), "The fireball bursts into light and embers.");
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
        public IEnumerator TheBattlefieldIsDrawnByTheCameraThroughBloom()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var stage = view.Enemies[0].TargetArea.GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(stage.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(
                stage.worldCamera,
                Is.Not.Null,
                "The scene's camera draws the battlefield."
            );
            Assert.That(
                stage.worldCamera.GetUniversalAdditionalCameraData().renderPostProcessing,
                Is.True
            );
            var volume = stage.GetComponentInChildren<Volume>();
            Assert.That(volume, Is.Not.Null);
            Assert.That(volume.sharedProfile.TryGet<Bloom>(out var bloom), Is.True);
            Assert.That(bloom.active, Is.True);
            Assert.That(
                bloom.threshold.value,
                Is.GreaterThanOrEqualTo(1f),
                "Only light past white spreads, so the pixel art stays crisp."
            );
            var controls = view.Hand.GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(controls.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(
                view.Vfx.Flash.canvas.rootCanvas,
                Is.SameAs(controls),
                "The flash is not bloomed."
            );
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
            Assert.That(stageCamera.Camera.orthographic, Is.False);
            var stage = view.Vfx.Stage;
            Assert.That(
                stage.Find("Backdrop/Background").gameObject.activeInHierarchy,
                Is.False,
                "The 3D stage takes the painted background's place."
            );

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
                Assert.That(
                    board.ShadowRenderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.ShadowsOnly),
                    "An upright board casts the shadow."
                );
                Assert.That(
                    board.ContactRenderer.enabled,
                    Is.True,
                    "A contact shadow ties it down."
                );

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

            // Bars, tags and the front effects over the boards; the back effects under them.
            int boardOrder = view.Allies[0].Body.GetComponent<Hd2dUiBillboard>().SortingOrder;
            Assert.That(
                view.Allies[0].HpBar.GetComponentInParent<Canvas>().sortingOrder,
                Is.GreaterThan(boardOrder)
            );
            Assert.That(
                view.Allies[0].CasterRing.GetComponent<Canvas>().sortingOrder,
                Is.LessThan(boardOrder),
                "The ring lies on the floor behind its ally."
            );
            Assert.That(
                view.Vfx.BackLayer.GetComponentInParent<Canvas>().sortingOrder,
                Is.LessThan(boardOrder)
            );
            Assert.That(
                view.Vfx.FrontLayer.GetComponentInParent<Canvas>().sortingOrder,
                Is.GreaterThan(boardOrder)
            );
        }

        [UnityTest]
        public IEnumerator TheCameraSlowlyCirclesTheBattlefieldButNotTheControls()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var stage = view.Vfx.Stage;
            var drift = stage.parent.GetComponent<BattleStageDrift>();
            Assert.That(drift, Is.Not.Null, "The stage moves inside the camera's circle.");
            Assert.That(view.Enemies[0].TargetArea.IsChildOf(stage), Is.True);
            Assert.That(view.Hand.IsChildOf(drift.transform), Is.False);
            var pixelArt = view.ActorMaterial;
            Assert.That(pixelArt, Is.Not.Null);
            Assert.That(pixelArt.shader.name, Is.EqualTo("Baryonyx/UI Pixel Art"));
            Assert.That(
                ActorMaterials(view),
                Has.All.SameAs(pixelArt),
                "The characters stay sharp between screen pixels."
            );
            var background = stage.Find("Backdrop/Background").GetComponent<RawImage>();
            Assert.That(background.material, Is.SameAs(pixelArt));

            // Sped up from its default (once round in tens of seconds) to see it go round.
            drift.Period = 1f;
            var hand = view.Hand.position;
            var drawn = new List<Vector2>();
            for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                drawn.Add(((RectTransform)drift.transform).anchoredPosition);
            }
            Assert.That(
                drawn.Select(offset => offset.magnitude),
                Has.All.EqualTo(drift.Radius).Within(0.01f),
                "The view's centre keeps on the circle."
            );
            Assert.That(drawn.Max(offset => offset.x), Is.GreaterThan(drift.Radius * 0.7f));
            Assert.That(drawn.Min(offset => offset.x), Is.LessThan(-drift.Radius * 0.7f));
            Assert.That(drawn.Max(offset => offset.y), Is.GreaterThan(drift.Radius * 0.7f));
            Assert.That(drawn.Min(offset => offset.y), Is.LessThan(-drift.Radius * 0.7f));
            Assert.That(view.Hand.position, Is.EqualTo(hand), "The controls stay still.");
        }

        [UnityTest]
        public IEnumerator TheCameraKeepsCirclingThroughASkillAndItsHitStop()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var vfx = view.Vfx;
            var drift = (RectTransform)vfx.Stage.parent;

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
        public IEnumerator ABlowLightsTheCharactersNearItAndLeavesTheFarOnesDark()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var vfx = view.Vfx;
            var wolf = view.Enemies[1];
            var slime = view.Enemies[0];
            var farAlly = view.Allies[1];
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
            Assert.That(vfx.LightOf(slime.Sprite).Lit, Is.True, "...and the enemy beside it.");
            Assert.That(vfx.LightOf(farAlly.Sprite).Lit, Is.False, "The far side stays dark.");

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
            Assert.That(
                shapes.Select(image => image.material),
                Has.All.Matches<Material>(material => vfx.Shapes.Contains(material))
            );
            Assert.That(
                shapes.Select(image => image.uvRect),
                Has.All.EqualTo(new Rect(0f, 0f, 1f, 1f))
            );
            Assert.That(
                shapes.Any(image => image.material == vfx.MaterialOf(BattleVfxShape.Pillar)),
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
            Assert.That(
                stageRect.localScale.x,
                Is.LessThan(0.5f),
                "As a root drawn by the camera, Unity scales it down to world units."
            );

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
            float shook = 0f;
            for (float t = 0f; !vfx.HitStopping && t < 2f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(vfx.HitStopping, Is.True, "The cut lands with a hit stop.");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(wolf.Hp, Is.LessThan(wolf.MaxHp), "The stop comes with the blow.");

            float stopped = Time.realtimeSinceStartup;
            while (vfx.HitStopping && Time.realtimeSinceStartup - stopped < 1f)
            {
                shook = Mathf.Max(shook, vfx.Stage.anchoredPosition.magnitude);
                yield return null;
            }
            Assert.That(vfx.HitStopping, Is.False);
            Assert.That(Time.realtimeSinceStartup - stopped, Is.LessThan(0.25f), "Only a moment.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(shook, Is.GreaterThan(0f), "The stage shakes even while the battle stops.");
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
            // 居合一閃 (cost 5) is dealt into the opening hand in place of the guard.
            int iai = DeckIndexOf(view, "Iai");
            view.RestartDeal(
                new[] { 0, 1, 2, 3, 4, iai }
                    .Concat(Enumerable.Range(0, view.Deck.Length).Where(i => i > 4 && i != iai))
                    .ToArray(),
                instant: true
            );
            int index = System.Array.FindIndex(
                view.Cards,
                card => card.InHand && card.DeckIndex == iai
            );
            var card = view.Cards[index];
            Assert.That(card.Cost, Is.GreaterThanOrEqualTo(vfx.CutInCost));
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
                new[] { 0, 1, 2, 3, 4, deck }
                    .Concat(Enumerable.Range(0, view.Deck.Length).Where(i => i > 4 && i != deck))
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
            int bash = DealInto(view, "ShieldBash");
            Play(view, bash, slime.TargetArea);
            yield return WaitActions(view);
            Assert.That(slime.Hp, Is.LessThan(slime.MaxHp));
            Assert.That(view.BlockOf(0), Is.GreaterThan(0), "Every ally takes the block.");
            Assert.That(view.BlockOf(3), Is.EqualTo(view.BlockOf(0)));
            int partyHp = view.Allies.Sum(ally => ally.Hp);

            view.EndTurnButton.onClick.Invoke();
            for (float t = 0f; view.EnemyTurn && t < 15f; t += Time.deltaTime)
                yield return null;
            int lost = partyHp - view.Allies.Sum(ally => ally.Hp);
            Assert.That(lost, Is.LessThan(wolf.Power + slime.Power));
            // The block lasts until the party's next turn.
            Assert.That(view.BlockOf(0), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ACardThatDrawsAddsCardsToTheHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int scout = DealInto(view, "Scout");
            Assert.That(
                view.Cards[scout].TargetsWholeSide,
                Is.True,
                "A card for no one needs no target."
            );
            int before = view.HandCards.Count;

            Play(view, scout, view.Allies[0].TargetArea);
            yield return WaitActions(view);
            for (float t = 0f; view.Dealing && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.HandCards, Has.Count.EqualTo(before - 1 + 2));
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
        public IEnumerator ABeatenEnemyCrumblesAwayDotByDot()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            wolf.Hp = 1;

            Play(view, 0, wolf.TargetArea);
            for (float t = 0f; wolf.Alive && t < 3f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(wolf.Alive, Is.False);
            for (float t = 0f; wolf.Group.alpha > 0f && t < 1f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(wolf.Group.alpha, Is.EqualTo(0f), "The wolf is gone at once.");

            // Its picture is drawn again where it stood, to be eaten away.
            var body = view
                .Vfx.FrontLayer.GetComponentsInChildren<RawImage>()
                .Single(image => image.texture == wolf.Sprite.texture);
            Assert.That(body.material.shader, Is.SameAs(view.Vfx.DefeatMaterial.shader));
            Assert.That(body.material.GetTexture("_OrderTex"), Is.Not.Null);

            // Its dots fly off as square motes, never turned.
            var mote = view.Vfx.MaterialOf(BattleVfxShape.Mote);
            int most = 0;
            // The board is used again by later effects once the crumble is over.
            bool Crumbling() => body.isActiveAndEnabled && body.texture == wolf.Sprite.texture;
            for (float t = 0f; Crumbling() && t < 3f; t += Time.unscaledDeltaTime)
            {
                var motes = view
                    .Vfx.FrontLayer.GetComponentsInChildren<RawImage>()
                    .Where(image => image.material == mote && image.color.a > 0f)
                    .ToList();
                most = Mathf.Max(most, motes.Count);
                Assert.That(motes.All(image => image.rectTransform.localEulerAngles.z == 0f));
                yield return null;
            }
            Assert.That(Crumbling(), Is.False, "It is eaten away to nothing.");
            Assert.That(most, Is.GreaterThanOrEqualTo(20), "Its dots fly off.");
            Assert.That(wolf.Sprite.color, Is.EqualTo(Color.white), "It comes back untinted.");
            yield return WaitActions(view);
        }

        [UnityTest]
        public IEnumerator AnEnemysBlowLandsWithABurstOnTheAlly()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int partyHp = view.Allies.Sum(ally => ally.Hp);
            view.EndTurnButton.onClick.Invoke();
            int most = 0;
            for (float t = 0f; view.EnemyTurn && t < 10f; t += Time.unscaledDeltaTime)
            {
                if (view.Allies.Sum(ally => ally.Hp) < partyHp)
                    most = Mathf.Max(most, view.Vfx.LiveCount);
                yield return null;
            }
            Assert.That(view.EnemyTurn, Is.False);
            Assert.That(most, Is.GreaterThan(5), "Claw marks and sparks burst on the ally.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
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
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
