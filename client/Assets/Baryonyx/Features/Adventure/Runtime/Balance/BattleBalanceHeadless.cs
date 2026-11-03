using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// One battle of the auto-battle without the battle screen (the request's
    /// <see cref="BattleBalanceRequest.engine"/> "headless"): the same party and encounter as on
    /// the screen, fought at once by <see cref="BattleSimulation"/>. What the battle without a
    /// screen leaves out is listed there; its record leaves out the healing and counts the damage
    /// taken from the HP lost.
    /// </summary>
    public static class BattleBalanceHeadless
    {
        /// <summary>The 4 slots of the party, as the screen's places.</summary>
        public const int Slots = 4;

        public static (BattleBalanceRecord Record, float[] Shares) Fight(
            BattleBalanceRequest request,
            string encounter,
            int seed,
            IReadOnlyList<BattlePartyMember> members
        )
        {
            BattleOdds.Seed(seed);
            var state = new BattleState(BattleDice.Seeded(seed))
            {
                KnowWeaknesses = request.knowWeaknesses,
            };
            var battle = new BattleSimulation(state);
            for (int slot = 0; slot < Slots; slot++)
            {
                var member = members != null && slot < members.Count ? members[slot] : null;
                state.Allies.Add(AllyOf(member));
                if (member == null)
                    continue;
                foreach (var kind in member.Passives ?? Array.Empty<CombatPassiveKind>())
                    if (CombatPassives.Find(kind)?.Effect is { } passive)
                        state.Give(slot, passive);
                foreach (var effect in member.Effects ?? Array.Empty<BattleEffect>())
                    state.Give(slot, effect);
                foreach (var boost in member.Boosts ?? Array.Empty<BattleStatBoost>())
                    if (boost != null)
                        state.Give(slot, BattleStatBoosts.EffectOf(boost));
                foreach (var card in member.Cards ?? Array.Empty<BattlePartyCard>())
                    if (card?.Skill != null)
                        battle.AddCard(card.Skill, slot);
            }
            var enemies = AdventureCatalog.Encounter(encounter);
            for (int place = 0; place < enemies.Kinds.Count; place++)
            {
                string kind = enemies.Kinds[place];
                state.Enemies.Add(
                    string.IsNullOrEmpty(kind)
                        ? new BattleFighter { Present = false }
                        : BattleFighter.OfEnemy(
                            kind,
                            enemies.Level,
                            enemies.HpScale,
                            enemies.PowerScale,
                            n => state.Dice.Below(n)
                        )
                );
            }
            var record = new BattleBalanceRecord
            {
                seed = seed,
                encounter = encounter,
                enemies = BattleBalanceEncounters.EnemiesOf(encounter),
                enemyHp = state.Enemies.Where(e => e.Present).Sum(e => e.Hp),
                hpStart = PartyShare(state),
            };
            int partyHp = state.Allies.Sum(a => Math.Max(0, a.Hp));
            var fought = battle.Fight(new CarefulSimPolicy(), Math.Max(1, request.maxTurns));
            record.outcome =
                fought.Victory ? BattleBalanceRecord.Victory
                : fought.Timeout ? BattleBalanceRecord.Timeout
                : BattleBalanceRecord.Defeat;
            record.turns = fought.Turns;
            record.dealt = fought.Dealt;
            record.taken = Math.Max(0, partyHp - state.Allies.Sum(a => Math.Max(0, a.Hp)));
            record.hpLowest = Math.Min(record.hpStart, fought.HpLowest);
            record.hpEnd = PartyShare(state);
            record.downAtEnd = state.Allies.Count(a => a.Present && a.Hp <= 0);
            record.fell = fought.Fell;
            record.cards = fought.Cards;
            record.dealtByTurn = fought.DealtByTurn.ToArray();
            var shares = new float[AdventureVitals.Slots];
            for (int slot = 0; slot < shares.Length; slot++)
            {
                var ally = slot < state.Allies.Count ? state.Allies[slot] : null;
                shares[slot] =
                    ally == null || !ally.Present || ally.MaxHp <= 0
                        ? 1f
                        : Math.Clamp(ally.Hp / (float)ally.MaxHp, 0f, 1f);
            }
            return (record, shares);
        }

        private static BattleFighter AllyOf(BattlePartyMember member)
        {
            if (member == null)
                return new BattleFighter { Present = false };
            int maxHp = Math.Max(1, member.Stats.Hp);
            return new BattleFighter
            {
                Name = member.Name ?? "",
                Level = Math.Max(1, member.Level),
                Stats = member.Stats,
                Unboosted = member.Unboosted,
                Percents = member.Percents ?? Array.Empty<float>(),
                MaxHp = maxHp,
                Hp = BattleInspectView.StartHpOf(maxHp, member.HpShare),
                TwiceChance = member.TwiceChance,
            };
        }

        private static float PartyShare(BattleState state)
        {
            int hp = 0;
            int max = 0;
            foreach (var ally in state.Allies)
                if (ally.Present)
                {
                    hp += Math.Max(0, ally.Hp);
                    max += ally.MaxHp;
                }
            return max > 0 ? hp / (float)max : 0f;
        }
    }
}
