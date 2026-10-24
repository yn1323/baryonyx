using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Health;
using Baryonyx.Home;
using Baryonyx.Party;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Builds the home screen from fixed sample data, except for today's steps, the rune
    /// balance, the adventure in progress and the party around the campfire (the formation),
    /// which come from the server through the shared <see cref="GameServices"/>. Tapping the ACT panel turns the ACT gained since the last
    /// claim into runes on the server, which keeps the balance. The destination card at the
    /// bottom right resumes the adventure in progress in the exploration scene, or, with none,
    /// opens the travel office to set out from. The tavern, workshop and temple buttons open
    /// their guide screen scenes behind the shutter; the button under the settings lists the
    /// debugging rooms (<see cref="SceneNames.DebugRooms"/>) and opens the one chosen, putting a
    /// button back to Home over a room without its own.
    /// </summary>
    public sealed class HomeBootstrap : MonoBehaviour
    {
        public const string CheckingMessage = "冒険の状態を確かめています";
        public const string DebugRoomsTitle = "デバッグルーム";
        public const string CloseChoice = "閉じる";
        public const string DebugRunesChoice = "ルーン+10000";
        public const string DebugRunesFailedMessage = "ルーンを足せませんでした";

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

        // 焚き火を囲む編成の4人の名前と絵。
        [SerializeField]
        private PartyMockData party;

        private HomePresenter presenter;
        private readonly CancellationTokenSource lifetime = new();
        private bool adventureKnown;

        public HomeView View => view;
        public HomeMockData Data => data;
        public SceneTransitionController Transition => transition;
        public string AdventureSceneName => adventureSceneName;
        public HomePresenter Presenter => presenter;
        public PartyMockData Party => party;

        // 実行中または直前の冒険の読み込み。テストで完了を待つために公開する。
        public Task AdventureTask { get; private set; } = Task.CompletedTask;

        // 実行中または直前の編成の読み込み。テストで完了を待つために公開する。
        public Task PartyTask { get; private set; } = Task.CompletedTask;

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
                OpenScreen,
                ShowDebugRooms
            );
            // モックの「冒険の途中」を実際の状態として見せない。
            presenter.ShowAdventure(false, "", "");
            AdventureTask = LoadAdventureAsync();
            PartyTask = LoadPartyAsync();
        }

        // 焚き火のまわりに、今の編成の4人を立たせる。読めなければ仮データの4人のまま。
        private async Task LoadPartyAsync()
        {
            if (party == null)
            {
                Debug.LogError("HomeBootstrap requires the party mock data.", this);
                return;
            }
            PartyFormation formation;
            try
            {
                formation = await PartySession.CurrentAsync(party, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("編成を取得できませんでした。" + exception.Message);
                return;
            }
            if (view != null)
                view.UseParty(PartySession.Members(formation));
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

        // 冒険の途中なら探索を再開し、そうでなければ街道の世界地図を開く。
        private bool OpenDestination()
        {
            if (!adventureKnown)
            {
                view.ShowToast(CheckingMessage);
                return false;
            }
            bool inProgress = AdventureSession.Current?.InProgress ?? false;
            return SceneLoader.Load(
                inProgress ? adventureSceneName : SceneNames.WorldMap,
                transition,
                this
            );
        }

        // デバッグルームの一覧。選ぶとそのルームを開き、閉じればHomeのまま。
        private void ShowDebugRooms()
        {
            var dialog = view.Dialog;
            if (dialog == null)
            {
                view.ShowToast(
                    HomeViewState.MessageFor(HomeAction.DebugRooms, HomeStepLink.Linked)
                );
                return;
            }
            var choices = SceneNames
                .DebugRooms.Select(room => new DialogChoice(
                    room.Name,
                    () => OpenDebugRoom(room.Scene, room.OwnHome)
                ))
                .Prepend(
                    new DialogChoice(
                        DebugRunesChoice,
                        () =>
                        {
                            dialog.Hide();
                            DebugRunesTask = AddDebugRunesAsync();
                        }
                    )
                )
                .Append(new DialogChoice(CloseChoice, dialog.Hide))
                .ToArray();
            dialog.Show(DebugRoomsTitle, null, null, dialog.Hide, choices);
        }

        // 直前の「ルーン+10000」。テストで完了を待つために公開する。
        public Task DebugRunesTask { get; private set; } = Task.CompletedTask;

        // 問答無用でルーンを足し、サーバーの残高まで数を増やして見せる。
        private async Task AddDebugRunesAsync()
        {
            var debug = GameServices.Current?.DebugRunes ?? DebugRunes.Offline;
            try
            {
                long after = await debug.AddAsync(destroyCancellationToken);
                presenter?.ShowRunesAdded(after, DebugRunes.Amount);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning(DebugRunesFailedMessage + "。" + exception.Message, this);
                if (this != null)
                    view.ShowToast(DebugRunesFailedMessage);
            }
        }

        // 戻るボタンのないルームには「ホームへ」を重ねる。そこから移れるルームにも重ねるため、全部を渡す。
        private void OpenDebugRoom(string scene, bool ownHome)
        {
            if (!ownHome)
                DebugRoomReturn.ShowInRooms(
                    view.RoomReturn,
                    SceneNames
                        .DebugRooms.Where(room => !room.OwnHome)
                        .ToDictionary(room => room.Scene, room => room.Corner)
                );
            if (SceneLoader.Load(scene, transition, this))
            {
                presenter?.MarkScreenOpened();
                return;
            }
            view.Dialog.Hide();
        }

        // Homeのボタンが開く案内人の画面。デバッグルームは一覧から選ぶ。
        private bool OpenScreen(HomeAction action)
        {
            var scene = SceneNames.GuideFor(action);
            return scene != null && SceneLoader.Load(scene, transition, this);
        }
    }
}
