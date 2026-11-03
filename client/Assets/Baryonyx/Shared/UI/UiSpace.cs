namespace Baryonyx.UI
{
    /// <summary>
    /// The spacing, hit size, window widths and pane splits the screens choose from, in design
    /// pixels of the 1920x1080 layout (doc/rules/ui-design.md の基本の値).
    /// </summary>
    public static class UiSpace
    {
        // 余白の段階。部品の中の小さなすき間から、面の端までの余白まで。
        public const float Gap = 8f;
        public const float Inset = 16f;
        public const float Pad = 24f;
        public const float Edge = 48f;

        // 指で押しやすい大きさ（×・－＋・小さなボタンの押せる範囲）。
        public const float Hit = 128f;

        // 窓の幅は3つ。小は確かめる窓、中は知らせる窓と結果、大は一覧の窓。
        public const float WindowSmall = 1000f;
        public const float WindowMedium = 1400f;
        public const float WindowLarge = 1840f;

        // パネルの左右の分け目。一覧と詳細は1160、冒険者のシートは人の列の幅で944か1044。
        // 冒険者のシートは、左から人の列・タブの列・中身（2026-11-03に、タブの列を右端から人の列の右へ移した）。
        public const float ListSplit = 1160f;
        public const float SheetSplit = 944f;
        public const float SheetSplitWide = 1044f;
    }
}
