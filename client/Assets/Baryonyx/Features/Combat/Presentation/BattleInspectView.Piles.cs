using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// How cards move between the hand and the piles by their keywords (doc/features/combat.md,
    /// 手札・カードのキーワード): the hand flows away at the end of the turn but for the cards that
    /// stay (保持), a card that would flow is out of the battle (霧散) and a played one may be too
    /// (消滅); some cards are always dealt first (初手), a sleeping card wakes as it flows away or
    /// is discarded (眠り札), and two cards fuse in hand (合体) or set off a combo (連携). A card
    /// may have the player discard from the hand or take back from the discard pile.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        /// <summary>The stages a sleeping card (眠り札) wakes to, and what each adds to its power (percent).</summary>
        public const int SleeperStages = 3;
        public const int SleeperStepPercent = 50;

        /// <summary>The cards of the discard pile shown at most to take one back.</summary>
        public const int TakeShown = 9;

        public const string DiscardPrompt = "捨てるカードを選ぶ";
        public const string TakePrompt = "手札に加えるカードを選ぶ";

        private static readonly Color PromptColor = new(0.9f, 0.86f, 0.62f);

        /// <summary>What the player picks now: nothing, cards of the hand to discard, or a card of the discard pile.</summary>
        public enum PileChoice
        {
            None,
            Discard,
            Take,
        }

        // 眠り札の目覚めた段（山札の何番目のカードか）。
        private int[] sleep = Array.Empty<int>();

        // このターンに使ったカードの元のID（連携の相手を探す）。
        private readonly HashSet<string> playedRoots = new();

        // 合体したカード（画面のカードの番号）と、元の2枚（山札の何番目のカードか）。
        private readonly Dictionary<int, (int first, int second)> fused = new();

        // 次に使うカードの上乗せ（%）と、効くカードの属性（空ならどのカードも）。
        private int nextBoost;
        private CardElement[] nextBoostElements = Array.Empty<CardElement>();
        private CardElement[] nextFreeElements = Array.Empty<CardElement>();

        // 使っているカードの威力の上乗せ（%）。眠り札の段と、次のカードの上乗せ。
        private int cardBoost;

        // 選んでいるもの、残りの数、捨て札から見せているカード（画面のカードの番号）。
        private PileChoice choosing;
        private int choiceLeft;
        private readonly List<int> choiceShown = new();
        private bool takenFree;

        // 効果で捨てたカード（山札の何番目か）。捨てたときの効果を順に働かせる。
        private readonly List<int> discardedNow = new();

        /// <summary>What the player picks now.</summary>
        public PileChoice Choosing => choosing;

        /// <summary>The cards of the discard pile shown to take one back (indexes of <see cref="Cards"/>).</summary>
        public IReadOnlyList<int> ChoiceCards => choiceShown;

        /// <summary>The stage a sleeping card of the deck has woken to (0 asleep).</summary>
        public int SleepOf(int deckIndex) =>
            deckIndex >= 0 && deckIndex < sleep.Length ? sleep[deckIndex] : 0;

        private bool HasTrait(int deckIndex, CardTrait trait) =>
            deckIndex >= 0 && deckIndex < Deck.Length && (SkillOf(Deck[deckIndex])?.Has(trait) ?? false);

        private bool Innate(int deckIndex) => HasTrait(deckIndex, CardTrait.Innate);

        /// <summary>The cards always in the opening hand (初手), so the deal holds them all.</summary>
        private int InnateCount()
        {
            int count = 0;
            for (int i = 0; i < Deck.Length; i++)
                if (Innate(i) && !Benched(i))
                    count++;
            return count;
        }

        /// <summary>Forgets what the last battle (or the turns before a rune revive) left on the cards.</summary>
        private void ResetPiles()
        {
            sleep = new int[Deck.Length];
            playedRoots.Clear();
            fused.Clear();
            nextBoost = 0;
            nextBoostElements = Array.Empty<CardElement>();
            nextFreeElements = Array.Empty<CardElement>();
            EndChoice();
        }

        // --- 合体 ----------------------------------------------------------------------------

        /// <summary>
        /// Puts the fused cards (合体) the deck's cards can make at its end, for the battle only:
        /// they are never dealt, and show when their two cards meet in the hand.
        /// </summary>
        private void AddFusions()
        {
            var deck = Deck.Where(card => !card.Generated).ToList();
            foreach (var fusion in CardSkills.Fusions)
            {
                int first = deck.FindIndex(card => SkillOf(card)?.FusesInto == fusion.Id);
                if (first < 0)
                    continue;
                string partner = SkillOf(deck[first]).FuseWith;
                int second = deck.FindIndex(card => SkillOf(card)?.RootId == partner);
                if (second < 0)
                    continue;
                // The fused card is used by the one of the two whose card it grew from.
                int user = SkillOf(deck[first]).User == fusion.User ? first : second;
                // The fused card made before is kept, so the deck stays the same deck.
                var made = Array.Find(Deck, card => card.Generated && SkillOf(card)?.Id == fusion.Id);
                if (made == null)
                {
                    made = CardOf(fusion, deck[user].Caster, deck[user].Art);
                    made.Generated = true;
                }
                deck.Add(made);
            }
            // The same cards keep the same deck (the battle's evolutions are kept by it).
            if (deck.Count == Deck.Length && deck.Select((card, i) => ReferenceEquals(card, Deck[i])).All(same => same))
                return;
            Deck = deck.ToArray();
            deckKeywords = null;
        }

        /// <summary>
        /// Fuses two cards of the hand marked [合体] with each other into their fused card: the
        /// first shows it, the second leaves the hand. They part again into the discard pile when
        /// the fused card leaves the hand.
        /// </summary>
        private void Fuse()
        {
            for (int a = 0; a < hand.Count; a++)
            {
                int first = hand[a];
                if (fused.ContainsKey(first))
                    continue;
                var skill = SkillOf(Deck[Cards[first].DeckIndex]);
                if (skill?.FuseWith == null)
                    continue;
                int second = hand.Find(index =>
                    index != first
                    && !fused.ContainsKey(index)
                    && SkillOf(Deck[Cards[index].DeckIndex])?.RootId == skill.FuseWith
                );
                int into = Array.FindIndex(Deck, card => card.Generated && SkillOf(card)?.Id == skill.FusesInto);
                if (second < 0 || into < 0)
                    continue;
                fused[first] = (Cards[first].DeckIndex, Cards[second].DeckIndex);
                hand.Remove(second);
                Cards[second].Place = BattleInspectCardPlace.Deck;
                RefreshCard(second);
                Show(first, into);
                ShowPopup(PartyAnchor, $"合体！ {Deck[into].Name}", Gold, 46f);
                LayoutHand();
                SortCards();
                Refresh();
                return;
            }
        }

        /// <summary>
        /// Puts the card leaving the hand on its pile: the two cards of a fused card part into the
        /// discard pile, a card out of the battle goes there (消滅), the others to the discard pile.
        /// </summary>
        private void PutAway(int index, bool exhaust)
        {
            if (fused.Remove(index, out var parts))
            {
                foreach (int part in new[] { parts.first, parts.second })
                    if (!Benched(part))
                        drawPile.Discard(part);
            }
            else if (exhaust)
                drawPile.Exhaust(Cards[index].DeckIndex);
            else
                drawPile.Discard(Cards[index].DeckIndex);
            Cards[index].Place = BattleInspectCardPlace.Discard;
            RefreshCard(index);
            RefreshDeck();
        }

        // --- 眠り札 --------------------------------------------------------------------------

        /// <summary>A sleeping card (眠り札) flowed away or discarded wakes a stage, up to the last.</summary>
        private void Wake(int deckIndex)
        {
            if (!HasTrait(deckIndex, CardTrait.Sleeper) || deckIndex >= sleep.Length)
                return;
            sleep[deckIndex] = Mathf.Min(SleeperStages, sleep[deckIndex] + 1);
        }

        /// <summary>The marks of a sleeping card's stage after its kind ("・目覚め●●○"), or nothing.</summary>
        private string SleepMarks(int deckIndex)
        {
            if (!HasTrait(deckIndex, CardTrait.Sleeper))
                return "";
            int stage = SleepOf(deckIndex);
            return "・目覚め" + new string('●', stage) + new string('○', SleeperStages - stage);
        }

        // --- ターンの終わり ------------------------------------------------------------------

        /// <summary>
        /// The hand flows away to the discard pile as the party's turn ends: a card that stays
        /// (保持) stays, one out of the battle at the end of the turn (霧散) crumbles away, and a
        /// sleeping card wakes.
        /// </summary>
        private void FlowHand()
        {
            PutBack();
            for (int slot = hand.Count - 1; slot >= 0; slot--)
            {
                int index = hand[slot];
                int deckIndex = Cards[index].DeckIndex;
                if (HasTrait(deckIndex, CardTrait.Retain))
                    continue;
                hand.RemoveAt(slot);
                bool ethereal = HasTrait(deckIndex, CardTrait.Ethereal);
                if (ethereal && Application.isPlaying && isActiveAndEnabled && Hand.gameObject.activeInHierarchy)
                    StartCoroutine(Crumble(CopyToVanish(index)));
                Wake(deckIndex);
                PutAway(index, ethereal);
            }
            LayoutHand();
            RefreshDeck();
        }

        // --- 選ぶ -----------------------------------------------------------------------------

        /// <summary>
        /// Discards the card from the hand by a card's effect (捨てる): it wakes if it sleeps, and
        /// its effect when discarded works after the card that discarded it.
        /// </summary>
        private void DiscardFromHand(int index)
        {
            if (!hand.Remove(index))
                return;
            int deckIndex = Cards[index].DeckIndex;
            // A fused card's effects when discarded are its two cards'.
            if (fused.TryGetValue(index, out var parts))
            {
                discardedNow.Add(parts.first);
                discardedNow.Add(parts.second);
            }
            else
                discardedNow.Add(deckIndex);
            Wake(deckIndex);
            PutAway(index, exhaust: false);
            LayoutHand();
        }

        /// <summary>
        /// Has the player discard <paramref name="count"/> cards of the hand (-1: the whole hand,
        /// with no pick), then works each discarded card's effect when discarded. Tells how many
        /// went.
        /// </summary>
        private IEnumerator DiscardCards(int count, int caster, Action<int> discarded)
        {
            discardedNow.Clear();
            if (count < 0 || hand.Count <= count)
            {
                foreach (int index in hand.ToArray())
                    DiscardFromHand(index);
            }
            else if (count > 0)
            {
                choosing = PileChoice.Discard;
                choiceLeft = count;
                ShowBanner(DiscardPrompt, PromptColor);
                Refresh();
                while (choosing == PileChoice.Discard)
                    yield return null;
            }
            discarded(discardedNow.Count);
            var triggered = discardedNow.ToArray();
            discardedNow.Clear();
            Refresh();
            foreach (int deckIndex in triggered)
                yield return WhenDiscarded(deckIndex);
        }

        /// <summary>The effect of a card when a card's effect discarded it (捨てたとき), on no one picked.</summary>
        private IEnumerator WhenDiscarded(int deckIndex)
        {
            var data = Deck[deckIndex];
            var skill = SkillOf(data);
            if (skill == null || !skill.In(CardActionPhase.Discarded).Any())
                yield break;
            var trigger = skill.Copy();
            trigger.Actions = Array.AsReadOnly(
                skill.In(CardActionPhase.Discarded).Select(AsPlayed).ToArray()
            );
            bool onEnemies = trigger.Actions.Any(action =>
                action.Target is CardTarget.OneEnemy or CardTarget.AllEnemies or CardTarget.RandomEnemies or CardTarget.ChainEnemies
            );
            int caster = data.Caster;
            if (caster >= 0 && caster < Allies.Length && Allies[caster].Down)
                yield break;
            yield return UseSkill(trigger, data, new List<int>(), onEnemies, caster);
        }

        /// <summary>
        /// Has the player take <paramref name="count"/> cards of the discard pile into the hand,
        /// with no pick when there are no more than that; they cost nothing this turn when
        /// <paramref name="free"/>.
        /// </summary>
        private IEnumerator TakeCards(int count, bool free)
        {
            takenFree = free;
            var pile = drawPile.DiscardPile;
            if (pile.Count <= count)
            {
                foreach (int deckIndex in pile.ToArray())
                    TakeBack(deckIndex, -1);
                takenFree = false;
                yield break;
            }
            choosing = PileChoice.Take;
            choiceLeft = count;
            ShowChoiceRow();
            ShowBanner(TakePrompt, PromptColor);
            Refresh();
            while (choosing == PileChoice.Take)
                yield return null;
            takenFree = false;
        }

        /// <summary>Shows the latest cards of the discard pile in a row across the middle of the screen.</summary>
        private void ShowChoiceRow()
        {
            choiceShown.Clear();
            var pile = drawPile.DiscardPile;
            int room = Mathf.Min(TakeShown, pile.Count, Settings.HandLimit - hand.Count);
            var free = Enumerable.Range(0, Cards.Length).Where(i => !Cards[i].InHand).Take(room).ToList();
            float width = Cards.Length > 0 ? Cards[0].Body.rect.width * Settings.CardScale : 200f;
            float step = width * 0.85f;
            for (int k = 0; k < free.Count; k++)
            {
                int index = free[k];
                int deckIndex = pile[pile.Count - 1 - k];
                Show(index, deckIndex);
                Cards[index].Place = BattleInspectCardPlace.Choice;
                var pose = new Pose(
                    new Vector2((k - (free.Count - 1) / 2f) * step, Settings.RaisedBottom) - Hand.anchoredPosition,
                    0f,
                    Settings.CardScale
                );
                motion[index].Rest = pose;
                motion[index].Snap(pose);
                ApplyMotion(index);
                choiceShown.Add(index);
            }
        }

        /// <summary>A tap on a card while the player picks: discards it from the hand, or takes it from the row.</summary>
        private bool PickCard(int index)
        {
            if (choosing == PileChoice.Discard && Cards[index].InHand)
            {
                DiscardFromHand(index);
                if (--choiceLeft <= 0 || hand.Count == 0)
                    EndChoice();
                Refresh();
                return true;
            }
            if (choosing == PileChoice.Take && Cards[index].Place == BattleInspectCardPlace.Choice)
            {
                choiceShown.Remove(index);
                TakeBack(Cards[index].DeckIndex, index);
                if (--choiceLeft <= 0 || choiceShown.Count == 0)
                    EndChoice();
                Refresh();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Takes a card of the deck off the discard pile into the hand, on the card on screen
        /// at <paramref name="index"/> (that shows it in the row) or on a free one (-1).
        /// </summary>
        private void TakeBack(int deckIndex, int index)
        {
            if (hand.Count >= Settings.HandLimit || !drawPile.TakeFromDiscard(deckIndex))
                return;
            if (index < 0)
                index = Array.FindIndex(Cards, card => !card.InHand && card.Place != BattleInspectCardPlace.Choice);
            if (index < 0)
                return;
            if (Cards[index].Place != BattleInspectCardPlace.Choice)
                Show(index, deckIndex);
            Cards[index].Place = BattleInspectCardPlace.Hand;
            hand.Insert(0, index);
            if (takenFree)
            {
                Cards[index].Cost = 0;
                if (Cards[index].Face != null)
                    Cards[index].Face.SetCost(0);
            }
            LayoutHand();
            SortCards();
            RefreshDeck();
            Fuse();
        }

        private void EndChoice()
        {
            if (choosing == PileChoice.None)
                return;
            choosing = PileChoice.None;
            choiceLeft = 0;
            foreach (int index in choiceShown)
            {
                Cards[index].Place = BattleInspectCardPlace.Deck;
                RefreshCard(index);
            }
            choiceShown.Clear();
            if (skillShown != null)
                ShowBanner(skillShown, skillNameColor);
            else
                FadeBanner();
        }

        // --- 使うとき ------------------------------------------------------------------------

        /// <summary>An action of a combo or of a discard, as one done when the card is played.</summary>
        private static CardAction AsPlayed(CardAction action)
        {
            var played = action.Copy();
            played.Phase = CardActionPhase.Play;
            return played;
        }

        /// <summary>True when the next card's boost or free cost works on <paramref name="element"/>.</summary>
        private static bool Covers(CardElement[] elements, CardElement element) =>
            elements.Length == 0 || Array.IndexOf(elements, element) >= 0;

        /// <summary>
        /// Plays a card skill by its keywords: the combo's actions first when its partner was
        /// played before it this turn, then its actions in order, the discards and takes asking
        /// the player between its blows, and the hits and draws grown by the cards it discarded.
        /// </summary>
        private IEnumerator PlaySkill(
            CardSkill skill,
            BattleInspectCardData card,
            List<int> chosen,
            bool onEnemies,
            int caster,
            bool combo,
            int boost
        )
        {
            var actions = new List<CardAction>();
            if (combo)
                actions.AddRange(skill.In(CardActionPhase.Combo).Select(AsPlayed));
            actions.AddRange(skill.In(CardActionPhase.Play));
            int firstBlow = actions.FindIndex(action => !IsPileAction(action));
            int discarded = 0;
            var before = firstBlow < 0 ? actions : actions.Take(firstBlow).ToList();
            var after = firstBlow < 0 ? new List<CardAction>() : actions.Skip(firstBlow).Where(IsPileAction).ToList();
            foreach (var action in before)
                yield return PileAction(action, caster, n => discarded += n);
            var blows = actions.Where(action => !IsPileAction(action)).Select(action => Grown(action, discarded)).ToArray();
            if (blows.Length > 0)
            {
                var main = skill.Copy();
                main.Actions = Array.AsReadOnly(blows);
                cardBoost = boost;
                yield return UseSkill(main, card, chosen, onEnemies, caster);
                cardBoost = 0;
            }
            foreach (var action in after)
            {
                // A card drawn by the card's blows is in hand before the player picks.
                while (dealing != null)
                    yield return null;
                yield return PileAction(action, caster, n => discarded += n);
            }
        }

        private static bool IsPileAction(CardAction action) =>
            action.Kind is CardActionKind.Discard or CardActionKind.TakeFromDiscard;

        private IEnumerator PileAction(CardAction action, int caster, Action<int> discarded)
        {
            if (action.Kind == CardActionKind.Discard)
                yield return DiscardCards(action.Amount, caster, discarded);
            else if (action.Kind == CardActionKind.TakeFromDiscard)
                yield return TakeCards(action.Amount, action.MakesFree);
        }

        // 捨てた枚数で、当たる回数や引く枚数が増える行動。
        private static CardAction Grown(CardAction action, int discarded)
        {
            if (!action.PerDiscarded)
                return action;
            var grown = action.Copy();
            if (grown.Kind == CardActionKind.Damage)
                grown.Hits += discarded;
            else
                grown.Amount += discarded;
            return grown;
        }

        /// <summary>
        /// The boost (percent) the card being paid for takes: its sleeping stages (眠り札), which
        /// it then loses, and the next card's boost when it covers the card's element.
        /// </summary>
        private int TakeBoost(int deckIndex, CardElement element)
        {
            int boost = SleepOf(deckIndex) * SleeperStepPercent;
            if (deckIndex < sleep.Length)
                sleep[deckIndex] = 0;
            if (nextBoost > 0 && Covers(nextBoostElements, element))
            {
                boost += nextBoost;
                nextBoost = 0;
            }
            return boost;
        }
    }
}
