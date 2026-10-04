using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure while the app runs, shared by Home, the travel office, the exploration and
    /// the battle. The app sets <see cref="Source"/> to the game server, which keeps each
    /// player's adventure, so it goes on from the room chosen after the app closes; without it
    /// (the showcase, or no server URL) the adventure is kept only while the app runs.
    /// Each screen reads the state on opening and keeps the latest answer in <see cref="Current"/>.
    /// </summary>
    public static class AdventureSession
    {
        private static AdventureLocalSource local;

        // 冒険を読み書きするサーバー。なければアプリを動かしている間だけの冒険を使う。
        public static IAdventureSource Source { get; set; }

        public static IAdventureSource SourceOrLocal =>
            Source ?? (local ??= new AdventureLocalSource());

        // 最後に読んだ、または保存した冒険。まだ読んでいなければnull。
        public static AdventureState Current { get; private set; }

        public static AdventureState Use(AdventureState state)
        {
            Current = state;
            return state;
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Source = null;
            Current = null;
            local = null;
        }
    }
}
