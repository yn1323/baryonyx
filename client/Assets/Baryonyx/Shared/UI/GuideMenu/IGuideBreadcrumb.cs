using System.Collections.Generic;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// A feature panel that names where it is for the breadcrumb at the top of the guide screen,
    /// e.g. the adventurer's page gives "冒険者" and the person's name. Without it, the breadcrumb
    /// shows the open item's label. The panel calls <see cref="GuideMenuView.RefreshTrail"/>
    /// when its trail changes (another person, the data loaded).
    /// </summary>
    public interface IGuideBreadcrumb
    {
        // 画面名のあとに続ける部分。空の文字は飛ばす。
        IReadOnlyList<string> Trail { get; }
    }
}
