using System.Collections.Generic;
using Baryonyx.CardLoadout;
using Baryonyx.Combat.Presentation;
using Baryonyx.Party;
using Baryonyx.Training;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The party an adventure's battle is fought with (doc/features/party.md): the formation's
    /// members in slot order, each at the level kept for them with the stats of that level, and
    /// the cards they bring to the deck. A unique skill brings the card of the stage the formation
    /// sets, once the level has unlocked it; a custom skill brings the owned card with its
    /// multiplier. Items and empty slots bring nothing yet. Each member brings the passive skills
    /// its level has unlocked (doc/features/passives.md).
    /// </summary>
    public static class AdventureParty
    {
        public static IReadOnlyList<BattlePartyMember> Of(
            PartyFormation formation,
            PartyMockData party,
            TrainingMockData training
        )
        {
            var members = PartySession.Members(formation);
            var store = new CardLoadoutSessionStore(party, training);
            var result = new BattlePartyMember[members.Count];
            for (int slot = 0; slot < members.Count; slot++)
                result[slot] = Member(members[slot], party, training, store);
            return result;
        }

        private static BattlePartyMember Member(
            PartyMember member,
            PartyMockData party,
            TrainingMockData training,
            CardLoadoutSessionStore store
        )
        {
            if (member == null)
                return null;
            int level = PartySession.LevelOf(party, member.Id);
            var growth = training != null ? training.Find(member.Id) : null;
            var cards = new List<BattlePartyCard>();
            var uniques = store.UniquesOf(member.Id);
            for (int i = 0; i < uniques.Count; i++)
                if (uniques[i] is { Unlocked: true })
                {
                    var skill = store.UniqueCardOf(member.Id, i);
                    if (skill == null)
                        continue;
                    // 戦闘で進化できるよう、誰の何番目の固有スキルかと、ほかの段階の挿絵の引き方を持たせる。
                    cards.Add(
                        new BattlePartyCard
                        {
                            Skill = skill,
                            Art = party.ArtOf(skill.Id),
                            Person = member.Id,
                            Unique = i,
                            ArtOf = party.ArtOf,
                        }
                    );
                }
            foreach (string key in PartySession.CardsOf(party, member.Id))
                Add(cards, PartySession.SkillOf(key), party);
            var passives = new List<Combat.CombatPassiveKind>();
            foreach (var passive in Combat.CombatPassives.UnlockedAt(member.Id, level))
                passives.Add(passive.Kind);
            return new BattlePartyMember
            {
                Name = member.Name,
                Art = member.Art,
                Tint = member.Tint,
                Level = level,
                Stats = growth != null ? growth.StatsAt(level) : default,
                Cards = cards,
                Passives = passives,
            };
        }

        private static void Add(
            List<BattlePartyCard> cards,
            Combat.CardSkill skill,
            PartyMockData party
        )
        {
            if (skill != null)
                cards.Add(new BattlePartyCard { Skill = skill, Art = party.ArtOf(skill.Id) });
        }
    }
}
