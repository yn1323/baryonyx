using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The forecast of a raised card on the HP bars (doc/features/screens/battle-deck.md, the battle mock):
    /// the HP each target would lose or get back and the block it would gain, worked out the way
    /// the card plays but without changing anyone. Every target the card can pick shows its own
    /// forecast at once, as a tap leaves no time to point at one first.
    /// </summary>
    /// <remarks>
    /// The card plays through the same blows and effects as in the battle, on a copy of the HP,
    /// block and statuses (<see cref="ForecastWorld"/>). Some of it cannot be known before the
    /// card plays, and the forecast leaves that out: a weakness not yet revealed (the forecast
    /// must not give it away), a critical hit, the effects that roll a chance, and where random
    /// hits land. A card that hits at random forecasts a single hit on each enemy and never
    /// promises a defeat.
    /// </remarks>
    public sealed partial class BattleInspectView
    {
        /// <summary>What a card would leave one character with.</summary>
        public struct BattleForecast
        {
            public bool Shown;
            public int Hp;
            public int Block;
            public bool Defeat;
        }

        /// <summary>The forecasts shown now, by enemy and by ally (empty without a raised card).</summary>
        public BattleForecast[] EnemyForecasts { get; private set; } = new BattleForecast[0];
        public BattleForecast[] AllyForecasts { get; private set; } = new BattleForecast[0];

        /// <summary>Shows the forecast of <paramref name="card"/> on the bars, or none without a card.</summary>
        private void ShowForecast(BattleInspectCard card)
        {
            var (enemies, allies) =
                card != null ? ForecastOf(card) : (new BattleForecast[0], new BattleForecast[0]);
            EnemyForecasts = enemies;
            AllyForecasts = allies;
            for (int i = 0; i < Enemies.Length; i++)
            {
                var enemy = Enemies[i];
                var forecast = i < enemies.Length ? enemies[i] : default;
                if (forecast.Shown)
                    enemy.Gauge.Forecast(
                        enemy.Hp,
                        forecast.Hp,
                        enemy.MaxHp,
                        enemy.Block,
                        enemy.Block,
                        forecast.Defeat
                    );
                else
                    enemy.Gauge.ClearForecast();
            }
            for (int i = 0; i < Allies.Length; i++)
            {
                var ally = Allies[i];
                var forecast = i < allies.Length ? allies[i] : default;
                if (forecast.Shown)
                    ally.Gauge.Forecast(
                        ally.Hp,
                        forecast.Hp,
                        ally.MaxHp,
                        BlockOf(i),
                        forecast.Block,
                        false
                    );
                else
                    ally.Gauge.ClearForecast();
            }
        }

        /// <summary>
        /// The forecast of a card from the hand: for a card on one target, each target it can
        /// pick as if picked; for a card on a whole side or on no one, the card as it plays.
        /// </summary>
        public (BattleForecast[] enemies, BattleForecast[] allies) ForecastOf(
            BattleInspectCard card
        )
        {
            var enemies = new BattleForecast[Enemies.Length];
            var allies = new BattleForecast[Allies.Length];
            var skill =
                card.DeckIndex >= 0 && card.DeckIndex < Deck.Length
                    ? SkillOf(Deck[card.DeckIndex])
                    : null;
            int caster = card.Caster;
            if (skill == null || caster < 0 || caster >= Allies.Length)
                return (enemies, allies);
            // The card's play as it would begin: its own effects join, and the effects are asked
            // without using anything up (居合の型's bonus, 連ね矢's hit).
            var (play, played) = CardPlays.Begin(
                new ForecastWorld(this),
                effects,
                skill,
                AllyAt(caster)
            );
            skill = played;
            try
            {
                bool random = HitsAtRandom(skill);
                bool onEnemies = card.TargetsEnemies;
                var picks = new List<int>();
                if (random)
                {
                    for (int i = 0; i < Enemies.Length; i++)
                        if (Enemies[i].Alive)
                            picks.Add(i);
                }
                else if (!card.TargetsWholeSide && card.Effect != BattleInspectCardEffect.Party)
                {
                    int count = onEnemies ? Enemies.Length : Allies.Length;
                    for (int i = 0; i < count; i++)
                        if (CanTarget(card, i))
                            picks.Add(i);
                }
                if (picks.Count == 0)
                    picks.Add(-1);

                for (int p = 0; p < picks.Count; p++)
                {
                    int pick = picks[p];
                    var run = PlayThrough(
                        skill,
                        play,
                        pick,
                        random ? true : onEnemies,
                        caster,
                        random
                    );
                    // A pick's own forecast comes from its own run; the other side's (the block a
                    // bash gives the party) is the same in every run, so the first run gives it.
                    bool pickOnEnemies = random || onEnemies;
                    for (int i = 0; i < enemies.Length; i++)
                        if (pick < 0 || (pickOnEnemies ? i == pick : p == 0))
                            enemies[i] = run.enemies[i];
                    for (int i = 0; i < allies.Length; i++)
                        if (pick < 0 || (!pickOnEnemies ? i == pick : p == 0))
                            allies[i] = run.allies[i];
                }
            }
            finally
            {
                CardPlays.RemoveEffects(effects, play);
            }
            return (enemies, allies);
        }

        /// <summary>True when the card's damage lands where chance puts it.</summary>
        private static bool HitsAtRandom(CardSkill skill)
        {
            foreach (var action in skill.Actions)
                if (
                    action.Kind == CardActionKind.Damage
                    && (
                        action.Target == CardTarget.RandomEnemies
                        || (action.Target == CardTarget.ChainEnemies && action.Hits > 1)
                    )
                )
                    return true;
            return false;
        }

        /// <summary>
        /// Plays the card on <paramref name="pick"/> on a copy of the HP, block and the statuses it
        /// puts on (<see cref="ForecastWorld"/>), landing each hit through <see cref="CardHits"/> as
        /// the battle does. With <paramref name="random"/>, every random hit goes to the pick, but
        /// only once.
        /// </summary>
        private (BattleForecast[] enemies, BattleForecast[] allies) PlayThrough(
            CardSkill skill,
            BattleCardPlay play,
            int pick,
            bool onEnemies,
            int caster,
            bool random
        )
        {
            var world = new ForecastWorld(this);
            play.Dealt = 0;
            var standing = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Alive)
                    standing.Add(i);
            var up = new List<int>();
            for (int i = 0; i < Allies.Length; i++)
                if (!Allies[i].Down)
                    up.Add(i);

            int at = random ? standing.IndexOf(pick) : 0;
            var plan = BattleCardPlan.Plan(
                skill,
                standing,
                up,
                pick,
                onEnemies,
                caster,
                count => Mathf.Clamp(at, 0, count - 1)
            );

            world.Random = random;
            for (int i = 0; i < plan.Count; i++)
                CardHits.Land(
                    world,
                    effects,
                    play,
                    skill,
                    plan[i],
                    caster,
                    i == 0 || plan[i].Wave != plan[i - 1].Wave
                );
            // 魔力の盾など、カードのあとに働く効果。
            CardPlays.Played(world, effects, play);

            var enemies = new BattleForecast[Enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
                enemies[i] = new BattleForecast
                {
                    Shown = world.EnemyHit[i] && world.EnemyHp[i] != Enemies[i].Hp,
                    Hp = world.EnemyHp[i],
                    Block = world.EnemyBlock[i],
                    Defeat = !random && world.EnemyHp[i] <= 0,
                };
            var allies = new BattleForecast[Allies.Length];
            for (int i = 0; i < allies.Length; i++)
                allies[i] = new BattleForecast
                {
                    Shown =
                        world.AllyHit[i]
                        && (world.AllyHp[i] != Allies[i].Hp || world.AllyBlock[i] != BlockOf(i)),
                    Hp = world.AllyHp[i],
                    Block = world.AllyBlock[i],
                };
            return (enemies, allies);
        }

        /// <summary>
        /// True when the blow hits a weakness already revealed: one still hidden counts as no
        /// weakness, so the forecast cannot give it away.
        /// </summary>
        private bool RevealedWeak(int enemy, CardElement element, int caster)
        {
            var mine = BattleCardText.ElementOf(element);
            bool fire = caster >= 0 && HasStatus(true, caster, CardStatus.FireBlade);
            foreach (var weakness in Enemies[enemy].Weaknesses)
                if (
                    weakness.Revealed
                    && (
                        (mine != BattleInspectElement.None && weakness.Element == mine)
                        || (fire && weakness.Element == BattleInspectElement.Fire)
                    )
                )
                    return true;
            return false;
        }

        /// <summary>
        /// The battle as a forecast sees it: a copy of the HP and block of both sides and of the
        /// statuses the card puts on enemies, changed as the card would change them. Everything
        /// else is read from the battle as it is. Nothing shows, nothing is used up, and the
        /// effects that roll a chance are left out (<see cref="IBattleWorld.Preview"/>).
        /// </summary>
        private sealed class ForecastWorld : ICardWorld
        {
            // カードの当たりがランダムなら、敵ごとに1回だけ当てる。
            public bool Random;
            private readonly HashSet<(CardAction, int)> landed = new();

            private readonly BattleInspectView view;
            private readonly IBattleWorld battle;

            public readonly int[] EnemyHp;
            public readonly int[] EnemyBlock;
            public readonly int[] AllyHp;
            public readonly int[] AllyBlock;
            public readonly bool[] EnemyHit;
            public readonly bool[] AllyHit;

            /// <summary>The statuses the card would put on enemies (by enemy).</summary>
            public readonly HashSet<(int, CardStatus)> Added = new();

            public ForecastWorld(BattleInspectView view)
            {
                this.view = view;
                battle = view;
                int enemies = view.Enemies.Length;
                int allies = view.Allies.Length;
                EnemyHp = new int[enemies];
                EnemyBlock = new int[enemies];
                EnemyHit = new bool[enemies];
                for (int i = 0; i < enemies; i++)
                {
                    EnemyHp[i] = view.Enemies[i].Hp;
                    EnemyBlock[i] = view.Enemies[i].Block;
                }
                AllyHp = new int[allies];
                AllyBlock = new int[allies];
                AllyHit = new bool[allies];
                for (int i = 0; i < allies; i++)
                {
                    AllyHp[i] = view.Allies[i].Hp;
                    AllyBlock[i] = view.BlockOf(i);
                }
            }

            /// <summary>
            /// A blow of <paramref name="damage"/> (with its weakness) lands on its enemy:
            /// protection and block soften it, and the effects are told.
            /// </summary>
            public void Land(BattleEvent blow, int damage)
            {
                int enemy = blow.Target.Index;
                if (EnemyHp[enemy] <= 0)
                    return;
                damage = view.effects.Modify(
                    this,
                    BattleValue.DamageTaken,
                    damage,
                    new BattleEvent { Target = blow.Target, Element = blow.Element }
                );
                int absorbed = Mathf.Min(EnemyBlock[enemy], damage);
                EnemyBlock[enemy] -= absorbed;
                damage -= absorbed;
                int before = EnemyHp[enemy];
                EnemyHp[enemy] = Mathf.Max(0, before - damage);
                EnemyHit[enemy] = true;
                view.Landed(this, blow, before - EnemyHp[enemy], EnemyHp[enemy] <= 0);
            }

            /// <summary>The ally at <paramref name="healer"/> heals <paramref name="target"/>; the effects are told what went past the max HP.</summary>
            public void HealBy(int healer, int target, int amount)
            {
                int maxHp = view.Allies[target].MaxHp;
                int overflow = CombatPassiveRules.Overflow(AllyHp[target], maxHp, amount);
                AllyHp[target] = Mathf.Min(maxHp, AllyHp[target] + amount);
                AllyHit[target] = true;
                view.effects.Fire(
                    this,
                    BattleTrigger.Healed,
                    new BattleEvent
                    {
                        Actor = AllyAt(healer),
                        Target = AllyAt(target),
                        Amount = amount,
                        Overflow = overflow,
                    }
                );
            }

            public void Revive(int target, int amount)
            {
                AllyHp[target] = Mathf.Min(view.Allies[target].MaxHp, AllyHp[target] + amount);
                AllyHit[target] = true;
            }

            public bool Preview => true;
            public int Turn => battle.Turn;
            public int TodayAct => battle.TodayAct;
            public int Energy => battle.Energy;

            public int Count(bool allies) => battle.Count(allies);

            public bool Stands(BattleUnit unit) =>
                !unit.IsNone
                && (
                    unit.IsAlly
                        ? unit.Index < AllyHp.Length && AllyHp[unit.Index] > 0
                        : unit.Index < EnemyHp.Length && EnemyHp[unit.Index] > 0
                );

            public int Hp(BattleUnit unit) =>
                unit.IsNone ? 0
                : unit.IsAlly ? AllyHp[unit.Index]
                : EnemyHp[unit.Index];

            public int MaxHp(BattleUnit unit) => battle.MaxHp(unit);

            public int Block(BattleUnit unit) =>
                unit.IsNone ? 0
                : unit.IsAlly ? AllyBlock[unit.Index]
                : EnemyBlock[unit.Index];

            public bool HasStatus(BattleUnit unit, CardStatus status) =>
                battle.HasStatus(unit, status)
                || (unit.IsEnemy && Added.Contains((unit.Index, status)));

            public int StatusAmount(BattleUnit unit, CardStatus status) =>
                battle.StatusAmount(unit, status);

            public int StatusTurns(BattleUnit unit, CardStatus status) =>
                battle.StatusTurns(unit, status);

            public void StatusesOf(BattleUnit unit, List<CardStatus> into)
            {
                battle.StatusesOf(unit, into);
                if (!unit.IsEnemy)
                    return;
                foreach (var (enemy, status) in Added)
                    if (enemy == unit.Index && !into.Contains(status))
                        into.Add(status);
            }

            public bool InBattle(BattleUnit unit) => battle.InBattle(unit);

            public void RemoveStatus(BattleUnit unit, CardStatus status) { }

            public int StatusPower(BattleUnit unit, CardStatus status) =>
                battle.StatusPower(unit, status);

            public int Stat(BattleUnit unit, CardStat stat) => battle.Stat(unit, stat);

            public int Defense(BattleUnit unit, CardStat stat) => battle.Defense(unit, stat);

            public int Level(BattleUnit unit) => battle.Level(unit);

            public int Pick(int count) => 0;

            public void Strike(BattleEvent blow, int power, BattleStrikeLook look)
            {
                if (!blow.Target.IsEnemy || blow.Target.Index >= EnemyHp.Length)
                    return;
                bool weak =
                    blow.Element != CardElement.None
                    && view.RevealedWeak(blow.Target.Index, blow.Element, -1);
                Land(blow, weak ? Mathf.RoundToInt(power * WeakMultiplier) : power);
            }

            public int AddBlock(BattleUnit unit, int amount)
            {
                if (!unit.IsAlly || unit.IsNone || unit.Index >= AllyBlock.Length)
                    return 0;
                int i = unit.Index;
                int before = AllyBlock[i];
                AllyBlock[i] = Mathf.Min(view.Allies[i].MaxHp, before + Mathf.Max(0, amount));
                AllyHit[i] = true;
                int gained = AllyBlock[i] - before;
                if (gained > 0)
                    view.effects.Fire(
                        this,
                        BattleTrigger.GainedBlock,
                        new BattleEvent { Target = unit, Amount = gained }
                    );
                return gained;
            }

            public void Heal(BattleUnit healer, BattleUnit target, int amount)
            {
                if (target.IsAlly && !target.IsNone)
                    HealBy(healer.IsAlly ? healer.Index : -1, target.Index, amount);
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
                if (!target.IsEnemy)
                    return false;
                Added.Add((target.Index, status));
                return true;
            }

            public int GainEnergy(int amount) => 0;

            public int CarryEnergy(int most) => 0;

            public int Draw(int count) => 0;

            public void Announce(BattleEffectInstance effect, BattleUnit at, string detail) { }

            public int CardPower(CardAction action, BattleUnit user) =>
                view.Power(action, user.Index);

            // 予測は、まだ見つけていない弱点を明かさない。
            public bool IsWeak(BattleUnit enemy, CardElement element, BattleUnit user) =>
                view.RevealedWeak(enemy.Index, element, user.Index);

            public bool RollCritical(BattleUnit user) => false;

            public BattleBlowWeight LandCardBlow(
                BattleEvent blow,
                int power,
                bool weak,
                bool critical
            )
            {
                if (Random && !landed.Add((blow.Action, blow.Target.Index)))
                    return BattleBlowWeight.Normal;
                Land(blow, weak ? Mathf.RoundToInt(power * WeakMultiplier) : power);
                return BattleBlowWeight.Normal;
            }

            public void LandCardHeal(BattleUnit healer, BattleUnit target, int amount)
            {
                if (target.IsAlly && !target.IsNone)
                    HealBy(healer.Index, target.Index, amount);
            }

            public void LandCardRevive(BattleUnit target, int percent)
            {
                if (target.IsAlly && !target.IsNone)
                    Revive(
                        target.Index,
                        Mathf.RoundToInt(view.Allies[target.Index].MaxHp * percent / 100f)
                    );
            }

            public void LandCardBlock(
                BattleUnit user,
                BattleUnit target,
                int block,
                bool party,
                bool first
            )
            {
                if (target.IsAlly)
                    AddBlock(target, block);
            }

            // 予測は、敵に付く状態だけを数える（確率は見ない）。
            public void LandCardStatus(BattleUnit user, BattleUnit target, CardAction action)
            {
                if (target.IsEnemy)
                    Added.Add((target.Index, action.Status));
            }

            public void LandCardSupport(
                BattleUnit user,
                BattleUnit target,
                CardAction action,
                bool first
            ) { }
        }
    }
}
