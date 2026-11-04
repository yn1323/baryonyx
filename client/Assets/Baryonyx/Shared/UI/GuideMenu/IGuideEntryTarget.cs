namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// A feature panel that can show one entry when another screen opens it for that entry
    /// (the library's page of a material pressed in the craft, and the craft's recipe when
    /// coming back from it). The entry's form is the panel's own.
    /// </summary>
    public interface IGuideEntryTarget
    {
        // 開いた直後に呼ぶ。読み込みの前でもよく、読み終えたらその物を選ぶ。
        void ShowEntry(string entry);

        // 今見せている物。ほかの画面から戻ってきたときに、同じ物を選び直すために控える。
        string CurrentEntry { get; }
    }
}
