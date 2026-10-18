using System;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The weaknesses known from earlier battles (doc/features/combat.md, revealing weaknesses):
    /// shown from the start of the battle in place of the mock's own, and those a card reveals
    /// told to the caller, which keeps them.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        private Func<string, BattleInspectElement, bool> knownWeakness;

        /// <summary>Raised when a card hits a hidden weakness: the enemy's kind and the element.</summary>
        public event Action<string, BattleInspectElement> WeaknessRevealed;

        /// <summary>
        /// Shows from the start the weaknesses <paramref name="known"/> says were revealed before,
        /// by the enemies' kinds, in place of those the mock starts with; the others show "?".
        /// Call it before the battle begins.
        /// </summary>
        public void UseRevealedWeaknesses(Func<string, BattleInspectElement, bool> known)
        {
            knownWeakness = known;
            foreach (var enemy in Enemies)
            foreach (var weakness in enemy.Weaknesses)
                weakness.Show(known != null && known(enemy.Id, weakness.Element));
        }

        /// <summary>
        /// Shows, without the glint, the weaknesses found known since (read after the battle
        /// began). It never hides one.
        /// </summary>
        public void RefreshRevealedWeaknesses()
        {
            if (knownWeakness == null)
                return;
            foreach (var enemy in Enemies)
            foreach (var weakness in enemy.Weaknesses)
                if (!weakness.Revealed && knownWeakness(enemy.Id, weakness.Element))
                    weakness.Show(true);
        }

        // 同じ戦闘にいる同じ種類の敵にも、同時に開示する。
        private void OnWeaknessRevealed(BattleInspectEnemy enemy, BattleInspectWeakness weakness)
        {
            if (string.IsNullOrEmpty(enemy.Id))
                return;
            foreach (var other in Enemies)
            {
                if (other == enemy || other.Id != enemy.Id)
                    continue;
                var same = Array.Find(other.Weaknesses, entry => entry.Element == weakness.Element);
                if (same != null && !same.Revealed)
                    StartCoroutine(same.Reveal(Vfx, () => other.Alive));
            }
            WeaknessRevealed?.Invoke(enemy.Id, weakness.Element);
        }
    }
}
