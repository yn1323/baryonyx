using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    /// <summary>A battle without a screen, run by the same rules as the battle screen.</summary>
    public sealed class BattleStateTests
    {
        [SetUp]
        public void FixOdds() => BattleOdds.Fix(critical: false, lands: true);

        [TearDown]
        public void FreeOdds() => BattleOdds.Unfix();

        private static BattleState Battle()
        {
            var battle = new BattleState(BattleDice.Seeded(1));
            for (int i = 0; i < 2; i++)
                battle.Allies.Add(
                    new BattleFighter
                    {
                        Name = "ally" + i,
                        Hp = 300,
                        MaxHp = 300,
                        Level = 10,
                        Stats = new CharacterStats(300, 100, 50, 100, 50, 100, 0, 0),
                    }
                );
            for (int i = 0; i < 2; i++)
                battle.Enemies.Add(
                    new BattleFighter
                    {
                        Name = "enemy" + i,
                        Hp = 1000,
                        MaxHp = 1000,
                        Level = 10,
                        PhysicalAttack = 100,
                        PhysicalDefense = 50,
                        MagicDefense = 50,
                    }
                );
            return battle;
        }

        private static CardSkill Card(CardElement element, params CardAction[] actions) =>
            new()
            {
                Id = "Test" + element,
                Name = "試し",
                Element = element,
                Kind = CardKind.Attack,
                Target = CardTarget.OneEnemy,
                Cost = 1,
                Actions = actions,
            };

        private static BattleEffect Passive(CombatPassiveKind kind) =>
            CombatPassives.Find(kind).Effect;

        [Test]
        public void AThunderCardTurnsIntoBlockAndTheBlockStrikesBack()
        {
            var battle = Battle();
            battle.Give(
                0,
                Passive(CombatPassiveKind.ManaShield),
                Passive(CombatPassiveKind.ChargedShield)
            );
            var card = Card(
                CardElement.Thunder,
                CardAction.Damage(CardTarget.OneEnemy, CardStat.MagicAttack, 100)
            );

            battle.PlayCard(card, 0, 0);

            int block = battle.Allies[0].Block;
            Assert.That(block, Is.GreaterThan(0));
            Assert.That(battle.Announced, Does.Contain($"魔力の盾 ブロック +{block}"));
            Assert.That(battle.Announced, Does.Contain("帯電の盾"));
            int dealt = 2000 - battle.Enemies[0].Hp - battle.Enemies[1].Hp;
            Assert.That(
                dealt,
                Is.GreaterThan(block * 100 / CombatPassiveRules.ManaShieldPercent),
                "The card and the bolt."
            );
            Assert.That(battle.Energy, Is.EqualTo(2));
        }

        [Test]
        public void AProtectedAllySoftensTheBlowAndCutsBack()
        {
            var battle = Battle();
            battle.Give(1, Passive(CombatPassiveKind.Riposte));
            battle.EnemyStrike(0, 1, 100);
            int plain = 300 - battle.Allies[1].Hp;
            Assert.That(battle.Enemies[0].Hp, Is.LessThan(1000), "切り返し");

            battle.Allies[1].Hp = 300;
            battle.AddStatus(
                BattleUnit.Ally(0),
                BattleUnit.Ally(1),
                CardAction.Apply(CardTarget.OneAlly, CardStatus.Protect, 2)
            );
            battle.EnemyStrike(0, 1, 100);
            Assert.That(300 - battle.Allies[1].Hp, Is.LessThan(plain), "プロテクト");

            battle.AddStatus(
                BattleUnit.Ally(0),
                BattleUnit.Ally(1),
                CardAction.Apply(CardTarget.OneAlly, CardStatus.Invincible, 1)
            );
            int before = battle.Allies[1].Hp;
            battle.EnemyStrike(0, 1, 100);
            Assert.That(battle.Allies[1].Hp, Is.EqualTo(before), "無敵");
        }

        [Test]
        public void TheTurnsCarryEnergyAndTheStatusesTickAndRunOut()
        {
            var battle = Battle();
            battle.Give(0, Passive(CombatPassiveKind.StarChart));
            battle.AddStatus(
                BattleUnit.Ally(0),
                BattleUnit.Enemy(0),
                CardAction.Apply(CardTarget.OneEnemy, CardStatus.Burn, 1)
            );

            battle.EndPartyTurn();
            battle.StartPartyTurn();

            Assert.That(battle.Energy, Is.EqualTo(4), "3 and the 1 carried over.");
            Assert.That(
                battle.Enemies[0].Hp,
                Is.EqualTo(1000 - BattleStatusRules.TickPower(CardStatus.Burn, 100))
            );
            Assert.That(
                battle.HasStatus(BattleUnit.Enemy(0), CardStatus.Burn),
                Is.False,
                "Run out."
            );
            Assert.That(battle.Turn, Is.EqualTo(2));
        }

        [Test]
        public void AttackUpAndVulnerableGrowACardsBlowAsOnTheScreen()
        {
            var plain = Battle();
            var card = Card(
                CardElement.Slash,
                CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 200)
            );
            plain.PlayCard(card, 0, 0);
            int alone = 1000 - plain.Enemies[0].Hp;

            var boosted = Battle();
            boosted.AddStatus(
                BattleUnit.Ally(0),
                BattleUnit.Ally(0),
                CardAction.Apply(CardTarget.Self, CardStatus.AttackUp, 2, 30)
            );
            boosted.AddStatus(
                BattleUnit.Ally(0),
                BattleUnit.Enemy(0),
                CardAction.Apply(CardTarget.OneEnemy, CardStatus.Vulnerable, 2)
            );
            boosted.PlayCard(card, 0, 0);

            Assert.That(1000 - boosted.Enemies[0].Hp, Is.GreaterThan(alone * 3 / 2));
        }

        // 連撃の1発目で倒れた敵には、同じカードの状態（出血・毒・防御ダウン）を付けない（2026-11-05）。
        [Test]
        public void AnEnemyBeatenByTheFirstBlowTakesNoStatusFromTheRestOfTheCard()
        {
            var card = CardSkills.Find("SerpentFang");
            var standing = Battle();
            standing.PlayCard(card, 0, 0);
            Assert.That(standing.Enemies[0].Hp, Is.GreaterThan(0));
            Assert.That(
                standing.Enemies[0].Statuses,
                Has.Count.EqualTo(3),
                "Standing, it takes all three."
            );

            var beaten = Battle();
            beaten.Enemies[0].Hp = 1;
            beaten.PlayCard(card, 0, 0);
            Assert.That(beaten.Enemies[0].Hp, Is.Zero);
            Assert.That(beaten.Enemies[0].Statuses, Is.Empty);
            Assert.That(
                beaten.Enemies[1].Hp,
                Is.EqualTo(1000),
                "The other enemy is not hit instead."
            );
        }

        private static EnemyMove MoveNamed(string name)
        {
            foreach (var behavior in CombatEnemyBehaviors.All)
                if (behavior.Move(name) is { Supported: true } move)
                    return move;
            return null;
        }

        [Test]
        public void AGlareParalysesAnAllyWhoseCardsHitLessForTwoTurns()
        {
            var battle = Battle();
            battle.Allies.RemoveAt(1);
            var card = Card(
                CardElement.Slash,
                CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 200)
            );
            var glare = MoveNamed("にらみつけ");
            Assert.That(glare, Is.Not.Null, "にらみつけ can be used now.");

            battle.PlayCard(card, 0, 0);
            int plain = 1000 - battle.Enemies[0].Hp;
            battle.UseEnemyMove(0, glare);
            Assert.That(battle.HasStatus(BattleUnit.Ally(0), CardStatus.Paralysis), Is.True);

            battle.Enemies[0].Hp = 1000;
            battle.PlayCard(card, 0, 0);
            Assert.That(1000 - battle.Enemies[0].Hp, Is.LessThan(plain));

            battle.EndPartyTurn();
            Assert.That(
                battle.HasStatus(BattleUnit.Ally(0), CardStatus.Paralysis),
                Is.True,
                "One turn left."
            );
            battle.StartEnemyTurn();
            battle.StartPartyTurn();
            battle.EndPartyTurn();
            Assert.That(battle.HasStatus(BattleUnit.Ally(0), CardStatus.Paralysis), Is.False);
        }

        [Test]
        public void SporesPoisonThePartyWhoseVowStopsItOnceATurn()
        {
            var battle = Battle();
            battle.Give(1, Passive(CombatPassiveKind.PurifyingVow));
            var spores = MoveNamed("胞子ばらまき");

            battle.UseEnemyMove(0, spores);
            Assert.That(
                battle.StatusAmount(BattleUnit.Ally(0), CardStatus.Poison),
                Is.EqualTo(0),
                "浄めの誓い"
            );
            Assert.That(battle.StatusAmount(BattleUnit.Ally(1), CardStatus.Poison), Is.EqualTo(2));
            battle.UseEnemyMove(0, spores);
            Assert.That(
                battle.StatusAmount(BattleUnit.Ally(0), CardStatus.Poison),
                Is.EqualTo(2),
                "Once a turn."
            );

            battle.UseEnemyMove(0, MoveNamed("胞子の芽吹き"));
            Assert.That(
                battle.StatusAmount(BattleUnit.Ally(1), CardStatus.Poison),
                Is.EqualTo(8),
                "4 doubled."
            );

            int power = battle.StatusPower(BattleUnit.Ally(1), CardStatus.Poison);
            battle.StartEnemyTurn();
            Assert.That(battle.Allies[1].Hp, Is.EqualTo(300 - power * 8));
            Assert.That(battle.StatusAmount(BattleUnit.Ally(1), CardStatus.Poison), Is.EqualTo(7));

            int before = battle.Allies[1].Hp;
            battle.UseEnemyMove(0, MoveNamed("毒まわし"));
            Assert.That(
                battle.Allies[1].Hp,
                Is.EqualTo(System.Math.Max(0, before - power * 7 * 8 / 2))
            );
            Assert.That(battle.HasStatus(BattleUnit.Ally(1), CardStatus.Poison), Is.False);
        }

        [Test]
        public void ASweetScentMakesThePartyTakeMore()
        {
            var battle = Battle();
            battle.EnemyStrike(0, 0, 100);
            int plain = 300 - battle.Allies[0].Hp;
            battle.Allies[0].Hp = 300;

            battle.UseEnemyMove(0, MoveNamed("甘い蜜の香り"));
            battle.EnemyStrike(0, 0, 100);

            Assert.That(
                300 - battle.Allies[0].Hp,
                Is.EqualTo((int)System.Math.Round(plain * 1.25f))
            );
        }

        [Test]
        public void AWholeBattleWithoutAScreenGoesToItsEnd()
        {
            var state = new BattleState(BattleDice.Seeded(3));
            var battle = new BattleSimulation(state);
            for (int i = 0; i < 4; i++)
            {
                state.Allies.Add(
                    new BattleFighter
                    {
                        Hp = 400,
                        MaxHp = 400,
                        Level = 10,
                        Stats = new CharacterStats(400, 200, 120, 200, 120, 150, 30, 20),
                    }
                );
                battle.AddCard(
                    Card(
                        CardElement.Slash,
                        CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 150)
                    ),
                    i
                );
                battle.AddCard(
                    Card(
                        CardElement.Fire,
                        CardAction.Damage(CardTarget.OneEnemy, CardStat.MagicAttack, 150)
                    ),
                    i
                );
            }
            state.Enemies.Add(BattleFighter.OfEnemy("moss-slime", 10, 1f, 1f, state.Dice.Below));
            state.Enemies.Add(BattleFighter.OfEnemy("moss-wolf", 10, 1f, 1f, state.Dice.Below));

            var result = battle.Fight(new CarefulSimPolicy(), 40);

            Assert.That(result.Victory || result.Timeout || result.Turns > 0, Is.True);
            Assert.That(result.Victory, Is.True, "Four strong allies beat a slime and a wolf.");
            Assert.That(result.Cards, Is.GreaterThan(0));
            Assert.That(result.Dealt, Is.EqualTo(state.Enemies[0].MaxHp + state.Enemies[1].MaxHp));
            Assert.That(result.DealtByTurn, Has.Count.EqualTo(result.Turns));
        }

        [Test]
        public void TheEnemiesHoldTheirTraitsThroughAWholeBattle()
        {
            var state = new BattleState(BattleDice.Seeded(3));
            var battle = new BattleSimulation(state);
            state.Allies.Add(
                new BattleFighter
                {
                    Hp = 400,
                    MaxHp = 400,
                    Level = 10,
                    Stats = new CharacterStats(400, 200, 120, 200, 120, 150, 30, 20),
                }
            );
            battle.AddCard(
                Card(
                    CardElement.Slash,
                    CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 150)
                ),
                0
            );
            var slime = BattleFighter.OfEnemy("moss-slime", 10, 1f, 1f, state.Dice.Below);
            var kind = slime.Brain.Behavior;
            slime.Brain = new EnemyBrain(
                new EnemyBehavior
                {
                    Id = kind.Id,
                    Moves = kind.Moves,
                    Early = kind.Early,
                    Effects = new[]
                    {
                        new BattleEffect("test-trait", "特性").On(
                            BattleTrigger.BattleStart,
                            BattleAct.Show
                        ),
                    },
                },
                state.Dice.Below
            );
            state.Enemies.Add(slime);

            battle.Fight(new CarefulSimPolicy(), 40);

            Assert.That(
                state.Announced,
                Does.Contain("特性"),
                "The slime held its trait from the start."
            );
        }

        [Test]
        public void AnEnemySlippingThroughTakesNoPhysicalBlowUntilItActs()
        {
            var battle = Battle();
            var slash = Card(
                CardElement.Slash,
                CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 100)
            );
            var fire = Card(
                CardElement.Fire,
                CardAction.Damage(CardTarget.OneEnemy, CardStat.MagicAttack, 100)
            );
            battle.UseEnemyMove(0, MoveNamed("すり抜け"));

            battle.PlayCard(slash, 0, 0);
            Assert.That(battle.Enemies[0].Hp, Is.EqualTo(1000), "Slipped through.");
            battle.PlayCard(fire, 0, 0);
            Assert.That(battle.Enemies[0].Hp, Is.LessThan(1000), "A spell lands.");

            int before = battle.Enemies[0].Hp;
            battle.Effects.OwnerActs(BattleUnit.Enemy(0));
            battle.PlayCard(slash, 0, 0);
            Assert.That(battle.Enemies[0].Hp, Is.LessThan(before), "Gone once it acted.");
        }

        [Test]
        public void ACurseHalvesTheBlockAndAReflectingEnemySendsTheCardBack()
        {
            var battle = Battle();
            var guard = new CardSkill
            {
                Id = "TestGuard",
                Name = "守り",
                Kind = CardKind.Guard,
                Target = CardTarget.Self,
                Cost = 1,
                Actions = new[]
                {
                    CardAction.Block(CardTarget.Self, CardStat.PhysicalDefense, 100),
                },
            };
            battle.PlayCard(guard, 0, 0);
            int plain = battle.Allies[0].Block;
            battle.Allies[0].Block = 0;
            battle.ApplyStatus(BattleUnit.Enemy(0), BattleUnit.Ally(0), CardStatus.Curse, 2, 0, 0);
            battle.PlayCard(guard, 0, 0);
            Assert.That(battle.Allies[0].Block, Is.EqualTo((int)System.Math.Round(plain * 0.5f)));

            battle.UseEnemyMove(1, MoveNamed("水晶角の照り返し"));
            var slash = Card(
                CardElement.Slash,
                CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 100)
            );
            int allyHp = battle.Allies[1].Hp;
            battle.PlayCard(slash, 1, 1);
            Assert.That(battle.Enemies[1].Hp, Is.EqualTo(1000), "Sent back.");
            Assert.That(battle.Allies[1].Hp, Is.LessThan(allyHp));
            Assert.That(battle.HasStatus(BattleUnit.Enemy(1), CardStatus.Reflect), Is.False);
        }

        [Test]
        public void ASwallowEatsTheWeakestOtherEnemyToHeal()
        {
            var battle = Battle();
            battle.Enemies[0].Hp = 500;
            battle.Enemies[1].Hp = 120;
            battle.UseEnemyMove(0, MoveNamed("丸のみ"));
            Assert.That(battle.Enemies[1].Hp, Is.EqualTo(0));
            Assert.That(
                battle.Enemies[0].Hp,
                Is.EqualTo(500 + System.Math.Min(120, 3 * battle.Enemies[0].MagicAttack))
            );
        }
    }
}
