using Baryonyx.Health;
using Baryonyx.Home;
using Baryonyx.StepBonus;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Builds the home screen from fixed sample data, except for today's steps and the rune
    /// balance, which come from the server through the shared <see cref="GameServices"/>.
    /// Tapping the UPT panel turns the UPT gained since the last claim into runes on the
    /// server, which keeps the balance. The adventure button
    /// leaves the scene only when a destination is set; otherwise it shows the same
    /// "coming soon" feedback as the other mock buttons. The tavern, workshop, temple and travel office
    /// buttons open their guide screen scenes behind the shutter, with today's UPT handed over
    /// for the tavern's bonus settings.
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
                data.ToSnapshot(HealthDays.Today()),
                string.IsNullOrWhiteSpace(adventureSceneName) ? null : StartAdventure,
                new HomeStepSource(GameServices.GetOrCreate(settings).Health),
                OpenScreen
            );
        }

        private void OnDestroy()
        {
            presenter?.Dispose();
            presenter = null;
        }

        private bool StartAdventure() => SceneLoader.Load(adventureSceneName, transition, this);

        private bool OpenScreen(HomeAction action)
        {
            var scene = SceneNames.GuideFor(action);
            if (scene == null)
                return false;
            StepBonusSession.TodayUpt = presenter?.TodayUpt;
            return SceneLoader.Load(scene, transition, this);
        }
    }
}
