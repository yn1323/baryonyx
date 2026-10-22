using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Training;

namespace Baryonyx.CardLoadout
{
    /// <summary>
    /// The store of the running app: the party's cards and levels, the training's mock growth
    /// for the unique skills, and the unique skills' progress kept while the app runs
    /// (<see cref="UniqueSkillSession"/>).
    /// </summary>
    public sealed class CardLoadoutSessionStore : ICardLoadoutStore
    {
        private readonly PartyMockData party;
        private readonly TrainingMockData training;

        public CardLoadoutSessionStore(PartyMockData party, TrainingMockData training)
        {
            this.party = party;
            this.training = training;
        }

        public IReadOnlyList<string> CardsOf(string id) => PartySession.CardsOf(party, id);

        // サーバーから読んだ持ち物。読んでいなければ null（仮データ）。
        public IReadOnlyList<string> OwnedSkills =>
            PartySession.State?.SkillCards?.Select(card => card.Key).ToArray();

        public IReadOnlyList<PartyItem> OwnedItems => PartySession.State?.Items;

        public CardSkill SkillOf(string key) => PartySession.SkillOf(key);

        public void SetCards(string id, IReadOnlyList<string> skills) =>
            PartySession.SetCards(id, skills);

        public string Selected
        {
            get => CardLoadoutSession.Selected;
            set => CardLoadoutSession.Selected = value;
        }

        public IReadOnlyList<CardLoadoutUnique> UniquesOf(string id) =>
            Uniques(training, id, PartySession.LevelOf(party, id), UniqueSkillSession.ProgressOf);

        /// <summary>The card of the stage set of the person's unique skill <paramref name="index"/>, or null.</summary>
        public CardSkill UniqueCardOf(string id, int index)
        {
            var uniques = UniquesOf(id);
            var unique = index >= 0 && index < uniques.Count ? uniques[index] : null;
            return (unique?.Tree?.StageAt(unique.Progress.SetStage) ?? unique?.Tree?.Root)?.Card;
        }

        /// <summary>Sets an acquired stage of the person's unique skill at once, and saves it on the server.</summary>
        public bool SetUniqueStage(string id, int index, int stage)
        {
            bool changed = UniqueSkillSession.SetStage(
                id,
                index,
                stage,
                training != null ? training.Find(id)?.MockProgressOf(index) : null
            );
            if (changed)
                PartyUniqueSkills.SetStage(id, index, stage);
            return changed;
        }

        /// <summary>
        /// The person's unique skills from the training's mock data at <paramref name="level"/>,
        /// with the progress <paramref name="progressOf"/> gives (person, index, mock progress).
        /// </summary>
        public static IReadOnlyList<CardLoadoutUnique> Uniques(
            TrainingMockData training,
            string id,
            int level,
            Func<string, int, UniqueSkillProgress, UniqueSkillProgress> progressOf
        )
        {
            var growth = training != null ? training.Find(id) : null;
            if (growth == null)
                return Array.Empty<CardLoadoutUnique>();
            return growth
                .Uniques.Select(
                    (skill, i) =>
                    {
                        var tree = UniqueSkillTrees.Find(skill.Tree);
                        return tree == null
                            ? null
                            : new CardLoadoutUnique
                            {
                                Tree = tree,
                                UnlockLevel = skill.UnlockLevel,
                                Unlocked = skill.UnlockLevel <= level,
                                Progress = progressOf(id, i, growth.MockProgressOf(i)),
                            };
                    }
                )
                .ToArray();
        }
    }
}
