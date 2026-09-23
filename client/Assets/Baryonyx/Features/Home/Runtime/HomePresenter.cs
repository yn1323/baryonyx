using System;

namespace Baryonyx.Home
{
    /// <summary>
    /// Turns home button presses into mock feedback. Resuming the adventure is delegated to
    /// the caller and accepted only once, so repeated taps cannot start two scene loads.
    /// </summary>
    public sealed class HomePresenter : IDisposable
    {
        private readonly HomeView view;
        private readonly HomeSnapshot snapshot;
        private readonly Func<bool> startAdventure;
        private bool adventureStarted;
        private bool disposed;

        public HomePresenter(HomeView view, HomeSnapshot snapshot, Func<bool> startAdventure)
        {
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            this.startAdventure = startAdventure;
            view.ActionRequested += Handle;
            view.Render(HomeViewState.From(snapshot));
        }

        public bool AdventureStarted => adventureStarted;

        public void Handle(HomeAction action)
        {
            if (disposed || adventureStarted)
                return;

            if (action == HomeAction.Resume && startAdventure != null)
            {
                adventureStarted = startAdventure();
                if (!adventureStarted)
                    view.ShowToast("再開できませんでした");
                return;
            }

            view.ShowToast(HomeViewState.MessageFor(action, snapshot.StepLink));
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (view != null)
                view.ActionRequested -= Handle;
        }
    }
}
