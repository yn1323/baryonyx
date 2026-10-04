using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Hands the guide screen the item to open on arrival, across the scene load. Home sets it
    /// just before loading the scene; the guide screen takes it once.
    /// </summary>
    public static class GuideSceneLaunch
    {
        // 次に開く案内人の画面で、直接開く項目のキー。なければnull。
        public static string Item { get; set; }

        public static string Take()
        {
            string item = Item;
            Item = null;
            return string.IsNullOrEmpty(item) ? null : item;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Item = null;
    }
}
