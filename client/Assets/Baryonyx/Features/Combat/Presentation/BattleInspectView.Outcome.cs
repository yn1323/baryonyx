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
                // A drop falls from the last enemy beaten; until one is, from one on the field.
                if (here)
                    lastBeaten = i;
            }
            RefreshTurnOrder();
        }

        /// <summary>
        /// Sets the encounter of an adventure's room before the battle begins: the enemy kind of
        /// each place on the field (<paramref name="kinds"/>, by place in <see cref="Enemies"/>;
        /// null or empty leaves the place empty), at <paramref name="level"/>, with their HP
        /// scaled by <paramref name="hpScale"/> and their attacks by <paramref name="powerScale"/>.
        /// Each place takes the kind's picture, drawn part, face, weaknesses and numbers
        /// (<see cref="CombatEnemyStats"/>). A kind without a picture keeps the place's own, and a
        /// place without a kind is gone from the field.
        /// </summary>
        public void UseEncounter(
            IReadOnlyList<string> kinds,
            int level,
            float hpScale,
            float powerScale = 1f
        )
        {
            var standing = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
            {
                string id = kinds != null && i < kinds.Count ? kinds[i] : null;
                if (string.IsNullOrEmpty(id))
                    continue;
                standing.Add(i);
                Become(i, id, level);
                Enemies[i].AttackScale = Mathf.Max(0.01f, powerScale);
                Enemies[i].Power = Mathf.Max(
                    1,
                    Mathf.RoundToInt(Enemies[i].Power * Enemies[i].AttackScale)
                );
            }
            UseEncounter(standing, hpScale);
        }

        // その場所の敵を、別の種類の敵にする。絵・狙える範囲・顔・弱点・数値を入れ替える。
        private void Become(int index, string id, int level)
        {
            var enemy = Enemies[index];
            var stats = CombatEnemyStats.Find(id);
            var definition = CombatEnemies.Find(id);
            var look = EnemyLooks != null ? EnemyLooks.Find(id) : null;
            if (stats == null || definition == null)
                return;
            enemy.Id = id;
            enemy.Level = Mathf.Max(1, level);
            enemy.MaxHp = EnemyStats.At(stats.Hp, enemy.Level);
            enemy.Magic = stats.Magic;
            enemy.Power = EnemyStats.At(
                stats.Magic ? stats.MagicAttack : stats.PhysicalAttack,
                enemy.Level
            );
            enemy.PhysicalDefense = EnemyStats.At(stats.PhysicalDefense, enemy.Level);
            enemy.MagicDefense = EnemyStats.At(stats.MagicDefense, enemy.Level);
            enemy.PhysicalAttack = EnemyStats.At(stats.PhysicalAttack, enemy.Level);
            enemy.MagicAttack = EnemyStats.At(stats.MagicAttack, enemy.Level);
            enemy.Speed = EnemyStats.At(stats.Speed, enemy.Level);
            enemy.AttackScale = 1f;
            enemy.Block = 0;
            enemy.PowerUp = 0;
            // その種類の技と行動パターンで動く（doc/catalog/enemies）。定義がなければ、技1つで攻撃する。
            var behavior = CombatEnemyBehaviors.Find(id);
            enemy.Brain =
                behavior != null
                    ? new EnemyBrain(behavior, n => UnityEngine.Random.Range(0, n))
                    : null;
            enemy.SkillName = stats.Skill;
            enemy.Boss = stats.Body == EnemyBody.Large;
            if (look != null && look.Texture != null && enemy.Sprite != null)
            {
                var old = enemy.Sprite.texture;
                // 1ドットの大きさは、今の絵の大きさから求める（戦闘の舞台では絵の幅÷ドット数）。
                float dot =
                    old != null && old.width > 0
                        ? enemy.Sprite.rectTransform.sizeDelta.x / old.width
                        : 3f;
                if (dot <= 0f)
                    dot = 3f;
                enemy.Sprite.texture = look.Texture;
                enemy.Sprite.rectTransform.sizeDelta =
                    new Vector2(look.Texture.width, look.Texture.height) * dot;
                enemy.IconUv = look.IconUv;
                if (enemy.TargetArea != null)
                {
                    var target = enemy.TargetArea;
                    var markerOffset =
                        enemy.Marker != null
                            ? enemy.Marker.anchoredPosition
                                - target.anchoredPosition
                                - new Vector2(0f, target.sizeDelta.y * 0.5f)
                            : Vector2.zero;
                    target.anchoredPosition = look.TargetCenter * dot;
                    target.sizeDelta = look.TargetSize * dot;
                    if (enemy.Marker != null)
                    {
                        // 印は、狙える範囲の上端から同じだけ上に置く。
                        enemy.Marker.anchoredPosition =
                            target.anchoredPosition
                            + new Vector2(0f, target.sizeDelta.y * 0.5f)
                            + markerOffset;
                        if (index < enemyMarkerBase.Length)
                            enemyMarkerBase[index] = enemy.Marker.anchoredPosition;
                    }
                }
                if (enemy.Shadow != null)
                    enemy.Shadow.sizeDelta = new Vector2(
                        look.Texture.width * dot * 0.8f,
                        enemy.Shadow.sizeDelta.y
                    );
            }
            ShowWeaknessesOf(enemy, definition);
        }

        // 弱点のアイコンを、その種類の弱点だけ、定義の順に並べて出す。
        private static void ShowWeaknessesOf(BattleInspectEnemy enemy, EnemyDefinition definition)
        {
            if (enemy.WeaknessSlots == null || enemy.WeaknessSlots.Length == 0)
                return;
            var shown = new List<BattleInspectWeakness>();
            foreach (var slot in enemy.WeaknessSlots)
            {
                var go = SlotOf(slot);
                if (go != null)
                    go.SetActive(false);
            }
            foreach (var element in definition.Weaknesses)
            {
                var wanted = BattleCardText.ElementOf(element);
                var slot = Array.Find(enemy.WeaknessSlots, entry => entry.Element == wanted);
                if (slot == null)
                    continue;
                var go = SlotOf(slot);
                if (go != null)
                {
                    go.SetActive(true);
                    go.transform.SetSiblingIndex(shown.Count);
                }
                slot.StartsRevealed = false;
                slot.Show(false);
                shown.Add(slot);
            }
            enemy.Weaknesses = shown.ToArray();
        }

        private static GameObject SlotOf(BattleInspectWeakness weakness) =>
            weakness != null && weakness.Known != null && weakness.Known.transform.parent != null
                ? weakness.Known.transform.parent.gameObject
                : null;

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
            ClearBlock();
            // The party stands up again with its moment, then its turn begins (Moments).
            Enqueue(Revived());
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
