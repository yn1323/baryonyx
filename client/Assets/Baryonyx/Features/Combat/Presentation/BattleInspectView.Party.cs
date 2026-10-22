using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// One member of the party a battle is fought with (doc/features/party.md): the name, the
    /// battle sprite and its tint, the level and its stats, and the cards the member brings to
    /// the deck.
    /// </summary>
    public sealed class BattlePartyMember
    {
        public string Name = "";
        public Texture Art;
        public Color Tint = Color.white;
        public int Level = 1;
        public CharacterStats Stats;
        public IReadOnlyList<BattlePartyCard> Cards = Array.Empty<BattlePartyCard>();

        /// <summary>The passive skills the member's level has unlocked (doc/features/passives.md).</summary>
        public IReadOnlyList<CombatPassiveKind> Passives = Array.Empty<CombatPassiveKind>();

        /// <summary>
        /// The share of the maximum HP the member starts the battle with: 1 is full, 0 is fallen
        /// (the adventure carries HP from room to room, doc/features/stage-progression.md).
        /// </summary>
        public float HpShare = 1f;
    }

    /// <summary>
    /// A card a party member brings to the deck: the card skill with its multiplier, and its art.
    /// A unique skill's stage also tells whose unique skill it is, so it can evolve in battle
    /// (doc/features/skill-evolution.md#戦闘中の進化).
    /// </summary>
    public sealed class BattlePartyCard
    {
        public CardSkill Skill;
        public Texture Art;

        /// <summary>The character whose unique skill the card is, or empty for a custom skill.</summary>
        public string Person = "";

        /// <summary>Which of the character's unique skills it is (0 or 1), or -1 for a custom skill.</summary>
        public int Unique = -1;

        /// <summary>The art of another card skill by its id, for the stage a unique skill evolves into.</summary>
        public Func<string, Texture> ArtOf;
    }

    /// <summary>The party of the battle: who stands where, and the deck made of their cards.</summary>
    public sealed partial class BattleInspectView
    {
        /// <summary>The card frames by element (<see cref="CardElement"/> order), for a deck made at run time.</summary>
        public Texture[] CardFrames = Array.Empty<Texture>();

        /// <summary>The element icons by element (<see cref="CardElement"/> order; none for None).</summary>
        public Texture[] ElementIcons = Array.Empty<Texture>();

        /// <summary>
        /// Sets the party before the battle begins: each place on the field takes the member of
        /// its party slot (<see cref="BattleInspectAlly.Slot"/>; <paramref name="slots"/> in slot
        /// order, null for an empty slot) with the member's sprite, name, level and stats at the HP
        /// it carries (<see cref="BattlePartyMember.HpShare"/>), and the deck becomes the members'
        /// cards. A place whose slot is empty is hidden and stays down for the whole battle; a
        /// member who comes in fallen stands greyed out and its cards stay out of the deck.
        /// </summary>
        public void UseParty(IReadOnlyList<BattlePartyMember> slots)
        {
            if (slots == null)
                return;
            var deck = new List<BattleInspectCardData>();
            var passives = new IReadOnlyList<CombatPassiveKind>[Allies.Length];
            var places = Enumerable
                .Range(0, Allies.Length)
                .OrderBy(place => Allies[place].Slot)
                .ToArray();
            foreach (int place in places)
            {
                var ally = Allies[place];
                int slot = ally.Slot;
                var member = slot >= 0 && slot < slots.Count ? slots[slot] : null;
                Stand(ally, member);
                if (member == null)
                    continue;
                passives[place] = member.Passives;
                foreach (var card in member.Cards)
                    if (card?.Skill != null)
                    {
                        var data = CardOf(card.Skill, place, card.Art);
                        data.Person = card.Person;
                        data.Unique = card.Unique;
                        data.ArtOf = card.ArtOf;
                        deck.Add(data);
                    }
            }
            Deck = deck.ToArray();
            deckKeywords = null;
            UsePassives(passives);
            ClearBlock();
            // The deck is dealt anew from the new cards; an empty slot's place is down from the
            // start, not fallen in the battle.
            ClearTable();
        }

        /// <summary>The HP a member of <paramref name="maxHp"/> starts with: none when fallen, otherwise at least 1.</summary>
        public static int StartHpOf(int maxHp, float share)
        {
            if (maxHp <= 0 || share <= 0f)
                return 0;
            return Mathf.Clamp(Mathf.RoundToInt(maxHp * Mathf.Clamp01(share)), 1, maxHp);
        }

        /// <summary>
        /// The share of the maximum HP each party slot has now (slot order, 4 slots; 1 for an
        /// empty slot), for the adventure to carry into the next room.
        /// </summary>
        public float[] HpSharesBySlot(int slots = 4)
        {
            var shares = new float[slots];
            for (int slot = 0; slot < slots; slot++)
                shares[slot] = 1f;
            foreach (var ally in Allies)
                if (!ally.Absent && ally.Slot >= 0 && ally.Slot < slots)
                    shares[ally.Slot] =
                        ally.MaxHp > 0 ? Mathf.Clamp01(ally.Hp / (float)ally.MaxHp) : 1f;
            return shares;
        }

        // その場所に、編成の1人を立たせる。いなければ隠して、戦闘のあいだ倒れたままにする。
        private static void Stand(BattleInspectAlly ally, BattlePartyMember member)
        {
            ally.Absent = member == null;
            ally.Body.gameObject.SetActive(!ally.Absent);
            ally.HpBar.gameObject.SetActive(!ally.Absent);
            if (ally.Absent)
            {
                ally.StartHp = ally.MaxHp = ally.Hp = 0;
                return;
            }
            ally.Name = member.Name ?? "";
            ally.Level = Mathf.Max(1, member.Level);
            ally.Stats = member.Stats;
            ally.MaxHp = Mathf.Max(1, member.Stats.Hp);
            ally.StartHp = ally.Hp = StartHpOf(ally.MaxHp, member.HpShare);
            ally.Tint = member.Tint;
            if (member.Art != null)
                ally.Sprite.texture = member.Art;
            if (
                ally.NameTag != null
                && ally.NameTag.GetComponent<TranslucentTextPanel>() is { } tag
            )
                tag.SetText(ally.Name);
            RefreshAlly(ally);
        }

        /// <summary>
        /// One card of the deck from a card skill used by the ally at <paramref name="caster"/>:
        /// its texts, the power worked out from the ally's stats (and today's ACT), its art, the
        /// frame and icon of its element, and how it is played.
        /// </summary>
        public BattleInspectCardData CardOf(CardSkill skill, int caster, Texture art)
        {
            var ally = caster >= 0 && caster < Allies.Length ? Allies[caster] : null;
            int element = (int)skill.Element;
            var data = CardData(
                skill,
                caster,
                ally?.Name ?? "",
                ally?.Stats ?? default,
                art,
                At(CardFrames, element),
                skill.Element == CardElement.None ? null : At(ElementIcons, element),
                TodayAct
            );
            data.Card = skill;
            return data;
        }

        /// <summary>
        /// One card of the deck from a card skill, its power worked out from the user's
        /// <paramref name="stats"/> and today's <paramref name="act"/>, with the given art, frame
        /// and element icon.
        /// </summary>
        public static BattleInspectCardData CardData(
            CardSkill skill,
            int caster,
            string owner,
            CharacterStats stats,
            Texture art,
            Texture frame,
            Texture icon,
            int act
        )
        {
            var first = skill.Powered().FirstOrDefault();
            return new BattleInspectCardData
            {
                Name = skill.Name,
                Owner = owner,
                Skill = skill.Id,
                Kind = CardText.KindLine(skill),
                Description = CardText.Description(skill),
                Art = art,
                Frame = frame,
                ElementIcon = icon,
                Cost = skill.Cost,
                Quality = skill.Quality,
                FoilStyle = skill.FoilStyle,
                Power = first == null ? 0 : CardRules.Power(first, StatOf(stats, first.Stat), act),
                Element = BattleCardText.ElementOf(skill.Element),
                Effect = BattleCardText.EffectOf(skill),
                Caster = caster,
            };
        }

        /// <summary>
        /// The card skill a card of the deck plays: the one it was made from, or the definition
        /// of its id for the baked deck. Null for a card of the mock's own.
        /// </summary>
        public static CardSkill SkillOf(BattleInspectCardData card) =>
            card == null ? null
            : card.Card != null ? card.Card
            : string.IsNullOrEmpty(card.Skill) ? null
            : CardSkills.Find(card.Skill);

        /// <summary>The stat of <paramref name="stats"/> that a power is a part of.</summary>
        public static int StatOf(CharacterStats stats, CardStat stat) =>
            stat switch
            {
                CardStat.PhysicalAttack => stats.PhysicalAttack,
                CardStat.MagicAttack => stats.MagicAttack,
                CardStat.PhysicalDefense => stats.PhysicalDefense,
                _ => 0,
            };

        private static Texture At(Texture[] textures, int index) =>
            textures != null && index >= 0 && index < textures.Length ? textures[index] : null;
    }
}
