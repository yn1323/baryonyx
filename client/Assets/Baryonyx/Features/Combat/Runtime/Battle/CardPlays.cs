using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// A card's play from beginning to end, the same for the battle, its forecast and a battle
    /// without a screen: its own effects join for the play, the effects are told it begins (and
    /// may give it more hits, 連ね矢) and that it has played, with the damage its blows dealt.
    /// </summary>
    public static class CardPlays
    {
        /// <summary>
        /// <paramref name="user"/> begins to play <paramref name="skill"/>: returns its play and
        /// the card as it plays.
        /// </summary>
        public static (BattleCardPlay Play, CardSkill Skill) Begin(
            IBattleWorld world,
            BattleEffects effects,
            CardSkill skill,
            BattleUnit user
        )
        {
            var play = new BattleCardPlay(skill, user);
            AddEffects(effects, play);
            effects.Fire(world, BattleTrigger.CardPlaying, BattleEvent.OfCard(play));
            return (play, WithHits(world, effects, skill, play));
        }

        /// <summary>The card has played: the effects are told the damage its blows dealt.</summary>
        public static void Played(IBattleWorld world, BattleEffects effects, BattleCardPlay play)
        {
            var played = BattleEvent.OfCard(play);
            played.Amount = play.Dealt;
            effects.Fire(world, BattleTrigger.CardPlayed, played);
        }

        /// <summary>Ends the play: told that it has played, its own effects leave.</summary>
        public static void End(IBattleWorld world, BattleEffects effects, BattleCardPlay play)
        {
            Played(world, effects, play);
            RemoveEffects(effects, play);
        }

        /// <summary>The card's own effects, held by its user while its play lasts.</summary>
        public static void AddEffects(BattleEffects effects, BattleCardPlay play)
        {
            if (play.Card?.Effects == null)
                return;
            foreach (var effect in play.Card.Effects)
                if (effect != null)
                    effects.Add(play.User, effect, play);
        }

        public static void RemoveEffects(BattleEffects effects, BattleCardPlay play)
        {
            for (int i = effects.All.Count - 1; i >= 0; i--)
                if (effects.All[i].Play == play)
                    effects.Remove(effects.All[i]);
        }

        /// <summary>
        /// The card with each damage that lands more than once landing as often as the effects
        /// say (<see cref="BattleValue.Hits"/>); the card itself when none changes.
        /// </summary>
        public static CardSkill WithHits(
            IBattleWorld world,
            BattleEffects effects,
            CardSkill skill,
            BattleCardPlay play
        )
        {
            List<CardAction> actions = null;
            for (int i = 0; i < skill.Actions.Count; i++)
            {
                var action = skill.Actions[i];
                if (action.Kind != CardActionKind.Damage)
                    continue;
                var moment = BattleEvent.OfCard(play);
                moment.Action = action;
                int hits = effects.Modify(world, BattleValue.Hits, action.Hits, moment);
                if (hits == action.Hits)
                    continue;
                actions ??= new List<CardAction>(skill.Actions);
                var more = action.Copy();
                more.Hits = hits;
                actions[i] = more;
            }
            if (actions == null)
                return skill;
            var copy = skill.Copy();
            copy.Actions = actions;
            return copy;
        }
    }
}
