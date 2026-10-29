using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Networking
{
    /// <summary>
    /// Slows down or fails the requests to the game server on purpose, to see on the screen how
    /// the optimistic updates go ahead and come back (doc/features/screens/debug-rooms.md): only in
    /// development builds and the Editor, chosen from Home's list of debugging rooms. Kept only
    /// while the app runs, so it is back to normal after a restart.
    /// </summary>
    public static class DebugNetwork
    {
        public enum Mode
        {
            Normal,
            Slow,
            VerySlow,
            Fail,
        }

        private static Mode mode;

        /// <summary>Whether this build may slow or fail the requests.</summary>
        public static bool CanSwitch => Debug.isDebugBuild || Application.isEditor;

        /// <summary>How the requests go now (always normal where it cannot be switched).</summary>
        public static Mode Current
        {
            get => CanSwitch ? mode : Mode.Normal;
            set => mode = CanSwitch ? value : Mode.Normal;
        }

        /// <summary>Steps to the next way, round to normal again.</summary>
        public static void Next() => Current = (Mode)(((int)Current + 1) % 4);

        /// <summary>How long a request waits before it goes (s).</summary>
        public static float DelayOf(Mode of) =>
            of switch
            {
                Mode.Slow => 1f,
                Mode.VerySlow => 3f,
                _ => 0f,
            };

        /// <summary>The words of the choice that switches it.</summary>
        public static string Label(Mode of) =>
            of switch
            {
                Mode.Slow => "通信：遅い（1秒）",
                Mode.VerySlow => "通信：とても遅い（3秒）",
                Mode.Fail => "通信：失敗する",
                _ => "通信：ふつう",
            };

        /// <summary>
        /// Waits as the way chosen asks before a request goes, and fails it as a lost connection
        /// when it is to fail.
        /// </summary>
        public static async Task BeforeAsync(CancellationToken token)
        {
            var now = Current;
            float delay = DelayOf(now);
            if (delay > 0f)
                await Awaitable.WaitForSecondsAsync(delay, token);
            if (now == Mode.Fail)
                throw new ServerApiException(0);
        }

        // Play Modeに入るたびに、ふつうへ戻す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => mode = Mode.Normal;
    }
}
