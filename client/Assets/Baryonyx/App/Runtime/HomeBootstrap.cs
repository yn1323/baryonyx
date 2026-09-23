using System;
using Baryonyx.Home;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.App
{
    /// <summary>
    /// Builds the home screen mock from fixed sample data. It never creates the health
    /// runtime; only the adventure button leaves for the existing wireframe scene.
    /// </summary>
    public sealed class HomeBootstrap : MonoBehaviour
    {
        [SerializeField]
        private HomeView view;

        [SerializeField]
        private HomeMockData data;

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private string adventureSceneName = "Main";

        private HomePresenter presenter;

        public HomeView View => view;
        public HomeMockData Data => data;
        public SceneTransitionController Transition => transition;
        public string AdventureSceneName => adventureSceneName;
        public HomePresenter Presenter => presenter;

        private void Start()
        {
            if (view == null || data == null)
            {
                Debug.LogError("HomeBootstrap requires a view and mock data.", this);
                return;
            }
            presenter = new HomePresenter(view, data.ToSnapshot(DateTime.Today), StartAdventure);
        }

        private void OnDestroy()
        {
            presenter?.Dispose();
            presenter = null;
        }

        private bool StartAdventure()
        {
            if (
                string.IsNullOrWhiteSpace(adventureSceneName)
                || !Application.CanStreamedLevelBeLoaded(adventureSceneName)
            )
            {
                Debug.LogError(
                    $"The adventure scene '{adventureSceneName}' is not enabled in Build Settings.",
                    this
                );
                return false;
            }

            if (transition != null)
                return transition.PlayOut(LoadAdventure);

            LoadAdventure();
            return true;
        }

        private void LoadAdventure() =>
            SceneManager.LoadSceneAsync(adventureSceneName, LoadSceneMode.Single);
    }
}
