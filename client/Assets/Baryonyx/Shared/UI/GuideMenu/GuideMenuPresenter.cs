using System;

namespace Baryonyx.UI.GuideMenu
{
    public enum GuideMenuPage
    {
        // The guide with the menu buttons (or the destinations) beside them.
        Menu,

        // The full list of one menu item.
        List,
    }

    /// <summary>What a guide screen shows. Recomputed by the presenter after every input.</summary>
    public readonly struct GuideMenuState
    {
        public GuideMenuState(GuideMenuPage page, int item, int selected)
        {
            Page = page;
            Item = item;
            Selected = selected;
        }

        public GuideMenuPage Page { get; }

        // The open menu item on the list page; -1 on the menu page.
        public int Item { get; }

        // The chosen list entry or destination; -1 when nothing is chosen.
        public int Selected { get; }
        public bool CanConfirm => Selected >= 0;
    }

    /// <summary>
    /// Walks a guide screen: menu → full list → entry, or destinations → destination. The confirm
    /// button shows a "coming soon" toast until the features exist. Back closes the list
    /// first and leaves the screen from the menu, once.
    /// </summary>
    public sealed class GuideMenuPresenter : IDisposable
    {
        private readonly IGuideMenuView view;
        private readonly GuideMenuDefinition definition;
        private readonly Func<bool> leave;
        private readonly Func<int, bool> depart;
        private GuideMenuState state = new(GuideMenuPage.Menu, -1, -1);
        private bool left;
        private bool disposed;

        public GuideMenuPresenter(
            IGuideMenuView view,
            GuideMenuDefinition definition,
            Func<bool> leave,
            Func<int, bool> depart = null
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.definition =
                definition != null
                    ? definition
                    : throw new ArgumentNullException(nameof(definition));
            this.leave = leave;
            this.depart = depart;
            view.ItemPressed += SelectItem;
            view.EntryPressed += SelectEntry;
            view.ConfirmPressed += Confirm;
            view.BackPressed += Back;
            view.Render(state);
        }

        public GuideMenuState State => state;
        public bool Left => left;

        public void SelectItem(int index)
        {
            if (disposed || left || definition.Layout != GuideMenuLayout.List)
                return;
            if (index < 0 || index >= definition.Items.Length)
                return;
            Show(new GuideMenuState(GuideMenuPage.List, index, -1));
        }

        public void SelectEntry(int index)
        {
            if (disposed || left)
                return;
            if (definition.Layout == GuideMenuLayout.Destinations)
            {
                if (index < 0 || index >= definition.Destinations.Length)
                    return;
                // Locked destinations cannot be chosen.
                Show(
                    new GuideMenuState(
                        GuideMenuPage.Menu,
                        -1,
                        definition.Destinations[index].Locked ? -1 : index
                    )
                );
                return;
            }

            if (state.Page != GuideMenuPage.List)
                return;
            if (index < 0 || index >= definition.Items[state.Item].Entries.Length)
                return;
            Show(new GuideMenuState(GuideMenuPage.List, state.Item, index));
        }

        public void Confirm()
        {
            if (disposed || left || !state.CanConfirm)
                return;
            // 行き先の出発は、出発できる行き先なら呼び出し元が引き受ける（旅の案内所）。
            if (
                definition.Layout == GuideMenuLayout.Destinations
                && depart != null
                && depart(state.Selected)
            )
                return;
            view.ShowToast(ComingSoon(ConfirmLabel()));
        }

        public void Back()
        {
            if (disposed || left)
                return;
            if (state.Page == GuideMenuPage.List)
            {
                Show(new GuideMenuState(GuideMenuPage.Menu, -1, -1));
                return;
            }
            if (leave == null)
                return;
            left = leave();
            if (!left)
                view.ShowToast("戻れませんでした");
        }

        public string ConfirmLabel() =>
            definition.Layout == GuideMenuLayout.Destinations ? definition.DepartLabel
            : state.Page == GuideMenuPage.List ? definition.Items[state.Item].ConfirmLabel
            : "";

        public static string ComingSoon(string label) => $"{label}（準備中）";

        private void Show(GuideMenuState next)
        {
            state = next;
            view.Render(state);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            view.ItemPressed -= SelectItem;
            view.EntryPressed -= SelectEntry;
            view.ConfirmPressed -= Confirm;
            view.BackPressed -= Back;
        }
    }

    public interface IGuideMenuView
    {
        event Action<int> ItemPressed;
        event Action<int> EntryPressed;
        event Action ConfirmPressed;
        event Action BackPressed;

        void Render(GuideMenuState state);
        void ShowToast(string message);
    }
}
