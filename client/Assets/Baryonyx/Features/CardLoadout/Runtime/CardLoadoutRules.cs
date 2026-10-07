using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Party;

namespace Baryonyx.CardLoadout
{
    /// <summary>What putting a card into one of a character's slots did.</summary>
    public enum CardLoadoutChange
    {
        // 何も変わらない（同じカード、範囲外など）。
        None,

        // 枠のカードを、その人がまだ付けていないカードに替えた。
        Replace,

        // その人のほかの枠にあるカードだったので、2つの枠の中身を入れ替えた。
        Swap,
    }

    /// <summary>
    /// The rules of a character's two custom skills (provisional, doc/features/party.md): a card
    /// fits a character whose elements include its element, and a card without an element fits
    /// everyone. Every card is owned and any number of characters may set the same card, but one
    /// character holds a card in one slot only.
    /// </summary>
    public static class CardLoadoutRules
    {
        public const int Size = 2;

        /// <summary>
        /// Puts <paramref name="skill"/> into <paramref name="slot"/>. When the character holds it
        /// in another slot already, the two slots swap and <paramref name="other"/> is that slot.
        /// </summary>
        public static CardLoadoutChange Apply(string[] cards, int slot, string skill, out int other)
        {
            other = -1;
            if (cards == null || slot < 0 || slot >= cards.Length || string.IsNullOrEmpty(skill))
                return CardLoadoutChange.None;
            if (cards[slot] == skill)
                return CardLoadoutChange.None;
            other = Array.IndexOf(cards, skill);
            if (other >= 0)
            {
                cards[other] = cards[slot];
                cards[slot] = skill;
                return CardLoadoutChange.Swap;
            }
            cards[slot] = skill;
            return CardLoadoutChange.Replace;
        }

        /// <summary>
        /// The elements the character can set, in the order of the cards the mock data gives them
        /// (provisional: which elements each character can use is not decided yet).
        /// </summary>
        public static IReadOnlyList<CardElement> Usable(PartyMember member) =>
            member == null
                ? Array.Empty<CardElement>()
                : member
                    .Cards.Select(card => CardSkills.Find(card.Skill))
                    .Where(card => card != null && card.Element != CardElement.None)
                    .Select(card => card.Element)
                    .Distinct()
                    .ToArray();

        public static bool CanUse(IReadOnlyList<CardElement> usable, CardSkill card) =>
            card != null
            && (
                card.Element == CardElement.None
                || (usable != null && usable.Contains(card.Element))
            );

        /// <summary>The cards the character can choose from: those they can use, by cost, then in the catalog's order.</summary>
        public static IReadOnlyList<CardSkill> Choices(IReadOnlyList<CardElement> usable) =>
            CardSkills.All.Where(card => CanUse(usable, card)).OrderBy(card => card.Cost).ToArray();
    }
}
