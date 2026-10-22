using System.Collections.Generic;
using Baryonyx.CardLoadout;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The altar of evolution (doc/features/stage-progression.md, マスの中身): one unique skill of
    /// the party, chosen, evolves one stage, the same as a chest's evolution
    /// (doc/features/skill-evolution.md#進化が起きる場面). The candidates are the next stages of the
    /// stage each unlocked unique skill sets; one whose set stage has none cannot evolve.
    /// </summary>
    public static class AdventureAltar
    {
        /// <summary>One unique skill that can evolve: whose, which, and the stages it can become.</summary>
        public sealed class Choice
        {
            public string Person = "";
            public string Name = "";
            public int Index;
            public UniqueSkillTree Tree;
            public int From;
            public IReadOnlyList<int> Next = new List<int>();

            public string CardName(int stage) => Tree?.StageAt(stage)?.Card?.Name ?? "";
        }

        /// <summary>The formation's unique skills that can evolve now.</summary>
        public static IReadOnlyList<Choice> ChoicesOf(
            PartyFormation formation,
            PartyMockData party,
            TrainingMockData training
        )
        {
            var choices = new List<Choice>();
            if (formation == null || party == null)
                return choices;
            var store = new CardLoadoutSessionStore(party, training);
            foreach (var member in PartySession.Members(formation))
            {
                if (member == null)
                    continue;
                var uniques = store.UniquesOf(member.Id);
                for (int index = 0; index < uniques.Count; index++)
                {
                    var unique = uniques[index];
                    if (unique == null || !unique.Unlocked || unique.Tree == null)
                        continue;
                    int from = unique.Progress.SetStage;
                    var next = UniqueSkillTreeShape.Children(from);
                    if (next.Count == 0)
                        continue;
                    choices.Add(
                        new Choice
                        {
                            Person = member.Id,
                            Name = member.Name,
                            Index = index,
                            Tree = unique.Tree,
                            From = from,
                            Next = next,
                        }
                    );
                }
            }
            return choices;
        }

        /// <summary>Evolves the unique skill into <paramref name="stage"/>, at once and on the server.</summary>
        public static void Evolve(Choice choice, int stage, TrainingMockData training)
        {
            UniqueSkillSession.Evolve(
                choice.Person,
                choice.Index,
                stage,
                training != null ? training.Find(choice.Person)?.MockProgressOf(choice.Index) : null
            );
            PartyUniqueSkills.Evolve(choice.Person, choice.Index, stage);
        }
    }
}
