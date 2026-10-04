using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>How a battle of the adventure ended.</summary>
    public enum BattleOutcome
    {
        None,

        // 敵をすべて倒した。
        Victory,

        // 味方がすべて倒れた。
        Defeat,
    }

    /// <summary>
    /// The adventure's way into the battle screen (doc/features/combat.md): the enemies of the
    /// room's encounter, the end of the battle when a side is beaten, and the revive paid with
    /// runes, after which the allies are back at full HP and the enemies keep the HP they lost.
    /// Off (<see cref="EndsWithOutcome"/> false, as in the mock and the labs), a beaten side comes
    /// back for the next turn and the battle goes on for ever.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        /// <summary>
        /// The battle ends when a side is beaten and tells it through <see cref="Finished"/>.
        /// The adventure's battle sets it before the battle begins.
        /// </summary>
        [NonSerialized]
        public bool EndsWithOutcome;

        /// <summary>How the battle ended; None while it goes on.</summary>
        public BattleOutcome Outcome { get; private set; }

        /// <summary>Raised once each time the battle ends (again after a revive).</summary>
        public event Action<BattleOutcome> Finished;

        /// <summary>
        /// Sets the encounter before the battle begins: only the enemies of
        /// <paramref name="standing"/> (indexes of <see cref="Enemies"/>) take part, with their HP
        /// scaled by <paramref name="hpScale"/>. The others are gone from the field and the turn
        /// order, as if beaten before the battle.
        /// </summary>
        public void UseEncounter(IReadOnlyCollection<int> standing, float hpScale)
        {
            var present = new HashSet<int>(standing ?? Array.Empty<int>());
            for (int i = 0; i < Enemies.Length; i++)
            {
                var enemy = Enemies[i];
                enemy.MaxHp = Mathf.Max(
                    1,
                    Mathf.RoundToInt(enemy.MaxHp * Mathf.Max(0.01f, hpScale))
                );
                bool here = present.Contains(i);
                enemy.Hp = here ? enemy.MaxHp : 0;
                if (enemy.Group != null)
                    enemy.Group.alpha = here ? 1f : 0f;
                RefreshEnemy(enemy);
            }
            RefreshTurnOrder();
        }

        /// <summary>
        /// Brings the beaten party back at full HP, keeping the HP the enemies have lost, and
        /// begins the party's next turn. Only after a defeat.
        /// </summary>
        public void Revive()
        {
            if (Outcome != BattleOutcome.Defeat)
                return;
            Outcome = BattleOutcome.None;
            ClearStatuses(true);
            foreach (var ally in Allies)
            {
                ally.Hp = ally.MaxHp;
                ally.Sprite.color = Color.white;
                RefreshAlly(ally);
            }
            EnemyTurn = false;
            StartOfTurn(Turn + 1);
            dealing = StartCoroutine(Deal(Settings.TurnDraw, 0f));
            ShowTurnBanner(PartyTurnText(Turn, again: false), enemies: false);
        }

        /// <summary>
        /// Ends the battle when a side is beaten, if it ends at all. True when it has ended. The
        /// party wins when every enemy is beaten, even if the last blow fell on its own last ally.
        /// </summary>
        private bool Settle()
        {
            if (!EndsWithOutcome)
                return false;
            if (Outcome != BattleOutcome.None)
                return true;
            if (Array.TrueForAll(Enemies, enemy => !enemy.Alive))
                Finish(BattleOutcome.Victory);
            else if (Array.TrueForAll(Allies, ally => ally.Down))
                Finish(BattleOutcome.Defeat);
            return Outcome != BattleOutcome.None;
        }

        private void Finish(BattleOutcome outcome)
        {
            Outcome = outcome;
            if (held >= 0)
                PutBack();
            // No card nor the turn's end takes input until the battle goes on again.
            EnemyTurn = true;
            Refresh();
            Finished?.Invoke(outcome);
        }
    }
}
