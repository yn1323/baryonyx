using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// One effect held by one character in a battle: its stacks, and how often each of its rules
    /// has worked this turn and this battle.
    /// </summary>
    public sealed class BattleEffectInstance
    {
        private readonly int[] turnUses;
        private readonly int[] battleUses;

        internal BattleEffectInstance(BattleEffect effect, BattleUnit owner, BattleCardPlay play)
        {
            Effect = effect;
            Owner = owner;
            Play = play;
            turnUses = new int[effect.Rules.Count];
            battleUses = new int[effect.Rules.Count];
        }

        public BattleEffect Effect { get; }
        public BattleUnit Owner { get; }

        /// <summary>
        /// For a card's own effect, the play it belongs to: it works only on what happens in that
        /// play. Null for an effect held through the battle.
        /// </summary>
        public BattleCardPlay Play { get; }

        /// <summary>What the effect counts (見よう見まね's cards watched), up to <see cref="BattleEffect.MaxStacks"/>.</summary>
        public int Stacks { get; set; }

        public bool Full => Effect.MaxStacks > 0 && Stacks >= Effect.MaxStacks;

        // ShowGrown が最後に見た、規則ごとの足す量。
        internal int[] Shown;

        internal bool CanUse(BattleEffectRule rule) =>
            rule.Limit <= 0
            || (rule.LimitSpan == BattleLimitSpan.Turn ? turnUses : battleUses)[rule.Index]
                < rule.Limit;

        internal void Use(BattleEffectRule rule)
        {
            turnUses[rule.Index]++;
            battleUses[rule.Index]++;
        }

        internal void StartTurn() => Array.Clear(turnUses, 0, turnUses.Length);

        internal void StartBattle()
        {
            Stacks = 0;
            Shown = null;
            Array.Clear(turnUses, 0, turnUses.Length);
            Array.Clear(battleUses, 0, battleUses.Length);
        }
    }

    /// <summary>
    /// The effects of a battle and who holds them: the battle asks them to change its numbers
    /// (<see cref="Modify"/>) and tells them what happens (<see cref="Fire"/>). An effect works
    /// only while its holder stands. Besides the effects added here (the passives, the items,
    /// a card's own), each status on a character works by its effect
    /// (<see cref="BattleStatuses.EffectOf"/>) while it is on. They are asked in the order they
    /// were added (the allies by place, then the enemies), then the statuses (by character, the
    /// smaller priority first), each one's rules in the order written. A happening that sets off
    /// another is told at once, down to <see cref="MaxDepth"/> happenings deep.
    /// </summary>
    public sealed class BattleEffects
    {
        /// <summary>How deep happenings that set off others may go before the deeper ones are left out.</summary>
        public const int MaxDepth = 8;

        private readonly List<BattleEffectInstance> all = new();
        private readonly Dictionary<(BattleUnit, CardStatus), BattleEffectInstance> statuses =
            new();
        private readonly List<CardStatus> statusBuffer = new();

        // しばらくのあいだ持つ効果（BattleAct.Hold）と、その長さ。
        private readonly List<(
            BattleEffectInstance Instance,
            BattleHoldSpan Span,
            int Turns
        )> held = new();
        private int depth;

        /// <summary>Told when a chain of happenings goes past <see cref="MaxDepth"/> (the battle logs it).</summary>
        public event Action<string> TooDeep;

        /// <summary>The effects added here (not those of the statuses).</summary>
        public IReadOnlyList<BattleEffectInstance> All => all;

        /// <summary>
        /// <paramref name="owner"/> holds <paramref name="effect"/>; with <paramref name="play"/>,
        /// only for what happens in that card's play (a card's own effect).
        /// </summary>
        public BattleEffectInstance Add(
            BattleUnit owner,
            BattleEffect effect,
            BattleCardPlay play = null
        )
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            var instance = new BattleEffectInstance(effect, owner, play);
            // 味方の場所の順、敵の順に並べる（同じ人の中では足した順）。
            int at = all.Count;
            while (at > 0 && Before(owner, all[at - 1].Owner))
                at--;
            all.Insert(at, instance);
            return instance;
        }

        public void Remove(BattleEffectInstance instance)
        {
            all.Remove(instance);
            held.RemoveAll(entry => entry.Instance == instance);
        }

        /// <summary>Takes away the effects of the holders <paramref name="whose"/> picks.</summary>
        public void RemoveOf(Func<BattleUnit, bool> whose)
        {
            all.RemoveAll(instance => whose(instance.Owner));
            held.RemoveAll(entry => whose(entry.Instance.Owner));
        }

        /// <summary>
        /// <paramref name="owner"/> holds <paramref name="effect"/> for a while: until it next acts
        /// (<see cref="OwnerActs"/>), for <paramref name="turns"/> party turns, or to the battle's end.
        /// </summary>
        public BattleEffectInstance Hold(
            BattleUnit owner,
            BattleEffect effect,
            BattleHoldSpan span,
            int turns = 0
        )
        {
            var instance = Add(owner, effect);
            held.Add((instance, span, Math.Max(1, turns)));
            return instance;
        }

        /// <summary><paramref name="owner"/> acts: what it held until then leaves it.</summary>
        public void OwnerActs(BattleUnit owner)
        {
            for (int i = held.Count - 1; i >= 0; i--)
                if (
                    held[i].Span == BattleHoldSpan.UntilOwnerActs
                    && held[i].Instance.Owner == owner
                )
                {
                    all.Remove(held[i].Instance);
                    held.RemoveAt(i);
                }
        }

        // a が b より前に並ぶ持ち主か（味方が先、それぞれ番号の小さい順）。
        private static bool Before(BattleUnit a, BattleUnit b) =>
            a.IsAlly != b.IsAlly ? a.IsAlly : a.Index < b.Index;

        public void Clear()
        {
            all.Clear();
            statuses.Clear();
            held.Clear();
        }

        /// <summary>The battle begins: every stack and count starts over, and what was held for a while is gone.</summary>
        public void StartBattle()
        {
            foreach (var entry in held)
                all.Remove(entry.Instance);
            held.Clear();
            foreach (var instance in all)
                instance.StartBattle();
            statuses.Clear();
        }

        /// <summary>
        /// A party turn begins: the counts of once-a-turn rules start over, and what is held for
        /// some turns counts a turn down.
        /// </summary>
        public void StartTurn()
        {
            for (int i = held.Count - 1; i >= 0; i--)
            {
                if (held[i].Span != BattleHoldSpan.Turns)
                    continue;
                if (held[i].Turns <= 1)
                {
                    all.Remove(held[i].Instance);
                    held.RemoveAt(i);
                }
                else
                    held[i] = (held[i].Instance, held[i].Span, held[i].Turns - 1);
            }
            foreach (var instance in all)
                instance.StartTurn();
            foreach (var instance in statuses.Values)
                instance.StartTurn();
        }

        /// <summary><paramref name="owner"/>'s effect <paramref name="id"/>, standing or not; null without it.</summary>
        public BattleEffectInstance Find(BattleUnit owner, string id)
        {
            foreach (var instance in all)
                if (instance.Owner == owner && instance.Effect.Id == id)
                    return instance;
            return null;
        }

        /// <summary>The first effect <paramref name="id"/> whose holder stands; null without one.</summary>
        public BattleEffectInstance Standing(IBattleWorld world, string id)
        {
            foreach (var instance in all)
                if (instance.Effect.Id == id && world.Stands(instance.Owner))
                    return instance;
            return null;
        }

        /// <summary>
        /// <paramref name="amount"/> of <paramref name="value"/> changed by the effects for
        /// <paramref name="moment"/>: the fixed amounts added, then the percents summed and grown
        /// once (a card's own <see cref="BattleCardPlay.BonusPercent"/> joins its damage), then
        /// each multiplier on its own in order, then held within the value's bounds.
        /// </summary>
        public int Modify(IBattleWorld world, BattleValue value, int amount, BattleEvent moment)
        {
            int flat = 0;
            int percent =
                value == BattleValue.Damage
                && moment.Kind == BattleDamageKind.Card
                && moment.Play != null
                    ? moment.Play.BonusPercent
                    : 0;
            List<(int Order, int Seq, int Percent)> multipliers = null;
            var saved = Save(moment);
            try
            {
                foreach (var instance in Working(world, moment))
                foreach (var rule in instance.Effect.Rules)
                {
                    if (!rule.IsValue || rule.Value != value)
                        continue;
                    if (!Ready(world, rule, instance))
                        continue;
                    Bind(moment, world, instance);
                    if (!rule.Condition.Holds(moment))
                        continue;
                    int addFlat = rule.Flat?.For(moment) ?? 0;
                    int addPercent = rule.Percent?.For(moment) ?? 0;
                    int times = rule.Multiplier?.For(moment) ?? 0;
                    if (addFlat == 0 && addPercent == 0 && times == 0)
                        continue;
                    flat += addFlat;
                    percent += addPercent;
                    if (times != 0)
                    {
                        multipliers ??= new List<(int, int, int)>();
                        multipliers.Add((rule.Order, multipliers.Count, times));
                    }
                    if (!world.Preview)
                        instance.Use(rule);
                    Announce(world, rule, instance, moment, null);
                }
            }
            finally
            {
                Restore(moment, saved);
            }
            int result = BattleMath.Grow(amount + flat, percent);
            if (multipliers != null)
            {
                multipliers.Sort((a, b) => a.Order != b.Order ? a.Order - b.Order : a.Seq - b.Seq);
                foreach (var multiplier in multipliers)
                    result = BattleMath.Scale(result, multiplier.Percent);
            }
            return Bound(value, result);
        }

        /// <summary>
        /// Tells the effects that <paramref name="trigger"/> happened, as <paramref name="moment"/>
        /// says; the rules that hold do their acts. Stops once one cancels or turns what happens
        /// (<see cref="BattleEvent.Cancelled"/>, <see cref="BattleEvent.Redirect"/>). Returns the
        /// moment, with what the effects changed.
        /// </summary>
        public BattleEvent Fire(IBattleWorld world, BattleTrigger trigger, BattleEvent moment)
        {
            if (depth >= MaxDepth)
            {
                TooDeep?.Invoke(
                    $"{trigger} went past {MaxDepth} happenings deep and was left out."
                );
                return moment;
            }
            depth++;
            var saved = Save(moment);
            try
            {
                foreach (var instance in Working(world, moment))
                foreach (var rule in instance.Effect.Rules)
                {
                    if (rule.IsValue || rule.Trigger != trigger)
                        continue;
                    if (!Ready(world, rule, instance))
                        continue;
                    Bind(moment, world, instance);
                    if (!rule.Condition.Holds(moment))
                        continue;
                    if (!world.Preview)
                        instance.Use(rule);
                    bool done = rule.Acts.Count == 0;
                    string detail = null;
                    foreach (var act in rule.Acts)
                    {
                        var result = act.Run(moment);
                        Bind(moment, world, instance);
                        if (!result.Done)
                            continue;
                        done = true;
                        if (!string.IsNullOrEmpty(result.Detail))
                            detail = detail == null ? result.Detail : detail + " " + result.Detail;
                    }
                    if (done)
                        Announce(world, rule, instance, moment, detail);
                    if (moment.Cancelled || !moment.Redirect.IsNone)
                        return moment;
                }
            }
            finally
            {
                Restore(moment, saved);
                depth--;
            }
            return moment;
        }

        /// <summary>
        /// Shows the name of each effect whose <see cref="BattleShow.WhenGrows"/> rule on
        /// <paramref name="value"/> has just started to add, or adds more than when last looked
        /// at, for its owner and any of <paramref name="stats"/> (背水・先手・弔い).
        /// </summary>
        public void ShowGrown(IBattleWorld world, BattleValue value, params int[] stats)
        {
            foreach (var instance in all)
            {
                bool shown = false;
                foreach (var rule in instance.Effect.Rules)
                {
                    if (!rule.IsValue || rule.Value != value || rule.Show != BattleShow.WhenGrows)
                        continue;
                    int now = 0;
                    if (world.Stands(instance.Owner))
                        foreach (int stat in stats)
                        {
                            var moment = new BattleEvent
                            {
                                Actor = instance.Owner,
                                Target = instance.Owner,
                                StatIndex = stat,
                            };
                            Bind(moment, world, instance);
                            if (rule.Condition.Holds(moment))
                                now = Math.Max(now, rule.Flat?.For(moment) ?? 0);
                        }
                    instance.Shown ??= new int[instance.Effect.Rules.Count];
                    int before = instance.Shown[rule.Index];
                    instance.Shown[rule.Index] = now;
                    if (now > before && !shown && !world.Preview)
                    {
                        shown = true;
                        world.Announce(instance, instance.Owner, null);
                    }
                }
            }
        }

        // 働く効果：足した効果（持ち主が立っていて、カードの効果ならその1枚の中）と、立っている人の状態の効果。
        // 働きの中で効果が足し引きされても回るよう、写しを返す。
        private List<BattleEffectInstance> Working(IBattleWorld world, BattleEvent moment)
        {
            var working = new List<BattleEffectInstance>(all.Count + 4);
            foreach (var instance in all)
                if (
                    (instance.Play == null || instance.Play == moment.Play)
                    && world.Stands(instance.Owner)
                )
                    working.Add(instance);
            AddStatuses(world, true, working);
            AddStatuses(world, false, working);
            return working;
        }

        private void AddStatuses(IBattleWorld world, bool allies, List<BattleEffectInstance> into)
        {
            int count = world.Count(allies);
            for (int i = 0; i < count; i++)
            {
                var unit = allies ? BattleUnit.Ally(i) : BattleUnit.Enemy(i);
                if (!world.Stands(unit))
                    continue;
                statusBuffer.Clear();
                world.StatusesOf(unit, statusBuffer);
                int first = into.Count;
                foreach (var status in statusBuffer)
                {
                    var effect = BattleStatuses.EffectOf(status);
                    if (effect == null)
                        continue;
                    if (!statuses.TryGetValue((unit, status), out var instance))
                        statuses[(unit, status)] = instance = new BattleEffectInstance(
                            effect,
                            unit,
                            null
                        );
                    into.Add(instance);
                }
                // 1人の状態は、優先の小さい順（無敵を反射より先に）。同じなら付いた順。
                for (int k = first + 1; k < into.Count; k++)
                {
                    var item = into[k];
                    int j = k - 1;
                    while (j >= first && into[j].Effect.Priority > item.Effect.Priority)
                    {
                        into[j + 1] = into[j];
                        j--;
                    }
                    into[j + 1] = item;
                }
            }
        }

        private static bool Ready(
            IBattleWorld world,
            BattleEffectRule rule,
            BattleEffectInstance instance
        ) => !(world.Preview && rule.Random) && instance.CanUse(rule);

        private static void Announce(
            IBattleWorld world,
            BattleEffectRule rule,
            BattleEffectInstance instance,
            BattleEvent moment,
            string detail
        )
        {
            if (world.Preview || rule.Show is BattleShow.Quiet or BattleShow.WhenGrows)
                return;
            if (
                rule.Show == BattleShow.OncePerCard
                && moment.Play != null
                && !moment.Play.Shown.Add(instance)
            )
                return;
            foreach (var unit in rule.ShowOver.In(moment))
                if (!unit.IsNone)
                    world.Announce(instance, unit, detail);
        }

        private void Bind(BattleEvent moment, IBattleWorld world, BattleEffectInstance instance)
        {
            moment.World = world;
            moment.Effects = this;
            moment.Effect = instance;
        }

        private static (IBattleWorld, BattleEffects, BattleEffectInstance) Save(
            BattleEvent moment
        ) => (moment.World, moment.Effects, moment.Effect);

        private static void Restore(
            BattleEvent moment,
            (IBattleWorld World, BattleEffects Effects, BattleEffectInstance Effect) saved
        )
        {
            moment.World = saved.World;
            moment.Effects = saved.Effects;
            moment.Effect = saved.Effect;
        }

        /// <summary>
        /// The bounds of each value: a chance stays within 0 to 100, a hit count at least 1, a
        /// stat's percent as it is (it may go down), the rest at least 0.
        /// </summary>
        public static int Bound(BattleValue value, int amount) =>
            value switch
            {
                BattleValue.TwiceChance => Math.Clamp(amount, 0, 100),
                BattleValue.Hits => Math.Max(1, amount),
                BattleValue.StatPercent => amount,
                _ => Math.Max(0, amount),
            };
    }
}
