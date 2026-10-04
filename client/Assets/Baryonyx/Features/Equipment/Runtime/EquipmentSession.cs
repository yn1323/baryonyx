using UnityEngine;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// Where the formation's equipment reads and saves what the characters wear, and who it
    /// shows while the app runs. The app sets <see cref="Source"/> to the game server, which
    /// keeps each player's items (doc/features/equipment.md); without it (the showcase, or no
    /// server URL) the equipment is kept only while the app runs.
    /// </summary>
    public static class EquipmentSession
    {
        // 編成のメニューの「装備」の項目のキー。この項目はリストの代わりに装備の画面を開く。
        public const string GuideItemKey = "equipment";

        private static EquipmentLocalSource local;

        // 装備を読み書きするサーバー。なければアプリを動かしている間だけの装備を使う。
        public static IEquipmentSource Source { get; set; }

        public static IEquipmentSource SourceOrLocal =>
            Source ?? (local ??= new EquipmentLocalSource());

        // 最後に見ていたキャラのID。開き直すとそのキャラから見せる。
        public static string Selected { get; set; }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            local = null;
            Selected = null;
        }
    }
}
