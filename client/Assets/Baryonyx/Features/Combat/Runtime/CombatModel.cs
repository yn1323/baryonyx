using System;

namespace Baryonyx.Combat
{
    public enum CombatOutcome
    {
        Running,
        Victory,
        Defeat,
    }

    public enum CombatEffect
    {
        Damage,
        Heal,
        Guard,
    }

    public enum CombatElement
    {
        None,
        Fire,
        Light,
    }

    public enum CombatWeapon
    {
        Sword,
        Staff,
        Dagger,
        Hammer,
    }

    // All values in this catalog are prototype tuning, not final game masters.
    public sealed class CombatSkill
    {
        public readonly string Name;
        public readonly int Power,
            Down;
        public readonly float Cast,
            Cooldown;
        public readonly CombatEffect Effect;
        public readonly CombatElement Element;
        public readonly CombatWeapon Weapon;

        public CombatSkill(
            string name,
            int power,
            int down,
            float cast,
            float cooldown,
            CombatEffect effect,
            CombatElement element,
            CombatWeapon weapon
        )
        {
            Name = name;
            Power = power;
            Down = down;
            Cast = cast;
            Cooldown = cooldown;
            Effect = effect;
            Element = element;
            Weapon = weapon;
        }
    }

    public sealed class CombatAlly
    {
        public readonly int Character,
            MaxHp,
            Power,
            Down;
        public readonly string Name,
            Passive;
        public readonly float AttackInterval;
        public readonly CombatWeapon Weapon;
        public readonly CombatSkill[] Skills;
        public readonly float[] Cooldowns = new float[2];
        public int Hp { get; internal set; }
        public float AttackRemaining { get; internal set; }
        public float CastRemaining { get; internal set; }
        public float GuardRemaining { get; internal set; }
        public int CastingSkill { get; internal set; } = -1;
        public int CastTarget { get; internal set; }
        public bool Alive => Hp > 0;
        public bool Casting => CastingSkill >= 0;
        public float HpRatio => Hp / (float)MaxHp;

        public CombatAlly(
            int character,
            string name,
            int maxHp,
            int power,
            int down,
            float interval,
            CombatWeapon weapon,
            string passive,
            CombatSkill[] skills
        )
        {
            Character = character;
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
            Power = power;
            Down = down;
            AttackInterval = interval;
            Weapon = weapon;
            Passive = passive;
            Skills = skills;
            AttackRemaining = interval;
        }
    }

    public sealed class CombatEnemy
    {
        public readonly string Name;
        public readonly int MaxHp,
            MaxDown,
            Power;
        public readonly CombatElement WeakElement;
        public readonly CombatWeapon WeakWeapon;
        public readonly bool Boss;
        public int Hp { get; internal set; }
        public float Down { get; internal set; }
        public float DownRemaining { get; internal set; }
        public float AttackRemaining { get; internal set; } = 2.5f;
        public float SpecialRemaining { get; internal set; } = 7;
        public float WarningRemaining { get; internal set; }
        public int Target { get; internal set; }
        public bool Alive => Hp > 0;
        public bool IsDown => DownRemaining > 0;
        public float HpRatio => Hp / (float)MaxHp;
        public float DownRatio => Down / MaxDown;

        public CombatEnemy(
            string name,
            int hp,
            int down,
            int power,
            bool boss,
            CombatElement element,
            CombatWeapon weapon
        )
        {
            Name = name;
            MaxHp = Hp = hp;
            MaxDown = down;
            Down = down;
            Power = power;
            Boss = boss;
            WeakElement = element;
            WeakWeapon = weapon;
        }
    }

    public readonly struct CombatHit
    {
        public readonly bool Enemy;
        public readonly int Index,
            Amount;
        public readonly bool Downed,
            Healing;

        public CombatHit(
            bool enemy,
            int index,
            int amount,
            bool downed = false,
            bool healing = false
        )
        {
            Enemy = enemy;
            Index = index;
            Amount = amount;
            Downed = downed;
            Healing = healing;
        }
    }
}
