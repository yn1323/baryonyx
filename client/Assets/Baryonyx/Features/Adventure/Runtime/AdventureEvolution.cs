using System;
using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using Baryonyx.SkillEvolution;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// Unique skills evolving in an adventure's battle (doc/features/skill-evolution.md): the
    /// chance by the kind of room and the destination's recommended level
    /// (doc/features/skill-evolution.md#確率の仮設定), and the evolutions of the room, which are
    /// kept once the room ends (won, or lost and the adventure over) and dropped when the battle
    /// is left unfinished (doc/features/skill-evolution.md#進化の保存).
    /// </summary>
    public sealed class AdventureEvolution
    {
        /// <summary>One evolution of the room: whose unique skill, and the stage it became.</summary>
        public readonly struct Step
        {
            public readonly string Person;
            public readonly int Unique;
            public readonly int Stage;

            public Step(string person, int unique, int stage)
            {
                Person = person;
                Unique = unique;
                Stage = stage;
            }
        }

        private readonly List<Step> pending = new();
        private readonly Action<Step> save;

        /// <param name="save">Saves one evolution kept (to the server); null keeps it in the app only.</param>
        public AdventureEvolution(Action<Step> save = null)
        {
            this.save = save;
        }

        /// <summary>The evolutions of the room not kept yet.</summary>
        public IReadOnlyList<Step> Pending => pending;

        /// <summary>
        /// The base chance (percent) that a unique skill's card evolves when used in a room of
        /// <paramref name="kind"/>: higher the stronger the enemies (a trial's are as strong as a
        /// strong monster's); none outside a battle.
        /// </summary>
        public static float BasePercentOf(AdventureRoomKind kind) =>
            kind switch
            {
                AdventureRoomKind.Battle => 4f,
                AdventureRoomKind.Elite => 8f,
                AdventureRoomKind.Trial => 8f,
                AdventureRoomKind.Boss => 10f,
                _ => 0f,
            };

        /// <summary>How unique skills evolve in a battle in a room of <paramref name="kind"/>.</summary>
        public BattleEvolution Rules(AdventureRoomKind kind, int recommended)
        {
            float basePercent = BasePercentOf(kind);
            return new BattleEvolution
            {
                Chance = level => UniqueSkillEvolution.Chance(basePercent, level, recommended),
                Acquired = (person, unique, stage) =>
                    UniqueSkillSession.ProgressOf(person, unique, null).Has(stage),
                Evolved = (person, unique, stage) => pending.Add(new Step(person, unique, stage)),
            };
        }

        /// <summary>
        /// Keeps the evolutions of the room: each stage is acquired and set for the rest of the
        /// adventure and after, and saved.
        /// </summary>
        public void Keep()
        {
            foreach (var step in pending)
            {
                UniqueSkillSession.Evolve(step.Person, step.Unique, step.Stage, null);
                save?.Invoke(step);
            }
            pending.Clear();
        }
    }
}
