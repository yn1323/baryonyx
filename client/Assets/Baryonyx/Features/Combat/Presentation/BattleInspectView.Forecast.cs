using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The forecast of a raised card on the HP bars (doc/features/screens.md, the battle mock):
    /// the HP each target would lose or get back and the block it would gain, worked out the way
    /// the card plays but without changing anyone. Every target the card can pick shows its own
    /// forecast at once, as a tap leaves no time to point at one first.
    /// </summary>
    /// <remarks>
    /// Some of it cannot be known before the card plays, and the forecast leaves that out: a
    /// weakness not yet revealed (the forecast must not give it away), the passives that roll a
    /// chance, and where random hits land. A card that hits at random forecasts a single hit on
    /// each enemy and never promises a defeat.
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

        // True while a forecast plays the card through, so the passives it meets stay quiet.
        private bool forecastingCard;

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
            skill = ForecastPassives(skill, caster, out int bonus);

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

            int saved = cardBonusPercent;
            forecastingCard = true;
            try
            {
                cardBonusPercent = bonus;
                for (int p = 0; p < picks.Count; p++)
                {
                    int pick = picks[p];
                    var run = PlayThrough(skill, pick, random ? true : onEnemies, caster, random);
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
                forecastingCard = false;
                cardBonusPercent = saved;
            }
            return (enemies, allies);
        }

        /// <summary>
        /// The card as the passives would change it before it plays (連ね矢's extra hit), and the
        /// bonus they would give it (居合の型), without using them up.
        /// </summary>
        private CardSkill ForecastPassives(CardSkill skill, int caster, out int bonus)
        {
            bonus = 0;
            if (
                PassiveHolder(CombatPassiveKind.IaiForm) >= 0
                && !iaiUsed
                && skill.Element == CardElement.Slash
                && Deals(skill)
            )
                bonus += CombatPassiveRules.IaiFormPercent;
            return
                HasPassive(caster, CombatPassiveKind.ChainedArrows)
                && CombatPassiveRules.HitsMoreThanOnce(skill)
                ? CombatPassiveRules.WithExtraHit(skill)
                : skill;
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
        /// puts on, landing each action as <see cref="ApplyHit"/> does. With
        /// <paramref name="random"/>, every random hit goes to the pick, but only once.
        /// </summary>
        private (BattleForecast[] enemies, BattleForecast[] allies) PlayThrough(
            CardSkill skill,
            int pick,
            bool onEnemies,
            int caster,
            bool random
        )
        {
            var enemyHp = new int[Enemies.Length];
            var enemyBlock = new int[Enemies.Length];
            var added = new HashSet<(int, CardStatus)>();
            var standing = new List<int>();
            for (int i = 0; i < Enemies.Length; i++)
            {
                enemyHp[i] = Enemies[i].Hp;
                enemyBlock[i] = Enemies[i].Block;
                if (Enemies[i].Alive)
                    standing.Add(i);
            }
            var allyHp = new int[Allies.Length];
            var allyBlock = new int[Allies.Length];
            var up = new List<int>();
            for (int i = 0; i < Allies.Length; i++)
            {
                allyHp[i] = Allies[i].Hp;
                allyBlock[i] = BlockOf(i);
                if (!Allies[i].Down)
                    up.Add(i);
            }

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

            bool On(int enemy, CardStatus status) =>
                enemy >= 0 && (HasStatus(false, enemy, status) || added.Contains((enemy, status)));
            bool Holds(CardCondition when, int enemy, bool weak) =>
                when switch
                {
                    CardCondition.Always => true,
                    CardCondition.OnWeakness => weak,
                    CardCondition.OnBurning => On(enemy, CardStatus.Burn),
                    CardCondition.OnWounded => enemy >= 0
                        && enemyHp[enemy] * 2 <= Enemies[enemy].MaxHp,
                    CardCondition.OnChilled => On(enemy, CardStatus.Chill),
                    CardCondition.OnPoisoned => On(enemy, CardStatus.Poison),
                    CardCondition.OnBleeding => On(enemy, CardStatus.Bleed),
                    CardCondition.OnVulnerable => On(enemy, CardStatus.Vulnerable),
                    CardCondition.OnParalyzed => On(enemy, CardStatus.Paralysis),
                    CardCondition.OnGuarded => allyBlock[caster] > 0,
                    _ => false,
                };

            var enemyHit = new bool[Enemies.Length];
            var allyHit = new bool[Allies.Length];
            var landed = new HashSet<(CardAction, int)>();
            int dealt = 0;
            foreach (var hit in plan)
            {
                bool weak = false;
                foreach (var action in hit.Actions)
                {
                    int enemy = hit.Ally ? -1 : hit.Index;
                    if (action.When != CardCondition.Always && !Holds(action.When, enemy, weak))
                        continue;
                    if (enemy >= 0 && enemyHp[enemy] <= 0)
                        continue;
                    switch (action.Kind)
                    {
                        case CardActionKind.Damage:
                        {
                            if (enemy < 0 || (random && !landed.Add((action, enemy))))
                                break;
                            weak = RevealedWeak(enemy, skill.Element, caster);
                            int damage = ForecastDamage(
                                skill,
                                action,
                                enemy,
                                caster,
                                weak,
                                Holds(action.BonusWhen, enemy, weak),
                                On(enemy, CardStatus.Vulnerable)
                            );
                            int absorbed = Mathf.Min(enemyBlock[enemy], damage);
                            enemyBlock[enemy] -= absorbed;
                            damage -= absorbed;
                            int before = enemyHp[enemy];
                            enemyHp[enemy] = Mathf.Max(0, before - damage);
                            dealt += before - enemyHp[enemy];
                            enemyHit[enemy] = true;
                            // 傷口を開く: a blow on a bleeding enemy makes it bleed once more.
                            var bleed = Find(false, enemy, CardStatus.Bleed);
                            if (
                                bleed != null
                                && enemyHp[enemy] > 0
                                && HasPassive(caster, CombatPassiveKind.OpenWounds)
                            )
                            {
                                int extra = ForecastProtect(enemy, bleed.Power);
                                int soaked = Mathf.Min(enemyBlock[enemy], extra);
                                enemyBlock[enemy] -= soaked;
                                before = enemyHp[enemy];
                                enemyHp[enemy] = Mathf.Max(0, before - (extra - soaked));
                                dealt += before - enemyHp[enemy];
                            }
                            break;
                        }
                        case CardActionKind.Heal:
                            if (!hit.Ally)
                                break;
                            Heal(hit.Index, HealWithPassives(Power(action, caster), caster));
                            break;
                        case CardActionKind.Revive:
                            if (!hit.Ally)
                                break;
                            allyHp[hit.Index] = Mathf.Min(
                                Allies[hit.Index].MaxHp,
                                allyHp[hit.Index]
                                    + Mathf.RoundToInt(
                                        Allies[hit.Index].MaxHp * action.Amount / 100f
                                    )
                            );
                            allyHit[hit.Index] = true;
                            break;
                        case CardActionKind.Block:
                            if (!hit.Ally)
                                break;
                            Guard(hit.Index, Power(action, caster));
                            break;
                        case CardActionKind.Status:
                            if (!hit.Ally)
                                added.Add((hit.Index, action.Status));
                            break;
                    }
                }
            }
            // 魔力の盾: part of a thunder card's damage turns into its user's block.
            if (
                skill.Element == CardElement.Thunder
                && dealt > 0
                && HasPassive(caster, CombatPassiveKind.ManaShield)
            )
                Guard(caster, CombatPassiveRules.ManaShieldBlock(dealt));

            var enemies = new BattleForecast[Enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
                enemies[i] = new BattleForecast
                {
                    Shown = enemyHit[i] && enemyHp[i] != Enemies[i].Hp,
                    Hp = enemyHp[i],
                    Block = enemyBlock[i],
                    Defeat = !random && enemyHp[i] <= 0,
                };
            var allies = new BattleForecast[Allies.Length];
            for (int i = 0; i < allies.Length; i++)
                allies[i] = new BattleForecast
                {
                    Shown = allyHit[i] && (allyHp[i] != Allies[i].Hp || allyBlock[i] != BlockOf(i)),
                    Hp = allyHp[i],
                    Block = allyBlock[i],
                };
            return (enemies, allies);

            // あふれる祈り turns what goes past the max HP into block, as HealFrom does.
            void Heal(int ally, int amount)
            {
                int overflow = CombatPassiveRules.Overflow(
                    allyHp[ally],
                    Allies[ally].MaxHp,
                    amount
                );
                allyHp[ally] = Mathf.Min(Allies[ally].MaxHp, allyHp[ally] + amount);
                allyHit[ally] = true;
                if (overflow > 0 && HasPassive(caster, CombatPassiveKind.OverflowingPrayer))
                    Guard(ally, overflow);
            }

            void Guard(int ally, int amount)
            {
                allyBlock[ally] = Mathf.Min(
                    Allies[ally].MaxHp,
                    allyBlock[ally] + Mathf.Max(0, amount)
                );
                allyHit[ally] = true;
            }
        }

        /// <summary>
        /// One blow's damage as <see cref="Damage"/> and <see cref="Hit"/> work it out, before the
        /// enemy's block: the bonuses, the statuses on both sides, the defense, a revealed
        /// weakness and protection.
        /// </summary>
        private int ForecastDamage(
            CardSkill skill,
            CardAction action,
            int enemy,
            int caster,
            bool weak,
            bool bonus,
            bool vulnerable
        )
        {
            int power = Power(action, caster);
            if (action.BonusWhen != CardCondition.Always && bonus)
                power = CardRules.WithBonus(action, power);
            power = CardPowerWithPassives(power, skill, enemy, caster);
            var up = Find(true, caster, CardStatus.AttackUp);
            if (up != null)
                power = Mathf.RoundToInt(power * (1f + up.Amount / 100f));
            if (vulnerable)
                power = Mathf.RoundToInt(power * (1f + VulnerableBonus));
            power = CombatFormula.Defend(
                power,
                DefenseOf(Enemies[enemy], action.Stat),
                Allies[caster].Level
            );
            int damage = weak ? Mathf.RoundToInt(power * WeakMultiplier) : power;
            return ForecastProtect(enemy, damage);
        }

        private int ForecastProtect(int enemy, int damage) =>
            HasStatus(false, enemy, CardStatus.Protect)
                ? Mathf.RoundToInt(damage * (1f - ProtectCut))
                : damage;

        /// <summary>
        /// True when the blow hits a weakness already revealed: one still hidden counts as no
        /// weakness, so the forecast cannot give it away.
        /// </summary>
        private bool RevealedWeak(int enemy, CardElement element, int caster)
        {
            var mine = BattleCardText.ElementOf(element);
            bool fire = HasStatus(true, caster, CardStatus.FireBlade);
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
    }
}
