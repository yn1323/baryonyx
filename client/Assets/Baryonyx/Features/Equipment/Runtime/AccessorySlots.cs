namespace Baryonyx.Equipment
{
    /// <summary>
    /// The three accessory slots and the level each one opens at. Accessories have no data yet
    /// (doc/features/equipment.md#アクセサリー), so an open slot stays empty; the levels are
    /// placeholders until the progression is decided.
    /// </summary>
    public static class AccessorySlots
    {
        public const int Count = 3;

        // 解放レベル。1つ目は初めから開いている（2026-10-24、ユーザーの指示）。
        private static readonly int[] UnlockLevels = { 1, 20, 40 };

        public static int UnlockLevel(int index) =>
            index >= 0 && index < UnlockLevels.Length ? UnlockLevels[index] : int.MaxValue;

        public static bool IsOpen(int index, int level) => level >= UnlockLevel(index);

        /// <summary>"Lv 20で解放", shown on a slot that is not open yet.</summary>
        public static string UnlockText(int index) => $"Lv {UnlockLevel(index)}で解放";
    }
}
