using System;
using System.Collections.Generic;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The party's passive skills in battle (provisional: doc/features/passives.md). Each one's
    /// effect (<see cref="CombatPassive.Effect"/>) is held by its place and works through the
    /// battle's effects (BattleInspectView.Effects); this part tells which place holds which.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        // Each place's passives (an index of Allies); empty for the baked mock's party.
        private CombatPassiveKind[][] allyPassives = Array.Empty<CombatPassiveKind[]>();

        /// <summary>How many ice or thunder cards of the others 見よう見まね has watched this battle.</summary>
        public int CopycatSeen
        {
            get
            {
                var copycat = CombatPassives.Find(CombatPassiveKind.Copycat);
                foreach (var instance in effects.All)
                    if (instance.Effect == copycat.Effect)
                        return instance.Stacks;
                return 0;
            }
        }

        /// <summary>True when the ally at <paramref name="place"/> stands and has <paramref name="kind"/>.</summary>
        public bool HasPassive(int place, CombatPassiveKind kind) =>
            place >= 0
            && place < Allies.Length
            && place < allyPassives.Length
            && !Allies[place].Down
            && Array.IndexOf(allyPassives[place], kind) >= 0;

        /// <summary>The place of a standing ally with <paramref name="kind"/>, or -1.</summary>
        public int PassiveHolder(CombatPassiveKind kind)
        {
            for (int i = 0; i < Allies.Length; i++)
                if (HasPassive(i, kind))
                    return i;
            return -1;
        }

        /// <summary>
        /// Sets each place's passives and the other effects it brings in (in place order), and
        /// starts their counts over.
        /// </summary>
        private void UsePassives(
            IReadOnlyList<CombatPassiveKind>[] byPlace,
            IReadOnlyList<BattleEffect>[] others = null
        )
        {
            allyPassives = new CombatPassiveKind[Allies.Length][];
            var held = new IReadOnlyList<BattleEffect>[Allies.Length];
            for (int i = 0; i < Allies.Length; i++)
            {
                var kinds = i < byPlace.Length ? byPlace[i] : null;
                allyPassives[i] = kinds == null ? Array.Empty<CombatPassiveKind>() : ToArray(kinds);
                var place = new List<BattleEffect>();
                foreach (var kind in allyPassives[i])
                    if (CombatPassives.Find(kind)?.Effect is { } effect)
                        place.Add(effect);
                if (others != null && i < others.Length && others[i] != null)
                    place.AddRange(others[i]);
                held[i] = place;
            }
            UseEffects(held);
        }

        private static CombatPassiveKind[] ToArray(IReadOnlyList<CombatPassiveKind> kinds)
        {
            var array = new CombatPassiveKind[kinds.Count];
            for (int i = 0; i < array.Length; i++)
                array[i] = kinds[i];
            return array;
        }
    }
}
