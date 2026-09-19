using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class CombatEncounterTests
    {
        private static CombatEncounter Encounter(int enemyHp = 10000, int down = 100, int power = 0)
        {
            var allies = new CombatAlly[4];
            for (int i = 0; i < 4; i++)
                allies[i] = new CombatAlly(
                    i,
                    "仲間",
                    100,
                    0,
                    0,
                    100,
                    CombatWeapon.Sword,
                    "",
                    new[]
                    {
                        new CombatSkill(
                            "砕く",
                            20,
                            100,
                            .8f,
                            10,
                            CombatEffect.Damage,
                            CombatElement.Fire,
                            CombatWeapon.Sword
                        ),
                        new CombatSkill(
                            "火球",
                            100,
                            0,
                            1.5f,
                            12,
                            CombatEffect.Damage,
                            CombatElement.Fire,
                            CombatWeapon.Sword
                        ),
                    }
                );
            return new CombatEncounter(
                allies,
                new[]
                {
                    new CombatEnemy(
                        "敵",
                        enemyHp,
                        down,
                        power,
                        true,
                        CombatElement.Fire,
                        CombatWeapon.Sword
                    ),
                }
            );
        }

        [Test]
        public void DamageAndDownWaitForCastAndCooldownIsPerSkill()
        {
            var battle = Encounter();
            Assert.That(battle.Use(0, 0, 0), Is.True);
            battle.Advance(.4f, false);
            Assert.That(battle.Enemies[0].Hp, Is.EqualTo(10000));
            Assert.That(battle.Enemies[0].IsDown, Is.False);
            Assert.That(battle.Use(0, 1, 0), Is.False);
            battle.Advance(.5f, false);
            Assert.That(battle.Enemies[0].Hp, Is.EqualTo(9970));
            Assert.That(battle.Enemies[0].IsDown, Is.True);
            Assert.That(battle.CanUse(0, 0), Is.False);
            Assert.That(battle.CanUse(0, 1), Is.True);
        }

        [Test]
        public void SelectionSlowsEveryTimerOnTheSameClock()
        {
            var normal = Encounter();
            var slow = Encounter();
            normal.Use(0, 0, 0);
            slow.Use(0, 0, 0);
            normal.Advance(1.2f, false);
            slow.Advance(10, true);
            Assert.That(slow.Elapsed, Is.EqualTo(normal.Elapsed).Within(.02));
            Assert.That(
                slow.Allies[0].Cooldowns[0],
                Is.EqualTo(normal.Allies[0].Cooldowns[0]).Within(.02)
            );
            Assert.That(
                slow.Enemies[0].DownRemaining,
                Is.EqualTo(normal.Enemies[0].DownRemaining).Within(.02)
            );
            Assert.That(slow.Enemies[0].Hp, Is.EqualTo(normal.Enemies[0].Hp));
        }

        [Test]
        public void DownCancelsWarningAndRestoresFullDownAfterRecovery()
        {
            var battle = Encounter();
            battle.Advance(7.2f, false);
            Assert.That(battle.Enemies[0].WarningRemaining, Is.GreaterThan(0));
            battle.Use(0, 0, 0);
            battle.Advance(.9f, false);
            Assert.That(battle.Enemies[0].WarningRemaining, Is.Zero);
            Assert.That(battle.Enemies[0].IsDown, Is.True);
            battle.Use(0, 1, 0);
            battle.Advance(1.6f, false);
            Assert.That(battle.Enemies[0].Hp, Is.EqualTo(9745));
            battle.Advance(4, false);
            Assert.That(battle.Enemies[0].IsDown, Is.False);
            Assert.That(battle.Enemies[0].Down, Is.EqualTo(100));
        }

        [Test]
        public void PartialDownDamageDoesNotInterruptWarning()
        {
            var battle = Encounter(down: 500);
            battle.Advance(7.2f, false);
            battle.Use(0, 0, 0);
            battle.Advance(.9f, false);
            Assert.That(battle.Enemies[0].IsDown, Is.False);
            Assert.That(battle.Enemies[0].WarningRemaining, Is.GreaterThan(0));
        }

        [Test]
        public void VictoryCanHappenWithoutDownAndCannotGrantMoreDamage()
        {
            var battle = Encounter(enemyHp: 20);
            int events = 0;
            battle.Hit += _ => events++;
            battle.Use(0, 0, 0);
            battle.Advance(2, false);
            Assert.That(battle.Outcome, Is.EqualTo(CombatOutcome.Victory));
            Assert.That(battle.Enemies[0].IsDown, Is.False);
            int completed = events;
            battle.Advance(100, false);
            Assert.That(events, Is.EqualTo(completed));
            Assert.That(battle.Use(0, 1, 0), Is.False);
        }

        [Test]
        public void DefeatAndRevivePreserveEnemyDamageAndRestoreParty()
        {
            var battle = Encounter(power: 500);
            battle.Use(0, 1, 0);
            battle.Advance(15, false);
            Assert.That(battle.Outcome, Is.EqualTo(CombatOutcome.Defeat));
            int hp = battle.Enemies[0].Hp;
            battle.Revive();
            Assert.That(battle.Outcome, Is.EqualTo(CombatOutcome.Running));
            Assert.That(battle.Enemies[0].Hp, Is.EqualTo(hp));
            foreach (var ally in battle.Allies)
            {
                Assert.That(ally.Hp, Is.EqualTo(ally.MaxHp));
                Assert.That(ally.Cooldowns[1], Is.Zero);
            }
        }

        [Test]
        public void NormalCombatCompletesWithAutoAttacksAndEquipmentChangesTheTime()
        {
            float Run(int power)
            {
                var allies = new CombatAlly[4];
                for (int i = 0; i < 4; i++)
                    allies[i] = CombatPrototype.Ally(i, "仲間", power, 8);
                var b = new CombatEncounter(allies, CombatPrototype.Enemies(false, 0));
                for (int i = 0; i < 1200 && b.Outcome == CombatOutcome.Running; i++)
                    b.Advance(.1f, false);
                Assert.That(b.Outcome, Is.EqualTo(CombatOutcome.Victory));
                return b.Elapsed;
            }
            Assert.That(Run(18), Is.LessThan(Run(12)));
        }

        [Test]
        public void HealingAndGuardAffectActualIncomingDamage()
        {
            CombatEncounter Create()
            {
                var allies = new CombatAlly[4];
                for (int i = 0; i < 4; i++)
                    allies[i] = CombatPrototype.Ally(i, "仲間", 0, 0);
                return new CombatEncounter(
                    allies,
                    new[]
                    {
                        new CombatEnemy(
                            "敵",
                            100000,
                            100000,
                            30,
                            true,
                            CombatElement.None,
                            CombatWeapon.Hammer
                        ),
                    }
                );
            }
            var plain = Create();
            var guarded = Create();
            guarded.Use(0, 0, 0);
            plain.Advance(3, false);
            guarded.Advance(3, false);
            Assert.That(guarded.Allies[0].Hp, Is.GreaterThan(plain.Allies[0].Hp));
            int damaged = plain.Allies[0].Hp;
            plain.Use(3, 0, 0);
            plain.Advance(.9f, false);
            Assert.That(plain.Allies[0].Hp, Is.GreaterThan(damaged));
            Assert.That(plain.Allies[0].Hp, Is.LessThanOrEqualTo(plain.Allies[0].MaxHp));
        }

        [Test]
        public void BossCanBeDefeatedByUsingThePartySkills()
        {
            var allies = new CombatAlly[4];
            for (int i = 0; i < 4; i++)
                allies[i] = CombatPrototype.Ally(i, "仲間", 18, 14);
            var battle = new CombatEncounter(allies, CombatPrototype.Enemies(true, 0));
            for (int frame = 0; frame < 1800 && battle.Outcome == CombatOutcome.Running; frame++)
            {
                for (int slot = 0; slot < 4; slot++)
                for (int skill = 0; skill < 2; skill++)
                    battle.Use(slot, skill, 0);
                battle.Advance(.1f, false);
            }
            Assert.That(battle.Outcome, Is.EqualTo(CombatOutcome.Victory));
        }

        [Test]
        public void InvalidElapsedAndInvalidInputsDoNotMutateBattle()
        {
            var battle = Encounter();
            battle.Advance(float.NaN, false);
            battle.Advance(float.PositiveInfinity, false);
            battle.Advance(-1, false);
            Assert.That(battle.Elapsed, Is.Zero);
            Assert.That(battle.Use(-1, 0, 0), Is.False);
            Assert.That(battle.Use(0, 2, 0), Is.False);
            Assert.That(battle.Use(0, 0, 4), Is.False);
        }
    }
}
