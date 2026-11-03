using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>What a battle without a screen came to.</summary>
    public sealed class BattleSimResult
    {
        public bool Victory;
        public bool Timeout;

        /// <summary>The party turns fought (the last one counted).</summary>
        public int Turns;

        /// <summary>The cards played.</summary>
        public int Cards;

        /// <summary>The allies who fell (counted each time).</summary>
        public int Fell;

        /// <summary>The lowest share of its max HP the party had left.</summary>
        public float HpLowest = 1f;

        /// <summary>The damage the enemies lost, and what each party turn took off them.</summary>
        public int Dealt;
        public readonly List<int> DealtByTurn = new();
    }

    /// <summary>Picks the next card a battle without a screen plays.</summary>
    public interface IBattleSimPolicy
    {
        /// <summary>
        /// The card of the hand to play (its place in <see cref="BattleSimulation.Hand"/>) and its
        /// target (an enemy, an ally or -1), or -1 to end the turn.
        /// </summary>
        (int Card, int Target) Choose(BattleSimulation battle);
    }

    /// <summary>
    /// A whole battle without a screen (doc/features/combat/effects.md, 予測と画面なしの戦闘):
    /// the deck, the hand and the energy of the party, the turn order by speed, and the enemies
    /// acting by their behaviours, all by the rules the battle screen uses
    /// (<see cref="BattleState"/>, <see cref="CardPlays"/>, <see cref="CardHits"/>,
    /// <see cref="EnemyBlows"/>, <see cref="EnemyActs"/>, the effects). It plays at once, so the
    /// auto-battle can fight many battles quickly. Left out for now (the battle screen has
    /// them): a combo (連携), a fusion (合体), a sleeper's stages (眠り札), the cards picked to
    /// discard or take back, a free or boosted next card, a unique skill's evolution, the
    /// revealing of weaknesses (all are counted as known when <see cref="KnowWeaknesses"/>, none
    /// otherwise), freezing's guard and the enemy's mock blow without a behaviour.
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly List<(CardSkill Skill, int User)> cards = new();
        private readonly List<int> hand = new();
        private BattleDeck deck;

        public BattleSimulation(BattleState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public BattleState State { get; }

        public int OpeningDraw = 5;
        public int TurnDraw = 5;
        public int HandLimit = 9;

        /// <summary>The cards of the hand, by their place in the deck (<see cref="CardOf"/>).</summary>
        public IReadOnlyList<int> Hand => hand;

        public (CardSkill Skill, int User) CardOf(int card) => cards[card];

        /// <summary>Puts <paramref name="skill"/>, used by the ally at <paramref name="user"/>, into the deck.</summary>
        public void AddCard(CardSkill skill, int user) => cards.Add((skill, user));

        /// <summary>
        /// Fights the battle to its end, by <paramref name="policy"/>'s choices: the enemies faster
        /// than the party act first, then the party's turns and the enemies' turns go round until
        /// a side is beaten, or <paramref name="maxTurns"/> party turns have passed.
        /// </summary>
        public BattleSimResult Fight(IBattleSimPolicy policy, int maxTurns = 40)
        {
            var result = new BattleSimResult();
            var state = State;
            deck = new BattleDeck(state.Dice);
            deck.Renew(
                cards.Count,
                card => !state.Stands(BattleUnit.Ally(cards[card].User)),
                card => cards[card].Skill.Has(CardTrait.Innate)
            );
            state.Effects.StartBattle();
            state.Turn = 1;
            state.Effects.StartTurn();
            state.Energy = state.Effects.Modify(
                state,
                BattleValue.TurnEnergy,
                state.MaxEnergy,
                new BattleEvent()
            );
            int enemyHp = EnemyHp();
            EnemiesAct(CombatTurnOrder.Opening(PartySpeed(), EnemySpeeds()), opening: true);
            Deal(
                state.Effects.Modify(state, BattleValue.OpeningDraw, OpeningDraw, new BattleEvent())
            );
            state.Effects.Fire(state, BattleTrigger.BattleStart, new BattleEvent());
            while (true)
            {
                if (Beaten(false))
                    break;
                int before = EnemyHp();
                PartyTurn(policy, result);
                result.DealtByTurn.Add(before - EnemyHp());
                result.Turns = state.Turn;
                if (Beaten(false))
                    break;
                EndPartyTurn();
                EnemiesAct(CombatTurnOrder.After(state.Turn, PartySpeed(), EnemySpeeds()), false);
                if (Beaten(true))
                    break;
                if (state.Turn >= maxTurns)
                {
                    result.Timeout = true;
                    break;
                }
                state.StartPartyTurn();
                Deal(
                    state.Effects.Modify(state, BattleValue.TurnDraw, TurnDraw, new BattleEvent())
                );
            }
            result.Victory = !result.Timeout && Beaten(false);
            result.Dealt = enemyHp - EnemyHp();
            result.Fell = fell;
            result.HpLowest = hpLowest;
            return result;
        }

        // --- The party -------------------------------------------------------------------------

        private int fell;

        private void PartyTurn(IBattleSimPolicy policy, BattleSimResult result)
        {
            var state = State;
            for (int guard = 0; guard < 50; guard++)
            {
                if (Beaten(false) || Beaten(true))
                    return;
                var (place, target) = policy.Choose(this);
                if (place < 0 || place >= hand.Count)
                    return;
                int card = hand[place];
                var (skill, user) = cards[card];
                int cost = CostOf(card);
                if (cost > state.Energy || !state.Stands(BattleUnit.Ally(user)))
                    return;
                state.Energy -= cost;
                hand.RemoveAt(place);
                if (skill.Has(CardTrait.Exhaust))
                    deck.Exhaust(card);
                else
                    deck.Discard(card);
                Play(skill, user, target);
                result.Cards++;
                CountFallen();
            }
        }

        /// <summary>The energy the card of the deck at <paramref name="card"/> costs now.</summary>
        public int CostOf(int card) => State.CostOf(cards[card].Skill, cards[card].User);

        private void Play(CardSkill skill, int user, int target)
        {
            var state = State;
            state.PlayCard(skill, user, target, pay: false);
            // 装備の効果で、攻撃のカードはその確率でもう一度効果を起こす。1回目と同じ相手が倒れていれば何も起きない。
            if (!Attacks(skill))
                return;
            int chance = state.Effects.Modify(
                state,
                BattleValue.TwiceChance,
                state.Allies[user].TwiceChance,
                new BattleEvent
                {
                    Actor = BattleUnit.Ally(user),
                    Card = skill,
                    Element = skill.Element,
                    Kind = BattleDamageKind.Card,
                }
            );
            if (chance <= 0 || !state.Dice.Chance(chance) || Beaten(false))
                return;
            if (skill.Target == CardTarget.OneEnemy && !state.Stands(BattleUnit.Enemy(target)))
                return;
            state.PlayCard(skill, user, target, pay: false);
        }

        private static bool Attacks(CardSkill skill) =>
            skill.Target
                is CardTarget.OneEnemy
                    or CardTarget.AllEnemies
                    or CardTarget.RandomEnemies
                    or CardTarget.ChainEnemies;

        private void EndPartyTurn()
        {
            State.EndPartyTurn();
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                var skill = cards[hand[i]].Skill;
                if (skill.Has(CardTrait.Retain))
                    continue;
                if (skill.Has(CardTrait.Ethereal))
                    deck.Exhaust(hand[i]);
                else
                    deck.Discard(hand[i]);
                hand.RemoveAt(i);
            }
        }

        private void Deal(int count)
        {
            for (int i = 0; i < count && hand.Count < HandLimit; i++)
            {
                int card = deck.Draw(c => !State.Stands(BattleUnit.Ally(cards[c].User)));
                if (card < 0)
                    break;
                hand.Add(card);
            }
        }

        // --- The enemies -----------------------------------------------------------------------

        private void EnemiesAct(List<int> order, bool opening)
        {
            var state = State;
            if (!opening)
                state.StartEnemyTurn();
            foreach (int index in order)
            {
                if (Beaten(true))
                    break;
                if (state.Stands(BattleUnit.Enemy(index)))
                    EnemyAct(index);
                CountFallen();
            }
            if (!opening)
                state.Effects.Fire(state, BattleTrigger.EnemyTurnEnd, new BattleEvent());
        }

        private void EnemyAct(int index)
        {
            var state = State;
            var enemy = state.Enemies[index];
            state.Effects.OwnerActs(BattleUnit.Enemy(index));
            if (BattleStatusRules.Find(enemy.Statuses, CardStatus.Freeze) is { } freeze)
            {
                enemy.Statuses.Remove(freeze);
                return;
            }
            // The block it put up lasts until it acts again.
            enemy.Block = 0;
            if (enemy.Brain == null)
            {
                Strike(index, EnemyAim.One, enemy.Power, enemy.Magic, null);
                return;
            }
            for (int n = 0; n < Math.Max(1, enemy.Brain.Pattern.ActionsPerTurn); n++)
            {
                if (!state.Stands(BattleUnit.Enemy(index)) || Beaten(true))
                    break;
                var step = enemy.Brain.Next(Situation(index));
                if (step.Move == null)
                    Strike(index, EnemyAim.One, enemy.Power, enemy.Magic, null);
                else if (step.Charging)
                {
                    var with =
                        step.Move.ChargeWith != null
                            ? enemy.Brain.Behavior.Move(step.Move.ChargeWith)
                            : null;
                    if (with != null && with.Supported)
                        Support(index, with, 100);
                    break;
                }
                else
                    UseMove(index, step);
            }
            // A bleeding enemy loses blood as it moves.
            var bleed = BattleStatusRules.Find(enemy.Statuses, CardStatus.Bleed);
            if (bleed != null && enemy.Hp > 0)
                enemy.Hp = Math.Max(0, enemy.Hp - bleed.Power);
        }

        private void UseMove(int index, EnemyStep step)
        {
            var state = State;
            var enemy = state.Enemies[index];
            var move = step.Move;
            if (move.Strikes)
            {
                int percent = move.Percent * step.Percent / 100;
                var aim = move.Aim;
                int spread = enemy.Brain.Pattern.SpreadPercent;
                if (spread > 0 && aim is EnemyAim.One or EnemyAim.Weakest or EnemyAim.MostBlock)
                {
                    aim = EnemyAim.All;
                    percent = percent * spread / 100;
                }
                int power = BlowPower(index, move.Stat, percent);
                enemy.PowerUp = 0;
                int splash =
                    move.SplashPercent > 0
                        ? BlowPower(index, move.Stat, move.SplashPercent * step.Percent / 100)
                        : 0;
                int before = PartyHp();
                for (int hit = 0; hit < Math.Max(1, move.Hits + step.ExtraHits); hit++)
                {
                    var aimed = Aimed(aim, index);
                    if (aimed.Count == 0)
                        break;
                    bool magic = move.Stat == CardStat.MagicAttack;
                    foreach (int target in aimed)
                        state.EnemyStrike(index, target, power, magic, move);
                    if (splash > 0 && aimed.Count == 1)
                        for (int i = 0; i < state.Allies.Count; i++)
                            if (i != aimed[0] && state.Stands(BattleUnit.Ally(i)))
                                state.EnemyStrike(index, i, splash, magic, move);
                    if (!state.Stands(BattleUnit.Enemy(index)))
                        break;
                }
                int dealt = before - PartyHp();
                if (move.DrainPercent > 0 && dealt > 0 && enemy.Hp > 0)
                    enemy.Hp = Math.Min(
                        enemy.MaxHp,
                        enemy.Hp + Math.Max(1, dealt * move.DrainPercent / 100)
                    );
                if (move.SelfCostPercent > 0 && enemy.Hp > 0)
                    enemy.Hp = Math.Max(
                        1,
                        enemy.Hp - Math.Max(1, enemy.MaxHp * move.SelfCostPercent / 100)
                    );
            }
            if (state.Stands(BattleUnit.Enemy(index)))
                Support(index, move, step.Percent);
            if (state.Stands(BattleUnit.Enemy(index)) && move.Effect != null)
                state.UseEnemyMove(index, move);
        }

        private void Strike(int index, EnemyAim aim, int power, bool magic, EnemyMove move)
        {
            foreach (int target in Aimed(aim, index))
                State.EnemyStrike(index, target, power, magic, move);
        }

        private int BlowPower(int index, CardStat stat, int percent)
        {
            var enemy = State.Enemies[index];
            int value = stat switch
            {
                CardStat.MagicAttack => enemy.MagicAttack,
                CardStat.PhysicalDefense => enemy.PhysicalDefense,
                _ => enemy.PhysicalAttack,
            };
            var up = BattleStatusRules.Find(enemy.Statuses, CardStatus.AttackUp);
            return EnemyActs.BlowPower(
                value,
                percent,
                enemy.AttackScale,
                up?.Amount,
                enemy.PowerUp,
                BattleStatusRules.Find(enemy.Statuses, CardStatus.Paralysis) != null
            );
        }

        private void Support(int index, EnemyMove move, int percent)
        {
            var state = State;
            var enemy = state.Enemies[index];
            var shares = new float[state.Enemies.Count];
            for (int i = 0; i < shares.Length; i++)
                shares[i] = state.Enemies[i].Hp > 0 ? Share(state.Enemies[i]) : -1f;
            var cared = EnemyActs.Cared(index, move.Care, shares);
            foreach (int target in cared)
            {
                var other = state.Enemies[target];
                if (move.BlockPercent > 0)
                {
                    int stat =
                        move.BlockStat == CardStat.MagicAttack
                            ? enemy.MagicAttack
                            : enemy.PhysicalDefense;
                    other.Block = Math.Min(
                        other.MaxHp,
                        other.Block + EnemyActs.GuardBlock(stat, move.BlockPercent, percent)
                    );
                }
                if (move.HealPercent > 0 && other.Hp > 0)
                    other.Hp = Math.Min(
                        other.MaxHp,
                        other.Hp + EnemyActs.Mend(move, other.MaxHp, enemy.MagicAttack)
                    );
                if (move.RegenTurns > 0)
                    BattleStatusRules.Put(
                        other.Statuses,
                        CardStatus.Regen,
                        move.RegenTurns,
                        EnemyActs.RegenPower(move, enemy.MagicAttack),
                        0,
                        -1
                    );
                if (move.AttackUpTurns > 0)
                    BattleStatusRules.Put(
                        other.Statuses,
                        CardStatus.AttackUp,
                        move.AttackUpTurns,
                        0,
                        EnemyActs.AttackUpPercent,
                        -1
                    );
                if (move.ProtectTurns > 0)
                    BattleStatusRules.Put(
                        other.Statuses,
                        CardStatus.Protect,
                        move.ProtectTurns,
                        0,
                        0,
                        -1
                    );
            }
            if (move.PowerUpPercent > 0)
                enemy.PowerUp = Math.Max(enemy.PowerUp, move.PowerUpPercent);
        }

        private List<int> Aimed(EnemyAim aim, int attacker)
        {
            var state = State;
            var standing = new List<int>();
            for (int i = 0; i < state.Allies.Count; i++)
                if (state.Stands(BattleUnit.Ally(i)))
                    standing.Add(i);
            return EnemyActs.Aimed(
                aim,
                standing,
                state.Dice.Below,
                i => Share(state.Allies[i]),
                i => state.Allies[i].Block,
                i => state.HasStatus(BattleUnit.Ally(i), CardStatus.Taunt)
            );
        }

        private EnemySituation Situation(int index)
        {
            var state = State;
            bool others = false;
            float othersLowest = 1f;
            for (int i = 0; i < state.Enemies.Count; i++)
                if (i != index && state.Enemies[i].Hp > 0)
                {
                    others = true;
                    othersLowest = Math.Min(othersLowest, Share(state.Enemies[i]));
                }
            float partyLowest = 1f;
            bool guarded = false;
            for (int i = 0; i < state.Allies.Count; i++)
                if (state.Allies[i].Hp > 0)
                {
                    partyLowest = Math.Min(partyLowest, Share(state.Allies[i]));
                    guarded |= state.Allies[i].Block > 0;
                }
            return new EnemySituation(
                state.Turn,
                Share(state.Enemies[index]),
                others,
                othersLowest,
                partyLowest,
                guarded
            );
        }

        // --- Speeds and counts -----------------------------------------------------------------

        private double PartySpeed()
        {
            var speeds = new List<double>();
            for (int i = 0; i < State.Allies.Count; i++)
                if (State.Stands(BattleUnit.Ally(i)))
                    speeds.Add(SpeedOf(BattleUnit.Ally(i), State.StatsOf(i).Speed));
            return CombatTurnOrder.PartySpeed(speeds);
        }

        private double[] EnemySpeeds()
        {
            var speeds = new double[State.Enemies.Count];
            for (int i = 0; i < speeds.Length; i++)
                speeds[i] =
                    State.Enemies[i].Hp > 0
                        ? SpeedOf(BattleUnit.Enemy(i), Math.Max(1, State.Enemies[i].Speed))
                        : 0;
            return speeds;
        }

        // 速度アップ・凍えを当てる（戦闘画面の SpeedWithStatuses と同じ）。
        private double SpeedOf(BattleUnit unit, double speed)
        {
            var fighter = State[unit];
            var up = BattleStatusRules.Find(fighter.Statuses, CardStatus.SpeedUp);
            if (up != null)
                speed *= 1 + (up.Amount > 0 ? up.Amount : SpeedUpPercent) / 100.0;
            if (BattleStatusRules.Find(fighter.Statuses, CardStatus.Chill) != null)
                speed *= 1 - ChillSpeedPercent / 100.0;
            return speed;
        }

        /// <summary>速度アップ・凍えの割合（戦闘画面の値と同じ）。</summary>
        public const int SpeedUpPercent = 30;
        public const int ChillSpeedPercent = 30;

        private bool Beaten(bool allies)
        {
            var side = allies ? State.Allies : State.Enemies;
            foreach (var fighter in side)
                if (fighter.Present && fighter.Hp > 0)
                    return false;
            return true;
        }

        private readonly HashSet<int> down = new();
        private float hpLowest = 1f;

        private void CountFallen()
        {
            int hp = 0;
            int max = 0;
            foreach (var ally in State.Allies)
                if (ally.Present)
                {
                    hp += Math.Max(0, ally.Hp);
                    max += ally.MaxHp;
                }
            if (max > 0)
                hpLowest = Math.Min(hpLowest, hp / (float)max);
            for (int i = 0; i < State.Allies.Count; i++)
            {
                bool isDown = State.Allies[i].Present && State.Allies[i].Hp <= 0;
                if (isDown && down.Add(i))
                    fell++;
                else if (!isDown)
                    down.Remove(i);
            }
        }

        private int EnemyHp()
        {
            int total = 0;
            foreach (var enemy in State.Enemies)
                total += Math.Max(0, enemy.Hp);
            return total;
        }

        private int PartyHp()
        {
            int total = 0;
            foreach (var ally in State.Allies)
                total += Math.Max(0, ally.Hp);
            return total;
        }

        private static float Share(BattleFighter fighter) =>
            fighter.MaxHp > 0 ? fighter.Hp / (float)fighter.MaxHp : 0f;
    }

    /// <summary>
    /// The careful choice of the auto-battle (the battle screen's <c>CarefulBattlePolicy</c>):
    /// bring a fallen ally back, heal one under half its HP, guard while hurt, else attack with
    /// the costliest card, on an enemy whose weakness it hits, else the one with the least HP.
    /// </summary>
    public sealed class CarefulSimPolicy : IBattleSimPolicy
    {
        public const float HurtShare = 0.5f;

        public (int Card, int Target) Choose(BattleSimulation battle)
        {
            var state = battle.State;
            bool down = false;
            int weakest = -1;
            float lowest = float.MaxValue;
            for (int i = 0; i < state.Allies.Count; i++)
            {
                var ally = state.Allies[i];
                if (!ally.Present)
                    continue;
                if (ally.Hp <= 0)
                {
                    down = true;
                    continue;
                }
                float share = ally.Hp / (float)Math.Max(1, ally.MaxHp);
                if (share < lowest)
                {
                    lowest = share;
                    weakest = i;
                }
            }
            bool hurt = lowest < HurtShare;
            int pick = -1;
            int best = int.MinValue;
            for (int place = 0; place < battle.Hand.Count; place++)
            {
                int card = battle.Hand[place];
                int cost = battle.CostOf(card);
                if (cost > state.Energy)
                    continue;
                var (skill, user) = battle.CardOf(card);
                if (!state.Stands(BattleUnit.Ally(user)))
                    continue;
                int score = Score(state, skill, user, cost, hurt, down);
                if (score > best)
                {
                    best = score;
                    pick = place;
                }
            }
            if (pick < 0 || best == int.MinValue)
                return (-1, -1);
            var (chosen, by) = battle.CardOf(battle.Hand[pick]);
            return (pick, TargetOf(state, chosen, by, weakest));
        }

        private static int Score(
            BattleState state,
            CardSkill skill,
            int user,
            int cost,
            bool hurt,
            bool down
        )
        {
            if (Revives(skill))
                return down ? 2000 + cost : int.MinValue;
            return skill.Kind switch
            {
                CardKind.Heal => hurt ? 1000 + cost : int.MinValue,
                CardKind.Guard => hurt ? 900 + cost : 200 + cost,
                CardKind.Attack => 500
                    + cost * 10
                    + Math.Min(99, PowerOf(state, skill, user) / 100),
                _ => 300 + cost,
            };
        }

        private static int PowerOf(BattleState state, CardSkill skill, int user)
        {
            foreach (var action in skill.Actions)
                if (action.HasPower)
                    return state.CardPower(action, BattleUnit.Ally(user));
            return 0;
        }

        private static bool Revives(CardSkill skill)
        {
            foreach (var action in skill.Actions)
                if (action.Kind == CardActionKind.Revive)
                    return true;
            return false;
        }

        private static int TargetOf(BattleState state, CardSkill skill, int user, int weakest)
        {
            switch (skill.Target)
            {
                case CardTarget.AllEnemies or CardTarget.AllAllies or CardTarget.None:
                    return -1;
                case CardTarget.Self:
                    return user;
                case CardTarget.OneEnemy or CardTarget.RandomEnemies or CardTarget.ChainEnemies:
                {
                    int target = -1;
                    bool targetWeak = false;
                    for (int i = 0; i < state.Enemies.Count; i++)
                    {
                        var enemy = state.Enemies[i];
                        if (enemy.Hp <= 0)
                            continue;
                        bool weak =
                            state.KnowWeaknesses
                            && state.IsWeak(
                                BattleUnit.Enemy(i),
                                skill.Element,
                                BattleUnit.Ally(user)
                            );
                        if (
                            target < 0
                            || (weak && !targetWeak)
                            || (weak == targetWeak && enemy.Hp < state.Enemies[target].Hp)
                        )
                        {
                            target = i;
                            targetWeak = weak;
                        }
                    }
                    return target;
                }
            }
            if (Revives(skill))
            {
                for (int i = 0; i < state.Allies.Count; i++)
                    if (state.Allies[i].Present && state.Allies[i].Hp <= 0)
                        return i;
            }
            return weakest >= 0 ? weakest : user;
        }
    }
}
