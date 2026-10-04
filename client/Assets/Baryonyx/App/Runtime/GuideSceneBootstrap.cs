using Baryonyx.Adventure;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs a guide screen scene (tavern, workshop, temple, travel office). The screen's own back
    /// button, or the device back key, closes an open list first and then returns to Home
    /// behind the shared shutter. On the travel office (<c>departs</c>), the departure sets out
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
        }

        private void OnDestroy()
        {
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
