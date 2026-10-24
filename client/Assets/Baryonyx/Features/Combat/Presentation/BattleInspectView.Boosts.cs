using System.Collections.Generic;

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
        // 強化ごとに、最後に見せた上がり幅（味方の場所と強化の番号で引く）。上がったときだけ名前を出す。
        private readonly Dictionary<(int Ally, int Boost), int> boostsShown = new();

        /// <summary>The ally's stats at this moment of the battle, with the boosts working now.</summary>
        public CharacterStats StatsNow(int index)
        {
            if (index < 0 || index >= Allies.Length)
                return default;
            var ally = Allies[index];
            return BattleStatBoosts.Now(
                ally.Stats,
                ally.Unboosted,
                ally.Percents,
                ally.Boosts,
                ally.Hp,
                ally.MaxHp,
                BattleTurn,
                FallenAllies()
            );
        }

        // 倒れている仲間の数（空いた枠は数えない）。
        private int FallenAllies()
        {
            int fallen = 0;
            foreach (var ally in Allies)
                if (!ally.Absent && ally.Down)
                    fallen++;
            return fallen;
        }

        /// <summary>Pops up the name of each boost that has just started to work or grown.</summary>
        private void ShowBoostsTurnedOn()
        {
            int fallen = FallenAllies();
            for (int place = 0; place < Allies.Length; place++)
            {
                var ally = Allies[place];
                var boosts = ally.Boosts;
                if (boosts == null)
                    continue;
                for (int k = 0; k < boosts.Count; k++)
                {
                    var boost = boosts[k];
                    int now =
                        !BattleStatBoosts.Works(boost, ally.Hp, ally.MaxHp, BattleTurn, fallen) ? 0
                        : boost.Condition == BattleBoostCondition.PerFallenAlly
                            ? boost.Percent * fallen
                        : boost.Percent;
                    boostsShown.TryGetValue((place, k), out int shown);
                    boostsShown[(place, k)] = now;
                    if (now > shown && !string.IsNullOrEmpty(boost.Name))
                        ShowPopup(ally.TargetArea, boost.Name, PassiveColor, 36f);
                }
            }
        }
    }
}
