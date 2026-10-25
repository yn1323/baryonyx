using System.Collections.Generic;
using System.Globalization;
using Baryonyx.Combat;
using TMPro;
using UnityEngine;
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

        /// <summary>
        /// Writes the values, each raised one followed by its raise in small green ("380 +98",
        /// doc/features/screens/loadout.md#装備), or empties them.
        /// </summary>
        public static void Show(
            IReadOnlyList<TMP_Text> labels,
            CharacterStats? stats,
            CharacterStats raise
        )
        {
            string green = ColorUtility.ToHtmlStringRGB(UiPalette.Rise);
            for (int i = 0; i < labels.Count && i < CharacterStats.Count; i++)
            {
                string value = stats.HasValue
                    ? stats.Value[i].ToString(CultureInfo.InvariantCulture)
                    : "";
                if (stats.HasValue && raise[i] > 0)
                    value +=
                        $"<size=70%><color=#{green}> +{raise[i].ToString(CultureInfo.InvariantCulture)}</color></size>";
                SetRich(labels[i], value);
            }
        }
    }
}
