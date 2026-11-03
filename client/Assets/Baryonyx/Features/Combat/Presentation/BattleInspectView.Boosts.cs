namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The allies' boosts that work only while their condition holds (the adventure's boons after
    /// a won battle: 背水・先手・弔い, doc/features/stage-progression.md). The battle reads each
    /// ally's stats through <see cref="StatsNow"/>, and a boost's name pops up over the ally when
    /// it starts to work or grows, as a passive's does.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        // 効果のステータスの割合を読んでいる間（効果の量がステータスを読むときに回り続けないため）。
        private int statsDepth;

        /// <summary>
        /// The ally's stats at this moment of the battle, with the percents the effects add now
        /// (<see cref="BattleValue.StatPercent"/>: the boosts and any other effect).
        /// </summary>
        public CharacterStats StatsNow(int index)
        {
            if (index < 0 || index >= Allies.Length)
                return default;
            var ally = Allies[index];
            if (statsDepth > 0)
                return ally.Stats;
            statsDepth++;
            try
            {
                int Added(int stat) =>
                    Modify(
                        BattleValue.StatPercent,
                        0,
                        new BattleEvent
                        {
                            Actor = AllyAt(index),
                            Target = AllyAt(index),
                            StatIndex = stat,
                        },
                        apply: false
                    );
                return BattleStatBoosts.Grown(
                    ally.Stats,
                    ally.Unboosted,
                    ally.Percents,
                    Added(BattleStatBoosts.PhysicalAttack),
                    Added(BattleStatBoosts.MagicAttack),
                    Added(BattleStatBoosts.Speed)
                );
            }
            finally
            {
                statsDepth--;
            }
        }

        /// <summary>Pops up the name of each boost that has just started to work or grown.</summary>
        private void ShowBoostsTurnedOn() =>
            effects.ShowGrown(
                this,
                BattleValue.StatPercent,
                BattleStatBoosts.PhysicalAttack,
                BattleStatBoosts.MagicAttack,
                BattleStatBoosts.Speed
            );
    }
}
