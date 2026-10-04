using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Party;

namespace Baryonyx.Training
{
    /// <summary>A character the training shows: the party's member and their growth.</summary>
    public sealed class TrainingMember
    {
        public TrainingMember(PartyMember member, TrainingCharacter growth)
        {
            Member = member ?? throw new ArgumentNullException(nameof(member));
            Growth = growth ?? throw new ArgumentNullException(nameof(growth));
        }

        public PartyMember Member { get; }
        public TrainingCharacter Growth { get; }
        public string Id => Member.Id;
    }

    public static class TrainingRoster
    {
        /// <summary>
        /// The characters in the order ◀ ▶ walks: the party's slots first, then the others in
        /// the order they are owned. Characters without growth data are left out.
        /// </summary>
        public static IReadOnlyList<TrainingMember> From(
            PartyFormation formation,
            TrainingMockData data
        )
        {
            if (formation == null || data == null)
                return Array.Empty<TrainingMember>();
            var party = Enumerable
                .Range(0, formation.SlotCount)
                .Select(formation.Member)
                .Where(id => id != null)
                .Select(formation.Find);
            return party
                .Concat(formation.Bench)
                .Where(member => member != null && data.Find(member.Id) != null)
                .Select(member => new TrainingMember(member, data.Find(member.Id)))
                .ToArray();
        }
    }
}
