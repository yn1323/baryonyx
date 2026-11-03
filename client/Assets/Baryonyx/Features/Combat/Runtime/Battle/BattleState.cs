using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// One character of a battle without a screen: its HP, block, level, stats, weaknesses and
    /// statuses. An ally has <see cref="Stats"/>; an enemy has its own attack and defense.
    /// </summary>
    public sealed class BattleFighter
    {
        public string Name = "";
        public int Hp;
        public int MaxHp;
        public int Block;
        public int Level = 1;

        /// <summary>An ally's stats.</summary>
        public CharacterStats Stats;

        /// <summary>An enemy's stats.</summary>
        public int PhysicalAttack;
        public int MagicAttack;
        public int PhysicalDefense;
        public int MagicDefense;
        public int Critical;
        public int Luck;

        public CardElement[] Weaknesses = Array.Empty<CardElement>();
        public readonly List<BattleStatusEntry> Statuses = new();

        /// <summary>False for an empty place (no one stands there, nor falls).</summary>
        public bool Present = true;

        /// <summary>An ally's stats before any boon, and the percents the boons grew them by (<see cref="BattleStatBoosts.Grown"/>).</summary>
        public CharacterStats Unboosted;
        public IReadOnlyList<float> Percents = Array.Empty<float>();

        /// <summary>An ally's chance in percent that its attack card plays once more (the equipment's).</summary>
        public int TwiceChance;

        /// <summary>An enemy's kind, its mock blow (when it has no behaviour) and whether that is a spell.</summary>
        public string Id = "";
        public int Power;
        public bool Magic;
        public int Speed;

        /// <summary>An enemy's room's scale on its blows, and the power it gathered for its next one.</summary>
        public float AttackScale = 1f;
        public int PowerUp;

        /// <summary>An enemy's choices of skills; null for one that only strikes.</summary>
        public EnemyBrain Brain;

        /// <summary>
        /// The enemy of kind <paramref name="id"/> at <paramref name="level"/>, its HP scaled by
        /// <paramref name="hpScale"/> and its blows by <paramref name="powerScale"/>, as the battle
        /// screen sets one up (<see cref="CombatEnemyStats"/>, <see cref="CombatEnemyBehaviors"/>).
        /// Null for an unknown kind.
        /// </summary>
        public static BattleFighter OfEnemy(
            string id,
            int level,
            float hpScale = 1f,
            float powerScale = 1f,
            Func<int, int> random = null
        )
        {
            var stats = CombatEnemyStats.Find(id);
            var definition = CombatEnemies.Find(id);
            if (stats == null || definition == null)
                return null;
            level = Math.Max(1, level);
            var fighter = new BattleFighter
            {
                Id = id,
                Name = definition.Name ?? id,
                Level = level,
                Magic = stats.Magic,
                Power = EnemyStats.At(
                    stats.Magic ? stats.MagicAttack : stats.PhysicalAttack,
                    level
                ),
                PhysicalDefense = EnemyStats.At(stats.PhysicalDefense, level),
                MagicDefense = EnemyStats.At(stats.MagicDefense, level),
                PhysicalAttack = EnemyStats.At(stats.PhysicalAttack, level),
                MagicAttack = EnemyStats.At(stats.MagicAttack, level),
                Speed = EnemyStats.At(stats.Speed, level),
                Critical = EnemyStats.At(stats.Critical, level),
                Luck = EnemyStats.At(stats.Luck, level),
                AttackScale = Math.Max(0.01f, powerScale),
                Weaknesses = new List<CardElement>(definition.Weaknesses).ToArray(),
            };
            fighter.Power = Math.Max(1, (int)Math.Round(fighter.Power * fighter.AttackScale));
            fighter.MaxHp = Math.Max(
                1,
                (int)Math.Round(EnemyStats.At(stats.Hp, level) * Math.Max(0.01f, hpScale))
            );
            fighter.Hp = fighter.MaxHp;
            var behavior = CombatEnemyBehaviors.Find(id);
            if (behavior != null)
                fighter.Brain = new EnemyBrain(behavior, random ?? (n => 0));
            return fighter;
        }
    }

    /// <summary>
    /// A battle without a screen (doc/plans/2026-11-03-battle-effect-system.md, 第7段): the
    /// allies, the enemies, the energy and the effects, changed by the same rules as the battle
    /// screen (<see cref="CardPlays"/>, <see cref="CardHits"/>, <see cref="EnemyBlows"/>,
    /// <see cref="BattleStatusRules"/>, the effects). A test or a tool sets the characters up
    /// and plays cards, strikes with enemies and turns the turns, at once, without the screen's
    /// timing and effects. The hand, the deck and the enemies' choices are left to the caller.
    /// Chance rolls by <see cref="BattleOdds"/> and picks by <see cref="Dice"/>.
    /// </summary>
    public sealed class BattleState : IFightWorld
    {
        /// <summary>A blow on a weakness does this many times its damage (the battle screen's own).</summary>
        public const float WeakMultiplier = 1.5f;

        /// <summary>The most energy the party can hold (the battle screen's own).</summary>
        public const int EnergyCap = 9;

        public BattleState(BattleDice dice = null) => Dice = dice ?? BattleDice.Seeded(1);

        public readonly List<BattleFighter> Allies = new();
        public readonly List<BattleFighter> Enemies = new();
        public BattleEffects Effects { get; } = new();
        public BattleDice Dice { get; set; }

        public int MaxEnergy = 3;
        public int Energy { get; set; } = 3;
        public int Turn { get; set; } = 1;
        public int TodayAct { get; set; }

        /// <summary>The energy carried into the next party turn (星図の計算).</summary>
        public int Carried { get; private set; }

        public bool Preview => false;

        public BattleFighter this[BattleUnit unit] =>
            unit.IsNone ? null
            : unit.IsAlly ? (unit.Index < Allies.Count ? Allies[unit.Index] : null)
            : unit.Index < Enemies.Count ? Enemies[unit.Index]
            : null;

        /// <summary>The ally at <paramref name="place"/> holds <paramref name="effects"/> (passives, an item's).</summary>
        public void Give(int place, params BattleEffect[] effects)
        {
            foreach (var effect in effects)
                Effects.Add(BattleUnit.Ally(place), effect);
        }

        // --- Playing ---------------------------------------------------------------------------

        /// <summary>
        /// The ally at <paramref name="user"/> plays <paramref name="skill"/> on
        /// <paramref name="target"/> (an enemy for a card on enemies, an ally otherwise; -1 for
        /// none): it pays the cost, and every hit lands at once. Tells how hard its blows hit.
        /// </summary>
        public BattleBlowWeight PlayCard(
            CardSkill skill,
            int user,
            int target = -1,
            bool pay = true
        )
        {
            if (pay)
                Energy = Math.Max(0, Energy - CostOf(skill, user));
            var by = BattleUnit.Ally(user);
            var (play, played) = CardPlays.Begin(this, Effects, skill, by);
            bool onEnemies =
                skill.Target
                is CardTarget.OneEnemy
                    or CardTarget.AllEnemies
                    or CardTarget.RandomEnemies
                    or CardTarget.ChainEnemies;
            var plan = BattleCardPlan.Plan(
                played,
                StandingOf(false),
                StandingOf(true),
                target,
                onEnemies,
                user,
                Dice.Below
            );
            var weight = BattleBlowWeight.Normal;
            for (int i = 0; i < plan.Count; i++)
            {
                var landed = CardHits.Land(
                    this,
                    Effects,
                    play,
                    played,
                    plan[i],
                    user,
                    i == 0 || plan[i].Wave != plan[i - 1].Wave
                );
                if (landed != BattleBlowWeight.Normal)
                    weight = landed;
            }
            CardPlays.End(this, Effects, play);
            return weight;
        }

        /// <summary>The energy <paramref name="skill"/> costs the ally at <paramref name="user"/> now, as the effects change it.</summary>
        public int CostOf(CardSkill skill, int user) =>
            Effects.Modify(
                this,
                BattleValue.Cost,
                skill.Cost,
                new BattleEvent
                {
                    Actor = BattleUnit.Ally(user),
                    Card = skill,
                    Element = skill.Element,
                }
            );

        /// <summary>The enemy at <paramref name="enemy"/> strikes the ally at <paramref name="ally"/> with <paramref name="power"/>.</summary>
        public void EnemyStrike(
            int enemy,
            int ally,
            int power,
            bool magic = false,
            EnemyMove move = null
        )
        {
            var aimed = Effects.Fire(
                this,
                BattleTrigger.EnemyTargeting,
                new BattleEvent
                {
                    Actor = BattleUnit.Enemy(enemy),
                    Target = BattleUnit.Ally(ally),
                    Kind = BattleDamageKind.Enemy,
                }
            );
            var target =
                aimed.Redirect.IsAlly && !aimed.Redirect.IsNone
                    ? aimed.Redirect
                    : BattleUnit.Ally(ally);
            EnemyBlows.Strike(this, Effects, BattleUnit.Enemy(enemy), target, power, magic, move);
        }

        /// <summary>The party's turn ends: the effects see the energy left (星図の計算).</summary>
        public void EndPartyTurn()
        {
            Carried = 0;
            Effects.Fire(this, BattleTrigger.PartyTurnEnd, new BattleEvent { Amount = Energy });
            // 敵が味方に付けた状態は、味方のターンの終わりに数える。
            foreach (var ally in Allies)
                BattleStatusRules.Countdown(ally.Statuses, fromEnemies: true);
        }

        /// <summary>The enemies' turn begins: the burns and poison an enemy put on the allies hurt them, past their block.</summary>
        public void StartEnemyTurn()
        {
            Effects.Fire(this, BattleTrigger.EnemyTurnStart, new BattleEvent());
            foreach (var status in new[] { CardStatus.Burn, CardStatus.Poison })
            foreach (var ally in Allies)
            {
                var entry = BattleStatusRules.Find(ally.Statuses, status);
                if (entry == null || ally.Hp <= 0)
                    continue;
                Soak(ally, BattleStatusRules.TickDamage(entry), pastBlock: true);
                if (status == CardStatus.Poison && --entry.Amount <= 0)
                    ally.Statuses.Remove(entry);
            }
        }

        /// <summary>The enemy at <paramref name="enemy"/> uses <paramref name="move"/>'s effect (麻痺・毒・防御ダウンを付ける技).</summary>
        public void UseEnemyMove(int enemy, EnemyMove move)
        {
            if (move?.Effect == null || !Stands(BattleUnit.Enemy(enemy)))
                return;
            var used = Effects.Add(BattleUnit.Enemy(enemy), move.Effect);
            try
            {
                Effects.Fire(
                    this,
                    BattleTrigger.Used,
                    new BattleEvent { Actor = BattleUnit.Enemy(enemy) }
                );
            }
            finally
            {
                Effects.Remove(used);
            }
        }

        /// <summary>
        /// The next party turn comes round: the statuses tick and count down, the block halves,
        /// and the turn begins with its energy and the energy carried over.
        /// </summary>
        public void StartPartyTurn()
        {
            TickStatuses();
            foreach (var ally in Allies)
                ally.Block = CardRules.BlockKept(ally.Block);
            Turn++;
            Effects.StartTurn();
            Energy =
                Effects.Modify(this, BattleValue.TurnEnergy, MaxEnergy, new BattleEvent())
                + Carried;
            Carried = 0;
            Effects.Fire(this, BattleTrigger.PartyTurnStart, new BattleEvent());
        }

        /// <summary>
        /// The statuses work as the party's turn comes round: burns and poison hurt the enemies,
        /// regen heals the allies, then every status counts down.
        /// </summary>
        public void TickStatuses()
        {
            foreach (var status in new[] { CardStatus.Burn, CardStatus.Poison })
                for (int i = 0; i < Enemies.Count; i++)
                {
                    var enemy = Enemies[i];
                    var entry = BattleStatusRules.Find(enemy.Statuses, status);
                    if (entry == null || enemy.Hp <= 0)
                        continue;
                    var blow = new BattleEvent
                    {
                        Actor = BattleUnit.Ally(entry.Source),
                        Target = BattleUnit.Enemy(i),
                        Kind = BattleDamageKind.Status,
                        Status = status,
                    };
                    int before = enemy.Hp;
                    enemy.Hp = Math.Max(0, enemy.Hp - BattleStatusRules.TickDamage(entry));
                    CardHits.Landed(this, Effects, blow, before - enemy.Hp, enemy.Hp <= 0);
                    if (status == CardStatus.Poison && --entry.Amount <= 0)
                        enemy.Statuses.Remove(entry);
                }
            // 爆炎の刻印：敵全体に、付けた味方の属攻を敵の属防で弱めた炎のダメージ。
            for (int i = 0; i < Enemies.Count; i++)
            {
                var sigil = BattleStatusRules.Find(Enemies[i].Statuses, CardStatus.BlastSigil);
                if (sigil == null)
                    continue;
                Enemies[i].Statuses.Remove(sigil);
                for (int k = 0; k < Enemies.Count; k++)
                    if (Enemies[k].Hp > 0)
                        Burst(k, sigil, CardElement.Fire);
            }
            for (int i = 0; i < Allies.Count; i++)
            {
                var regen = BattleStatusRules.Find(Allies[i].Statuses, CardStatus.Regen);
                if (regen != null && Allies[i].Hp > 0)
                    Heal(BattleUnit.Ally(regen.Source), BattleUnit.Ally(i), regen.Power);
            }
            // 敵のリジェネ（敵の技で付く）。
            foreach (var enemy in Enemies)
            {
                var regen = BattleStatusRules.Find(enemy.Statuses, CardStatus.Regen);
                if (regen != null && enemy.Hp > 0)
                    enemy.Hp = Math.Min(enemy.MaxHp, enemy.Hp + regen.Power);
            }
            // 雷雲：ランダムな敵1体に雷。
            foreach (var ally in Allies)
            {
                var cloud = BattleStatusRules.Find(ally.Statuses, CardStatus.Thundercloud);
                var standing = StandingOf(false);
                if (cloud != null && standing.Count > 0)
                    Burst(standing[Dice.Below(standing.Count)], cloud, CardElement.Thunder);
            }
            foreach (var fighter in Allies)
                BattleStatusRules.Countdown(fighter.Statuses);
            foreach (var fighter in Enemies)
                BattleStatusRules.Countdown(fighter.Statuses);
        }

        /// <summary>
        /// <paramref name="source"/> puts <paramref name="action"/>'s status on
        /// <paramref name="target"/>, unless an effect stops it (浄めの誓い). True when it is on.
        /// </summary>
        public bool AddStatus(BattleUnit source, BattleUnit target, CardAction action)
        {
            var fighter = this[target];
            if (fighter == null)
                return false;
            if (
                Effects
                    .Fire(
                        this,
                        BattleTrigger.StatusApplying,
                        new BattleEvent
                        {
                            Actor = source,
                            Target = target,
                            Status = action.Status,
                        }
                    )
                    .Cancelled
            )
                return false;
            int power = action.HasPower ? CardPower(action, source) : 0;
            if (action.Status == CardStatus.Regen && source.IsAlly)
                power = CardHits.HealPower(this, Effects, null, null, power, source, target);
            var (tickStat, _) = CardRules.TickOf(action.Status);
            int tick = BattleStatusRules.TickPower(action.Status, Stat(source, tickStat));
            if (tick > 0)
                power = tick;
            BattleStatusRules.Put(
                fighter.Statuses,
                action.Status,
                action.Turns,
                power,
                action.Amount,
                source.IsAlly ? source.Index : -1,
                target.IsAlly && source.IsEnemy ? source.Index : -1
            );
            Effects.Fire(
                this,
                BattleTrigger.StatusApplied,
                new BattleEvent
                {
                    Actor = source,
                    Target = target,
                    Status = action.Status,
                }
            );
            return true;
        }

        // 刻印・雷雲の一撃：付けた味方のLvで敵の属防に弱められ、弱点・プロテクト・ブロックを通る。
        private void Burst(int enemy, BattleStatusEntry status, CardElement element)
        {
            var source = BattleUnit.Ally(status.Source);
            int power = CombatFormula.Defend(
                status.Power,
                Enemies[enemy].MagicDefense,
                Level(source)
            );
            var blow = new BattleEvent
            {
                Actor = source,
                Target = BattleUnit.Enemy(enemy),
                Kind = BattleDamageKind.Status,
                Status = status.Status,
                Element = element,
            };
            bool weak = Weak(Enemies[enemy], element, false);
            LandOnEnemy(blow, weak ? (int)Math.Round(power * WeakMultiplier) : power);
        }

        private List<int> StandingOf(bool allies)
        {
            var standing = new List<int>();
            var side = allies ? Allies : Enemies;
            for (int i = 0; i < side.Count; i++)
                if (side[i].Hp > 0)
                    standing.Add(i);
            return standing;
        }

        // --- IBattleWorld ----------------------------------------------------------------------

        public int Count(bool allies) => allies ? Allies.Count : Enemies.Count;

        public bool Stands(BattleUnit unit) => this[unit] is { Present: true, Hp: > 0 };

        public int Hp(BattleUnit unit) => this[unit]?.Hp ?? 0;

        public int MaxHp(BattleUnit unit) => this[unit]?.MaxHp ?? 0;

        public int Block(BattleUnit unit) => this[unit]?.Block ?? 0;

        public bool HasStatus(BattleUnit unit, CardStatus status) =>
            this[unit] is { } fighter && BattleStatusRules.Find(fighter.Statuses, status) != null;

        public int StatusTurns(BattleUnit unit, CardStatus status) =>
            this[unit] is { } fighter
                ? BattleStatusRules.Find(fighter.Statuses, status)?.Turns ?? 0
                : 0;

        public int StatusAmount(BattleUnit unit, CardStatus status) =>
            this[unit] is { } fighter
                ? BattleStatusRules.Find(fighter.Statuses, status)?.Amount ?? 0
                : 0;

        public void StatusesOf(BattleUnit unit, List<CardStatus> into)
        {
            if (this[unit] is { } fighter)
                foreach (var entry in fighter.Statuses)
                    into.Add(entry.Status);
        }

        public bool InBattle(BattleUnit unit) => this[unit] is { Present: true };

        public int StatusPower(BattleUnit unit, CardStatus status) =>
            this[unit] is { } fighter
                ? BattleStatusRules.Find(fighter.Statuses, status)?.Power ?? 0
                : 0;

        public int Stat(BattleUnit unit, CardStat stat)
        {
            var fighter = this[unit];
            if (fighter == null)
                return 0;
            if (unit.IsAlly)
            {
                var stats = StatsOf(unit.Index);
                return stat switch
                {
                    CardStat.PhysicalAttack => stats.PhysicalAttack,
                    CardStat.MagicAttack => stats.MagicAttack,
                    CardStat.PhysicalDefense => stats.PhysicalDefense,
                    _ => 0,
                };
            }
            return stat switch
            {
                CardStat.PhysicalAttack => fighter.PhysicalAttack,
                CardStat.MagicAttack => fighter.MagicAttack,
                CardStat.PhysicalDefense => fighter.PhysicalDefense,
                _ => 0,
            };
        }

        public int Defense(BattleUnit unit, CardStat stat)
        {
            var fighter = this[unit];
            if (fighter == null)
                return 0;
            bool magic = stat == CardStat.MagicAttack;
            if (unit.IsAlly)
                return magic
                    ? StatsOf(unit.Index).MagicDefense
                    : StatsOf(unit.Index).PhysicalDefense;
            return magic ? fighter.MagicDefense : fighter.PhysicalDefense;
        }

        public int Level(BattleUnit unit) => this[unit]?.Level ?? 1;

        /// <summary>True when the party knows every weakness (the auto-battle picks its targets by them).</summary>
        public bool KnowWeaknesses;

        private int statsDepth;

        /// <summary>
        /// The ally's stats now, with the percents the effects add (<see cref="BattleValue.StatPercent"/>:
        /// 背水・先手・弔い and any other), as the battle screen's StatsNow works them out.
        /// </summary>
        public CharacterStats StatsOf(int place)
        {
            var ally = place >= 0 && place < Allies.Count ? Allies[place] : null;
            if (ally == null)
                return default;
            if (statsDepth > 0)
                return ally.Stats;
            statsDepth++;
            try
            {
                int Added(int stat) =>
                    Effects.Modify(
                        new Quiet(this),
                        BattleValue.StatPercent,
                        0,
                        new BattleEvent
                        {
                            Actor = BattleUnit.Ally(place),
                            Target = BattleUnit.Ally(place),
                            StatIndex = stat,
                        }
                    );
                return BattleStatBoosts.Grown(
                    ally.Stats,
                    ally.Unboosted,
                    ally.Percents,
                    Added(BattleStatBoosts.PhysicalAttack),
                    Added(BattleStatBoosts.MagicAttack),
                    Added(BattleStatBoosts.Speed)
                );
            }
            finally
            {
                statsDepth--;
            }
        }

        public int Pick(int count) => Dice.Below(count);

        public void Strike(BattleEvent blow, int power, BattleStrikeLook look)
        {
            var fighter = this[blow.Target];
            if (fighter == null || fighter.Hp <= 0)
                return;
            if (blow.Target.IsAlly)
            {
                Soak(fighter, power, false);
                return;
            }
            if (blow.Pierce)
            {
                int before = fighter.Hp;
                fighter.Hp = Math.Max(0, fighter.Hp - power);
                CardHits.Landed(this, Effects, blow, before - fighter.Hp, fighter.Hp <= 0);
                return;
            }
            bool weak = blow.Element != CardElement.None && Weak(fighter, blow.Element, false);
            LandOnEnemy(blow, weak ? (int)Math.Round(power * WeakMultiplier) : power);
        }

        public int AddBlock(BattleUnit unit, int amount)
        {
            var fighter = this[unit];
            if (fighter == null || !unit.IsAlly)
                return 0;
            int before = fighter.Block;
            fighter.Block = Math.Min(fighter.MaxHp, before + Math.Max(0, amount));
            int gained = fighter.Block - before;
            if (gained > 0)
                Effects.Fire(
                    this,
                    BattleTrigger.GainedBlock,
                    new BattleEvent { Target = unit, Amount = gained }
                );
            return gained;
        }

        public void Heal(BattleUnit healer, BattleUnit target, int amount)
        {
            var fighter = this[target];
            if (fighter == null)
                return;
            int overflow = CombatPassiveRules.Overflow(fighter.Hp, fighter.MaxHp, amount);
            fighter.Hp = Math.Min(fighter.MaxHp, fighter.Hp + Math.Max(0, amount));
            Effects.Fire(
                this,
                BattleTrigger.Healed,
                new BattleEvent
                {
                    Actor = healer,
                    Target = target,
                    Amount = amount,
                    Overflow = overflow,
                }
            );
        }

        public bool ApplyStatus(
            BattleUnit source,
            BattleUnit target,
            CardStatus status,
            int turns,
            int chance,
            int amount
        )
        {
            var action = CardAction
                .Apply(
                    target.IsAlly ? CardTarget.OneAlly : CardTarget.OneEnemy,
                    status,
                    turns,
                    amount
                )
                .WithChance(chance);
            if (!target.IsAlly && source.IsAlly && !Lands(source, target, action))
                return false;
            return AddStatus(source, target, action);
        }

        public void RemoveStatus(BattleUnit unit, CardStatus status)
        {
            if (this[unit] is { } fighter)
                fighter.Statuses.RemoveAll(entry => entry.Status == status);
        }

        public int GainEnergy(int amount)
        {
            int before = Energy;
            Energy = Math.Max(Energy, Math.Min(EnergyCap, Energy + amount));
            return Energy - before;
        }

        public int CarryEnergy(int most)
        {
            int more = Math.Clamp(Energy - Carried, 0, Math.Max(0, most));
            Carried += more;
            return more;
        }

        /// <summary>A battle without a screen has no hand: a draw is only counted.</summary>
        public int Draw(int count) => Math.Max(0, count);

        /// <summary>What the effects showed, in order ("星図の計算 +1").</summary>
        public readonly List<string> Announced = new();

        public void Announce(BattleEffectInstance effect, BattleUnit at, string detail) =>
            Announced.Add(
                string.IsNullOrEmpty(detail) ? effect.Effect.Name : $"{effect.Effect.Name} {detail}"
            );

        // --- ICardWorld --------------------------------------------------------------------------

        public int CardPower(CardAction action, BattleUnit user) =>
            CardRules.Power(action, Stat(user, action.Stat), TodayAct);

        public bool IsWeak(BattleUnit enemy, CardElement element, BattleUnit user) =>
            this[enemy] is { } fighter
            && Weak(fighter, element, HasStatus(user, CardStatus.FireBlade));

        public bool RollCritical(BattleUnit user)
        {
            var fighter = this[user];
            if (fighter == null)
                return false;
            int critical = user.IsAlly ? fighter.Stats.Critical : fighter.Critical;
            return BattleOdds.Critical(CombatFormula.CriticalChance(critical, fighter.Level));
        }

        public BattleBlowWeight LandCardBlow(BattleEvent blow, int power, bool weak, bool critical)
        {
            var fighter = this[blow.Target];
            if (fighter == null || fighter.Hp <= 0)
                return BattleBlowWeight.Normal;
            LandOnEnemy(blow, weak ? (int)Math.Round(power * WeakMultiplier) : power);
            return fighter.Hp <= 0 ? BattleBlowWeight.Defeat
                : weak ? BattleBlowWeight.Weak
                : BattleBlowWeight.Normal;
        }

        public void LandCardHeal(BattleUnit healer, BattleUnit target, int amount) =>
            Heal(healer, target, amount);

        public void LandCardRevive(BattleUnit target, int percent)
        {
            if (this[target] is { } fighter)
                fighter.Hp = Math.Min(
                    fighter.MaxHp,
                    fighter.Hp + (int)Math.Round(fighter.MaxHp * percent / 100f)
                );
        }

        public void LandCardBlock(
            BattleUnit user,
            BattleUnit target,
            int block,
            bool party,
            bool first
        ) => AddBlock(target, block);

        public void LandCardStatus(BattleUnit user, BattleUnit target, CardAction action)
        {
            if (target.IsAlly || Lands(user, target, action))
                AddStatus(user, target, action);
        }

        public void LandCardSupport(
            BattleUnit user,
            BattleUnit target,
            CardAction action,
            bool first
        )
        {
            switch (action.Kind)
            {
                case CardActionKind.Energy:
                    Energy = Math.Min(EnergyCap, Energy + action.Amount);
                    break;
                case CardActionKind.Cleanse:
                    this[target]?.Statuses.RemoveAll(entry => CardRules.IsBad(entry.Status));
                    break;
            }
        }

        // --- IFightWorld -------------------------------------------------------------------------

        public void BreakBlock(BattleUnit ally)
        {
            if (this[ally] is { } fighter)
                fighter.Block = 0;
        }

        public void LandEnemyBlow(
            BattleUnit attacker,
            BattleUnit target,
            int power,
            bool magic,
            bool pastBlock,
            bool critical,
            EnemyMove move
        )
        {
            if (this[target] is { } fighter)
                Soak(fighter, power, pastBlock);
        }

        // 敵に当たる：受ける側の効果（プロテクト）、ブロック、HPの順。効果に伝える。
        private void LandOnEnemy(BattleEvent blow, int damage)
        {
            var fighter = this[blow.Target];
            damage = Effects.Modify(
                this,
                BattleValue.DamageTaken,
                damage,
                new BattleEvent { Target = blow.Target, Element = blow.Element }
            );
            int before = fighter.Hp;
            Soak(fighter, damage, false);
            CardHits.Landed(this, Effects, blow, before - fighter.Hp, fighter.Hp <= 0);
        }

        private static void Soak(BattleFighter fighter, int damage, bool pastBlock)
        {
            if (!pastBlock)
            {
                int absorbed = BattleBlows.Soaked(fighter.Block, damage);
                fighter.Block -= absorbed;
                damage -= absorbed;
            }
            fighter.Hp = Math.Max(0, fighter.Hp - damage);
        }

        private static bool Weak(BattleFighter fighter, CardElement element, bool fireBlade) =>
            (element != CardElement.None && Array.IndexOf(fighter.Weaknesses, element) >= 0)
            || (fireBlade && Array.IndexOf(fighter.Weaknesses, CardElement.Fire) >= 0);

        // 敵への悪い状態は、使い手と敵の幸運で動く確率で付く（doc/features/combat.md）。
        private bool Lands(BattleUnit user, BattleUnit target, CardAction action)
        {
            if (!CardRules.IsBad(action.Status) || action.Chance <= 0)
                return true;
            var by = this[user];
            var on = this[target];
            return BattleOdds.Lands(
                CombatFormula.StatusChance(
                    action.Chance,
                    by?.Stats.Luck ?? 0,
                    by?.Level ?? 1,
                    on?.Luck ?? 0
                )
            );
        }

        // ステータスを読むための、何も使い切らず名前も出さない窓口。
        private sealed class Quiet : IBattleWorld
        {
            private readonly BattleState battle;

            public Quiet(BattleState battle) => this.battle = battle;

            public bool Preview => true;
            public int Turn => battle.Turn;
            public int TodayAct => battle.TodayAct;
            public int Energy => battle.Energy;

            public int Count(bool allies) => battle.Count(allies);

            public bool Stands(BattleUnit unit) => battle.Stands(unit);

            public int Hp(BattleUnit unit) => battle.Hp(unit);

            public int MaxHp(BattleUnit unit) => battle.MaxHp(unit);

            public int Block(BattleUnit unit) => battle.Block(unit);

            public bool HasStatus(BattleUnit unit, CardStatus status) =>
                battle.HasStatus(unit, status);

            public int StatusAmount(BattleUnit unit, CardStatus status) =>
                battle.StatusAmount(unit, status);

            public int StatusTurns(BattleUnit unit, CardStatus status) =>
                battle.StatusTurns(unit, status);

            public void StatusesOf(BattleUnit unit, List<CardStatus> into) =>
                battle.StatusesOf(unit, into);

            public bool InBattle(BattleUnit unit) => battle.InBattle(unit);

            public int StatusPower(BattleUnit unit, CardStatus status) =>
                battle.StatusPower(unit, status);

            public int Stat(BattleUnit unit, CardStat stat) => battle.Stat(unit, stat);

            public int Defense(BattleUnit unit, CardStat stat) => battle.Defense(unit, stat);

            public int Level(BattleUnit unit) => battle.Level(unit);

            public int Pick(int count) => 0;

            public void Strike(BattleEvent blow, int power, BattleStrikeLook look) { }

            public int AddBlock(BattleUnit unit, int amount) => 0;

            public void Heal(BattleUnit healer, BattleUnit target, int amount) { }

            public bool ApplyStatus(
                BattleUnit source,
                BattleUnit target,
                CardStatus status,
                int turns,
                int chance,
                int amount
            ) => false;

            public void RemoveStatus(BattleUnit unit, CardStatus status) { }

            public int GainEnergy(int amount) => 0;

            public int CarryEnergy(int most) => 0;

            public int Draw(int count) => 0;

            public void Announce(BattleEffectInstance effect, BattleUnit at, string detail) { }
        }
    }
}
