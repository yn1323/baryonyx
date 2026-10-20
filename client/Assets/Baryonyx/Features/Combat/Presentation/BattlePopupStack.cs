using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Lines up the popups over one character, so a damage number and the names put on with it
    /// (a card's 出血・毒・防御ダウン) read one under another instead of on top of each other. Each
    /// popup goes under the ones still showing over that character; once they have all gone, the
    /// next starts where it wants to be. Popups end at their own heights and rise by their own
    /// distances (a number lifts a little, a name further), so a popup is lowered until it stays
    /// under the others all the way up, not only where they all stop.
    /// </summary>
    public sealed class BattlePopupStack
    {
        /// <summary>How long a popup keeps rising after it appears; it hangs where it stopped after that.</summary>
        public const float RiseTime = 0.5f;

        private readonly Dictionary<
            Object,
            List<(float until, float rising, float bottom, float rise)>
        > lines = new();

        /// <summary>
        /// How far below <paramref name="y"/> (where its middle stops) a popup
        /// <paramref name="height"/> tall, rising <paramref name="rise"/> to get there, goes over
        /// <paramref name="anchor"/> at the time <paramref name="now"/>, showing until
        /// <paramref name="duration"/> later.
        /// </summary>
        public float Place(
            Object anchor,
            float y,
            float height,
            float rise,
            float now,
            float duration
        )
        {
            if (!lines.TryGetValue(anchor, out var shown))
                lines[anchor] = shown = new List<(float until, float rising, float bottom, float rise)>();
            shown.RemoveAll(line => line.until <= now);
            float drop = 0f;
            foreach (var line in shown)
            {
                // その文字の下端より、この文字の上端が下になるまで下げる。
                // 先の文字がまだ上がっている間は、その文字の残りの上がり幅が、この文字の上がり幅より大きい分だけ、さらに下げる。
                float behind = now < line.rising ? Mathf.Max(0f, line.rise - rise) : 0f;
                drop = Mathf.Max(drop, y + height / 2f - line.bottom + behind);
            }
            shown.Add((now + duration, now + RiseTime, y - drop - height / 2f, rise));
            return drop;
        }

        /// <summary>The same for popups that all stop at one height and rise alike.</summary>
        public float Place(Object anchor, float height, float now, float duration) =>
            Place(anchor, 0f, height, 0f, now, duration);
    }
}
