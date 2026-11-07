using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using NUnit.Framework;
using static Baryonyx.Combat.BattleCondition;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattleEffectsTests
    {
        private static readonly BattleUnit Toma = BattleUnit.Ally(0);
        private static readonly BattleUnit Luka = BattleUnit.Ally(1);
        private static readonly BattleUnit Slime = BattleUnit.Enemy(0);

        [Test]
        public void AValueAddsTheFixedAmountsThenGrowsOnceByThePercentsSummed()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(Toma, new BattleEffect("a", "A").Add(BattleValue.Damage, 10));
            effects.Add(Toma, new BattleEffect("b", "B").AddPercent(BattleValue.Damage, 30));
            effects.Add(Luka, new BattleEffect("c", "C").AddPercent(BattleValue.Damage, 20));

            int damage = effects.Modify(world, BattleValue.Damage, 90, new BattleEvent());

            Assert.That(damage, Is.EqualTo(150), "(90 + 10) grown by 30% + 20%.");
        }

        [Test]
        public void EachValueStaysWithinItsBounds()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(
                Toma,
                new BattleEffect("a", "A")
                    .Add(BattleValue.Cost, -5)
                    .Add(BattleValue.Hits, -5)
                    .Add(BattleValue.TwiceChance, 500)
            );
            var moment = new BattleEvent();

            Assert.That(effects.Modify(world, BattleValue.Cost, 2, moment), Is.EqualTo(0));
            Assert.That(effects.Modify(world, BattleValue.Hits, 2, moment), Is.EqualTo(1));
            Assert.That(
                effects.Modify(world, BattleValue.TwiceChance, 10, moment),
                Is.EqualTo(100)
            );
        }

        [Test]
        public void ConditionsJoinAndTheRuleWorksOnlyWhileTheyHold()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(
                Toma,
                new BattleEffect("thermal", "寒暖")
                    .AddPercent(BattleValue.Damage, 50)
                    .When(TargetHas(CardStatus.Burn) & TargetHas(CardStatus.Chill))
                    .AddPercent(BattleValue.Damage, 100)
                    .When(!TargetIsEnemy | TargetHpAtMost(10))
            );
            var blow = new BattleEvent { Actor = Toma, Target = Slime };

            Assert.That(effects.Modify(world, BattleValue.Damage, 100, blow), Is.EqualTo(100));
            world.Statuses.Add((Slime, CardStatus.Burn));
            Assert.That(effects.Modify(world, BattleValue.Damage, 100, blow), Is.EqualTo(100));
            world.Statuses.Add((Slime, CardStatus.Chill));
            Assert.That(effects.Modify(world, BattleValue.Damage, 100, blow), Is.EqualTo(150));
            world.SetHp(Slime, 5);
            Assert.That(effects.Modify(world, BattleValue.Damage, 100, blow), Is.EqualTo(250));
        }

        [Test]
        public void AnEffectWorksOnlyWhileItsHolderStands()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(Toma, new BattleEffect("a", "A").Add(BattleValue.OpeningDraw, 1));

            Assert.That(
                effects.Modify(world, BattleValue.OpeningDraw, 5, new BattleEvent()),
                Is.EqualTo(6)
            );
            world.SetHp(Toma, 0);
            Assert.That(
                effects.Modify(world, BattleValue.OpeningDraw, 5, new BattleEvent()),
                Is.EqualTo(5)
            );
        }

        [Test]
        public void ARuleOnceATurnWorksAgainOnlyAsTheNextTurnBegins()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(
                Toma,
                new BattleEffect("kill", "影の一刺し")
                    .On(BattleTrigger.EnemyDefeated, BattleAct.GainEnergy(1))
                    .OncePerTurn()
                    .On(BattleTrigger.EnemyDefeated, BattleAct.GainEnergy(10))
                    .OncePerBattle()
            );

            for (int i = 0; i < 3; i++)
                effects.Fire(world, BattleTrigger.EnemyDefeated, new BattleEvent());
            Assert.That(world.EnergyGained, Is.EqualTo(11));
            effects.StartTurn();
            effects.Fire(world, BattleTrigger.EnemyDefeated, new BattleEvent());
            Assert.That(world.EnergyGained, Is.EqualTo(12), "Once a battle stays used up.");
            effects.StartBattle();
            effects.Fire(world, BattleTrigger.EnemyDefeated, new BattleEvent());
            Assert.That(world.EnergyGained, Is.EqualTo(23));
        }

        [Test]
        public void StacksGrowUpToTheirMostAndStartOverWithTheBattle()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            var copycat = effects.Add(
                Toma,
                new BattleEffect("copycat", "見よう見まね")
                    .Stacks(3)
                    .On(BattleTrigger.CardPlaying, BattleAct.AddStacks(1, n => $"+{n * 10}%"))
                    .When(ActorIsOtherAlly & StacksBelowMax)
                    .AddPercent(BattleValue.Damage, BattleAmount.PerStack(10))
                    .When(ActorIsOwner)
            );

            for (int i = 0; i < 5; i++)
                effects.Fire(world, BattleTrigger.CardPlaying, new BattleEvent { Actor = Luka });
            effects.Fire(world, BattleTrigger.CardPlaying, new BattleEvent { Actor = Toma });

            Assert.That(copycat.Stacks, Is.EqualTo(3));
            Assert.That(copycat.Full, Is.True);
            Assert.That(
                world.Shown,
                Is.EqualTo(new[] { "見よう見まね +10%", "見よう見まね +20%", "見よう見まね +30%" })
            );
            Assert.That(
                effects.Modify(world, BattleValue.Damage, 100, new BattleEvent { Actor = Toma }),
                Is.EqualTo(130)
            );
            effects.StartBattle();
            Assert.That(copycat.Stacks, Is.EqualTo(0));
        }

        [Test]
        public void AForecastLeavesOutChanceAndUsesNothingUp()
        {
            var effects = new BattleEffects();
            var holder = effects.Add(
                Toma,
                new BattleEffect("a", "A")
                    .Stacks(5)
                    .On(BattleTrigger.CardPlaying, BattleAct.AddStacks(1))
                    .On(BattleTrigger.DealtDamage, BattleAct.GainBlock(BattleWho.Owner, 5))
                    .OncePerTurn()
                    .On(BattleTrigger.DealtDamage, BattleAct.GainBlock(BattleWho.Owner, 100))
                    .AtRandom()
            );
            var forecast = new FakeWorld { Preview = true };

            effects.Fire(forecast, BattleTrigger.CardPlaying, new BattleEvent());
            effects.Fire(forecast, BattleTrigger.DealtDamage, new BattleEvent());
            effects.Fire(forecast, BattleTrigger.DealtDamage, new BattleEvent());

            Assert.That(holder.Stacks, Is.EqualTo(0));
            Assert.That(
                forecast.BlockOf(Toma),
                Is.EqualTo(10),
                "Once a turn, but not used up; the random rule left out."
            );
            Assert.That(forecast.Shown, Is.Empty);

            var battle = new FakeWorld();
            effects.Fire(battle, BattleTrigger.DealtDamage, new BattleEvent());
            effects.Fire(battle, BattleTrigger.DealtDamage, new BattleEvent());
            Assert.That(battle.BlockOf(Toma), Is.EqualTo(205));
        }

        [Test]
        public void ACancelOrATakeInsteadStopsTheOthersFromBeingAsked()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(
                Toma,
                new BattleEffect("vow", "浄めの誓い")
                    .On(BattleTrigger.StatusApplying, BattleAct.Cancel)
                    .When(TargetIsAlly & StatusIsBad)
                    .OncePerTurn()
            );
            effects.Add(
                Luka,
                new BattleEffect("vow2", "二つ目")
                    .On(BattleTrigger.StatusApplying, BattleAct.Cancel)
                    .When(TargetIsAlly & StatusIsBad)
                    .OncePerTurn()
                    .On(BattleTrigger.EnemyTargeting, BattleAct.TakeInstead)
                    .When(!TargetIsOwner & TargetHpAtMost(30))
            );

            var first = effects.Fire(
                world,
                BattleTrigger.StatusApplying,
                new BattleEvent { Target = Toma, Status = CardStatus.Burn }
            );
            var good = effects.Fire(
                world,
                BattleTrigger.StatusApplying,
                new BattleEvent { Target = Toma, Status = CardStatus.AttackUp }
            );
            Assert.That(first.Cancelled, Is.True);
            Assert.That(good.Cancelled, Is.False);
            Assert.That(
                world.Shown,
                Is.EqualTo(new[] { "浄めの誓い" }),
                "Only the first holder works."
            );

            world.SetHp(Toma, 20);
            var aimed = effects.Fire(
                world,
                BattleTrigger.EnemyTargeting,
                new BattleEvent { Actor = Slime, Target = Toma }
            );
            Assert.That(aimed.Redirect, Is.EqualTo(Luka));
        }

        [Test]
        public void AChainOfHappeningsStopsPastItsDepth()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            world.Effects = effects;
            string told = null;
            effects.TooDeep += message => told = message;
            // ブロックを得るたびにブロックを得る：止めなければ終わらない。
            effects.Add(
                Toma,
                new BattleEffect("loop", "ループ")
                    .On(BattleTrigger.GainedBlock, BattleAct.GainBlock(BattleWho.Owner, 1))
                    .When(TargetIsOwner)
            );

            ((IBattleWorld)world).AddBlock(Toma, 1);

            Assert.That(world.BlockOf(Toma), Is.EqualTo(1 + BattleEffects.MaxDepth));
            Assert.That(told, Does.Contain("GainedBlock"));
        }

        [Test]
        public void ACardsOwnEffectWorksOnlyInItsPlay()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            var card = new CardSkill { Id = "Test", Element = CardElement.Fire };
            var play = new BattleCardPlay(card, Toma);
            effects.Add(
                Toma,
                new BattleEffect("own", "自分").AddPercent(BattleValue.Damage, 50),
                play
            );

            var inPlay = new BattleEvent { Actor = Toma, Play = play };
            var outside = new BattleEvent { Actor = Toma };
            var another = new BattleEvent { Actor = Toma, Play = new BattleCardPlay(card, Toma) };

            Assert.That(effects.Modify(world, BattleValue.Damage, 100, inPlay), Is.EqualTo(150));
            Assert.That(effects.Modify(world, BattleValue.Damage, 100, outside), Is.EqualTo(100));
            Assert.That(effects.Modify(world, BattleValue.Damage, 100, another), Is.EqualTo(100));
        }

        [Test]
        public void ACardsPlayBonusJoinsOnlyItsCardDamage()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(Toma, new BattleEffect("a", "A").AddPercent(BattleValue.Damage, 10));
            var play = new BattleCardPlay(new CardSkill(), Toma) { BonusPercent = 40 };

            var card = new BattleEvent
            {
                Actor = Toma,
                Kind = BattleDamageKind.Card,
                Play = play,
            };
            var counter = new BattleEvent
            {
                Actor = Toma,
                Kind = BattleDamageKind.Effect,
                Play = play,
            };

            Assert.That(effects.Modify(world, BattleValue.Damage, 100, card), Is.EqualTo(150));
            Assert.That(effects.Modify(world, BattleValue.Damage, 100, counter), Is.EqualTo(110));
        }

        [Test]
        public void EveryPassiveHasItsEffectWithItsIdAndName()
        {
            foreach (var passive in CombatPassives.All)
            {
                Assert.That(passive.Effect, Is.Not.Null, passive.Id);
                Assert.That(passive.Effect.Id, Is.EqualTo(passive.Id));
                Assert.That(passive.Effect.Name, Is.EqualTo(passive.Name));
                Assert.That(passive.Effect.Rules, Is.Not.Empty, passive.Id);
            }
        }

        [Test]
        public void ThePassivesWorkAsTheirTextsSay()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            world.Effects = effects;
            Hold(effects, Toma, CombatPassiveKind.StarChart);
            Hold(effects, Toma, CombatPassiveKind.ThermalShock);
            Hold(effects, Luka, CombatPassiveKind.ChainedArrows);
            Hold(effects, Luka, CombatPassiveKind.ManaShield);

            // 星図の計算：残りのエネルギーを1まで。
            world.Energy = 3;
            effects.Fire(world, BattleTrigger.PartyTurnEnd, new BattleEvent());
            Assert.That(world.Carried, Is.EqualTo(1));

            // 寒暖の理：やけどと凍えの両方で、味方の攻撃に+30%。継続ダメージには掛けない。
            world.Statuses.Add((Slime, CardStatus.Burn));
            world.Statuses.Add((Slime, CardStatus.Chill));
            var blow = new BattleEvent
            {
                Actor = Luka,
                Target = Slime,
                Kind = BattleDamageKind.Card,
            };
            var lasting = new BattleEvent
            {
                Actor = Luka,
                Target = Slime,
                Kind = BattleDamageKind.Status,
            };
            Assert.That(effects.Modify(world, BattleValue.Damage, 100, blow), Is.EqualTo(130));
            Assert.That(effects.Modify(world, BattleValue.Damage, 100, lasting), Is.EqualTo(100));

            // 連ね矢：持ち主の×回数の攻撃だけ、回数+1。
            var twice = CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 50, 2);
            var once = CardAction.Damage(CardTarget.OneEnemy, CardStat.PhysicalAttack, 50);
            Assert.That(Hits(effects, world, Luka, twice), Is.EqualTo(3));
            Assert.That(Hits(effects, world, Luka, once), Is.EqualTo(1));
            Assert.That(Hits(effects, world, Toma, twice), Is.EqualTo(2));

            // 魔力の盾：雷のカードで与えたダメージの20%（少なくとも1）をブロックに。
            var thunder = new CardSkill { Element = CardElement.Thunder };
            var played = BattleEvent.OfCard(new BattleCardPlay(thunder, Luka));
            played.Amount = 3;
            effects.Fire(world, BattleTrigger.CardPlayed, played);
            Assert.That(world.BlockOf(Luka), Is.EqualTo(1));
            Assert.That(world.Shown, Does.Contain("魔力の盾 ブロック +1"));
        }

        [Test]
        public void AnItemsEffectInBattleGrowsWithItsMultiplier()
        {
            var item = new EquipmentDefinition(
                "test-ring",
                "試しの指輪",
                EquipmentSlot.Accessory,
                1,
                1.5f,
                new[] { new EquipmentEffect(0, 10) }
            ).WithBattle(
                "毒の敵へのダメージ+20%",
                (effect, multiplier) =>
                    effect
                        .AddPercent(BattleValue.Damage, Inventory.Quality.Amount(20, multiplier))
                        .When(ActorIsOwner & TargetHas(CardStatus.Poison))
            );

            var effects = EquipmentStats.BattleEffects(new[] { (item, 1.5f) });

            Assert.That(effects.Count, Is.EqualTo(1));
            Assert.That(effects[0].Id, Is.EqualTo("test-ring"));
            Assert.That(effects[0].Name, Is.EqualTo("試しの指輪"));
            Assert.That(item.DetailAt(1.5f), Does.EndWith("｜毒の敵へのダメージ+30%"));
            var world = new FakeWorld();
            var held = new BattleEffects();
            held.Add(Toma, effects[0]);
            world.Statuses.Add((Slime, CardStatus.Poison));
            var blow = new BattleEvent { Actor = Toma, Target = Slime };
            Assert.That(held.Modify(world, BattleValue.Damage, 100, blow), Is.EqualTo(130));
        }

        [Test]
        public void AttackUpAndVulnerableMultiplyACardsDamageEachOnItsOwnInOrder()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(Toma, new BattleEffect("a", "A").AddPercent(BattleValue.Damage, 10));
            world.Statuses.Add((Toma, CardStatus.AttackUp));
            world.Statuses.Add((Slime, CardStatus.Vulnerable));
            var card = new BattleEvent
            {
                Actor = Toma,
                Target = Slime,
                Kind = BattleDamageKind.Card,
            };
            var counter = new BattleEvent
            {
                Actor = Toma,
                Target = Slime,
                Kind = BattleDamageKind.Effect,
            };

            // 101 → +10% = 111 → ×1.3 = 144.3 → 144 → ×1.25 = 180
            Assert.That(effects.Modify(world, BattleValue.Damage, 101, card), Is.EqualTo(180));
            Assert.That(effects.Modify(world, BattleValue.Damage, 101, counter), Is.EqualTo(111));
        }

        [Test]
        public void ProtectCutsWhatItsHolderTakesAndInvincibleStopsTheBlowBeforeAReflection()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            world.Effects = effects;
            world.Statuses.Add((Slime, CardStatus.Protect));
            world.Statuses.Add((Toma, CardStatus.Protect));
            Assert.That(
                effects.Modify(
                    world,
                    BattleValue.DamageTaken,
                    100,
                    new BattleEvent { Target = Slime }
                ),
                Is.EqualTo(75)
            );
            Assert.That(
                effects.Modify(world, BattleValue.Incoming, 100, new BattleEvent { Target = Toma }),
                Is.EqualTo(75)
            );

            world.Statuses.Add((Luka, CardStatus.Reflect));
            world.Statuses.Add((Luka, CardStatus.Invincible));
            var blow = effects.Fire(
                world,
                BattleTrigger.BlowLanding,
                new BattleEvent
                {
                    Actor = Slime,
                    Target = Luka,
                    Amount = 40,
                }
            );
            Assert.That(blow.Cancelled, Is.True);
            Assert.That(world.Shown, Is.EqualTo(new[] { "無敵" }));
            Assert.That(world.Statuses, Has.Member((Luka, CardStatus.Reflect)), "Not used up.");

            world.Statuses.Remove((Luka, CardStatus.Invincible));
            effects.Fire(
                world,
                BattleTrigger.BlowLanding,
                new BattleEvent
                {
                    Actor = Slime,
                    Target = Luka,
                    Amount = 40,
                }
            );
            Assert.That(world.Hp(Slime), Is.EqualTo(60), "The blow goes back to the enemy.");
            Assert.That(world.Statuses, Has.No.Member((Luka, CardStatus.Reflect)));
        }

        [Test]
        public void ABoostAddsToItsStatsWhileItsConditionHoldsAndShowsWhenItGrows()
        {
            var world = new FakeWorld();
            var effects = new BattleEffects();
            effects.Add(
                Toma,
                BattleStatBoosts.EffectOf(
                    new BattleStatBoost
                    {
                        Name = "弔い",
                        Condition = BattleBoostCondition.PerFallenAlly,
                        Stats = BattleBoostStats.Attacks,
                        Percent = 10,
                    }
                )
            );
            int Attack() =>
                effects.Modify(
                    world,
                    BattleValue.StatPercent,
                    0,
                    new BattleEvent
                    {
                        Actor = Toma,
                        Target = Toma,
                        StatIndex = BattleStatBoosts.PhysicalAttack,
                    }
                );

            var boost = effects.All[0];
            Assert.That(boost.Effect.LooksLike, Is.EqualTo(CardStatus.AttackUp));
            Assert.That(Attack(), Is.EqualTo(0));
            world.SetHp(Luka, 0);
            Assert.That(Attack(), Is.EqualTo(10));
            Assert.That(boost.Adding, Is.False, "Not looked at yet.");
            effects.ShowGrown(world, BattleValue.StatPercent, BattleStatBoosts.PhysicalAttack);
            effects.ShowGrown(world, BattleValue.StatPercent, BattleStatBoosts.PhysicalAttack);
            Assert.That(world.Shown, Is.EqualTo(new[] { "弔い" }), "Once, as it starts.");
            Assert.That(boost.Adding, Is.True, "Its icon shows while it works.");
            Assert.That(
                effects.Modify(
                    world,
                    BattleValue.StatPercent,
                    0,
                    new BattleEvent
                    {
                        Actor = Toma,
                        Target = Toma,
                        StatIndex = BattleStatBoosts.Speed,
                    }
                ),
                Is.EqualTo(0)
            );
        }

        // 名前の代わりに画面に出すもの：止める効果は「効かなかった」、速度の強化は速度アップ。
        [Test]
        public void TheEffectsThatStopWhatCameShowAsResistedAndASpeedBoostAsSpeedUp()
        {
            Assert.That(BattleStatuses.EffectOf(CardStatus.Invincible).Resisting, Is.True);
            Assert.That(BattleStatuses.EffectOf(CardStatus.Reflect).Resisting, Is.False);
            Assert.That(CombatPassives.Find(CombatPassiveKind.PurifyingVow).Effect.Resisting, Is.True);
            Assert.That(CombatPassives.Find(CombatPassiveKind.ChargedShield).Effect.Resisting, Is.False);
            var vanguard = BattleStatBoosts.EffectOf(
                new BattleStatBoost
                {
                    Name = "先手",
                    Condition = BattleBoostCondition.FirstTurns,
                    Stats = BattleBoostStats.Speed,
                    Percent = 25,
                    Turns = 2,
                }
            );
            Assert.That(vanguard.LooksLike, Is.EqualTo(CardStatus.SpeedUp));
            Assert.That(vanguard.Resisting, Is.False);
        }

        private static void Hold(BattleEffects effects, BattleUnit unit, CombatPassiveKind kind) =>
            effects.Add(unit, CombatPassives.Find(kind).Effect);

        private static int Hits(
            BattleEffects effects,
            IBattleWorld world,
            BattleUnit user,
            CardAction action
        ) =>
            effects.Modify(
                world,
                BattleValue.Hits,
                action.Hits,
                new BattleEvent { Actor = user, Action = action }
            );

        /// <summary>Two allies and one enemy, each with 100 HP and 100 of every stat.</summary>
        private sealed class FakeWorld : IBattleWorld
        {
            private readonly Dictionary<BattleUnit, int> hp = new();
            private readonly Dictionary<BattleUnit, int> block = new();

            public readonly HashSet<(BattleUnit, CardStatus)> Statuses = new();
            public readonly List<string> Shown = new();
            public BattleEffects Effects;
            public int EnergyGained;
            public int Carried;

            public bool Preview { get; set; }
            public int Turn { get; set; } = 1;
            public int TodayAct { get; set; } = 6000;
            public int Energy { get; set; }

            public void SetHp(BattleUnit unit, int value) => hp[unit] = value;

            public int BlockOf(BattleUnit unit) =>
                block.TryGetValue(unit, out int value) ? value : 0;

            public int Count(bool allies) => allies ? 2 : 1;

            public bool Stands(BattleUnit unit) => !unit.IsNone && Hp(unit) > 0;

            public int Hp(BattleUnit unit) => hp.TryGetValue(unit, out int value) ? value : 100;

            public int MaxHp(BattleUnit unit) => 100;

            public int Block(BattleUnit unit) => BlockOf(unit);

            public bool HasStatus(BattleUnit unit, CardStatus status) =>
                Statuses.Contains((unit, status));

            public int StatusTurns(BattleUnit unit, CardStatus status) =>
                HasStatus(unit, status) ? 1 : 0;

            public int StatusAmount(BattleUnit unit, CardStatus status) =>
                HasStatus(unit, status) ? 30 : 0;

            public void StatusesOf(BattleUnit unit, List<CardStatus> into)
            {
                foreach (var (who, status) in Statuses)
                    if (who == unit)
                        into.Add(status);
            }

            public bool InBattle(BattleUnit unit) => !unit.IsNone;

            public void RemoveStatus(BattleUnit unit, CardStatus status) =>
                Statuses.Remove((unit, status));

            public int StatusPower(BattleUnit unit, CardStatus status) =>
                HasStatus(unit, status) ? 10 : 0;

            public int Stat(BattleUnit unit, CardStat stat) => 100;

            public int Defense(BattleUnit unit, CardStat stat) => 0;

            public int Level(BattleUnit unit) => 10;

            public int Pick(int count) => 0;

            public void Strike(BattleEvent blow, int power, BattleStrikeLook look) =>
                hp[blow.Target] = Hp(blow.Target) - power;

            int IBattleWorld.AddBlock(BattleUnit unit, int amount)
            {
                block[unit] = BlockOf(unit) + amount;
                Effects?.Fire(
                    this,
                    BattleTrigger.GainedBlock,
                    new BattleEvent { Target = unit, Amount = amount }
                );
                return amount;
            }

            public void Heal(BattleUnit healer, BattleUnit target, int amount) =>
                hp[target] = Hp(target) + amount;

            public bool ApplyStatus(
                BattleUnit source,
                BattleUnit target,
                CardStatus status,
                int turns,
                int chance,
                int amount
            ) => Statuses.Add((target, status));

            public int GainEnergy(int amount)
            {
                EnergyGained += amount;
                return amount;
            }

            public int CarryEnergy(int most)
            {
                int more = System.Math.Min(most, Energy - Carried);
                Carried += more;
                return more;
            }

            public int Draw(int count) => count;

            public void Announce(BattleEffectInstance effect, BattleUnit at, string detail) =>
                Shown.Add(detail == null ? effect.Effect.Name : $"{effect.Effect.Name} {detail}");
        }
    }
}
