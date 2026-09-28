using Baryonyx.Health;
using Baryonyx.Home;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.App
{
    /// <summary>
    /// Builds the home screen from fixed sample data, except for today's steps, which come
    /// from the server through the shared <see cref="GameServices"/>. Runes come from the
    /// server too unless the sample data turns on mock rune gains. The adventure button
    /// leaves the scene only when a destination is set; otherwise it shows the same
    /// "coming soon" feedback as the other mock buttons. The tavern, workshop, temple and travel office
    /// buttons open their guide screen scenes behind the shutter.
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

        // テストで仮データの設定に関係なく、ルーンの取得元を選ぶ。nullなら仮データに従う。
        internal static bool? MockRuneGainOverride;

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
            bool mockRunes = MockRuneGainOverride ?? data.MockRuneGain;
            IHomeStepSource steps = new HomeStepSource(
                GameServices.GetOrCreate(settings).Health,
                runes: !mockRunes
            );
            if (mockRunes)
                steps = new HomeMockRuneSource(steps, data.Runes, data.MockGrantedRunes);
            presenter = new HomePresenter(
                view,
                data.ToSnapshot(HealthDays.Today()),
                string.IsNullOrWhiteSpace(adventureSceneName) ? null : StartAdventure,
                steps,
                OpenScreen
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

        /// <summary>The guide screen scene each Home button opens (built by GuideSceneSetup).</summary>
        public static string ScreenSceneFor(HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => "Pub",
                HomeAction.Workshop => "Shop",
                HomeAction.Temple => "Temple",
                HomeAction.TravelOffice => "TravelOffice",
                _ => null,
            };

        private bool OpenScreen(HomeAction action)
        {
            var scene = ScreenSceneFor(action);
            if (scene == null)
                return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"The scene '{scene}' is not enabled in Build Settings.", this);
                return false;
            }
            void Load() => SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (transition != null)
                return transition.PlayOut(Load);
            Load();
            return true;
        }

        private void LoadAdventure() =>
            SceneManager.LoadSceneAsync(adventureSceneName, LoadSceneMode.Single);
    }
}
