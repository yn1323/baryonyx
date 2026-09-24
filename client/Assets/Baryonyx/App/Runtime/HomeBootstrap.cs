using System;
using Baryonyx.Health;
using Baryonyx.Home;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.App
{
    /// <summary>
    /// Builds the home screen from fixed sample data, except for today's steps, which come
    /// from the server through the shared <see cref="GameServices"/>. The adventure button
    /// leaves the scene only when a destination is set; otherwise it shows the same
    /// "coming soon" feedback as the other mock buttons.
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
        private string adventureSceneName = "";

        [SerializeField]
        private HealthConnectionSettings settings;

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
            presenter = new HomePresenter(
                view,
                data.ToSnapshot(DateTime.Today),
                string.IsNullOrWhiteSpace(adventureSceneName) ? null : StartAdventure,
                new HomeStepSource(GameServices.GetOrCreate(settings).Health)
            );
        }

        private void OnDestroy()
        {
            presenter?.Dispose();
            presenter = null;
        }

        private bool StartAdventure()
        {
            if (!Application.CanStreamedLevelBeLoaded(adventureSceneName))
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
