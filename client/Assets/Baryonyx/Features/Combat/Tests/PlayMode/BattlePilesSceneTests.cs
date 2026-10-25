using System.Collections;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // カードの動きのキーワード（doc/features/combat.md のカードのキーワード）：消滅・初手・保持・霧散・眠り札、
    // 効果で捨てる・捨てたとき・捨て札から拾う、連携と合体を、戦闘画面のモックで確かめる。
    public sealed class BattlePilesSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity";
        private Scene loadedScene;
        private FixedRandom random;

        [SetUp]
        public void FixRandom() => random = FixedRandom.Use();

        [UnityTest]
        public IEnumerator AnInnateCardIsDealtFirstAndAnExhaustedOneLeavesTheBattle()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int warCry = Swap(view, 10, "WarCry");
            int prayer = Swap(view, 11, "ManaPrayer");

            // 初手：鬨の声は、切り混ぜた山札からでも初めの手札に入る。
            view.RestartDeal(instant: true);
            Assert.That(DeckInHand(view), Has.Member(warCry));

            // 消滅：マナの祈りは使うとその戦闘から除かれ、消滅の数が出る。
            int index = DealFirst(view, prayer);
            Play(view, index, view.Allies[0].TargetArea);
            yield return WaitActions(view);
            Assert.That(view.Energy, Is.EqualTo(BattleInspectView.EnergyPerTurn + 2));
            Assert.That(view.ExhaustedCards, Is.EqualTo(new[] { prayer }));
            Assert.That(view.DiscardPile, Has.No.Member(prayer));
            Assert.That(view.ExhaustAnchor.gameObject.activeSelf, Is.True);
            Assert.That(view.ExhaustLabel.text, Is.EqualTo("1"));
        }

        [UnityTest]
        public IEnumerator AtTheTurnsEndRetainStaysEtherealVanishesAndASleeperWakes()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int protect = Swap(view, 10, "Protect");
            int purify = Swap(view, 11, "Purify");
            int regen = Swap(view, 12, "Regen");
            view.RestartDeal(
                new[] { protect, purify, regen, 0, 1 }
                    .Concat(Enumerable.Range(2, view.Deck.Length - 2).Where(i => i < 10 || i > 12))
                    .ToArray(),
                instant: true
            );

            view.EndTurn();
            yield return WaitEnemyTurn(view);
            yield return WaitDealt(view);
            Assert.That(DeckInHand(view), Has.Member(protect), "保持 stays in the hand.");
            Assert.That(view.ExhaustedCards, Is.EqualTo(new[] { purify }), "霧散 leaves the battle.");
            Assert.That(view.DiscardPile, Has.Member(regen));
            Assert.That(view.SleepOf(regen), Is.EqualTo(1), "眠り札 wakes a stage as it flows away.");
            Assert.That(view.DiscardPile, Is.EquivalentTo(new[] { regen, 0, 1 }));
        }

        [UnityTest]
        public IEnumerator ACardDiscardedByAnEffectWorksItsDiscardEffect()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int scout = Swap(view, 10, "Scout");
            int icicles = Swap(view, 11, "Icicles");
            view.RestartDeal(
                new[] { scout, icicles, 0, 1, 2 }
                    .Concat(Enumerable.Range(3, view.Deck.Length - 3).Where(i => i < 10 || i > 11))
                    .ToArray(),
                instant: true
            );
            foreach (var enemy in view.Enemies)
                enemy.Hp = enemy.MaxHp = 999999;

            // 偵察：2枚引いてから、手札を1枚選んで捨てる。
            Play(view, DealFirst(view, scout), view.Allies[0].TargetArea);
            for (float t = 0f; view.Choosing == BattleInspectView.PileChoice.None && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.Choosing, Is.EqualTo(BattleInspectView.PileChoice.Discard));
            Assert.That(view.HandCards, Has.Count.EqualTo(6), "4 left and 2 drawn.");
            Assert.That(view.EndTurnButton.interactable, Is.False, "The turn waits for the pick.");

            int card = System.Array.FindIndex(view.Cards, c => c.InHand && c.DeckIndex == icicles);
            view.PressCard(card, Vector2.zero);
            yield return WaitActions(view);
            Assert.That(view.Choosing, Is.EqualTo(BattleInspectView.PileChoice.None));
            Assert.That(view.DiscardPile, Has.Member(icicles));
            // つらら落としの捨てたとき：ランダムな敵に2回当たる。
            Assert.That(view.Enemies.Sum(enemy => enemy.MaxHp - enemy.Hp), Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ACardOfTheDiscardPileIsPickedBackIntoTheHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int revelation = Swap(view, 10, "Revelation");
            view.RestartDeal(
                new[] { revelation, 0, 1, 2, 3 }
                    .Concat(Enumerable.Range(4, view.Deck.Length - 4).Where(i => i != 10))
                    .ToArray(),
                instant: true
            );
            foreach (var enemy in view.Enemies)
                enemy.Hp = enemy.MaxHp = 999999;
            // Energy left over keeps the turn from ending by itself (which would flow the hand away).
            typeof(BattleInspectView).GetProperty("Energy").SetValue(view, 9);
            // 斬り払いとファイアを使い、捨て札を2枚にしてから天啓で1枚拾う。
            Play(view, DealFirst(view, 0), view.Enemies[1].TargetArea);
            Play(view, DealFirst(view, 1), view.Enemies[1].TargetArea);
            yield return WaitActions(view);
            Assert.That(view.DiscardPile, Is.EquivalentTo(new[] { 0, 1 }));
            Play(view, DealFirst(view, revelation), view.Allies[0].TargetArea);
            for (float t = 0f; view.Choosing == BattleInspectView.PileChoice.None && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.Choosing, Is.EqualTo(BattleInspectView.PileChoice.Take));
            Assert.That(view.ChoiceCards, Is.Not.Empty);

            int picked = view.ChoiceCards[0];
            int deckIndex = view.Cards[picked].DeckIndex;
            view.PressCard(picked, Vector2.zero);
            yield return WaitActions(view);
            Assert.That(DeckInHand(view), Has.Member(deckIndex));
            Assert.That(view.DiscardPile, Has.No.Member(deckIndex));
            Assert.That(view.ExhaustedCards, Has.Member(revelation), "天啓 leaves the battle.");
        }

        [UnityTest]
        public IEnumerator AComboWorksAfterItsPartnerAndTwoCardsFuseInTheHand()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            int armor = Swap(view, 10, "ArmorBreak");
            int splitter = Swap(view, 11, "EarthSplitter");
            foreach (var enemy in view.Enemies)
                enemy.Hp = enemy.MaxHp = 999999;
            view.RestartDeal(
                new[] { armor, splitter, 0, 1, 2 }
                    .Concat(Enumerable.Range(3, view.Deck.Length - 3).Where(i => i < 10 || i > 11))
                    .ToArray(),
                instant: true
            );
            BattleOdds.Fix(critical: false, lands: true);
            // 連携：鎧砕きのあとの大地割りは、先に敵全体を防御ダウンにする。
            Play(view, DealFirst(view, armor), view.Enemies[0].TargetArea);
            yield return WaitActions(view);
            Assert.That(view.HasStatus(false, 1, CardStatus.Vulnerable), Is.False);
            Play(view, DealFirst(view, splitter), view.Enemies[1].TargetArea);
            yield return WaitActions(view);
            Assert.That(
                Enumerable.Range(0, view.Enemies.Length).All(i => view.HasStatus(false, i, CardStatus.Vulnerable)),
                Is.True
            );

            // 合体：斬り払いとフレイムエンチャントが手札にそろうと、炎の斬り払いの1枚になる。
            int enchant = Swap(view, 12, "FlameEnchant");
            view.RestartDeal(
                new[] { 0, enchant, 1, 2, 3 }
                    .Concat(Enumerable.Range(4, view.Deck.Length - 4).Where(i => i < 10 || i > 12))
                    .ToArray(),
                instant: false
            );
            yield return WaitDealt(view);
            Assert.That(view.HandCards, Has.Count.EqualTo(4));
            var fused = view.HandCards.Select(i => view.Deck[view.Cards[i].DeckIndex]).Single(data => data.Generated);
            Assert.That(fused.Skill, Is.EqualTo("FlameSlash"));
            Assert.That(DeckInHand(view), Has.No.Member(0).And.No.Member(enchant));
        }

        // 見本の山札の slot 番目を、同じ使い手のまま skill のカードに替える。
        private static int Swap(BattleInspectView view, int slot, string skill)
        {
            var card = CardSkills.Find(skill);
            var was = view.Deck[slot];
            view.Deck[slot] = new BattleInspectCardData
            {
                Name = card.Name,
                Owner = was.Owner,
                Skill = card.Id,
                Kind = Baryonyx.UI.Cards.CardText.KindLine(card),
                Description = Baryonyx.UI.Cards.CardText.Description(card),
                Art = was.Art,
                Frame = was.Frame,
                Cost = card.Cost,
                Element = BattleCardText.ElementOf(card.Element),
                Effect = BattleCardText.EffectOf(card),
                Caster = was.Caster,
            };
            return slot;
        }

        private static int[] DeckInHand(BattleInspectView view) =>
            view.HandCards.Select(i => view.Cards[i].DeckIndex).ToArray();

        // 手札の中の deck のカード（画面のカードの番号）。
        private static int DealFirst(BattleInspectView view, int deck)
        {
            int index = System.Array.FindIndex(view.Cards, card => card.InHand && card.DeckIndex == deck);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"Card {deck} is in the hand.");
            return index;
        }


        private static void Play(BattleInspectView view, int card, RectTransform target)
        {
            var point = Center(view.Cards[card].Body);
            view.PressCard(card, point);
            view.DragCard(Center(target));
            view.ReleaseCard(Center(target));
        }

        private static Vector2 Center(RectTransform rect) =>
            BattleInspectView.ScreenPointOf(rect, rect.rect.center);

        private static IEnumerator WaitActions(BattleInspectView view)
        {
            for (float t = 0f; view.Acting && t < 8f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.Acting, Is.False, "The actions end.");
        }

        private static IEnumerator WaitEnemyTurn(BattleInspectView view)
        {
            for (float t = 0f; view.EnemyTurn && t < 10f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.EnemyTurn, Is.False, "The enemy turn ends.");
        }

        private static IEnumerator WaitDealt(BattleInspectView view)
        {
            for (float t = 0f; view.Dealing && t < 5f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.Dealing, Is.False, "The deal ends.");
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
            view.RestartDeal(Enumerable.Range(0, view.Cards.Length).ToArray(), instant: true);
            found(view);
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Time.timeScale = 1f;
            BattleOdds.Fix(critical: false, lands: true);
            random?.Dispose();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
