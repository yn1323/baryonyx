using System;

namespace Baryonyx.UI.GuideMenu
{
    public enum GuideMenuPage
    {
        // The guide with the menu buttons (or the map) beside them.
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

        // The chosen list entry or map point; -1 when nothing is chosen.
        public int Selected { get; }
        public bool CanConfirm => Selected >= 0;
    }

    /// <summary>
    /// Walks a guide screen: menu → full list → entry, or map → destination. The confirm
    /// button shows a "coming soon" toast until the features exist. Back closes the list
    /// first and leaves the screen from the menu, once. A list opened directly from another
    /// screen (<see cref="OpenDirect"/>) leaves the screen on back, back to where it came from.
    /// </summary>
    public sealed class GuideMenuPresenter : IDisposable
    {
        private readonly IGuideMenuView view;
        private readonly GuideMenuDefinition definition;
        private readonly Func<bool> leave;
        private GuideMenuState state = new(GuideMenuPage.Menu, -1, -1);
        private bool direct;
        private bool left;
        private bool disposed;

        public GuideMenuPresenter(
            IGuideMenuView view,
            GuideMenuDefinition definition,
            Func<bool> leave
        )
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.definition =
                definition != null
                    ? definition
                    : throw new ArgumentNullException(nameof(definition));
            this.leave = leave;
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
            direct = false;
            Show(new GuideMenuState(GuideMenuPage.List, index, -1));
        }

        /// <summary>Opens the item with the given key straight away; back then leaves the screen.</summary>
        public bool OpenDirect(string key)
        {
            if (disposed || left || string.IsNullOrEmpty(key))
                return false;
            int index = Array.FindIndex(definition.Items, item => item.Key == key);
            if (index < 0)
                return false;
            SelectItem(index);
            direct = true;
            return true;
        }

        public void SelectEntry(int index)
        {
            if (disposed || left)
                return;
            if (definition.Layout == GuideMenuLayout.Map)
            {
                if (index < 0 || index >= definition.MapPoints.Length)
                    return;
                // Locked destinations can be inspected but not chosen.
                Show(
                    new GuideMenuState(
                        GuideMenuPage.Menu,
                        -1,
                        definition.MapPoints[index].Locked ? -1 : index
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
            view.ShowToast(ComingSoon(ConfirmLabel()));
        }

        public void Back()
        {
            if (disposed || left)
                return;
            if (state.Page == GuideMenuPage.List && !direct)
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
            definition.Layout == GuideMenuLayout.Map ? definition.DepartLabel
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
