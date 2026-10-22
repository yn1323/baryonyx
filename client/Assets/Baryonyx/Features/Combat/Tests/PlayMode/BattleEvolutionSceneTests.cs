using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.SkillEvolution;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    /// <summary>A unique skill's card evolving in the battle (doc/features/skill-evolution.md#戦闘中の進化).</summary>
    public sealed class BattleEvolutionSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity";
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator AUniqueSkillUsedEvolvesIntoTheStagePickedAndThatStageIsPlayed()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseParty(view, "ManaBurst");
            var evolved = new List<(string Person, int Unique, int Stage)>();
            var draws = 0;
            view.Evolution = new BattleEvolution
            {
                Chance = level => 1f,
                Draw = () =>
                {
                    draws++;
                    return 0f;
                },
                Acquired = (person, unique, stage) => stage == 5,
                Evolved = (person, unique, stage) => evolved.Add((person, unique, stage)),
            };
            int deck = DeckOf(view, "ManaBurst");
            Play(view, DealInto(view, deck), view.Enemies[0].TargetArea);

            // Before its effect the card rises and its stages are offered, the hand set aside.
            yield return WaitUntil(() => view.Choice.IsOpen, "The stages are offered.");
            Assert.That(view.Evolving, Is.True);
            Assert.That(view.Choice.Count, Is.EqualTo(3));
            Assert.That(view.Choice.AcquiredTags[2].activeSelf, Is.True, "Stage 5 is acquired.");
            Assert.That(view.Choice.AcquiredTags[0].activeSelf, Is.False);
            Assert.That(view.Hand.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(evolved, Is.Empty, "Nothing evolves before the pick.");

            view.Choice.Select(1);
            view.Choice.Confirm();
            yield return WaitActions(view);
            Assert.That(evolved, Is.EqualTo(new[] { ("toma", 0, 3) }));
            Assert.That(
                view.Deck[deck].Skill,
                Is.EqualTo("ManaBurst.3"),
                "The card is the stage now."
            );
            Assert.That(view.Deck[deck].Person, Is.EqualTo("toma"));
            Assert.That(view.HasEvolved(deck), Is.True);
            Assert.That(view.Evolving, Is.False);
            Assert.That(view.Hand.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));

            // A card evolves once a battle: used again, it is played at once.
            Play(view, DealInto(view, deck), view.Enemies[0].TargetArea);
            yield return null;
            Assert.That(view.Choice.IsOpen, Is.False);
            Assert.That(view.Evolving, Is.False);
            yield return WaitActions(view);
            Assert.That(evolved, Has.Count.EqualTo(1));
            Assert.That(draws, Is.EqualTo(1), "No draw for a card evolved already.");
        }

        [UnityTest]
        public IEnumerator NothingEvolvesWhenTheDrawMissesOrOutsideAnAdventure()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseParty(view, "ManaBurst");
            int deck = DeckOf(view, "ManaBurst");

            // Outside an adventure nothing evolves.
            view.Evolution = null;
            Play(view, DealInto(view, deck), view.Enemies[0].TargetArea);
            yield return null;
            Assert.That(view.Choice.IsOpen, Is.False);
            yield return WaitActions(view);
            Assert.That(view.Deck[deck].Skill, Is.EqualTo("ManaBurst"));

            // A draw over the chance misses.
            var seen = new List<int>();
            view.Evolution = new BattleEvolution
            {
                Chance = level =>
                {
                    seen.Add(level);
                    return 0.04f;
                },
                Draw = () => 0.5f,
            };
            Play(view, DealInto(view, deck), view.Enemies[0].TargetArea);
            yield return null;
            Assert.That(view.Choice.IsOpen, Is.False);
            yield return WaitActions(view);
            Assert.That(view.Deck[deck].Skill, Is.EqualTo("ManaBurst"));
            Assert.That(
                seen,
                Is.EqualTo(new[] { view.Allies[0].Level }),
                "The chance is for the user's level."
            );
            Assert.That(view.HasEvolved(deck), Is.False);
        }

        [UnityTest]
        public IEnumerator FromAFirstStepItsOneStageGoesOnByItself()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseParty(view, "ManaBurst.1");
            view.Evolution = new BattleEvolution { Chance = level => 1f, Draw = () => 0f };
            int deck = DeckOf(view, "ManaBurst.1");
            Play(view, DealInto(view, deck), view.Enemies[0].TargetArea);
            yield return WaitUntil(() => view.Choice.IsOpen, "Its stage is shown.");
            Assert.That(view.Choice.Count, Is.EqualTo(1));
            yield return WaitActions(view);
            Assert.That(view.Deck[deck].Skill, Is.EqualTo("ManaBurst.2"));
        }

        // The battle's own party, the first ally bringing a unique skill's stage of "toma".
        private static void UseParty(BattleInspectView view, string stage)
        {
            Assert.That(UniqueSkillEvolution.TryFind(stage, out var tree, out int at), stage);
            var slots = new BattlePartyMember[view.Allies.Length];
            for (int place = 0; place < view.Allies.Length; place++)
            {
                var ally = view.Allies[place];
                var cards = view
                    .Deck.Where(data => data.Caster == place)
                    .Take(2)
                    .Select(data => BattleInspectView.SkillOf(data))
                    .Where(skill => skill != null)
                    .Select(skill => new BattlePartyCard { Skill = skill })
                    .ToList();
                if (place == 0)
                    cards.Add(
                        new BattlePartyCard
                        {
                            Skill = tree.StageAt(at).Card,
                            Person = "toma",
                            Unique = 0,
                        }
                    );
                slots[ally.Slot] = new BattlePartyMember
                {
                    Name = ally.Name,
                    Art = ally.Sprite.texture,
                    Level = ally.Level,
                    Stats = ally.Stats,
                    Cards = cards,
                };
            }
            view.UseParty(slots);
        }

        private static int DeckOf(BattleInspectView view, string skill)
        {
            int deck = System.Array.FindIndex(view.Deck, data => data.Skill == skill);
            Assert.That(deck, Is.GreaterThanOrEqualTo(0), skill);
            return deck;
        }

        // Deals the card of the deck into a fresh opening hand (with the turn's energy), and returns its card.
        private static int DealInto(BattleInspectView view, int deck)
        {
            view.RestartDeal(
                new[] { deck }
                    .Concat(Enumerable.Range(0, view.Deck.Length).Where(i => i != deck))
                    .ToArray(),
                instant: true
            );
            return System.Array.FindIndex(
                view.Cards,
                card => card.InHand && card.DeckIndex == deck
            );
        }

        private static void Play(BattleInspectView view, int card, RectTransform target)
        {
            Assert.That(card, Is.GreaterThanOrEqualTo(0), "The card is in the hand.");
            var body = view.Cards[card].Body;
            var rect = body.rect;
            var press = RectTransformUtility.WorldToScreenPoint(
                null,
                body.TransformPoint(new Vector2(rect.center.x, rect.yMax - 40f))
            );
            var aim = BattleInspectView.ScreenPointOf(target, target.rect.center);
            view.PressCard(card, press);
            view.DragCard(aim);
            view.ReleaseCard(aim);
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, string message)
        {
            for (float t = 0f; !condition() && t < 6f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private static IEnumerator WaitActions(BattleInspectView view)
        {
            for (float t = 0f; view.Acting && t < 10f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(view.Acting, Is.False, "The actions end.");
        }

        private IEnumerator Load(System.Action<BattleInspectView> found)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            found(
                loadedScene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BattleInspectView>(true))
                    .Single()
            );
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Time.timeScale = 1f;
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
