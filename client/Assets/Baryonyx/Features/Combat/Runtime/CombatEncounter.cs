using System;
using System.Linq;

namespace Baryonyx.Combat
{
    // A fixed simulation step gives every timer the same clock, including during selection.
    public sealed class CombatEncounter
    {
        public const float SelectionSpeed = .12f;
        public const float DownDuration = 5;
        private const float Step = 1f / 60;
        private double accumulator;
        private int enemyTurns;
        public CombatAlly[] Allies { get; }
        public CombatEnemy[] Enemies { get; }
        public CombatOutcome Outcome { get; private set; }
        public float Elapsed { get; private set; }
        public int Target { get; set; }
        public event Action<CombatHit> Hit;

        public CombatEncounter(CombatAlly[] allies, CombatEnemy[] enemies)
        {
            if (allies == null || allies.Length != 4 || enemies == null || enemies.Length == 0)
                throw new ArgumentException(
                    "An encounter needs four allies and at least one enemy."
                );
            Allies = allies;
            Enemies = enemies;
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].AttackRemaining += i * .5f;
                enemies[i].SpecialRemaining += i * 2;
            }
        }

        public bool CanUse(int slot, int skill) =>
            Outcome == CombatOutcome.Running
            && slot >= 0
            && slot < Allies.Length
            && skill >= 0
            && skill < 2
            && Allies[slot].Alive
            && !Allies[slot].Casting
            && Allies[slot].Cooldowns[skill] <= 0;

        public bool Use(int slot, int skill, int target)
        {
            if (!CanUse(slot, skill))
                return false;
            var actor = Allies[slot];
            if (
                actor.Skills[skill].Effect == CombatEffect.Damage
                && (target < 0 || target >= Enemies.Length || !Enemies[target].Alive)
            )
                return false;
            actor.CastingSkill = skill;
            actor.CastRemaining = actor.Skills[skill].Cast;
            actor.CastTarget = target;
            // Cooldown starts on confirmation; a cancelled/dead caster never receives a free cast.
            actor.Cooldowns[skill] = actor.Skills[skill].Cooldown;
            return true;
        }

        public void Advance(float realSeconds, bool selecting)
        {
            if (
                Outcome != CombatOutcome.Running
                || realSeconds <= 0
                || float.IsNaN(realSeconds)
                || float.IsInfinity(realSeconds)
            )
                return;
            accumulator += realSeconds * (selecting ? SelectionSpeed : 1);
            while (accumulator >= Step && Outcome == CombatOutcome.Running)
            {
                accumulator -= Step;
                Tick();
            }
        }

        private void Tick()
        {
            Elapsed += Step;
            foreach (var ally in Allies)
            {
                for (int i = 0; i < 2; i++)
                    ally.Cooldowns[i] = Math.Max(0, ally.Cooldowns[i] - Step);
                ally.GuardRemaining = Math.Max(0, ally.GuardRemaining - Step);
                if (!ally.Alive)
                    continue;
                if (ally.Casting)
                {
                    ally.CastRemaining -= Step;
                    if (ally.CastRemaining <= 0)
                        Resolve(ally);
                }
                else
                {
                    ally.AttackRemaining -= Step;
                    if (ally.AttackRemaining <= 0)
                    {
                        ally.AttackRemaining += ally.AttackInterval;
                        DamageEnemy(
                            LivingEnemy(Target),
                            ally.Power,
                            ally.Down,
                            CombatElement.None,
                            ally.Weapon
                        );
                    }
                }
            }
            if (Enemies.All(e => !e.Alive))
            {
                Outcome = CombatOutcome.Victory;
                return;
            }
            foreach (var enemy in Enemies)
            {
                if (!enemy.Alive)
                    continue;
                if (enemy.IsDown)
                {
                    enemy.DownRemaining = Math.Max(0, enemy.DownRemaining - Step);
                    if (!enemy.IsDown)
                        enemy.Down = enemy.MaxDown;
                    continue;
                }
                if (enemy.WarningRemaining > 0)
                {
                    enemy.WarningRemaining = Math.Max(0, enemy.WarningRemaining - Step);
                    if (enemy.WarningRemaining <= 0)
                    {
                        if (enemy.Boss)
                            for (int i = 0; i < Allies.Length; i++)
                                DamageAlly(i, enemy.Power * 2);
                        else
                            DamageAlly(LivingAlly(enemy.Target), enemy.Power * 3);
                        enemy.SpecialRemaining = enemy.Boss ? 10 : 13;
                        enemy.AttackRemaining = 2.5f;
                    }
                    continue;
                }
                enemy.SpecialRemaining -= Step;
                if (enemy.SpecialRemaining <= 0)
                {
                    enemy.Target = LivingAlly(enemyTurns++ % 4);
                    enemy.WarningRemaining = enemy.Boss ? 4 : 3;
                    continue;
                }
                enemy.AttackRemaining -= Step;
                if (enemy.AttackRemaining <= 0)
                {
                    DamageAlly(LivingAlly(enemyTurns++ % 4), enemy.Power);
                    enemy.AttackRemaining += enemy.Boss ? 2.4f : 3;
                }
            }
            if (Allies.All(a => !a.Alive))
                Outcome = CombatOutcome.Defeat;
        }

        private void Resolve(CombatAlly ally)
        {
            var skill = ally.Skills[ally.CastingSkill];
            ally.CastingSkill = -1;
            ally.CastRemaining = 0;
            if (skill.Effect == CombatEffect.Heal)
            {
                int index = -1;
                for (int i = 0; i < Allies.Length; i++)
                    if (Allies[i].Alive && (index < 0 || Allies[i].HpRatio < Allies[index].HpRatio))
                        index = i;
                if (index < 0)
                    return;
                int amount = Math.Min(
                    Allies[index].MaxHp - Allies[index].Hp,
                    skill.Power + (ally.Character == 3 ? 10 : 0)
                );
                Allies[index].Hp += amount;
                Hit?.Invoke(new CombatHit(false, index, amount, healing: true));
            }
            else if (skill.Effect == CombatEffect.Guard)
            {
                foreach (var member in Allies)
                    if (member.Alive)
                        member.GuardRemaining = 6;
            }
            else
                DamageEnemy(
                    LivingEnemy(ally.CastTarget),
                    skill.Power + ally.Power,
                    skill.Down + ally.Down,
                    skill.Element,
                    skill.Weapon
                );
        }

        private int LivingEnemy(int preferred) =>
            preferred >= 0 && preferred < Enemies.Length && Enemies[preferred].Alive
                ? preferred
                : Array.FindIndex(Enemies, e => e.Alive);

        private int LivingAlly(int preferred) =>
            preferred >= 0 && preferred < Allies.Length && Allies[preferred].Alive
                ? preferred
                : Array.FindIndex(Allies, a => a.Alive);

        private void DamageEnemy(
            int index,
            int power,
            int down,
            CombatElement element,
            CombatWeapon weapon
        )
        {
            if (index < 0)
                return;
            var enemy = Enemies[index];
            float weak =
                (element != CombatElement.None && element == enemy.WeakElement)
                || weapon == enemy.WeakWeapon
                    ? 1.5f
                    : 1;
            int damage = (int)Math.Round(power * weak * (enemy.IsDown ? 1.5f : 1));
            enemy.Hp = Math.Max(0, enemy.Hp - damage);
            bool downed = false;
            if (!enemy.IsDown && enemy.Alive)
            {
                enemy.Down = Math.Max(0, enemy.Down - down * weak);
                if (enemy.Down <= 0)
                {
                    downed = true;
                    enemy.DownRemaining = DownDuration;
                    enemy.WarningRemaining = 0;
                    enemy.SpecialRemaining = enemy.Boss ? 7 : 10;
                    enemy.AttackRemaining = 2.5f;
                }
            }
            Hit?.Invoke(new CombatHit(true, index, damage, downed));
        }

        private void DamageAlly(int index, int power)
        {
            if (index < 0 || !Allies[index].Alive)
                return;
            var ally = Allies[index];
            int damage = (int)
                Math.Round(
                    power * (ally.GuardRemaining > 0 ? .45f : 1) * (ally.Character == 0 ? .85f : 1)
                );
            ally.Hp = Math.Max(0, ally.Hp - damage);
            if (!ally.Alive)
            {
                ally.CastingSkill = -1;
                ally.CastRemaining = 0;
            }
            Hit?.Invoke(new CombatHit(false, index, damage));
        }

        public void Revive()
        {
            if (Outcome != CombatOutcome.Defeat)
                return;
            foreach (var ally in Allies)
            {
                ally.Hp = ally.MaxHp;
                ally.CastingSkill = -1;
                ally.CastRemaining = 0;
                ally.AttackRemaining = ally.AttackInterval;
                ally.GuardRemaining = 0;
                Array.Clear(ally.Cooldowns, 0, ally.Cooldowns.Length);
            }
            foreach (var enemy in Enemies)
                enemy.WarningRemaining = 0;
            accumulator = 0;
            Outcome = CombatOutcome.Running;
        }
    }
}
