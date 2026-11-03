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
            ShowPopup(Allies[ally.Index].TargetArea, "ブロックを砕かれた", GuardColor, 36f);
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
            int gained = AddBlock(target.Index, block);
            // A block for the party shows once, over the party.
            if (party)
            {
                if (first)
                    ShowPopup(PartyAnchor, $"ブロック +{block}", GuardColor, 48f);
            }
            else
                ShowPopup(Allies[target.Index].TargetArea, $"ブロック +{gained}", GuardColor, 44f);
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
            var on = AreaOf(target);
            // 誰にも向かない働き（エネルギー・手札など）は、敵への当たりに載っていても使い手の上に知らせる。
            var area =
                action.Target == CardTarget.None && user.Index >= 0 && user.Index < Allies.Length
                    ? Allies[user.Index].TargetArea
                    : on;
            switch (action.Kind)
            {
                case CardActionKind.Energy:
                    Energy = Mathf.Min(MaxEnergyCap, Energy + action.Amount);
                    ShowPopup(area, $"エネルギー +{action.Amount}", Gold, 42f);
                    break;
                case CardActionKind.Draw:
                    ShowPopup(area, $"{action.Amount}枚引く", SupportColor, 42f);
                    if (dealing == null)
                        dealing = StartCoroutine(Deal(action.Amount, 0f));
                    break;
                case CardActionKind.CostDown:
                    LowerCosts(action);
                    ShowPopup(area, $"コスト -{action.Amount}", SupportColor, 42f);
                    break;
                case CardActionKind.NextCardFree:
                    nextCardFree = true;
                    nextFreeElements = action.Elements;
                    ShowPopup(area, "次のカード コスト0", Gold, 42f);
                    break;
                case CardActionKind.NextCardBoost:
                    nextBoost = action.Amount;
                    nextBoostElements = action.Elements;
                    ShowPopup(on, $"次のカード 威力+{action.Amount}%", Gold, 42f);
                    break;
                case CardActionKind.RevealWeakness:
                {
                    var enemy = Enemies[target.Index];
                    foreach (var weakness in enemy.Weaknesses)
                        if (!weakness.Revealed)
                            StartCoroutine(weakness.Reveal(Vfx, () => enemy.Alive));
                    ShowPopup(enemy.TargetArea, "弱点を見抜いた", SupportColor, 40f);
                    break;
                }
                case CardActionKind.Delay:
                    ShowPopup(
                        Enemies[target.Index].TargetArea,
                        "行動が遅れる",
                        StatusColor(CardStatus.Chill),
                        40f
                    );
                    break;
                case CardActionKind.Cleanse:
                    StatusesOf(true, target.Index)
                        .RemoveAll(active => CardRules.IsBad(active.Status));
                    if (first)
                        ShowPopup(PartyAnchor, "浄化", SupportColor, 44f);
                    break;
                case CardActionKind.Dispel:
                    ShowPopup(Enemies[target.Index].TargetArea, "強化を消した", SupportColor, 40f);
                    break;
            }
        }

        private RectTransform AreaOf(BattleUnit unit) =>
            unit.IsAlly ? Allies[unit.Index].TargetArea : Enemies[unit.Index].TargetArea;
    }
}
