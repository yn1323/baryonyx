using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Economy;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using Baryonyx.WorldMap;
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

        // 「もどる」で戻る画面。空ならHome。旅の案内所（ダンジョン一覧）は世界地図へ戻る。
        // 右上の×は、どの画面でもHomeへ戻る。
        [SerializeField]
        private string backSceneName = "";

        // 行き先の出発で冒険を始める（旅の案内所）。
        [SerializeField]
        private bool departs;

        private GuideMenuPresenter presenter;
        private TravelDeparture departure;

        // ほかの画面から1つの項目のために開いたとき（冒険の装備）の、もどる・×の戻り先。
        private string returnScene;

        // 戻り先で開き直す項目と物（合成からライブラリを開いたときの、合成の作る物）。
        private GuideSceneEntry.Request back;
        private readonly CancellationTokenSource lifetime = new();

        // 所持ルーンの読み込み。テストで完了を待つために公開する。
        public Task RunesTask { get; private set; } = Task.CompletedTask;

        // 解放したダンジョンの読み込み（旅の案内所）。テストで完了を待つために公開する。
        public Task UnlockTask { get; private set; } = Task.CompletedTask;

        public GuideMenuView View => view;
        public SceneTransitionController Transition => transition;
        public GuideMenuPresenter Presenter => presenter;
        public TravelDeparture Departure => departure;
        public string HomeSceneName => homeSceneName;
        public string BackSceneName =>
            !string.IsNullOrEmpty(returnScene) ? returnScene
            : string.IsNullOrEmpty(backSceneName) ? homeSceneName
            : backSceneName;

        // ほかの画面から1つの項目のために開いたときの戻り先。Homeから開いたときは空。
        public string ReturnScene => returnScene;

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
                departure = NewDeparture();
            // ほかの画面からも行き先を指して冒険に出られるようにする（ライブラリの入手先）。
            view.SetOut = SetOut;
            view.ShowInLibrary = ShowInLibrary;
            // 冒険の装備のように、ほかの画面から項目を指して開いたときは、その項目から始め、
            // もどる・×で元の画面へ戻る（doc/features/screens/guide.md）。
            var entry = GuideSceneEntry.Take();
            int entryItem = entry != null ? view.IndexOfItem(entry.ItemKey) : -1;
            if (entryItem >= 0)
            {
                returnScene = entry.ReturnScene;
                view.TitleOverride = entry.Title;
                back = entry.Back;
            }
            presenter = new GuideMenuPresenter(
                view,
                view.Definition,
                GoBack,
                departure != null ? Depart : null,
                ReturnHome
            );
            if (entryItem >= 0)
            {
                // 戻り先のない頼み（ほかの画面から戻ってきたとき）は、メニューから開いたのと同じにする。
                if (string.IsNullOrEmpty(entry.ReturnScene))
                    view.OpenItem(entry.ItemKey);
                else
                    presenter.OpenAsRoot(entryItem);
                ShowEntry(entryItem, entry.Entry);
            }
            RunesTask = LoadRunesAsync();
            if (departs)
                UnlockTask = ShowUnlockedAsync();
        }

        // ダンジョン一覧には、着いた街のダンジョンだけを出す（今の世界地図で町のマスを開けたか、
        // 前の街道でクリアした街。DungeonUnlocks）。読んである進み具合を先に出し、
        // 読み直した進み具合で出し直す。読めなければ、手元の進み具合のまま出す。
        private async Task ShowUnlockedAsync()
        {
            ShowUnlocked(WorldMapSession.Known ?? WorldMapProgress.Start);
            try
            {
                var atlas = LoadAtlasAsync();
                var progress = await WorldMapSession.LoadAsync(lifetime.Token);
                await atlas;
                if (progress != null && view != null)
                    ShowUnlocked(progress);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning("街道の進み具合を取得できませんでした。" + exception.Message);
            }
        }

        // 今の世界地図の開けたマス。読めなくても、街道の進み具合だけで出す。
        private async Task LoadAtlasAsync()
        {
            try
            {
                await WorldAtlasSession.LoadAsync(lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("開けたマスを取得できませんでした。" + exception.Message);
            }
        }

        private void ShowUnlocked(WorldMapProgress progress) =>
            view.ShowDestinations(destination =>
                DungeonUnlocks.Reached(destination.Town, progress)
            );

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

        private TravelDeparture NewDeparture() =>
            new(
                view.ShowToast,
                AdventureSession.SourceOrLocal,
                () => SceneLoader.Load(SceneNames.Exploration, transition, this)
            );

        // 行き先を指して冒険に出る。旅の案内所でない画面では、初めて出るときに出発を用意する。
        private bool SetOut(string destinationId)
        {
            if (lifetime.IsCancellationRequested)
                return false;
            departure ??= NewDeparture();
            return departure.Depart(destinationId);
        }

        private bool Depart(int index)
        {
            var destinations = view.Definition.Destinations;
            if (index < 0 || index >= destinations.Length)
                return false;
            return departure.Depart(destinations[index].Id);
        }

        // 開いた項目のパネルに、見せる物を渡す。
        private void ShowEntry(int item, string shown)
        {
            var panel = view.PanelFor(item);
            if (
                panel != null
                && !string.IsNullOrEmpty(shown)
                && panel.TryGetComponent(out IGuideEntryTarget target)
            )
                target.ShowEntry(shown);
        }

        // ライブラリの項目を開いてその物を見せる。もどる・×で、この画面の今の項目と物へ戻る（合成の材料の札）。
        private bool ShowInLibrary(string itemKey, string shown)
        {
            if (lifetime.IsCancellationRequested || presenter == null)
                return false;
            int item = presenter.State.Item;
            var panel = view.PanelFor(item);
            string current =
                panel != null && panel.TryGetComponent(out IGuideEntryTarget target)
                    ? target.CurrentEntry
                    : null;
            var here =
                item >= 0 && item < view.Definition.Items.Length
                    ? new GuideSceneEntry.Request(view.Definition.Items[item].Key, null, null, current)
                    : null;
            GuideSceneEntry.Open(
                new GuideSceneEntry.Request(itemKey, null, gameObject.scene.name, shown, here)
            );
            bool left = SceneLoader.Load(SceneNames.Library, transition, this);
            if (!left)
                GuideSceneEntry.Clear();
            return left;
        }

        private bool ReturnHome() =>
            Leave(string.IsNullOrEmpty(returnScene) ? homeSceneName : returnScene);

        private bool GoBack() => Leave(BackSceneName);

        // 元の画面へ戻るときは、開き直す項目と物を頼んでから移る。
        private bool Leave(string scene)
        {
            bool returning = back != null && scene == returnScene;
            if (returning)
                GuideSceneEntry.Open(back);
            bool left = SceneLoader.Load(scene, transition, this);
            if (!left && returning)
                GuideSceneEntry.Clear();
            return left;
        }
    }
}
