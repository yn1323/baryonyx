using Baryonyx.Party;
using UnityEngine;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// Where the formation's equipment reads and saves what the characters wear, and who it
    /// shows while the app runs. The app sets <see cref="Source"/> to the game server, which
    /// keeps each player's items (doc/features/equipment.md), and a change shows at once and is
    /// saved behind it (<see cref="EquipmentSaves"/>); without it (the showcase, or no server
    /// URL) the equipment is kept only while the app runs.
    /// </summary>
    public static class EquipmentSession
    {
        // 編成の「装備」の項目のキー。メニューには出さず、冒険者の個別の画面から付け替えの画面として開く。
        public const string GuideItemKey = "equipment";

        private static EquipmentLocalSource local;
        private static EquipmentSaves saves;

        // 装備を読み書きするサーバー。なければアプリを動かしている間だけの装備を使う。
        public static IEquipmentSource Source { get; set; }

        // 画面が読み書きする置き場。サーバーがあれば、すぐ表示して裏で保存する置き場を通す。
        public static IEquipmentSource SourceOrLocal =>
            Source == null ? local ??= new EquipmentLocalSource()
            : saves != null && saves.Server == Source ? saves
            : saves = new EquipmentSaves(Source);

        // 最後に見ていたキャラのID。冒険者の一覧・個別・スキルの画面と共有する。
        public static string Selected
        {
            get => PartySession.Selected;
            set => PartySession.Selected = value;
        }

        // 次に開いたときに選んでおく枠。個別の画面で押した枠を渡す。
        public static EquipmentSlot OpenSlot { get; set; } = EquipmentSlot.Weapon;

        // 次に開いたときに選んでおくアクセサリーの枠（0〜2）。武器・防具を開くときは-1。
        public static int OpenAccessory { get; set; } = -1;

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            local = null;
            saves = null;
            OpenSlot = EquipmentSlot.Weapon;
            OpenAccessory = -1;
        }
    }
}
