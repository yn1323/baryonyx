using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Health;
using Baryonyx.Home;
using Baryonyx.StepBonus;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Builds the home screen from fixed sample data, except for today's steps, the rune
    /// balance and the adventure in progress, which come from the server through the shared
    /// <see cref="GameServices"/>. Tapping the UPT panel turns the UPT gained since the last
    /// claim into runes on the server, which keeps the balance. The destination card at the
    /// bottom right resumes the adventure in progress in the exploration scene, or, with none,
    /// opens the travel office to set out from. The tavern, workshop and temple buttons open
    /// their guide screen scenes behind the shutter, with today's UPT handed over for the
    /// tavern's bonus settings.
    /// </summary>
    public sealed class HomeBootstrap : MonoBehaviour
    {
        public const string CheckingMessage = "冒険の状態を確かめています";

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
        private readonly CancellationTokenSource lifetime = new();
        private bool adventureKnown;

        public HomeView View => view;
        public HomeMockData Data => data;
        public SceneTransitionController Transition => transition;
        public string AdventureSceneName => adventureSceneName;
        public HomePresenter Presenter => presenter;

        // 実行中または直前の冒険の読み込み。テストで完了を待つために公開する。
        public Task AdventureTask { get; private set; } = Task.CompletedTask;

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
                string.IsNullOrWhiteSpace(adventureSceneName) ? null : OpenDestination,
                new HomeStepSource(GameServices.GetOrCreate(settings).Health),
                OpenScreen
            );
            // モックの「冒険の途中」を実際の状態として見せない。
            presenter.ShowAdventure(false, "", "");
            AdventureTask = LoadAdventureAsync();
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
            presenter?.Dispose();
            presenter = null;
        }

        private async Task LoadAdventureAsync()
        {
            AdventureState state;
            try
            {
                state = AdventureSession.Use(
                    await AdventureSession.SourceOrLocal.LoadAsync(lifetime.Token)
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                // 読めなければ旅の案内所を開く。出発するときにサーバーが冒険の途中かを確かめる。
                Debug.LogWarning("冒険の状態を取得できませんでした。" + exception.Message);
                adventureKnown = true;
                return;
            }
            if (presenter == null)
                return;
            adventureKnown = true;
            var run = state.Run;
            presenter.ShowAdventure(
                run != null,
                run != null ? AdventureCatalog.DestinationName(run.DestinationId) : "",
                run != null ? AdventureCatalog.FloorText(run.Floor) : ""
            );
        }

        // 冒険の途中なら探索を再開し、そうでなければ旅の案内所を開く。
        private bool OpenDestination()
        {
            if (!adventureKnown)
            {
                view.ShowToast(CheckingMessage);
                return false;
            }
            bool inProgress = AdventureSession.Current?.InProgress ?? false;
            if (!inProgress)
                StepBonusSession.TodayUpt = presenter?.TodayUpt;
            return SceneLoader.Load(
                inProgress ? adventureSceneName : SceneNames.TravelOffice,
                transition,
                this
            );
        }

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
