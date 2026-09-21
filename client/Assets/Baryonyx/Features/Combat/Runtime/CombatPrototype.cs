namespace Baryonyx.Combat
{
    public static class CombatPrototype
    {
        public static CombatAlly Ally(
            int character,
            string name,
            int equipmentPower,
            int equipmentDown
        )
        {
            var weapon = character switch
            {
                0 => CombatWeapon.Sword,
                2 => CombatWeapon.Dagger,
                4 => CombatWeapon.Hammer,
                _ => CombatWeapon.Staff,
            };
            CombatSkill Skill(
                string title,
                int power,
                int down,
                float cast,
                float cooldown,
                CombatEffect effect = CombatEffect.Damage,
                CombatElement element = CombatElement.None
            ) => new(title, power, down, cast, cooldown, effect, element, weapon);
            var skills = character switch
            {
                0 => new[]
                {
                    Skill("かばう", 0, 0, .3f, 12, CombatEffect.Guard),
                    Skill("盾の一撃", 32, 58, .8f, 10),
                },
                1 => new[]
                {
                    Skill("火の矢", 38, 24, .3f, 7, element: CombatElement.Fire),
                    Skill("大きな火球", 108, 18, 1.5f, 14, element: CombatElement.Fire),
                },
                2 => new[] { Skill("速突き", 32, 30, .3f, 6), Skill("足払い", 38, 65, .8f, 12) },
                3 => new[]
                {
                    Skill("手当て", 65, 0, .8f, 10, CombatEffect.Heal, CombatElement.Light),
                    Skill("守りの光", 0, 0, .3f, 14, CombatEffect.Guard, CombatElement.Light),
                },
                _ => new[] { Skill("横なぎ", 68, 30, .8f, 9), Skill("砕き打ち", 74, 78, 1.5f, 14) },
            };
            string passive = character switch
            {
                0 => "守り手：受けるダメージを15%軽減",
                1 => "集中：通常攻撃の威力+2",
                2 => "身軽：通常攻撃が速い",
                3 => "慈愛：回復量+10",
                _ => "重撃：通常攻撃のダウン値+3",
            };
            return new CombatAlly(
                character,
                name,
                character == 0 ? 185 : 145,
                equipmentPower + (character == 1 ? 2 : 0),
                equipmentDown + (character == 4 ? 3 : 0),
                character == 2 ? 1.55f
                    : character == 1 ? 2.6f
                    : 2.2f,
                weapon,
                passive,
                skills
            );
        }

        public static CombatEnemy[] Enemies(bool boss, int area) =>
            boss
                ? new[]
                {
                    new CombatEnemy(
                        "遺跡の守り手",
                        2100,
                        270,
                        22,
                        true,
                        CombatElement.Fire,
                        CombatWeapon.Hammer
                    ),
                }
                : new[]
                {
                    new CombatEnemy(
                        area == 0 ? "森の牙獣" : "坑道の牙獣",
                        245,
                        105,
                        12,
                        false,
                        CombatElement.Fire,
                        CombatWeapon.Sword
                    ),
                    new CombatEnemy(
                        "苔スライム",
                        185,
                        85,
                        10,
                        false,
                        CombatElement.Light,
                        CombatWeapon.Staff
                    ),
                    new CombatEnemy(
                        "見張りの牙獣",
                        245,
                        105,
                        12,
                        false,
                        CombatElement.Fire,
                        CombatWeapon.Dagger
                    ),
                };
    }
}
