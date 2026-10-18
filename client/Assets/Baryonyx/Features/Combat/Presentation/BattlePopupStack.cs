using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Lines up the popups over one character, so names put on together (a card's 出血・毒・防御ダウン)
    /// read one under another instead of on top of each other. Each popup goes under the ones
    /// still showing over that character; once they have all gone, the next starts at the top.
    /// A later popup always sits lower and rises the same way, so it never runs into the earlier
    /// ones.
    /// </summary>
    public sealed class BattlePopupStack
    {
        private readonly Dictionary<Object, List<(float until, float bottom)>> lines = new();

        /// <summary>
        /// How far below the anchor's spot a popup <paramref name="height"/> tall goes, at the
        /// time <paramref name="now"/>, showing until <paramref name="duration"/> later.
        /// </summary>
        public float Place(Object anchor, float height, float now, float duration)
        {
            if (!lines.TryGetValue(anchor, out var shown))
                lines[anchor] = shown = new List<(float until, float bottom)>();
            shown.RemoveAll(line => line.until <= now);
            float drop = 0f;
            foreach (var line in shown)
                drop = Mathf.Max(drop, line.bottom);
            shown.Add((now + duration, drop + height));
            return drop;
        }
    }
}
