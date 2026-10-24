namespace Baryonyx.Equipment
{
    /// <summary>
    /// The three accessory slots and the level each one opens at
    /// (doc/features/equipment.md#アクセサリー). The server checks the same levels
    /// (ACCESSORY_UNLOCK_LEVELS in server/src/features/equipment/catalog.ts).
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
