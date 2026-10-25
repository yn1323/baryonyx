using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// The text and accent colours the screens share: the warm off-white body text, the dimmer
    /// sub text, the faint hint text, the teal of linked/selected states and the gold of rewards.
    /// </summary>
    public static class UiPalette
    {
        public static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        public static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        public static readonly Color TextFaint = new(0.604f, 0.580f, 0.514f);
        public static readonly Color Teal = new(0.498f, 0.890f, 0.839f);
        public static readonly Color Gold = new(1f, 0.843f, 0.4f);

        // 見出しの明るい金（枠つきの画面の「パーティ」「一覧」など）。
        public static readonly Color GoldLight = new(0.992f, 0.894f, 0.353f);

        // ステータスが上がる数字の緑と、下がる数字の赤（装備の上昇分と増減）。
        public static readonly Color Rise = new(0.49f, 0.88f, 0.42f);
        public static readonly Color Fall = new(1f, 0.45f, 0.4f);
    }
}
