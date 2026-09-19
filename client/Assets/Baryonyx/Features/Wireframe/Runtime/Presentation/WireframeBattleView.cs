using System;
using Baryonyx.Combat;
using UnityEngine;

namespace Baryonyx.Wireframe
{
    public sealed partial class WireframeView
    {
        private void RenderBattle(string[] names)
        {
            var s = Session;
            var battle = s.Battle;
            if (battle == null)
                return;
            string warnings = "";
            foreach (var enemy in battle.Enemies)
                if (enemy.Alive && enemy.WarningRemaining > 0)
                    warnings +=
                        $"{enemy.Name}：大きな一撃 → {(enemy.Boss ? "全員" : names[enemy.Target])}  {enemy.WarningRemaining:F1}\n";
            Set(
                "BattleWarning",
                warnings.Length == 0
                    ? "通常攻撃はオート  /  仲間をタップしてスキル"
                    : warnings.TrimEnd()
            );
            for (int i = 0; i < 4; i++)
            {
                var ally = battle.Allies[i];
                Caption(
                    "Ally" + i,
                    ally.Casting ? "詠唱中"
                        : ally.GuardRemaining > 0 ? "防護"
                        : ""
                );
                Caption("Hp" + i, $"{names[i]}\n{ally.Hp}/{ally.MaxHp}");
                Enable("Hp" + i, ally.Alive);
                Enable("Ally" + i, ally.Alive);
                HpFills[i].anchorMax = new Vector2(ally.HpRatio, 1);
            }
            for (int i = 0; i < 3; i++)
            {
                bool visible = i < battle.Enemies.Length && battle.Enemies[i].Alive;
                Visible("Enemy" + i, visible);
                if (!visible)
                    continue;
                var enemy = battle.Enemies[i];
                Caption(
                    "Enemy" + i,
                    (s.Target == i ? "◎ " : "")
                        + enemy.Name
                        + (
                            enemy.IsDown ? "\nダウン中"
                            : enemy.DownRatio < .35f ? "\nよろけ"
                            : ""
                        )
                );
                HpFills[i + 4].anchorMax = new Vector2(enemy.HpRatio, 1);
                var rect = (RectTransform)Button("Enemy" + i).transform;
                if (i == 0)
                {
                    rect.anchorMin = s.IsBoss ? new Vector2(.50f, .26f) : new Vector2(.57f, .66f);
                    rect.anchorMax = s.IsBoss ? new Vector2(.99f, .95f) : new Vector2(.98f, .99f);
                }
            }
            var actor = battle.Allies[s.Slot];
            for (int i = 0; i < 3; i++)
            {
                Visible("Skill" + i, s.SkillSelection && i < 2);
                if (i >= 2)
                    continue;
                var skill = actor.Skills[i];
                string kind =
                    skill.Effect == CombatEffect.Damage
                        ? $"威力 {skill.Power + actor.Power}  ダウン {skill.Down + actor.Down}"
                    : skill.Effect == CombatEffect.Heal
                        ? $"回復 {skill.Power + (actor.Character == 3 ? 10 : 0)}"
                    : "味方全員を防護";
                Caption(
                    "Skill" + i,
                    skill.Name
                        + "\n"
                        + (actor.Cooldowns[i] > 0 ? $"あと {actor.Cooldowns[i]:F1}秒" : kind)
                );
                Enable("Skill" + i, battle.CanUse(s.Slot, i));
            }
            string selection =
                actor.Casting ? $"{actor.Name}  {actor.Skills[actor.CastingSkill].Name}を準備中"
                : s.SkillSelection ? $"{actor.Name}  /  スロー中"
                : "仲間を選んで、戦況を変えよう";
            if (s.SkillSelection && s.Skill >= 0)
            {
                var skill = actor.Skills[s.Skill];
                selection +=
                    skill.Effect == CombatEffect.Damage
                        ? $"\n標的：{battle.Enemies[s.Target].Name}"
                        : "\n対象：味方";
            }
            Set("BattleSelection", selection);
            Visible("UseSkill", s.SkillSelection);
            Enable("UseSkill", s.Skill >= 0 && battle.CanUse(s.Slot, s.Skill));
        }
    }
}
