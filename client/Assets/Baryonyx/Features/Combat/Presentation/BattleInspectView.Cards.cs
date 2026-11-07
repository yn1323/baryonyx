using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The card skills' effects in the mock battle (provisional rules: doc/features/combat.md,
    /// card statuses). A card's actions land target by target as its effect plays: damage (with
    /// its bonuses, the user's attack up, a fire blade and the target's weakness), healing,
    /// block, statuses, energy, draws, lowered costs, a free next card and revealed weaknesses.
    /// Statuses last a number of party turns: burns, poison, sigils, clouds and regen work as the
    /// party's turn comes round; freezing, paralysis, bleeding, taunts, reflection, protection and
    /// invincibility change the enemies' blows. Chill, speed up and delays only show (they do not
    /// change the turn order yet); block carries over, halved as each party turn begins.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        /// <summary>The words over an enemy a status missed, and their colour.</summary>
        public const string ResistedText = "効かなかった";
        private static readonly Color ResistedColor = new(0.72f, 0.72f, 0.76f);

        // 凍結が解けて、次に動くまでは凍結にかからない敵。
        private readonly HashSet<int> thawed = new();

        /// <summary>Today's ACT, for the cards that grow with it (a mock value; the battle has no step data yet).</summary>
        [Min(0)]
        public int TodayAct = 6000;

        private readonly Dictionary<int, List<BattleStatusEntry>> statuses = new();
        private int[] allyBlock = Array.Empty<int>();
        private bool nextCardFree;

        /// <summary>True while the next card played costs nothing (天啓).</summary>
        public bool NextCardFree => nextCardFree;

        /// <summary>The block an ally holds now (it lasts until the party's next turn).</summary>
        public int BlockOf(int ally) => ally >= 0 && ally < allyBlock.Length ? allyBlock[ally] : 0;

        /// <summary>True when the ally (or enemy) has <paramref name="status"/> now.</summary>
        public bool HasStatus(bool ally, int index, CardStatus status) =>
            Find(ally, index, status) != null;

        /// <summary>The energy a card in hand costs now: nothing when the next card is free.</summary>
        public int CostOf(BattleInspectCard card) =>
            nextCardFree && Covers(nextFreeElements, CardElementOf(card)) ? 0 : card.Cost;

        // 画面のカードの属性（カードスキルの属性。スキルのないモックのカードは属性なし）。
        private CardElement CardElementOf(BattleInspectCard card) =>
            card.DeckIndex >= 0 && card.DeckIndex < Deck.Length
                ? SkillOf(Deck[card.DeckIndex])?.Element ?? CardElement.None
                : CardElement.None;

        /// <summary>The stat of an ally that a power is a part of.</summary>
        public int StatOf(int ally, CardStat stat) =>
            ally >= 0 && ally < Allies.Length ? StatOf(StatsNow(ally), stat) : 0;

        /// <summary>Plays a card skill: its effect from the user, each action landing as its blow does.</summary>
        private IEnumerator UseSkill(
            CardSkill skill,
            BattleInspectCardData card,
            List<int> chosen,
            bool onEnemies,
            int caster
        )
        {
            skill = BeginCard(skill, caster);
            var plan = PlanHits(skill, chosen, onEnemies, caster);
            if (Vfx == null)
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    Retarget(plan, null, i);
                    ApplyHit(skill, plan[i], caster, i == 0 || plan[i].Wave != plan[i - 1].Wave);
                }
                EndCard(skill);
                Refresh();
                yield break;
            }
            var hits = new List<BattleSkillHit>();
            foreach (var hit in plan)
                hits.Add(
                    new BattleSkillHit(
                        hit.Ally ? Allies[hit.Index].TargetArea : Enemies[hit.Index].TargetArea,
                        hit.Ally,
                        hit.Wave
                    )
                );
            var user = caster >= 0 ? Allies[caster] : null;
            // An enemy beaten by one of its blows stays until the last has landed (HitReaction).
            holdingDefeats++;
            try
            {
                yield return Vfx.PlayCard(
                    skill.Id,
                    skill.Name,
                    user != null ? user.TargetArea : PartyAnchor,
                    user != null ? user.Sprite.texture : null,
                    skill.Has(CardTrait.CutIn),
                    hits,
                    AtContact(i =>
                    {
                        Retarget(plan, hits, i);
                        blowCritical = false;
                        var weight = ApplyHit(
                            skill,
                            plan[i],
                            caster,
                            i == 0 || plan[i].Wave != plan[i - 1].Wave
                        );
                        // 会心の一撃と連撃の最後の1発は、技の演出の止めと揺れに重みを足す。
                        Vfx.Accent(blowCritical, BattleCardPlan.Finishes(plan, i));
                        for (int j = i + 1; j < plan.Count; j++)
                            Retarget(plan, hits, j);
                        return weight;
                    })
                );
            }
            finally
            {
                holdingDefeats--;
            }
            EndCard(skill);
            Refresh();
        }

        /// <summary>
        /// A blow on an enemy picked at random (or by a chain) whose enemy is already beaten goes
        /// to another still standing instead, picked at random too (and, in a chain, not the one
        /// the blow before hit when another can be). Blows on a chosen enemy, or on all, stay.
        /// <paramref name="hits"/> (the effect's) follows, for the blows it has not drawn yet.
        /// </summary>
        private void Retarget(List<BattleCardPlan.Hit> plan, List<BattleSkillHit> hits, int index)
        {
            var hit = plan[index];
            if (hit.Ally || Enemies[hit.Index].Alive || !hit.AtRandom)
                return;
            var standing = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Alive)
                    standing.Add(i);
            if (standing.Count == 0)
                return;
            int before = index > 0 && !plan[index - 1].Ally ? plan[index - 1].Index : -1;
            if (hit.Chained && standing.Count > 1)
                standing.Remove(before);
            hit.Index = standing[Dice.Below(standing.Count)];
            if (hits != null)
                hits[index] = new BattleSkillHit(Enemies[hit.Index].TargetArea, false, hit.Wave);
        }

        /// <summary>Who each action of a card lands on, among the enemies and allies still standing.</summary>
        private List<BattleCardPlan.Hit> PlanHits(
            CardSkill skill,
            List<int> chosen,
            bool onEnemies,
            int caster
        )
        {
            var enemies = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Alive)
                    enemies.Add(i);
            var allies = new List<int>();
            for (int i = 0; i < Allies.Length; i++)
                if (!Allies[i].Down)
                    allies.Add(i);
            return BattleCardPlan.Plan(
                skill,
                enemies,
                allies,
                chosen.Count > 0 ? chosen[0] : -1,
                onEnemies,
                caster >= 0 ? caster : 0,
                count => Dice.Below(count)
            );
        }

        /// <summary>
        /// Lands one hit of a card: each of its actions on the character, in order, as
        /// <see cref="CardHits"/> works them out for the battle and its forecast alike. Tells how
        /// hard the blow hit, so the effect can hit as hard.
        /// </summary>
        private BattleHitWeight ApplyHit(
            CardSkill skill,
            BattleCardPlan.Hit hit,
            int caster,
            bool first
        )
        {
            var weight = CardHits.Land(this, effects, cardPlay, skill, hit, caster, first);
            Refresh();
            return weight switch
            {
                BattleBlowWeight.Defeat => BattleHitWeight.Defeat,
                BattleBlowWeight.Weak => BattleHitWeight.Weak,
                _ => BattleHitWeight.Normal,
            };
        }

        // 眠り札の段と次のカードの上乗せ（cardBoost）は、使っているカードの威力・回復量・ブロックに掛かる。
        private int Power(CardAction action, int caster) =>
            Mathf.RoundToInt(
                CardRules.Power(action, StatOf(caster, action.Stat), TodayAct)
                    * (1f + cardBoost / 100f)
            );

        /// <summary>
        /// The enemy's defense against a power worked out from <paramref name="stat"/>: 属防 for
        /// a spell (from 属攻), 物防 for a blow (from 物攻 or 物防).
        /// </summary>
        private static int DefenseOf(BattleInspectEnemy enemy, CardStat stat) =>
            stat == CardStat.MagicAttack ? enemy.MagicDefense : enemy.PhysicalDefense;

        /// <summary>True when the blow of the card's element (or the user's fire blade) hits a weakness.</summary>
        private bool IsWeak(int enemy, CardElement element, int caster)
        {
            var mine = BattleCardText.ElementOf(element);
            bool fire = HasStatus(true, caster, CardStatus.FireBlade);
            foreach (var weakness in Enemies[enemy].Weaknesses)
                if (
                    (mine != BattleInspectElement.None && weakness.Element == mine)
                    || (fire && weakness.Element == BattleInspectElement.Fire)
                )
                    return true;
            return false;
        }

        private void HealAlly(int index, int amount)
        {
            var ally = Allies[index];
            ally.Hp = Mathf.Min(ally.MaxHp, ally.Hp + amount);
            RefreshAlly(ally);
            ShowNumber(HealNumber, ally.TargetArea, amount.ToString());
            ShowBoostsTurnedOn();
        }

        /// <summary>
        /// <paramref name="healer"/> heals an ally, and the effects are told how much and how much
        /// went past the max HP (あふれる祈り turns it into block).
        /// </summary>
        private void HealFrom(int target, int amount, int healer)
        {
            var ally = Allies[target];
            int overflow = CombatPassiveRules.Overflow(ally.Hp, ally.MaxHp, amount);
            HealAlly(target, amount);
            Fire(
                BattleTrigger.Healed,
                new BattleEvent
                {
                    Actor = AllyAt(healer),
                    Target = AllyAt(target),
                    Amount = amount,
                    Overflow = overflow,
                    Play = cardPlay,
                }
            );
        }

        /// <summary>Lowers the cost of the cards in hand of the action's elements, down to 0.</summary>
        private void LowerCosts(CardAction action)
        {
            foreach (int index in hand)
            {
                var card = Cards[index];
                if (Array.IndexOf(action.Elements, BattleCardText.ElementOf(card.Element)) < 0)
                    continue;
                card.Cost = Mathf.Max(0, card.Cost - action.Amount);
                if (card.Face != null)
                    card.Face.SetCost(card.Cost);
            }
        }

        // --- Statuses ------------------------------------------------------------------------

        private List<BattleStatusEntry> StatusesOf(bool ally, int index)
        {
            int key = ally ? 1000 + index : index;
            if (!statuses.TryGetValue(key, out var list))
                statuses[key] = list = new List<BattleStatusEntry>();
            return list;
        }

        private BattleStatusEntry Find(bool ally, int index, CardStatus status)
        {
            if (index < 0)
                return null;
            foreach (var active in StatusesOf(ally, index))
                if (active.Status == status)
                    return active;
            return null;
        }

        /// <summary>Takes <paramref name="status"/> off the character, telling whether it had it.</summary>
        private bool TakeStatus(bool ally, int index, CardStatus status)
        {
            var active = Find(ally, index, status);
            if (active == null)
                return false;
            StatusesOf(ally, index).Remove(active);
            return true;
        }

        /// <summary>
        /// Puts a card's status on a character (again: the longer turns and the stronger power
        /// stay, and poison stacks up), and shows its name over it.
        /// </summary>
        private bool AddStatus(bool ally, int index, CardAction action, int caster) =>
            PutStatus(AllyAt(caster), ally, index, action);

        /// <summary>
        /// <paramref name="from"/> (an ally, an enemy or no one) puts the action's status on a
        /// character, unless an effect stops it (浄めの誓い). A lasting status's damage comes from
        /// the stat of the one who put it on. True when it is on.
        /// </summary>
        private bool PutStatus(BattleUnit from, bool ally, int index, CardAction action)
        {
            int caster = from.IsAlly ? from.Index : -1;
            var applying = new BattleEvent
            {
                Actor = from,
                Target = ally ? AllyAt(index) : EnemyAt(index),
                Status = action.Status,
                Play = cardPlay,
            };
            // 浄めの誓いなど、効果が付く前に取り消すことがある。
            if (Fire(BattleTrigger.StatusApplying, applying).Cancelled)
                return false;
            int power = action.HasPower ? Power(action, caster) : 0;
            if (action.Status == CardStatus.Regen && caster >= 0)
                power = CardHits.HealPower(
                    this,
                    effects,
                    cardPlay,
                    null,
                    power,
                    AllyAt(caster),
                    ally ? AllyAt(index) : BattleUnit.None
                );
            var (tickStat, _) = CardRules.TickOf(action.Status);
            int tick = BattleStatusRules.TickPower(
                action.Status,
                from.IsEnemy ? ((IBattleWorld)this).Stat(from, tickStat) : StatOf(caster, tickStat)
            );
            if (tick > 0)
                power = tick;
            var active = BattleStatusRules.Put(
                StatusesOf(ally, index),
                action.Status,
                action.Turns,
                power,
                action.Amount,
                caster,
                ally && from.IsEnemy ? from.Index : -1
            );
            var area = ally ? Allies[index].TargetArea : Enemies[index].TargetArea;
            string name = CardRules.StatusName(action.Status);
            if (action.Status == CardStatus.Poison)
                name += $" ×{active.Amount}";
            // A cloud over the battle shows over the enemies' side rather than the user.
            if (action.Status == CardStatus.Thundercloud && Enemies.Length > 0)
                area = Enemies[0].TargetArea;
            ShowStatusNote(area, name, StatusColor(action.Status));
            Fire(
                BattleTrigger.StatusApplied,
                new BattleEvent
                {
                    Actor = applying.Actor,
                    Target = applying.Target,
                    Status = action.Status,
                    Play = cardPlay,
                }
            );
            return true;
        }

        /// <summary>
        /// True when a card's status lands on an enemy (doc/features/combat.md, how easily
        /// statuses land): a bad status lands by its chance moved by the luck of the user and the
        /// enemy, and an enemy that has just come out of a freeze cannot be frozen until it acts
        /// again. A miss shows over the enemy.
        /// </summary>
        private bool Lands(int enemy, CardAction action, int caster, bool quiet = false)
        {
            if (!CardRules.IsBad(action.Status))
                return true;
            bool guarded = action.Status == CardStatus.Freeze && thawed.Contains(enemy);
            if (!guarded && action.Chance <= 0)
                return true;
            if (
                !guarded
                && BattleOdds.Lands(
                    CombatFormula.StatusChance(
                        action.Chance,
                        StatsNow(caster).Luck,
                        Allies[caster].Level,
                        Enemies[enemy].Luck
                    )
                )
            )
                return true;
            if (!quiet)
                ShowStatusNote(Enemies[enemy].TargetArea, ResistedText, ResistedColor);
            return false;
        }

        /// <summary>
        /// True when the enemy is frozen and loses its action, which thaws it. Until it next acts,
        /// it cannot be frozen again, so no enemy stays frozen turn after turn.
        /// </summary>
        private bool Frozen(int index)
        {
            if (TakeStatus(false, index, CardStatus.Freeze))
            {
                thawed.Add(index);
                return true;
            }
            thawed.Remove(index);
            return false;
        }

        /// <summary>Forgets every status of one side (the enemies, or the allies).</summary>
        private void ClearStatuses(bool allies)
        {
            if (!allies)
                thawed.Clear();
            int count = allies ? Allies.Length : Enemies.Length;
            for (int i = 0; i < count; i++)
                StatusesOf(allies, i).Clear();
        }

        /// <summary>
        /// The start of a party turn: burns and poison hurt the enemies, sigils blow up, regen
        /// heals, clouds strike, then every status counts down a turn. Block stays. Each of the
        /// burns, the poison and the regen works on everyone it is on at once, not one by one
        /// (2026-10-29).
        /// </summary>
        private IEnumerator PartyTurnStatuses()
        {
            foreach (var status in new[] { CardStatus.Burn, CardStatus.Poison })
            {
                var ticks = new List<IEnumerator>();
                for (int i = 0; i < Enemies.Length; i++)
                {
                    var active = Find(false, i, status);
                    if (active == null || !Enemies[i].Alive)
                        continue;
                    int damage = BattleStatusRules.TickDamage(active);
                    int enemy = i;
                    int source = active.Source;
                    ticks.Add(
                        Tick(
                            status,
                            Enemies[i].TargetArea,
                            () => HitLasting(enemy, damage, source, status)
                        )
                    );
                    if (status == CardStatus.Poison && --active.Amount <= 0)
                        StatusesOf(false, i).Remove(active);
                }
                yield return Together(ticks);
            }
            for (int i = 0; i < Enemies.Length; i++)
            {
                var sigil = Find(false, i, CardStatus.BlastSigil);
                if (sigil == null)
                    continue;
                StatusesOf(false, i).Remove(sigil);
                yield return Detonate(i, sigil.Power, sigil.Source);
            }
            var regens = EnemyRegen();
            for (int i = 0; i < Allies.Length; i++)
            {
                var regen = Find(true, i, CardStatus.Regen);
                if (regen == null || Allies[i].Down)
                    continue;
                int ally = i;
                regens.Add(
                    Tick(
                        CardStatus.Regen,
                        Allies[i].TargetArea,
                        () => HealFrom(ally, regen.Power, regen.Source)
                    )
                );
            }
            yield return Together(regens);
            for (int i = 0; i < Allies.Length; i++)
            {
                var cloud = Find(true, i, CardStatus.Thundercloud);
                if (cloud == null)
                    continue;
                yield return CloudStrike(cloud.Power, cloud.Source);
            }

            // Every lasting status counts down; poison wanes by its stacks, and freezing and
            // reflection wait to be used.
            foreach (var list in statuses.Values)
                BattleStatusRules.Countdown(list);
            Refresh();
        }

        /// <summary>Plays <paramref name="routines"/> side by side and ends when the last has.</summary>
        private IEnumerator Together(List<IEnumerator> routines)
        {
            int running = routines.Count;
            foreach (var routine in routines)
                StartCoroutine(Then(routine, () => running--));
            while (running > 0)
                yield return null;
        }

        private static IEnumerator Then(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }

        private IEnumerator Tick(CardStatus status, RectTransform target, Action land)
        {
            if (Vfx == null)
            {
                land();
                yield break;
            }
            yield return Vfx.StatusTick(status, target, land);
        }

        /// <summary>The sigil's blast on every enemy, a spell each enemy's 属防 softens.</summary>
        private IEnumerator Detonate(int marked, int power, int caster)
        {
            int Blast(int enemy) =>
                CombatFormula.Defend(power, Enemies[enemy].MagicDefense, Allies[caster].Level);
            var alive = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Alive)
                    alive.Add(i);
            BattleHitWeight Land(int enemy) =>
                HitBy(
                    Lasting(enemy, caster, CardStatus.BlastSigil),
                    Blast(enemy),
                    BattleInspectElement.Fire
                );
            if (Vfx == null)
            {
                foreach (int enemy in alive)
                    Land(enemy);
                yield break;
            }
            var areas = alive.ConvertAll(enemy => Enemies[enemy].TargetArea);
            yield return Vfx.Detonate(Enemies[marked].TargetArea, areas, i => Land(alive[i]));
        }

        /// <summary>The cloud's bolt on an enemy at random, a spell its 属防 softens.</summary>
        private IEnumerator CloudStrike(int power, int caster)
        {
            var alive = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Alive)
                    alive.Add(i);
            if (alive.Count == 0)
                yield break;
            int enemy = Dice.Pick(alive);
            power = CombatFormula.Defend(power, Enemies[enemy].MagicDefense, Allies[caster].Level);
            BattleHitWeight Land() =>
                HitBy(
                    Lasting(enemy, caster, CardStatus.Thundercloud),
                    power,
                    BattleInspectElement.Thunder
                );
            if (Vfx == null)
            {
                Land();
                yield break;
            }
            yield return Vfx.CloudStrike(Enemies[enemy].TargetArea, Land);
        }

        /// <summary>A bleeding enemy loses blood after it moves.</summary>
        private IEnumerator Bleed(int enemy)
        {
            var bleed = Find(false, enemy, CardStatus.Bleed);
            if (bleed == null || !Enemies[enemy].Alive)
                yield break;
            yield return Tick(
                CardStatus.Bleed,
                Enemies[enemy].TargetArea,
                () => HitLasting(enemy, bleed.Power, bleed.Source, CardStatus.Bleed)
            );
        }

        // 状態が起こすダメージ（爆炎の刻印・雷雲）の、効果に伝える記録。
        private static BattleEvent Lasting(int enemy, int source, CardStatus status) =>
            new()
            {
                Actor = AllyAt(source),
                Target = EnemyAt(enemy),
                Kind = BattleDamageKind.Status,
                Status = status,
            };

        /// <summary>An enemy's blow, weakened while it is paralysed.</summary>
        private int EnemyPower(int enemy)
        {
            return BattleBlows.Weakened(
                Enemies[enemy].Power,
                HasStatus(false, enemy, CardStatus.Paralysis)
            );
        }

        private void EnsureBlock()
        {
            if (allyBlock.Length != Allies.Length)
                allyBlock = new int[Allies.Length];
        }

        /// <summary>
        /// Adds to an ally's block, up to the ally's max HP, and returns how much it grew. The
        /// block stays from turn to turn until damage takes it, halved as each party turn begins.
        /// </summary>
        private int AddBlock(int ally, int amount)
        {
            EnsureBlock();
            int before = allyBlock[ally];
            allyBlock[ally] = Mathf.Min(Allies[ally].MaxHp, before + Mathf.Max(0, amount));
            RefreshBlock(ally);
            int gained = allyBlock[ally] - before;
            if (gained > 0)
                Fire(
                    BattleTrigger.GainedBlock,
                    new BattleEvent
                    {
                        Target = AllyAt(ally),
                        Amount = gained,
                        Play = cardPlay,
                    }
                );
            return gained;
        }

        /// <summary>
        /// The party's turn comes round: each ally keeps half of its block (<see cref="CardRules.BlockKept"/>),
        /// and the yellow strip shrinks to it.
        /// </summary>
        private void HalveBlock()
        {
            EnsureBlock();
            for (int i = 0; i < allyBlock.Length; i++)
            {
                int kept = CardRules.BlockKept(allyBlock[i]);
                if (kept >= allyBlock[i])
                    continue;
                allyBlock[i] = kept;
                RefreshBlock(i);
            }
        }

        /// <summary>Takes every ally's block away.</summary>
        private void ClearBlock()
        {
            EnsureBlock();
            Array.Clear(allyBlock, 0, allyBlock.Length);
            for (int i = 0; i < Allies.Length; i++)
                RefreshBlock(i);
        }

        /// <summary>Shows an ally's block as the yellow strip over the HP bar.</summary>
        private void RefreshBlock(int ally) =>
            Allies[ally].Gauge.SetBlock(BlockOf(ally), Allies[ally].MaxHp);

        /// <summary>The damage left after an ally's block takes what it can.</summary>
        private int Absorb(int ally, int damage)
        {
            EnsureBlock();
            if (ally < 0 || ally >= allyBlock.Length || allyBlock[ally] <= 0)
                return damage;
            int absorbed = BattleBlows.Soaked(allyBlock[ally], damage);
            allyBlock[ally] -= absorbed;
            RefreshBlock(ally);
            return damage - absorbed;
        }


        /// <summary>The colour a status's name shows in.</summary>
        public static Color StatusColor(CardStatus status) =>
            status switch
            {
                CardStatus.Burn or CardStatus.BlastSigil => new Color(1f, 0.6f, 0.25f),
                CardStatus.Poison => new Color(0.65f, 1f, 0.4f),
                CardStatus.Bleed => new Color(1f, 0.4f, 0.38f),
                CardStatus.Chill or CardStatus.Freeze or CardStatus.Reflect => new Color(
                    0.6f,
                    0.88f,
                    1f
                ),
                CardStatus.Paralysis or CardStatus.Thundercloud or CardStatus.SpeedUp => new Color(
                    1f,
                    0.9f,
                    0.4f
                ),
                CardStatus.Vulnerable => new Color(0.82f, 0.62f, 1f),
                CardStatus.Regen => new Color(0.6f, 1f, 0.5f),
                CardStatus.FireBlade or CardStatus.AttackUp or CardStatus.Taunt => new Color(
                    1f,
                    0.72f,
                    0.4f
                ),
                _ => new Color(0.6f, 0.78f, 1f),
            };
    }
}
