using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The battle screen as a card's actions and an enemy's blows see it
    /// (<see cref="IFightWorld"/>): what <see cref="CardHits"/> and <see cref="EnemyBlows"/>
    /// work out lands here with its numbers, popups and effects. The forecast and a battle
    /// without a screen (<see cref="BattleState"/>) land the same rules on copies.
    /// </summary>
    public sealed partial class BattleInspectView : IFightWorld
    {
        int ICardWorld.CardPower(CardAction action, BattleUnit user) => Power(action, user.Index);

        bool ICardWorld.IsWeak(BattleUnit enemy, CardElement element, BattleUnit user) =>
            IsWeak(enemy.Index, element, user.Index);

        bool ICardWorld.RollCritical(BattleUnit user) =>
            BattleOdds.Critical(
                user.IsAlly
                    ? CombatFormula.CriticalChance(
                        StatsNow(user.Index).Critical,
                        Allies[user.Index].Level
                    )
                    : CombatFormula.CriticalChance(
                        Enemies[user.Index].Critical,
                        Enemies[user.Index].Level
                    )
            );

        void IFightWorld.BreakBlock(BattleUnit ally)
        {
            EnsureBlock();
            allyBlock[ally.Index] = 0;
            RefreshBlock(ally.Index);
        }

        void IFightWorld.LandEnemyBlow(
            BattleUnit attacker,
            BattleUnit target,
            int power,
            bool magic,
            bool pastBlock,
            bool critical,
            EnemyMove move
        )
        {
            int enemy = attacker.IsEnemy ? attacker.Index : -1;
            if (Vfx != null)
                Vfx.Strike(
                    Allies[target.Index].TargetArea,
                    move != null ? move.Name
                        : enemy >= 0 ? Enemies[enemy].SkillName
                        : null,
                    enemy >= 0 ? Enemies[enemy].Body : null,
                    magic
                );
            HitAlly(target.Index, power, pastBlock, critical);
        }

        BattleBlowWeight ICardWorld.LandCardBlow(
            BattleEvent blow,
            int power,
            bool weak,
            bool critical
        )
        {
            int caster = blow.Actor.Index;
            int enemy = blow.Target.Index;
            var element = BattleCardText.ElementOf(blow.Element);
            var extra = HasStatus(true, caster, CardStatus.FireBlade)
                ? BattleInspectElement.Fire
                : BattleInspectElement.None;
            var target = Enemies[enemy];
            bool alive = target.Alive;
            int before = target.Hp;
            var weight = Hit(enemy, power, element, extra, critical: critical);
            if (alive)
                Landed(this, blow, before - target.Hp, !target.Alive);
            return weight switch
            {
                BattleHitWeight.Defeat => BattleBlowWeight.Defeat,
                BattleHitWeight.Weak => BattleBlowWeight.Weak,
                _ => BattleBlowWeight.Normal,
            };
        }

        void ICardWorld.LandCardHeal(BattleUnit healer, BattleUnit target, int amount) =>
            HealFrom(target.Index, amount, healer.Index);

        void ICardWorld.LandCardRevive(BattleUnit target, int percent)
        {
            var ally = Allies[target.Index];
            HealAlly(target.Index, Mathf.RoundToInt(ally.MaxHp * percent / 100f));
        }

        void ICardWorld.LandCardBlock(
            BattleUnit user,
            BattleUnit target,
            int block,
            bool party,
            bool first
        )
        {
            // The block shows on the yellow strip over the HP bar, with no words.
            AddBlock(target.Index, block);
        }

        void ICardWorld.LandCardStatus(BattleUnit user, BattleUnit target, CardAction action)
        {
            if (target.IsAlly || Lands(target.Index, action, user.Index))
                AddStatus(target.IsAlly, target.Index, action, user.Index);
        }

        void ICardWorld.LandCardSupport(
            BattleUnit user,
            BattleUnit target,
            CardAction action,
            bool first
        )
        {
            // 働いたことは文字で知らせず、エネルギー・手札・コスト・弱点のアイコンの変わり方で見せる。
            switch (action.Kind)
            {
                case CardActionKind.Energy:
                    Energy = Mathf.Min(MaxEnergyCap, Energy + action.Amount);
                    break;
                case CardActionKind.Draw:
                    if (dealing == null)
                        dealing = StartCoroutine(Deal(action.Amount, 0f));
                    break;
                case CardActionKind.CostDown:
                    LowerCosts(action);
                    break;
                case CardActionKind.NextCardFree:
                    nextCardFree = true;
                    nextFreeElements = action.Elements;
                    break;
                case CardActionKind.NextCardBoost:
                    nextBoost = action.Amount;
                    nextBoostElements = action.Elements;
                    break;
                case CardActionKind.RevealWeakness:
                {
                    var enemy = Enemies[target.Index];
                    foreach (var weakness in enemy.Weaknesses)
                        if (!weakness.Revealed)
                            StartCoroutine(weakness.Reveal(Vfx, () => enemy.Alive));
                    break;
                }
                case CardActionKind.Delay:
                    ShowStatusNote(
                        Enemies[target.Index].TargetArea,
                        "行動が遅れる",
                        StatusColor(CardStatus.Chill)
                    );
                    break;
                case CardActionKind.Cleanse:
                    StatusesOf(true, target.Index)
                        .RemoveAll(active => CardRules.IsBad(active.Status));
                    break;
            }
        }
    }
}
