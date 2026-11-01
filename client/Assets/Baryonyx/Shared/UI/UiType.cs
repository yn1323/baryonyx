namespace Baryonyx.UI
{
    /// <summary>
    /// The text sizes the screens choose from, in design pixels of the 1920x1080 layout
    /// (doc/rules/ui-design.md の基本の値). Each step has a role; a screen picks the role, not
    /// a number, so the same kind of text has the same size on every screen. The HUD and the
    /// show-off moments (damage numbers, the summon's reveal) keep their own larger sizes.
    /// </summary>
    public static class UiType
    {
        // 札の数・☆の横の小さな印。
        public const float Tiny = 22f;

        // 札の名前・小さな注記。
        public const float Caption = 26f;

        // 行の説明・効果の行。
        public const float Small = 30f;

        // 本文・分類・行の名前・ボタン。
        public const float Body = 34f;

        // 窓の本文・強める本文。
        public const float Lead = 40f;

        // 窓の題・窓のボタン・大きな見出し。
        public const float Title = 44f;

        // 知らせる窓の題・詳細の名前。
        public const float Heading = 56f;

        // 大きな数字・画面の題。
        public const float Display = 64f;
    }
}
