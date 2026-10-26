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
        // Damage up and down from statuses (provisional).
        private const float VulnerableBonus = 0.25f;

        /// <summary>The words over an enemy a status missed, and their colour.</summary>
        public const string ResistedText = "効かなかった";
        private static readonly Color ResistedColor = new(0.72f, 0.72f, 0.76f);

        // 凍結が解けて、次に動くまでは凍結にかからない敵。
        private readonly HashSet<int> thawed = new();
        private const float ProtectCut = 0.25f;

        /// <summary>Today's ACT, for the cards that grow with it (a mock value; the battle has no step data yet).</summary>
        [Min(0)]
        public int TodayAct = 6000;

        private readonly Dictionary<int, List<ActiveStatus>> statuses = new();
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

        private sealed class ActiveStatus
        {
            public CardStatus Status;
            public int Turns;

            /// <summary>Poison's stacks, or the part (percent) a buff or debuff changes.</summary>
            public int Amount;

            /// <summary>The damage or healing it does each time it works.</summary>
            public int Power;

            /// <summary>The ally who put it on.</summary>
            public int Source;
        }

        /// <summary>Plays a card skill: its effect from the user, each action landing as its blow does.</summary>
        private IEnumerator UseSkill(
            CardSkill skill,
            BattleInspectCardData card,
            List<int> chosen,
            bool onEnemies,
            int caster
        )
        {
            skill = BeginCardPassives(skill, caster);
            var plan = PlanHits(skill, chosen, onEnemies, caster);
            if (Vfx == null)
            {
                for (int i = 0; i < plan.Count; i++)
                    ApplyHit(skill, plan[i], caster, i == 0 || plan[i].Wave != plan[i - 1].Wave);
                EndCardPassives(skill, caster);
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
            yield return Vfx.PlayCard(
                skill.Id,
                skill.Name,
                user != null ? user.TargetArea : PartyAnchor,
                user != null ? user.Sprite.texture : null,
                skill.Has(CardTrait.CutIn),
                hits,
                AtContact(i =>
                    ApplyHit(skill, plan[i], caster, i == 0 || plan[i].Wave != plan[i - 1].Wave)
                )
            );
            EndCardPassives(skill, caster);
            Refresh();
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
        /// Lands one hit of a card: each of its actions on the character, in order. Tells how
        /// hard the blow hit, so the effect can hit as hard.
        /// </summary>
        private BattleHitWeight ApplyHit(
            CardSkill skill,
            BattleCardPlan.Hit hit,
            int caster,
            bool first
        )
        {
            var weight = BattleHitWeight.Normal;
            bool weak = false;
            foreach (var action in hit.Actions)
            {
                if (
                    action.When != CardCondition.Always
                    && !Holds(action.When, hit.Ally ? -1 : hit.Index, caster, weak)
                )
                    continue;
                if (!hit.Ally && !Enemies[hit.Index].Alive && action.Kind != CardActionKind.Damage)
                    continue;
                switch (action.Kind)
                {
                    case CardActionKind.Damage:
                        if (!hit.Ally)
                        {
                            weak = IsWeak(hit.Index, skill.Element, caster);
                            var dealt = Damage(skill, action, hit.Index, caster, weak);
                            if (dealt != BattleHitWeight.Normal || weight == BattleHitWeight.Normal)
                                weight = dealt;
                        }
                        break;
                    case CardActionKind.Heal:
                        HealFrom(
                            hit.Index,
                            HealWithPassives(Power(action, caster), caster),
                            caster
                        );
                        break;
                    case CardActionKind.Revive:
                    {
                        var ally = Allies[hit.Index];
                        HealAlly(hit.Index, Mathf.RoundToInt(ally.MaxHp * action.Amount / 100f));
                        break;
                    }
                    case CardActionKind.Block:
                    {
                        int block = Power(action, caster);
                        int gained = AddBlock(hit.Index, block);
                        // A block for the party shows once, over the party.
                        if (action.Target == CardTarget.AllAllies)
                        {
                            if (first)
                                ShowPopup(PartyAnchor, $"ブロック +{block}", GuardColor, 48f);
                        }
                        else
                            ShowPopup(
                                Allies[hit.Index].TargetArea,
                                $"ブロック +{gained}",
                                GuardColor,
                                44f
                            );
                        break;
                    }
                    case CardActionKind.Status:
                        if (hit.Ally || Lands(hit.Index, action, caster))
                            AddStatus(hit.Ally, hit.Index, action, caster);
                        break;
                    case CardActionKind.Energy:
                        Energy = Mathf.Min(MaxEnergyCap, Energy + action.Amount);
                        ShowPopup(
                            PopupAreaOf(hit, action, caster),
                            $"エネルギー +{action.Amount}",
                            Gold,
                            42f
                        );
                        break;
                    case CardActionKind.Draw:
                        ShowPopup(
                            PopupAreaOf(hit, action, caster),
                            $"{action.Amount}枚引く",
                            SupportColor,
                            42f
                        );
                        if (dealing == null)
                            dealing = StartCoroutine(Deal(action.Amount, 0f));
                        break;
                    case CardActionKind.CostDown:
                        LowerCosts(action);
                        ShowPopup(
                            PopupAreaOf(hit, action, caster),
                            $"コスト -{action.Amount}",
                            SupportColor,
                            42f
                        );
                        break;
                    case CardActionKind.NextCardFree:
                        nextCardFree = true;
                        nextFreeElements = action.Elements;
                        ShowPopup(
                            PopupAreaOf(hit, action, caster),
                            "次のカード コスト0",
                            Gold,
                            42f
                        );
                        break;
                    case CardActionKind.NextCardBoost:
                        nextBoost = action.Amount;
                        nextBoostElements = action.Elements;
                        ShowPopup(AreaOf(hit), $"次のカード 威力+{action.Amount}%", Gold, 42f);
                        break;
                    case CardActionKind.RevealWeakness:
                    {
                        var enemy = Enemies[hit.Index];
                        foreach (var weakness in enemy.Weaknesses)
                            if (!weakness.Revealed)
                                StartCoroutine(weakness.Reveal(Vfx, () => enemy.Alive));
                        ShowPopup(enemy.TargetArea, "弱点を見抜いた", SupportColor, 40f);
                        break;
                    }
                    case CardActionKind.Delay:
                        ShowPopup(
                            Enemies[hit.Index].TargetArea,
                            "行動が遅れる",
                            StatusColor(CardStatus.Chill),
                            40f
                        );
                        break;
                    case CardActionKind.Cleanse:
                        StatusesOf(true, hit.Index)
                            .RemoveAll(active => CardRules.IsBad(active.Status));
                        if (first)
                            ShowPopup(PartyAnchor, "浄化", SupportColor, 44f);
                        break;
                    case CardActionKind.Dispel:
                        ShowPopup(Enemies[hit.Index].TargetArea, "強化を消した", SupportColor, 40f);
                        break;
                }
            }
            Refresh();
            return weight;
        }

        /// <summary>A card's damage on an enemy, grown by its bonuses and the statuses on both sides.</summary>
        private BattleHitWeight Damage(
            CardSkill skill,
            CardAction action,
            int enemy,
            int caster,
            bool weak
        )
        {
            var target = Enemies[enemy];
            int power = Power(action, caster);
            // シールドバッシュ：使い手のブロックの割合を足す。
            if (action.BlockPercent > 0)
                power += Mathf.RoundToInt(BlockOf(caster) * action.BlockPercent / 100f);
            if (
                action.BonusWhen != CardCondition.Always
                && Holds(action.BonusWhen, enemy, caster, weak)
            )
                power = CardRules.WithBonus(action, power);
            power = CardPowerWithPassives(power, skill, enemy, caster);
            var up = Find(true, caster, CardStatus.AttackUp);
            if (up != null)
                power = Mathf.RoundToInt(power * (1f + up.Amount / 100f));
            if (HasStatus(false, enemy, CardStatus.Vulnerable))
                power = Mathf.RoundToInt(power * (1f + VulnerableBonus));
            power = CombatFormula.Defend(
                power,
                DefenseOf(target, action.Stat),
                Allies[caster].Level
            );
            // Each blow may land as a critical hit, by the user's critical stat (doc/features/combat.md).
            bool critical = BattleOdds.Critical(
                CombatFormula.CriticalChance(StatsNow(caster).Critical, Allies[caster].Level)
            );
            if (critical)
                power = CombatFormula.Critical(power);
            var element = BattleCardText.ElementOf(skill.Element);
            var extra = HasStatus(true, caster, CardStatus.FireBlade)
                ? BattleInspectElement.Fire
                : BattleInspectElement.None;
            int before = target.Hp;
            var weight = Hit(enemy, power, element, extra, critical: critical);
            AfterCardBlow(skill, enemy, caster, before - target.Hp);
            return weight;
        }

        /// <summary>
        /// True when <paramref name="when"/> holds for the card: on the enemy it lands on (-1 when
        /// it lands on an ally), for its user, or for the blow just dealt (<paramref name="weak"/>).
        /// </summary>
        private bool Holds(CardCondition when, int enemy, int caster, bool weak)
        {
            bool On(CardStatus status) => enemy >= 0 && HasStatus(false, enemy, status);
            return when switch
            {
                CardCondition.Always => true,
                CardCondition.OnWeakness => weak,
                CardCondition.OnBurning => On(CardStatus.Burn),
                CardCondition.OnWounded => enemy >= 0
                    && Enemies[enemy].Hp * 2 <= Enemies[enemy].MaxHp,
                CardCondition.OnChilled => On(CardStatus.Chill),
                CardCondition.OnPoisoned => On(CardStatus.Poison),
                CardCondition.OnBleeding => On(CardStatus.Bleed),
                CardCondition.OnVulnerable => On(CardStatus.Vulnerable),
                CardCondition.OnParalyzed => On(CardStatus.Paralysis),
                CardCondition.OnGuarded => BlockOf(caster) > 0,
                _ => false,
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

        private RectTransform AreaOf(BattleCardPlan.Hit hit) =>
            hit.Ally ? Allies[hit.Index].TargetArea : Enemies[hit.Index].TargetArea;

        // 誰にも向かない働き（エネルギー・手札など）は、敵への当たりに載っていても使い手の上に知らせる。
        private RectTransform PopupAreaOf(BattleCardPlan.Hit hit, CardAction action, int caster) =>
            action.Target == CardTarget.None && caster >= 0 && caster < Allies.Length
                ? Allies[caster].TargetArea
                : AreaOf(hit);

        // --- Statuses ------------------------------------------------------------------------

        private List<ActiveStatus> StatusesOf(bool ally, int index)
        {
            int key = ally ? 1000 + index : index;
            if (!statuses.TryGetValue(key, out var list))
                statuses[key] = list = new List<ActiveStatus>();
            return list;
        }

        private ActiveStatus Find(bool ally, int index, CardStatus status)
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
        private void AddStatus(bool ally, int index, CardAction action, int caster)
        {
            if (ally && Purified(index, action.Status))
                return;
            int power = action.HasPower ? Power(action, caster) : 0;
            if (action.Status == CardStatus.Regen)
                power = HealWithPassives(power, caster);
            var (tickStat, tickPercent) = CardRules.TickOf(action.Status);
            if (tickPercent > 0)
                power = Mathf.Max(
                    1,
                    Mathf.RoundToInt(StatOf(caster, tickStat) * tickPercent / 100f)
                );
            var active = Find(ally, index, action.Status);
            if (active == null)
            {
                active = new ActiveStatus { Status = action.Status, Source = caster };
                StatusesOf(ally, index).Add(active);
            }
            active.Turns = Mathf.Max(active.Turns, action.Turns);
            active.Power = Mathf.Max(active.Power, power);
            active.Amount =
                action.Status == CardStatus.Poison
                    ? active.Amount + action.Amount
                    : Mathf.Max(active.Amount, action.Amount);
            var area = ally ? Allies[index].TargetArea : Enemies[index].TargetArea;
            string name = CardRules.StatusName(action.Status);
            if (action.Status == CardStatus.Poison)
                name += $" ×{active.Amount}";
            // A cloud over the battle shows over the enemies' side rather than the user.
            if (action.Status == CardStatus.Thundercloud && Enemies.Length > 0)
                area = Enemies[0].TargetArea;
            ShowPopup(area, name, StatusColor(action.Status), 40f);
        }

        /// <summary>
        /// True when a card's status lands on an enemy (doc/features/combat.md, how easily
        /// statuses land): a bad status lands by its chance moved by the luck of the user and the
        /// enemy, and an enemy that has just come out of a freeze cannot be frozen until it acts
        /// again. A miss shows over the enemy.
        /// </summary>
        private bool Lands(int enemy, CardAction action, int caster)
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
            ShowPopup(Enemies[enemy].TargetArea, ResistedText, ResistedColor, 40f);
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
        /// heals, clouds strike, then every status counts down a turn. Block stays.
        /// </summary>
        private IEnumerator PartyTurnStatuses()
        {
            for (int i = 0; i < Enemies.Length; i++)
            {
                foreach (var status in new[] { CardStatus.Burn, CardStatus.Poison })
                {
                    var active = Find(false, i, status);
                    if (active == null || !Enemies[i].Alive)
                        continue;
                    int damage =
                        status == CardStatus.Poison ? active.Power * active.Amount : active.Power;
                    int enemy = i;
                    yield return Tick(
                        status,
                        Enemies[i].TargetArea,
                        () => Hit(enemy, damage, BattleInspectElement.None, lasting: true)
                    );
                    if (status == CardStatus.Poison && --active.Amount <= 0)
                        StatusesOf(false, i).Remove(active);
                }
            }
            for (int i = 0; i < Enemies.Length; i++)
            {
                var sigil = Find(false, i, CardStatus.BlastSigil);
                if (sigil == null)
                    continue;
                StatusesOf(false, i).Remove(sigil);
                yield return Detonate(i, sigil.Power, sigil.Source);
            }
            yield return EnemyRegen();
            for (int i = 0; i < Allies.Length; i++)
            {
                var regen = Find(true, i, CardStatus.Regen);
                if (regen == null || Allies[i].Down)
                    continue;
                int ally = i;
                yield return Tick(
                    CardStatus.Regen,
                    Allies[i].TargetArea,
                    () => HealFrom(ally, regen.Power, regen.Source)
                );
            }
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
            {
                for (int n = list.Count - 1; n >= 0; n--)
                {
                    var active = list[n];
                    if (
                        active.Status
                        is CardStatus.Poison
                            or CardStatus.Freeze
                            or CardStatus.Reflect
                    )
                        continue;
                    if (--active.Turns <= 0)
                        list.RemoveAt(n);
                }
            }
            Refresh();
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
            if (Vfx == null)
            {
                foreach (int enemy in alive)
                    Hit(enemy, Blast(enemy), BattleInspectElement.Fire);
                yield break;
            }
            var areas = alive.ConvertAll(enemy => Enemies[enemy].TargetArea);
            yield return Vfx.Detonate(
                Enemies[marked].TargetArea,
                areas,
                i => Hit(alive[i], Blast(alive[i]), BattleInspectElement.Fire)
            );
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
            if (Vfx == null)
            {
                Hit(enemy, power, BattleInspectElement.Thunder);
                yield break;
            }
            yield return Vfx.CloudStrike(
                Enemies[enemy].TargetArea,
                () => Hit(enemy, power, BattleInspectElement.Thunder)
            );
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
                () => Hit(enemy, bleed.Power, BattleInspectElement.None, lasting: true)
            );
        }

        /// <summary>An enemy's blow, weakened while it is paralysed.</summary>
        private int EnemyPower(int enemy)
        {
            return BattleBlows.Weakened(
                Enemies[enemy].Power,
                HasStatus(false, enemy, CardStatus.Paralysis)
            );
        }

        /// <summary>
        /// Guards an ally from an enemy's blow: an invincible ally takes nothing, a mirror throws
        /// the blow back at the attacker once, and protection softens it. True when the blow is
        /// stopped.
        /// </summary>
        private bool Guarded(int ally, ref int power, int attacker)
        {
            var area = Allies[ally].TargetArea;
            if (HasStatus(true, ally, CardStatus.Invincible))
            {
                ShowPopup(area, "無敵", StatusColor(CardStatus.Invincible), 44f);
                return true;
            }
            if (attacker >= 0 && TakeStatus(true, ally, CardStatus.Reflect))
            {
                ShowPopup(area, "反射", StatusColor(CardStatus.Reflect), 44f);
                var self = Enemies[attacker];
                Hit(
                    attacker,
                    CombatFormula.Defend(power, self.PhysicalDefense, self.Level),
                    BattleInspectElement.None
                );
                return true;
            }
            if (HasStatus(true, ally, CardStatus.Protect))
                power = Mathf.RoundToInt(power * (1f - ProtectCut));
            return false;
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
                ChargedShield(ally);
            return gained;
        }

        /// <summary>
        /// The party's turn comes round: each ally keeps half of its block (<see cref="CardRules.BlockKept"/>)
        /// and shows what it lost.
        /// </summary>
        private void HalveBlock()
        {
            EnsureBlock();
            for (int i = 0; i < allyBlock.Length; i++)
            {
                int kept = CardRules.BlockKept(allyBlock[i]);
                int lost = allyBlock[i] - kept;
                if (lost <= 0)
                    continue;
                allyBlock[i] = kept;
                RefreshBlock(i);
                ShowPopup(Allies[i].TargetArea, $"ブロック -{lost}", GuardColor, 36f);
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
            ShowPopup(Allies[ally].TargetArea, $"ブロック -{absorbed}", GuardColor, 36f);
            return damage - absorbed;
        }

        private static readonly Color Gold = new(1f, 0.86f, 0.42f);
        private static readonly Color SupportColor = new(0.78f, 0.86f, 0.95f);

        /// <summary>The colour a status's name pops up in.</summary>
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
