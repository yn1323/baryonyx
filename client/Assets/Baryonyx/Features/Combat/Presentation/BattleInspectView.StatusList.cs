using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The list of a character's statuses (doc/features/screens/battle-deck.md, the battle mock): the
    /// battlefield shows no status icons, so a tap on an enemy or an ally while no card is up
    /// lists its name, its HP out of its max and each status with what is left of it and what it
    /// does, in plaques in the middle above the battlefield.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        /// <summary>The plaques of the list, made like the keyword notes.</summary>
        public BattleInspectKeywordTips StatusList;

        private bool listedAlly;
        private int listed = -1;

        private static readonly Color ListTitle = new(0.96f, 0.93f, 0.86f);

        /// <summary>True while the list is open.</summary>
        public bool StatusListOpen => listed >= 0;

        /// <summary>Whose statuses the list shows: an ally or an enemy, by index (-1 when closed).</summary>
        public (bool ally, int index) Listed => (listedAlly, listed);

        /// <summary>
        /// A tap on the battlefield with no card up: lists the statuses of the character tapped,
        /// closes the list on a second tap on it or a tap on no one, and moves it to another
        /// character tapped.
        /// </summary>
        private void TapCharacter(Vector2 screenPoint)
        {
            var (ally, index) = CharacterAt(screenPoint);
            if (index < 0 || (StatusListOpen && ally == listedAlly && index == listed))
                HideStatusList();
            else
                ShowStatusList(ally, index);
        }

        /// <summary>The standing enemy or present ally nearest a screen point, or index -1.</summary>
        private (bool ally, int index) CharacterAt(Vector2 screenPoint)
        {
            var found = (ally: false, index: -1);
            float best = float.MaxValue;
            for (int i = 0; i < Enemies.Length; i++)
            {
                if (!Enemies[i].Alive)
                    continue;
                float distance = DropDistance(
                    Enemies[i].TargetArea,
                    screenPoint,
                    CameraOf(Enemies[i].TargetArea)
                );
                if (distance < best)
                    (best, found) = (distance, (false, i));
            }
            for (int i = 0; i < Allies.Length; i++)
            {
                if (Allies[i].Absent)
                    continue;
                float distance = DropDistance(
                    Allies[i].TargetArea,
                    screenPoint,
                    CameraOf(Allies[i].TargetArea)
                );
                if (distance < best)
                    (best, found) = (distance, (true, i));
            }
            return found;
        }

        /// <summary>Opens the list on a character, or shows it again as it is now.</summary>
        public void ShowStatusList(bool ally, int index)
        {
            if (StatusList == null)
                return;
            listedAlly = ally;
            listed = index;
            var lines = StatusLines(ally, index);
            StatusList.Show(lines.notes);
            for (int i = 0; i < StatusList.Titles.Length && i < lines.colors.Count; i++)
                StatusList.Titles[i].color = lines.colors[i];
        }

        /// <summary>Closes the list.</summary>
        public void HideStatusList()
        {
            listed = -1;
            if (StatusList != null)
                StatusList.Hide();
        }

        // Keeps the open list up to date as HP and statuses change, and closes it on a beaten enemy.
        private void RefreshStatusList()
        {
            if (!StatusListOpen)
                return;
            if (!listedAlly && !Enemies[listed].Alive)
                HideStatusList();
            else
                ShowStatusList(listedAlly, listed);
        }

        /// <summary>
        /// The list's plaques for a character: its name with its HP out of its max, then one for
        /// each status (or one saying there is none), each with its title colour.
        /// </summary>
        public (List<CardKeyword> notes, List<Color> colors) StatusLines(bool ally, int index)
        {
            var notes = new List<CardKeyword>();
            var colors = new List<Color>();
            string name;
            int hp;
            int max;
            if (ally)
            {
                name = Allies[index].Name;
                hp = Allies[index].Hp;
                max = Allies[index].MaxHp;
            }
            else
            {
                var enemy = Enemies[index];
                name = CombatEnemies.Find(enemy.Id)?.Name ?? enemy.Id;
                hp = enemy.Hp;
                max = enemy.MaxHp;
            }
            notes.Add(new CardKeyword(name, HpLine(hp, max)));
            colors.Add(ListTitle);

            var active = StatusesOf(ally, index);
            if (active.Count == 0)
            {
                notes.Add(new CardKeyword(NoStatusText, ""));
                colors.Add(ListTitle);
            }
            foreach (var status in active)
            {
                string left = LeftOf(status);
                string title = CardRules.StatusName(status.Status);
                notes.Add(
                    new CardKeyword(left.Length > 0 ? $"{title}　{left}" : title, NoteOf(status))
                );
                colors.Add(StatusColor(status.Status));
            }
            return (notes, colors);
        }

        public const string NoStatusText = "状態なし";

        /// <summary>The HP line under the name: the HP now out of the max.</summary>
        public static string HpLine(int hp, int max) => $"HP {Mathf.Max(0, hp)} / {max}";

        // What is left of a status: poison's stacks, the turns of the others, or the one time
        // of those that wait to be used.
        private static string LeftOf(ActiveStatus status) =>
            status.Status switch
            {
                CardStatus.Poison => $"×{status.Amount}",
                CardStatus.Freeze or CardStatus.Reflect => "1回",
                _ => status.Turns > 0 ? $"残り{status.Turns}ターン" : "",
            };

        private static string NoteOf(ActiveStatus status) =>
            status.Status == CardStatus.AttackUp && status.Amount > 0
                ? $"与えるダメージ+{status.Amount}%。"
                : CardRules.StatusNote(status.Status);
    }
}
