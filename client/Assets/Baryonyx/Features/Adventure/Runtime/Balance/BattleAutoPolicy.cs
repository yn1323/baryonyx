using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The next step of the party's turn in an automatic battle: a card of the hand (an index of
    /// <see cref="BattleInspectView.Cards"/>) and its target (an index of the enemies, or of the
    /// allies for a card on the party side; -1 for a card on a whole side, which takes them all),
    /// or the end of the turn.
    /// </summary>
    public readonly struct BattleAutoMove
    {
        public BattleAutoMove(int card, int target)
        {
            Card = card;
            Target = target;
        }

        public int Card { get; }
        public int Target { get; }
        public bool EndsTurn => Card < 0;

        public static BattleAutoMove EndTurn => new(-1, -1);
    }

    /// <summary>How an automatic battle chooses the party's cards. Another way of playing can take its place.</summary>
    public interface IBattleAutoPolicy
    {
        /// <summary>
        /// The step to take now that the party's turn waits for one. <paramref name="refused"/>
        /// holds the cards of the hand already tried this turn that did not play.
        /// </summary>
        BattleAutoMove Choose(BattleInspectView battle, ICollection<int> refused);
    }

    /// <summary>The ways of playing a request can name (<see cref="BattleBalanceRequest.policy"/>).</summary>
    public static class BattleAutoPolicies
    {
        public const string Default = "default";

        // 今の選び方は1つだけ。ほかの選び方を足したら、名前で引けるようにする。
        public static IBattleAutoPolicy Of(string name) => new CarefulBattlePolicy();
    }

    /// <summary>
    /// The way of playing the balance trials used (doc/features/stage-progression.md, 戦闘の試し):
    /// a fallen member is raised first, a member under half its HP is healed or guarded, and
    /// otherwise the costliest attack goes first, at an enemy whose weakness it hits (only the
    /// weaknesses revealed), or else at the enemy with the least HP. Healing and raising wait
    /// until they are needed; the other cards are played once the attacks are spent.
    /// </summary>
    public sealed class CarefulBattlePolicy : IBattleAutoPolicy
    {
        /// <summary>A member under this share of its maximum HP needs healing.</summary>
        public const float HurtShare = 0.5f;

        public BattleAutoMove Choose(BattleInspectView battle, ICollection<int> refused)
        {
            var allies = battle.Allies;
            bool down = allies.Any(ally => !ally.Absent && ally.Down);
            int weakest = -1;
            float lowest = float.MaxValue;
            for (int i = 0; i < allies.Length; i++)
            {
                if (allies[i].Absent || allies[i].Down || allies[i].MaxHp <= 0)
                    continue;
                float share = allies[i].Hp / (float)allies[i].MaxHp;
                if (share < lowest)
                {
                    lowest = share;
                    weakest = i;
                }
            }
            bool hurt = lowest < HurtShare;

            int pick = -1;
            int best = int.MinValue;
            foreach (int index in battle.HandCards)
            {
                if (refused != null && refused.Contains(index))
                    continue;
                var card = battle.Cards[index];
                int cost = battle.CostOf(card);
                if (cost > battle.Energy)
                    continue;
                var skill = BattleInspectView.SkillOf(battle.Deck[card.DeckIndex]);
                int score = Score(skill, card, cost, hurt, down);
                if (score > best)
                {
                    best = score;
                    pick = index;
                }
            }
            if (pick < 0)
                return BattleAutoMove.EndTurn;
            return new BattleAutoMove(pick, TargetOf(battle, battle.Cards[pick], weakest));
        }

        // 高いほど先に使う。使わないカードは int.MinValue。
        private static int Score(
            CardSkill skill,
            BattleInspectCard card,
            int cost,
            bool hurt,
            bool down
        )
        {
            if (Revives(skill))
                return down ? 2000 + cost : int.MinValue;
            return skill?.Kind switch
            {
                CardKind.Heal => hurt ? 1000 + cost : int.MinValue,
                CardKind.Guard => hurt ? 900 + cost : 200 + cost,
                CardKind.Attack or null => 500 + cost * 10 + Math.Min(99, card.Power / 100),
                _ => 300 + cost,
            };
        }

        private static bool Revives(CardSkill skill) =>
            skill != null && skill.Actions.Any(action => action.Kind == CardActionKind.Revive);

        private static int TargetOf(BattleInspectView battle, BattleInspectCard card, int weakest)
        {
            if (card.TargetsWholeSide)
                return -1;
            if (card.TargetsEnemies)
            {
                int target = -1;
                bool targetWeak = false;
                for (int i = 0; i < battle.Enemies.Length; i++)
                {
                    var enemy = battle.Enemies[i];
                    if (!enemy.Alive)
                        continue;
                    bool weak = enemy.Weaknesses.Any(weakness =>
                        weakness.Revealed && weakness.Element == card.Element
                    );
                    if (
                        target < 0
                        || (weak && !targetWeak)
                        || (weak == targetWeak && enemy.Hp < battle.Enemies[target].Hp)
                    )
                    {
                        target = i;
                        targetWeak = weak;
                    }
                }
                return target;
            }
            var skill = BattleInspectView.SkillOf(battle.Deck[card.DeckIndex]);
            if (Revives(skill))
                return Array.FindIndex(battle.Allies, ally => !ally.Absent && ally.Down);
            return weakest >= 0 ? weakest : Math.Max(0, card.Caster);
        }
    }
}
