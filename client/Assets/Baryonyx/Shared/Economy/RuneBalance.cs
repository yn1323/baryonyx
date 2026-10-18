using System;
using System.Globalization;
using UnityEngine;

namespace Baryonyx.Economy
{
    /// <summary>
    /// The latest rune balance the app knows, shared by the screens that read or change it: Home
    /// and the guide screens read it from the server, and the formation's training spends it.
    /// The guide screens show it at the top right and redraw when it changes.
    /// </summary>
    public static class RuneBalance
    {
        // まだ一度も読めていなければnull。
        public static long? Known { get; private set; }

        public static event Action Changed;

        public static void Set(long value)
        {
            value = Math.Max(0L, value);
            if (Known == value)
                return;
            Known = value;
            Changed?.Invoke();
        }

        // 「12,345」。読めていなければ「--」。
        public static string Text(long? value) =>
            value.HasValue
                ? Math.Max(0L, value.Value).ToString("N0", CultureInfo.InvariantCulture)
                : "--";

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Known = null;
            Changed = null;
        }
    }
}
