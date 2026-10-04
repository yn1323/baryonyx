namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// A feature panel that steps back inside itself first, e.g. closing a dialog it opened
    /// over itself. The guide screen asks the open panel on every back press (the back button
    /// and the device back key) and goes back itself only when the panel did not.
    /// </summary>
    public interface IGuideBackHandler
    {
        // パネルの中で戻れたらtrue。falseなら、案内人の画面がメニューやホームへ戻る。
        bool HandleBack();
    }
}
