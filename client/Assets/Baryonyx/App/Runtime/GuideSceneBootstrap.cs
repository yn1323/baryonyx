using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Economy;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs a guide screen scene (tavern, workshop, temple, travel office). The screen's own back
    /// button, or the device back key, closes an open list first and then returns to Home
    /// behind the shared shutter; the close button at the top right returns to Home at once.
    /// On opening, it reads the rune balance shown at the top right from the server again. On the travel office (<c>departs</c>), the departure sets out
    /// on the adventure for a destination it can go to and opens the exploration.
    /// </summary>
    public sealed class GuideSceneBootstrap : MonoBehaviour
    {
        [SerializeField]
        private GuideMenuView view;

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private string homeSceneName = SceneNames.Home;

        // 行き先の出発で冒険を始める（旅の案内所）。
        [SerializeField]
        private bool departs;

        private GuideMenuPresenter presenter;
        private TravelDeparture departure;
        private readonly CancellationTokenSource lifetime = new();

        // 所持ルーンの読み込み。テストで完了を待つために公開する。
        public Task RunesTask { get; private set; } = Task.CompletedTask;

        public GuideMenuView View => view;
        public SceneTransitionController Transition => transition;
        public GuideMenuPresenter Presenter => presenter;
        public TravelDeparture Departure => departure;
        public string HomeSceneName => homeSceneName;

        private void Awake()
        {
            // Before the view's Start, so it does not make its own presenter.
            if (view != null)
                view.MarkBound();
        }

        private void Start()
        {
            if (view == null || view.Definition == null)
            {
                Debug.LogError("GuideSceneBootstrap requires a view with a definition.", this);
                return;
            }
            if (departs)
            {
                departure = new TravelDeparture(
                    view.ShowToast,
                    AdventureSession.SourceOrLocal,
                    () => SceneLoader.Load(SceneNames.Exploration, transition, this)
                );
            }
            presenter = new GuideMenuPresenter(
                view,
                view.Definition,
                ReturnHome,
                departure != null ? Depart : null
            );
            RunesTask = LoadRunesAsync();
        }

        // Homeから来たときは、Homeで読んだ残高を先に出しておき、読み直した値で描き直す。
        private async Task LoadRunesAsync()
        {
            var health = GameServices.Current?.Health;
            if (health == null)
                return;
            try
            {
                RuneBalance.Set(await health.ReadRunesAsync(lifetime.Token));
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning("所持ルーンを取得できませんでした。" + exception.Message);
            }
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
            presenter?.Dispose();
            presenter = null;
            departure?.Dispose();
            departure = null;
        }

        private bool Depart(int index)
        {
            var destinations = view.Definition.Destinations;
            if (index < 0 || index >= destinations.Length)
                return false;
            return departure.Depart(destinations[index].Id);
        }

        private bool ReturnHome() => SceneLoader.Load(homeSceneName, transition, this);
    }
}
