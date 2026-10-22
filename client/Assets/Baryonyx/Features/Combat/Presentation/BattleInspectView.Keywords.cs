using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The keyword notes beside the raised card (doc/features/screens.md, the battle mock): each
    /// keyword its text names, with what it means, right of the card while it is up. They go
    /// while the card is swiped toward a target, so they hide nothing to aim at.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        /// <summary>The notes; placed beside the raised card, under the same anchor as the hand.</summary>
        public BattleInspectKeywordTips KeywordTips;

        /// <summary>Space between the raised card's right edge and the notes, in px.</summary>
        public float KeywordTipsGap = 16f;

        private IReadOnlyList<CardKeyword>[] deckKeywords;

        private void RefreshKeywordTips()
        {
            if (KeywordTips == null)
                return;
            bool shown = held >= 0 && !armed && !EnemyTurn && !IsFaceDown(held);
            if (!shown)
            {
                KeywordTips.Hide();
                return;
            }
            ShowKeywordTips(held);
        }

        /// <summary>Shows the notes of the card <paramref name="index"/> beside where it is raised.</summary>
        private void ShowKeywordTips(int index)
        {
            KeywordTips.Show(KeywordsOf(Cards[index].DeckIndex));
            var tips = (RectTransform)KeywordTips.transform;
            var card = Cards[index].Body;
            float scale = Settings.RaisedScale * Settings.CardScale;
            // Level with the card's top; when the notes run longer than the card, they rise so
            // their bottom stays as far above the screen's bottom as the card's.
            float top = Mathf.Max(
                Settings.RaisedBottom + card.rect.height * scale,
                Settings.RaisedBottom + tips.rect.height
            );
            var place = new Vector2(card.rect.width * scale / 2f + KeywordTipsGap, top);
            if (tips.anchoredPosition != place)
                tips.anchoredPosition = place;
        }

        /// <summary>The keywords in the text of the deck's card <paramref name="deckIndex"/>, worked out once.</summary>
        private IReadOnlyList<CardKeyword> KeywordsOf(int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= Deck.Length)
                return Array.Empty<CardKeyword>();
            if (deckKeywords == null || deckKeywords.Length != Deck.Length)
                deckKeywords = new IReadOnlyList<CardKeyword>[Deck.Length];
            if (deckKeywords[deckIndex] == null)
            {
                var skill = SkillOf(Deck[deckIndex]);
                deckKeywords[deckIndex] =
                    skill == null ? Array.Empty<CardKeyword>() : CardKeywords.In(skill.Text);
            }
            return deckKeywords[deckIndex];
        }
    }
}
