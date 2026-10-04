using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs a guide screen scene (tavern, workshop, temple, travel office). The screen's own back
    /// button, or the device back key, closes an open list first and then returns to Home
    /// behind the shared shutter. When Home asks for an item (<see cref="GuideSceneLaunch"/>),
    /// the screen opens on it and back returns straight to Home.
    /// </summary>
    public sealed class GuideSceneBootstrap : MonoBehaviour
    {
        [SerializeField]
        private GuideMenuView view;

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private string homeSceneName = SceneNames.Home;

        private GuideMenuPresenter presenter;

        public GuideMenuView View => view;
        public SceneTransitionController Transition => transition;
        public GuideMenuPresenter Presenter => presenter;
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
            presenter = new GuideMenuPresenter(view, view.Definition, ReturnHome);
            string item = GuideSceneLaunch.Take();
            if (item != null)
                presenter.OpenDirect(item);
        }

        private void OnDestroy()
        {
            presenter?.Dispose();
            presenter = null;
        }

        private bool ReturnHome() => SceneLoader.Load(homeSceneName, transition, this);
    }
}
