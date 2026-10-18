using System.Collections.Generic;
using System.Globalization;
using Baryonyx.Combat;
using TMPro;
using static Baryonyx.UI.UiText;

namespace Baryonyx.UI
{
    /// <summary>
    /// The eight stats of a character as label and value cells (StatGridAssets bakes them), shared
    /// by the adventurer's page and its change screens.
    /// </summary>
    public static class StatGrid
    {
        /// <summary>Writes the values in <see cref="CharacterStats"/> order, or empties them.</summary>
        public static void Show(IReadOnlyList<TMP_Text> labels, CharacterStats? stats)
        {
            for (int i = 0; i < labels.Count && i < CharacterStats.Count; i++)
                Set(
                    labels[i],
                    stats.HasValue ? stats.Value[i].ToString(CultureInfo.InvariantCulture) : ""
                );
        }
    }
}
