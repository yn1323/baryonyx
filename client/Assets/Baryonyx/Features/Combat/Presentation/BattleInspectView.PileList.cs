using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The list of a pile's cards (doc/features/screens/battle.md#山札と捨て札の一覧): a tap on the
    /// deck, the discard pile or the cards out of the battle lays out their cards over the
    /// battle. The deck is listed by cost and name, so the list never tells what comes next;
    /// the discard pile and the cards out of the battle the last one first. The list follows
    /// the pile while it is open.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        public BattlePileList PileList;

        /// <summary>The taps on the deck's, the discard pile's and the out-of-battle counters.</summary>
        public Button DeckButton;
        public Button DiscardButton;
        public Button ExhaustButton;

        /// <summary>True while a pile's cards are listed.</summary>
        public bool PileListOpen => PileList != null && PileList.IsOpen;

        private void SetUpPileList()
        {
            if (DeckButton != null)
                DeckButton.onClick.AddListener(() => OpenPile(BattlePile.Draw));
            if (DiscardButton != null)
                DiscardButton.onClick.AddListener(() => OpenPile(BattlePile.Discard));
            if (ExhaustButton != null)
                ExhaustButton.onClick.AddListener(() => OpenPile(BattlePile.Exhaust));
        }

        /// <summary>
        /// Lists the cards of <paramref name="pile"/>: a card up goes back to the hand and the
        /// status list closes first. Not while a unique skill's next stage is picked or after the win.
        /// </summary>
        public void OpenPile(BattlePile pile)
        {
            if (PileList == null || pressing || handAway || (Choice != null && Choice.IsOpen))
                return;
            PutBack();
            HideStatusList();
            ShowPile(pile);
        }

        /// <summary>Closes the list.</summary>
        public void ClosePileList()
        {
            if (PileList != null)
                PileList.Close();
        }

        /// <summary>The cards of a pile (indexes of <see cref="Deck"/>) in the order the list shows them.</summary>
        public IReadOnlyList<int> PileOrder(BattlePile pile) =>
            pile switch
            {
                BattlePile.Draw => ByCost(drawPile.Pile),
                BattlePile.Discard => drawPile.DiscardPile.Reverse().ToList(),
                _ => drawPile.Exhausted.Reverse().ToList(),
            };

        // By cost, then by name: the deck's own order is what is drawn next.
        private List<int> ByCost(IEnumerable<int> cards) =>
            cards
                .OrderBy(deckIndex => CostWithPassives(Deck[deckIndex]))
                .ThenBy(deckIndex => Deck[deckIndex].Name, StringComparer.Ordinal)
                .ToList();

        private void ShowPile(BattlePile pile) =>
            ShowPile(pile, PileOrder(pile));

        private void ShowPile(BattlePile pile, IReadOnlyList<int> cards)
        {
            if (PileList.MakeGlint == null)
                PileList.MakeGlint = MakeGlint;
            PileList.Open(pile, cards.Select(PileEntry).ToList());
        }

        // Keeps the open list up to date as cards move between the piles.
        private void RefreshPileList()
        {
            if (PileListOpen)
                ShowPile(PileList.Pile);
        }

        // A card of the deck as the hand shows it: its cost with the passives, its sleeping stage
        // and its light when it awakens when used (lit in the hand and put away unused).
        private BattlePileList.Entry PileEntry(int deckIndex)
        {
            var data = Deck[deckIndex];
            return new BattlePileList.Entry
            {
                Name = data.Name,
                Owner = data.Owner,
                Kind = data.Kind + SleepMarks(deckIndex),
                Description = data.Description,
                Art = data.Art,
                Frame = data.Frame,
                Element = data.ElementIcon,
                Cost = CostWithPassives(data),
                Quality = data.Quality,
                FoilStyle = data.FoilStyle,
                Omen = HasOmen(deckIndex),
            };
        }
    }
}
