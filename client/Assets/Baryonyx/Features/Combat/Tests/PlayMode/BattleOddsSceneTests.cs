using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 戦闘の会心と状態異常のかかりやすさ（doc/features/combat.md）を、戦闘画面のモックで通す。
    // 確率はテストごとに BattleOdds.Fix で決め、終わったら全体の固定（会心なし・必ず付く）へ戻す。
    public sealed class BattleOddsSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity";
        private Scene loadedScene;

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            BattleOdds.Fix(critical: false, lands: true);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }

        // 会心の一撃は1.5倍のダメージになり、数字の上に「会心！」と出る。
        [UnityTest]
        public IEnumerator ACriticalHitDealsHalfAsMuchAgainAndSaysSo()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var wolf = view.Enemies[1];
            int start = wolf.Hp;

            var words = new List<string>();
            Play(view, 0, wolf.TargetArea);
            yield return WaitActions(view, words);
            int normal = start - wolf.Hp;
            Assert.That(normal, Is.GreaterThan(0));
            Assert.That(words, Has.No.Member(BattleInspectView.CriticalText));

            wolf.Hp = start;
            BattleOdds.Fix(critical: true, lands: true);
            view.RestartDeal(Enumerable.Range(0, view.Cards.Length).ToArray(), instant: true);
            Play(view, 0, wolf.TargetArea);
            yield return WaitActions(view, words);
            Assert.That(start - wolf.Hp, Is.EqualTo(CombatFormula.Critical(normal)).Within(1));
            Assert.That(words, Has.Member(BattleInspectView.CriticalText));
        }

        // 外れた状態は付かず、敵の上に「効かなかった」と出る。当たれば付く。
        [UnityTest]
        public IEnumerator AStatusThatMissesIsNotPutOnAndSaysSo()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            var target = view.Enemies[1];

            BattleOdds.Fix(critical: false, lands: false);
            var words = new List<string>();
            Play(view, DealInto(view, "Ice"), target.TargetArea);
            yield return WaitActions(view, words);
            Assert.That(view.HasStatus(false, 1, CardStatus.Chill), Is.False);
            Assert.That(words, Has.Member(BattleInspectView.ResistedText));

            BattleOdds.Fix(critical: false, lands: true);
            Play(view, DealInto(view, "Ice"), target.TargetArea);
            yield return WaitActions(view, words);
            Assert.That(view.HasStatus(false, 1, CardStatus.Chill), Is.True);
        }

        // 凍結で動けなかった敵は、次に動くまで凍結にかからない。動いたあとは、またかかる。
        // 戦闘画面のモックの敵は決まった順番で動き、毎ターン動くとは限らないため、解けるまでターンを進める。
        [UnityTest]
        public IEnumerator AnEnemyJustThawedCannotBeFrozenUntilItActsAgain()
        {
            var view = default(BattleInspectView);
            yield return Load(value => view = value);
            UseCheapCard(view, "AbsoluteZero");
            var target = view.Enemies[1];
            target.MaxHp = target.Hp = 1000000;
            var words = new List<string>();

            Play(view, DealInto(view, "AbsoluteZero"), target.TargetArea);
            yield return WaitActions(view, words);
            Assert.That(view.HasStatus(false, 1, CardStatus.Freeze), Is.True, "Frozen at first.");

            // 凍結で1回休むと解ける。解けたすぐあとは、必ず付く確率でもかからない。
            for (int turn = 0; turn < 4 && view.HasStatus(false, 1, CardStatus.Freeze); turn++)
                yield return EndTurn(view);
            Assert.That(view.HasStatus(false, 1, CardStatus.Freeze), Is.False, "It thaws.");
            words.Clear();
            Play(view, DealInto(view, "AbsoluteZero"), target.TargetArea);
            yield return WaitActions(view, words);
            Assert.That(
                view.HasStatus(false, 1, CardStatus.Freeze),
                Is.False,
                "Not frozen again at once."
            );
            Assert.That(words, Has.Member(BattleInspectView.ResistedText));

            // 一度動いたあとは、また凍結にかかる。
            for (int turn = 0; turn < 4 && !view.HasStatus(false, 1, CardStatus.Freeze); turn++)
            {
                yield return EndTurn(view);
                Play(view, DealInto(view, "AbsoluteZero"), target.TargetArea);
                yield return WaitActions(view, words);
            }
            Assert.That(
                view.HasStatus(false, 1, CardStatus.Freeze),
                Is.True,
                "Frozen again after it acts."
            );
        }

        // 見本のデッキの1枚目を、コスト1にした <paramref name="skill"/> に替える（エネルギーが足りるように）。
        private static void UseCheapCard(BattleInspectView view, string skill)
        {
            var card = CardSkills.Find(skill);
            var first = view.Deck[0];
            view.Deck[0] = new BattleInspectCardData
            {
                Name = card.Name,
                Owner = first.Owner,
                Skill = card.Id,
                Kind = Baryonyx.UI.Cards.CardText.KindLine(card),
                Description = Baryonyx.UI.Cards.CardText.Description(card),
                Art = first.Art,
                Frame = first.Frame,
                Cost = 1,
                Element = BattleCardText.ElementOf(card.Element),
                Effect = BattleCardText.EffectOf(card),
                Caster = first.Caster,
            };
        }

        /// <summary>Deals the card of <paramref name="skill"/> into the hand and tells where it is.</summary>
        private static int DealInto(BattleInspectView view, string skill)
        {
            int deck = System.Array.FindIndex(view.Deck, data => data.Skill == skill);
            Assert.That(deck, Is.GreaterThanOrEqualTo(0), skill + " is in the deck.");
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
            view.PressCard(card, CardPoint(view.Cards[card]));
            view.DragCard(Center(target));
            view.ReleaseCard(Center(target));
        }

        // カードが動き終えるまで待ち、そのあいだに出た文字を集める。
        private static IEnumerator WaitActions(BattleInspectView view, List<string> words)
        {
            for (float t = 0f; t < 0.1f || (view.Acting && t < 8f); t += Time.deltaTime)
            {
                words.AddRange(Rising(view.PopupTemplate));
                yield return null;
            }
            Assert.That(view.Acting, Is.False, "The actions end.");
        }

        private static IEnumerator EndTurn(BattleInspectView view)
        {
            view.EndTurnButton.onClick.Invoke();
            for (float t = 0f; view.EnemyTurn && t < 15f; t += Time.deltaTime)
                yield return null;
            Assert.That(view.EnemyTurn, Is.False, "The enemy turn ends.");
            for (float t = 0f; view.Dealing && t < 5f; t += Time.deltaTime)
                yield return null;
        }

        private static IEnumerable<string> Rising(TMP_Text template) =>
            template
                .transform.parent.GetComponentsInChildren<TMP_Text>()
                .Where(text => text != template && text.name == template.name + "(Clone)")
                .Select(text => text.text);

        private static Vector2 CardPoint(BattleInspectCard card)
        {
            var rect = card.Body.rect;
            return RectTransformUtility.WorldToScreenPoint(
                null,
                card.Body.TransformPoint(new Vector2(rect.center.x, rect.yMax - 40f))
            );
        }

        private static Vector2 Center(RectTransform rect) =>
            BattleInspectView.ScreenPointOf(rect, rect.rect.center);

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
    }
}
