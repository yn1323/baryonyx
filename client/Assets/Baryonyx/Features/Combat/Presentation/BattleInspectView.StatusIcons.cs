using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// One place in the row of status icons under a character's HP bar: shown while the
    /// character has a status of its kind.
    /// </summary>
    [Serializable]
    public sealed class BattleInspectStatusIcon
    {
        public BattleStatusIcon Kind;
        public GameObject Icon;
    }

    /// <summary>
    /// The status icons under the HP bars (doc/features/screens/battle-deck.md, 状態のアイコン):
    /// every ally standing and every enemy alive shows an icon for each kind of status it has,
    /// boosts and the rest alike, in the order of <see cref="BattleStatusIcons.Order"/>. An
    /// enemy gathering its strength (力を溜める) shows attack up.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        private readonly HashSet<BattleStatusIcon> iconsShown = new();

        // Checked every frame: statuses come and go from cards, enemies' skills and the turns'
        // countdown alike, and the check is only a few flags.
        private void RefreshStatusIcons()
        {
            for (int i = 0; i < Allies.Length; i++)
            {
                var ally = Allies[i];
                ShowStatusIcons(ally.StatusIcons, true, i, !ally.Absent && !ally.Down, 0);
            }
            for (int i = 0; i < Enemies.Length; i++)
            {
                var enemy = Enemies[i];
                ShowStatusIcons(enemy.StatusIcons, false, i, enemy.Alive, enemy.PowerUp);
            }
        }

        private void ShowStatusIcons(
            BattleInspectStatusIcon[] icons,
            bool ally,
            int index,
            bool standing,
            int powerUp
        )
        {
            if (icons == null || icons.Length == 0)
                return;
            iconsShown.Clear();
            if (standing)
            {
                foreach (var active in StatusesOf(ally, index))
                    iconsShown.Add(BattleStatusIcons.Of(active.Status));
                if (powerUp > 0)
                    iconsShown.Add(BattleStatusIcon.AttackUp);
            }
            foreach (var icon in icons)
            {
                if (icon.Icon == null)
                    continue;
                bool on = iconsShown.Contains(icon.Kind);
                if (icon.Icon.activeSelf != on)
                    icon.Icon.SetActive(on);
            }
        }
    }
}
