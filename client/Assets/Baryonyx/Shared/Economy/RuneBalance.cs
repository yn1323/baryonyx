using System;
using System.Globalization;
using UnityEngine;

namespace Baryonyx.Economy
{
    /// <summary>
    /// The latest rune balance the app knows, shared by the screens that read or change it: Home
    /// and the guide screens read it from the server, and the formation's training spends it.
    /// The guide screens show it at the top right and redraw when it changes. Runes a change
    /// spends before the server confirms it (a level-up, doc/rules/data-storage.md) are
    /// reserved: they are taken off the shown balance until the change is saved or fails, so a
    /// balance read meanwhile does not bring them back.
    /// </summary>
    public static class RuneBalance
    {
        // サーバーから最後に読んだ残高。まだ一度も読めていなければnull。
        private static long? read;

        // 保存を待っている変更が先に使ったルーン。
        private static long reserved;

        // 画面に出す残高。読んだ残高から、保存を待っている変更が使ったルーンを引く。読めていなければnull。
        public static long? Known => read.HasValue ? Math.Max(0L, read.Value - reserved) : null;

        public static event Action Changed;

        // サーバーから読んだ、または保存した結果の残高にする。
        public static void Set(long value)
        {
            value = Math.Max(0L, value);
            if (read == value)
                return;
            read = value;
            Changed?.Invoke();
        }

        // 保存を待っている変更が使うルーンを、先に残高から引いて見せる。
        public static void Reserve(long runes)
        {
            if (runes <= 0)
                return;
            reserved += runes;
            Changed?.Invoke();
        }

        // 変更を保存した、または失敗したので、先に引いた分を戻す。
        public static void Release(long runes)
        {
            if (runes <= 0)
                return;
            reserved = Math.Max(0L, reserved - runes);
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
            read = null;
            reserved = 0;
            Changed = null;
        }
    }
}
