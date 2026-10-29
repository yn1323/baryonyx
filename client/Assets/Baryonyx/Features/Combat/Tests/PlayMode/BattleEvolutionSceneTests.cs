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
    /// <summary>
    /// A unique skill's card lighting up as it is drawn and evolving when used in the battle
    /// (doc/features/skill-evolution.md#戦闘中の覚醒).
    /// </summary>
    public sealed class BattleEvolutionSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity";
        private Scene loadedScene;

        private FixedRandom random;

        // 手札を配る順、狙う相手、敵の行動を、いつも同じ乱数で決める。
        [SetUp]
        public void FixRandom() => random = FixedRandom.Use();

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
            int dealt = DealInto(view, deck);

            // Drawn under the chance, it lights up in the hand.
            Assert.That(draws, Is.EqualTo(1), "The draw is made as the card comes into the hand.");
            Assert.That(view.HasOmen(deck), Is.True);
            Assert.That(view.Glints(dealt), Is.True, "The card lit shows its light.");
            Play(view, dealt, view.Enemies[0].TargetArea);
            Assert.That(view.Glints(dealt), Is.False, "Its light goes with it out of the hand.");

            // Before its effect the card rises and its stages are offered, the hand set aside.
            yield return WaitUntil(() => view.Choice.IsOpen, "The stages are offered.");
            Assert.That(view.Evolving, Is.True);
            Assert.That(view.Choice.Count, Is.EqualTo(3));
            Assert.That(view.Choice.AcquiredTags[2].activeSelf, Is.True, "Stage 5 is acquired.");
            Assert.That(view.Choice.AcquiredTags[0].activeSelf, Is.False);
            Assert.That(view.Hand.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(evolved, Is.Empty, "Nothing evolves before the pick.");
            // The check confirming stands where the end of the turn is, pressed once a stage is picked.
            Assert.That(view.EndTurnButton.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(view.Choice.ConfirmButton.interactable, Is.False);

            view.Choice.Select(1);
            yield return null;
            Assert.That(view.Choice.ConfirmButton.interactable, Is.True);
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
            Assert.That(view.HasOmen(deck), Is.False, "Its light is spent.");
            Assert.That(view.Evolving, Is.False);
            Assert.That(view.Hand.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(view.EndTurnButton.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));

            // A card evolves once a battle: drawn again it stays unlit, and is played at once.
            dealt = DealInto(view, deck);
            Assert.That(view.Glints(dealt), Is.False);
            Play(view, dealt, view.Enemies[0].TargetArea);
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
            int dealt = DealInto(view, deck);
            Assert.That(view.Glints(dealt), Is.False, "A card drawn over the chance stays unlit.");
            // The draw was made as it came into the hand: a draw under the chance now changes nothing.
            view.Evolution.Draw = () => 0f;
            Play(view, dealt, view.Enemies[0].TargetArea);
            yield return null;
            Assert.That(view.Choice.IsOpen, Is.False);
            yield return WaitActions(view);
            Assert.That(view.Deck[deck].Skill, Is.EqualTo("ManaBurst"));
            Assert.That(
                seen,
                Is.EqualTo(new[] { view.Allies[0].Level }),
                "The chance is for the user's level, drawn once as it came into the hand."
            );
            Assert.That(view.HasEvolved(deck), Is.False);
        }

        [UnityTest]
        public IEnumerator FromAFirstStepItsOneStageIsPickedAndConfirmed()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseParty(view, "ManaBurst.1");
            view.Evolution = new BattleEvolution { Chance = level => 1f, Draw = () => 0f };
            int deck = DeckOf(view, "ManaBurst.1");
            Play(view, DealInto(view, deck), view.Enemies[0].TargetArea);
            yield return WaitUntil(() => view.Choice.IsOpen, "Its stage is shown.");
            Assert.That(view.Choice.Count, Is.EqualTo(1));
            // 1つでも、押して選んで決めるまで待つ。
            for (int frame = 0; frame < 90; frame++)
                yield return null;
            Assert.That(view.Choice.IsOpen, Is.True, "It waits for a tap.");
            Assert.That(view.Deck[deck].Skill, Is.EqualTo("ManaBurst.1"));
            view.Choice.Select(0);
            view.Choice.Confirm();
            yield return WaitActions(view);
            Assert.That(view.Deck[deck].Skill, Is.EqualTo("ManaBurst.2"));
        }

        // 持っているスキルの1枚（doc/features/skill-awakening.md）：引いて光り、使うと2つの枝が
        // 出る。選んだ枝になって、その1枚と枝を知らせる。
        [UnityTest]
        public IEnumerator AnOwnedSkillCardAwakensIntoTheBranchPicked()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseOwnedCard(view, "Fire", "card-7");
            var awakened = new List<(string Card, int Branch)>();
            view.Evolution = new BattleEvolution
            {
                AwakenChance = level => 1f,
                Draw = () => 0f,
                Awakened = (card, branch) => awakened.Add((card, branch)),
            };
            int deck = DeckOf(view, "Fire");
            int dealt = DealInto(view, deck);
            Assert.That(view.Glints(dealt), Is.True, "The owned card lights up.");
            Play(view, dealt, view.Enemies[0].TargetArea);
            yield return WaitUntil(() => view.Choice.IsOpen, "Its branches are offered.");
            Assert.That(view.Choice.Count, Is.EqualTo(2));
            view.Choice.Select(1);
            view.Choice.Confirm();
            yield return WaitActions(view);
            Assert.That(view.Deck[deck].Skill, Is.EqualTo("Fire.2"));
            Assert.That(view.Deck[deck].OwnedCard, Is.EqualTo("card-7"));
            Assert.That(awakened, Is.EqualTo(new[] { ("card-7", 2) }));
            Assert.That(view.HasEvolved(deck), Is.True);
        }

        // The battle's own party, the first ally bringing the owned card of a card skill.
        private static void UseOwnedCard(BattleInspectView view, string skill, string card)
        {
            var slots = new BattlePartyMember[view.Allies.Length];
            for (int place = 0; place < view.Allies.Length; place++)
            {
                var ally = view.Allies[place];
                var cards = view
                    .Deck.Where(data => data.Caster == place && data.Skill != skill)
                    .Take(2)
                    .Select(data => BattleInspectView.SkillOf(data))
                    .Where(found => found != null)
                    .Select(found => new BattlePartyCard { Skill = found })
                    .ToList();
                if (place == 0)
                    cards.Add(
                        new BattlePartyCard { Skill = CardSkills.Find(skill), OwnedCard = card }
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
            random?.Dispose();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
