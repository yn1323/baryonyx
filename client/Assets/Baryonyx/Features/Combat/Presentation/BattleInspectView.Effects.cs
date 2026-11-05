using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The battle's effects (doc/plans/2026-11-03-battle-effect-system.md): the passives, the
    /// effects the party brings in and a card's own while it plays. The battle tells them what
    /// happens (<see cref="Fire"/>) and asks them for its numbers (<see cref="Modify"/>), and they
    /// read and change the battle through this view as an <see cref="IBattleWorld"/>. An effect's
    /// name pops up over its holder as it works.
    /// </summary>
    public sealed partial class BattleInspectView : IBattleWorld
    {
        // 効果（パッシブ・装備）と戦闘後の強化の名前を出す色。
        private static readonly Color PassiveColor = new(0.92f, 0.82f, 1f);

        private readonly BattleEffects effects = CreateEffects();

        // 星図の計算などで、次の味方のターンへ持ち越すエネルギー。
        private int carriedEnergy;

        // 使っているカードの1枚（その間に起きたことの記録）。カードの外ではnull。
        private BattleCardPlay cardPlay;

        /// <summary>The effects held in this battle.</summary>
        public BattleEffects Effects => effects;

        /// <summary>The energy carried into the next party turn (星図の計算).</summary>
        public int CarriedEnergy => carriedEnergy;

        private static BattleEffects CreateEffects()
        {
            var created = new BattleEffects();
            created.TooDeep += message => Debug.LogWarning($"[BattleEffects] {message}");
            return created;
        }

        /// <summary>
        /// Gives each place the effects it holds (in place order; null or empty for none), the
        /// passives first. The counts start over with the battle.
        /// </summary>
        private void UseEffects(IReadOnlyList<BattleEffect>[] byPlace)
        {
            // 味方の効果だけを入れ替え、敵の効果（敵の特性）は残す。
            effects.RemoveOf(unit => unit.IsAlly);
            for (int place = 0; place < byPlace.Length; place++)
                if (byPlace[place] != null)
                    foreach (var effect in byPlace[place])
                        if (effect != null)
                            effects.Add(BattleUnit.Ally(place), effect);
            StartEffects();
        }

        /// <summary>The battle's counts start over (its cards are dealt anew).</summary>
        private void StartEffects()
        {
            carriedEnergy = 0;
            effects.StartBattle();
        }

        private int Modify(BattleValue value, int amount, BattleEvent moment, bool apply = true) =>
            effects.Modify(apply ? this : QuietOf(this), value, amount, moment);

        private BattleEvent Fire(BattleTrigger trigger, BattleEvent moment) =>
            effects.Fire(this, trigger, moment);

        private static BattleUnit AllyAt(int place) => BattleUnit.Ally(place);

        private static BattleUnit EnemyAt(int index) => BattleUnit.Enemy(index);

        // --- Cards ---------------------------------------------------------------------------

        /// <summary>
        /// A card is about to play (<see cref="CardPlays"/>): its own effects join for its play,
        /// the effects are told, and the card comes back as it plays (with more hits, 連ね矢).
        /// </summary>
        private CardSkill BeginCard(CardSkill skill, int caster)
        {
            var (play, played) = CardPlays.Begin(this, effects, skill, AllyAt(caster));
            cardPlay = play;
            beatenByCard.Clear();
            return played;
        }

        /// <summary>A card has played: the effects are told the damage its blows dealt, and its own effects leave.</summary>
        private void EndCard(CardSkill skill)
        {
            if (cardPlay == null)
                return;
            CardPlays.End(this, effects, cardPlay);
            cardPlay = null;
            beatenByCard.Clear();
            Refresh();
        }

        /// <summary>The cost of a card of the deck now, as the effects change it (独り立ち).</summary>
        private int CostWithEffects(BattleInspectCardData data)
        {
            var skill = SkillOf(data);
            return Modify(
                BattleValue.Cost,
                data.Cost,
                new BattleEvent
                {
                    Actor = AllyAt(data.Caster),
                    Card = skill,
                    Element = skill?.Element ?? CardElement.None,
                },
                apply: false
            );
        }

        /// <summary>
        /// The cards in hand follow a change of the effects on their cost, keeping what the cards
        /// played changed of it (a lowered cost stays lowered).
        /// </summary>
        private void RefreshCosts()
        {
            foreach (int index in hand)
            {
                var card = Cards[index];
                if (card.DeckIndex < 0 || card.DeckIndex >= Deck.Length)
                    continue;
                int now = CostWithEffects(Deck[card.DeckIndex]);
                if (card.CostByEffects < 0)
                    card.CostByEffects = now;
                if (now == card.CostByEffects)
                    continue;
                card.Cost = Mathf.Max(0, card.Cost + now - card.CostByEffects);
                card.CostByEffects = now;
                if (card.Face != null)
                    card.Face.SetCost(card.Cost);
            }
        }

        /// <summary>The chance that the ally's attack card plays once more (the equipment's 2回発動 and the effects).</summary>
        private int TwiceChanceOf(BattleInspectCardData data, int caster) =>
            Modify(
                BattleValue.TwiceChance,
                Allies[caster].TwiceChance,
                new BattleEvent
                {
                    Actor = AllyAt(caster),
                    Card = SkillOf(data),
                    Element = ElementOf(data),
                    Kind = BattleDamageKind.Card,
                }
            );

        private static CardElement ElementOf(BattleInspectCardData data) =>
            SkillOf(data)?.Element ?? CardElement.None;

        // --- Blows ---------------------------------------------------------------------------

        private void Landed(IBattleWorld world, BattleEvent blow, int dealt, bool defeated) =>
            CardHits.Landed(world, effects, blow, dealt, defeated);

        /// <summary>A lasting blow of a status the ally at <paramref name="source"/> put on (burn, poison, bleeding).</summary>
        private BattleHitWeight HitLasting(int enemy, int damage, int source, CardStatus status) =>
            HitBy(
                new BattleEvent
                {
                    Actor = AllyAt(source),
                    Target = EnemyAt(enemy),
                    Kind = BattleDamageKind.Status,
                    Status = status,
                },
                damage,
                BattleInspectElement.None,
                lasting: true
            );

        /// <summary>Lands <paramref name="blow"/> on its enemy and tells the effects.</summary>
        private BattleHitWeight HitBy(
            BattleEvent blow,
            int power,
            BattleInspectElement element,
            bool lasting = false
        )
        {
            var enemy = Enemies[blow.Target.Index];
            bool alive = enemy.Alive;
            int before = enemy.Hp;
            var weight = Hit(blow.Target.Index, power, element, lasting: lasting);
            if (alive)
                Landed(this, blow, before - enemy.Hp, !enemy.Alive);
            return weight;
        }

        /// <summary>The enemy's blow on the ally is turned by the effects (かばう); the ally it lands on.</summary>
        private int TargetOf(int target, int attacker)
        {
            if (target < 0)
                return target;
            var aimed = Fire(
                BattleTrigger.EnemyTargeting,
                new BattleEvent
                {
                    Actor = EnemyAt(attacker),
                    Target = AllyAt(target),
                    Kind = BattleDamageKind.Enemy,
                }
            );
            return aimed.Redirect.IsAlly && !aimed.Redirect.IsNone ? aimed.Redirect.Index : target;
        }

        // --- Enemies ---------------------------------------------------------------------------

        /// <summary>The enemy at <paramref name="index"/> holds the effects of its kind (敵の特性) in place of the ones before.</summary>
        private void UseEnemyEffects(int index, IReadOnlyList<BattleEffect> held)
        {
            var enemy = EnemyAt(index);
            effects.RemoveOf(unit => unit == enemy);
            if (held != null)
                foreach (var effect in held)
                    if (effect != null)
                        effects.Add(enemy, effect);
        }

        /// <summary>The enemy uses a skill whose working is an effect (麻痺・毒・防御ダウンを付ける技).</summary>
        private void UseMoveEffect(int index, BattleEffect effect)
        {
            var used = effects.Add(EnemyAt(index), effect);
            try
            {
                Fire(BattleTrigger.Used, new BattleEvent { Actor = EnemyAt(index) });
            }
            finally
            {
                effects.Remove(used);
            }
        }

        /// <summary>
        /// The enemies' turn begins: the burns and poison an enemy put on the allies hurt them,
        /// all at once for each, past their block (as an enemy's do).
        /// </summary>
        private IEnumerator AllyTurnStatuses()
        {
            foreach (var status in new[] { CardStatus.Burn, CardStatus.Poison })
            {
                var ticks = new List<IEnumerator>();
                for (int i = 0; i < Allies.Length; i++)
                {
                    var active = Find(true, i, status);
                    if (active == null || Allies[i].Down)
                        continue;
                    int damage = BattleStatusRules.TickDamage(active);
                    int ally = i;
                    ticks.Add(Tick(status, Allies[i].TargetArea, () => HurtAlly(ally, damage)));
                    if (status == CardStatus.Poison && --active.Amount <= 0)
                        StatusesOf(true, i).Remove(active);
                }
                yield return Together(ticks);
            }
        }

        // 状態のダメージで味方のHPを減らし、倒れたら効果に伝える。
        private void HurtAlly(int ally, int damage)
        {
            int before = Allies[ally].Hp;
            HitAlly(ally, damage, pastBlock: true);
            if (before > 0 && Allies[ally].Down)
                Fire(
                    BattleTrigger.AllyFell,
                    new BattleEvent { Target = AllyAt(ally), Kind = BattleDamageKind.Status }
                );
        }

        /// <summary>The party's turn ends: the statuses the enemies put on the allies count down.</summary>
        private void CountdownAllyStatuses()
        {
            for (int i = 0; i < Allies.Length; i++)
                BattleStatusRules.Countdown(StatusesOf(true, i), fromEnemies: true);
        }

        // --- IBattleWorld ----------------------------------------------------------------------

        bool IBattleWorld.Preview => false;
        int IBattleWorld.Turn => BattleTurn;
        int IBattleWorld.TodayAct => TodayAct;
        int IBattleWorld.Energy => Energy;

        int IBattleWorld.Count(bool allies) => allies ? Allies.Length : Enemies.Length;

        bool IBattleWorld.Stands(BattleUnit unit) => Stands(unit);

        private bool Stands(BattleUnit unit) =>
            unit.IsAlly
                ? unit.Index >= 0 && unit.Index < Allies.Length && !Allies[unit.Index].Down
                : unit.Index >= 0 && unit.Index < Enemies.Length && Enemies[unit.Index].Alive;

        int IBattleWorld.Hp(BattleUnit unit) => HpOf(unit);

        private int HpOf(BattleUnit unit) =>
            !Exists(unit) ? 0
            : unit.IsAlly ? Allies[unit.Index].Hp
            : Enemies[unit.Index].Hp;

        int IBattleWorld.MaxHp(BattleUnit unit) =>
            !Exists(unit) ? 0
            : unit.IsAlly ? Allies[unit.Index].MaxHp
            : Enemies[unit.Index].MaxHp;

        int IBattleWorld.Block(BattleUnit unit) =>
            !Exists(unit) ? 0
            : unit.IsAlly ? BlockOf(unit.Index)
            : Enemies[unit.Index].Block;

        bool IBattleWorld.HasStatus(BattleUnit unit, CardStatus status) =>
            Exists(unit) && HasStatus(unit.IsAlly, unit.Index, status);

        int IBattleWorld.StatusTurns(BattleUnit unit, CardStatus status) =>
            Exists(unit) ? Find(unit.IsAlly, unit.Index, status)?.Turns ?? 0 : 0;

        int IBattleWorld.StatusAmount(BattleUnit unit, CardStatus status) =>
            Exists(unit) ? Find(unit.IsAlly, unit.Index, status)?.Amount ?? 0 : 0;

        void IBattleWorld.StatusesOf(BattleUnit unit, List<CardStatus> into)
        {
            if (!Exists(unit))
                return;
            foreach (var active in StatusesOf(unit.IsAlly, unit.Index))
                into.Add(active.Status);
        }

        bool IBattleWorld.InBattle(BattleUnit unit) =>
            Exists(unit) && (!unit.IsAlly || !Allies[unit.Index].Absent);

        void IBattleWorld.RemoveStatus(BattleUnit unit, CardStatus status)
        {
            if (Exists(unit))
                TakeStatus(unit.IsAlly, unit.Index, status);
        }

        int IBattleWorld.StatusPower(BattleUnit unit, CardStatus status) =>
            Exists(unit) ? Find(unit.IsAlly, unit.Index, status)?.Power ?? 0 : 0;

        int IBattleWorld.Stat(BattleUnit unit, CardStat stat) =>
            !Exists(unit) ? 0
            : unit.IsAlly ? StatOf(unit.Index, stat)
            : stat switch
            {
                CardStat.PhysicalAttack => Enemies[unit.Index].PhysicalAttack,
                CardStat.MagicAttack => Enemies[unit.Index].MagicAttack,
                CardStat.PhysicalDefense => Enemies[unit.Index].PhysicalDefense,
                _ => 0,
            };

        int IBattleWorld.Defense(BattleUnit unit, CardStat stat)
        {
            if (!Exists(unit))
                return 0;
            if (!unit.IsAlly)
                return DefenseOf(Enemies[unit.Index], stat);
            var stats = StatsNow(unit.Index);
            return stat == CardStat.MagicAttack ? stats.MagicDefense : stats.PhysicalDefense;
        }

        int IBattleWorld.Level(BattleUnit unit) =>
            !Exists(unit) ? 1
            : unit.IsAlly ? Allies[unit.Index].Level
            : Enemies[unit.Index].Level;

        int IBattleWorld.Pick(int count) => Dice.Below(count);

        void IBattleWorld.Strike(BattleEvent blow, int power, BattleStrikeLook look)
        {
            if (!Exists(blow.Target))
                return;
            if (blow.Target.IsAlly)
            {
                // 状態のダメージ（毒まわし）はブロックを通らない。
                HitAlly(blow.Target.Index, power, pastBlock: blow.Kind == BattleDamageKind.Status);
                return;
            }
            var element = BattleCardText.ElementOf(blow.Element);
            BattleHitWeight Land() => HitBy(blow, power, element, lasting: blow.Pierce);
            if (look == BattleStrikeLook.Bolt && Vfx != null)
                StartCoroutine(Vfx.CloudStrike(Enemies[blow.Target.Index].TargetArea, Land));
            else
                Land();
        }

        int IBattleWorld.AddBlock(BattleUnit unit, int amount) =>
            Exists(unit) && unit.IsAlly ? AddBlock(unit.Index, amount) : 0;

        void IBattleWorld.Heal(BattleUnit healer, BattleUnit target, int amount)
        {
            if (!Exists(target))
                return;
            if (target.IsAlly)
                HealFrom(target.Index, amount, healer.IsAlly ? healer.Index : -1);
            else
            {
                var enemy = Enemies[target.Index];
                enemy.Hp = Mathf.Min(enemy.MaxHp, enemy.Hp + Mathf.Max(0, amount));
                RefreshEnemy(enemy);
                ShowNumber(HealNumber, enemy.TargetArea, amount.ToString());
            }
        }

        bool IBattleWorld.ApplyStatus(
            BattleUnit source,
            BattleUnit target,
            CardStatus status,
            int turns,
            int chance,
            int amount
        )
        {
            if (!Exists(target))
                return false;
            var action = CardAction
                .Apply(
                    target.IsAlly ? CardTarget.OneAlly : CardTarget.OneEnemy,
                    status,
                    turns,
                    amount
                )
                .WithChance(chance);
            if (
                !target.IsAlly
                && source.IsAlly
                && !Lands(target.Index, action, source.Index, quiet: true)
            )
                return false;
            // 敵が味方に付ける悪い状態は、敵と味方の幸運で動く確率で付く（0なら必ず）。
            if (
                target.IsAlly
                && source.IsEnemy
                && chance > 0
                && CardRules.IsBad(status)
                && !BattleOdds.Lands(
                    CombatFormula.StatusChance(
                        chance,
                        Enemies[source.Index].Luck,
                        Enemies[source.Index].Level,
                        StatsNow(target.Index).Luck
                    )
                )
            )
                return false;
            return PutStatus(source, target.IsAlly, target.Index, action);
        }

        int IBattleWorld.GainEnergy(int amount)
        {
            int before = Energy;
            Energy = Mathf.Max(Energy, Mathf.Min(MaxEnergyCap, Energy + amount));
            Refresh();
            return Energy - before;
        }

        int IBattleWorld.CarryEnergy(int most)
        {
            int more = Mathf.Clamp(Energy - carriedEnergy, 0, Mathf.Max(0, most));
            carriedEnergy += more;
            return more;
        }

        int IBattleWorld.Draw(int count)
        {
            if (count <= 0 || dealing != null)
                return 0;
            dealing = StartCoroutine(Deal(count, 0f));
            return count;
        }

        void IBattleWorld.Announce(BattleEffectInstance effect, BattleUnit at, string detail)
        {
            if (!Exists(at))
                return;
            string text = string.IsNullOrEmpty(detail)
                ? effect.Effect.Name
                : $"{effect.Effect.Name} {detail}";
            var area = at.IsAlly ? Allies[at.Index].TargetArea : Enemies[at.Index].TargetArea;
            // 状態の効果（無敵・反射）は状態の色で大きく、ほかは薄紫で出す。
            var status = effect.Effect.Status;
            if (status != CardStatus.None)
                ShowPopup(area, text, StatusColor(status), 44f);
            else
                ShowPopup(area, text, PassiveColor, 36f);
        }

        private bool Exists(BattleUnit unit) =>
            !unit.IsNone
            && (unit.IsAlly ? unit.Index < Allies.Length : unit.Index < Enemies.Length);

        // --- Asking without using up -----------------------------------------------------------

        private QuietWorld quietWorld;

        private IBattleWorld QuietOf(IBattleWorld world) =>
            world == (IBattleWorld)this ? quietWorld ??= new QuietWorld(this) : world;

        /// <summary>
        /// The battle as it is, asked for a number only to show it (a cost in hand): nothing is
        /// used up and nothing shows, as in a forecast.
        /// </summary>
        private sealed class QuietWorld : IBattleWorld
        {
            private readonly IBattleWorld battle;

            public QuietWorld(IBattleWorld battle) => this.battle = battle;

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

            public void RemoveStatus(BattleUnit unit, CardStatus status) { }

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

            public int GainEnergy(int amount) => 0;

            public int CarryEnergy(int most) => 0;

            public int Draw(int count) => 0;

            public void Announce(BattleEffectInstance effect, BattleUnit at, string detail) { }
        }
    }
}
