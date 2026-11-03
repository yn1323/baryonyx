namespace Baryonyx.UI
{
    /// <summary>
    /// What a button does, which picks its look (doc/rules/ui-design.md#決めるボタン): one
    /// <see cref="Primary"/> per window or screen for the step forward, <see cref="Secondary"/>
    /// for cancelling or passing, and <see cref="Danger"/> only to confirm what cannot be undone.
    /// </summary>
    public enum ButtonRole
    {
        // 前に進める操作（決める・買う・開ける・もう一度・OK・ホームへ など）。青い板に白い文字。
        Primary,

        // 取り消し・見送り（やめる・あとで・つづける・通り過ぎる）。すりガラスの札。
        Secondary,

        // 取り返せない操作の確認（冒険をやめる）。赤い板に白い文字。
        Danger,
    }
}
