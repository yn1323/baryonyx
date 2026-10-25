using System.Collections;
using System.Threading;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Tavern;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 冒険の途中に、探索の「装備」から酒場の装備の人選びを開き、もどる・×で探索へ戻る
    // （doc/features/screens/adventure.md の冒険の画面）。冒険はアプリ内だけの冒険を使う。
    public sealed class AdventureEquipmentTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            AdventureVitals.UseStore(new MemoryAdventureVitalsStore());
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            GuideSceneEntry.Clear();
            AdventureVitals.UseStore(null);
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(AdventureEquipmentTests));
        }

        // 冒険を始めて最初のマスへ進み、その出来事を終えて、次のマスを選ぶところにする。
        private void StartAndClearFirstRoom()
        {
            var source = services.Adventure;
            var state = AdventureSession.Use(
                source
                    .StartAsync(AdventureCatalog.ForestRuins, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
            );
            var room = state.Run.Exits[0];
            source.MoveAsync(room, CancellationToken.None).GetAwaiter().GetResult();
            AdventureSession.Use(
                source
                    .ClearAsync(
                        room.Id,
                        CancellationToken.None,
                        null,
                        new AdventureClearOptions
                        {
                            Pass = room.Kind == AdventureRoomKind.ShadyChest,
                        }
                    )
                    .GetAwaiter()
                    .GetResult()
            );
        }

        private static bool ExplorationReady(ExplorationBootstrap bootstrap) =>
            bootstrap.Flow != null
            && !bootstrap.Flow.Busy
            && !bootstrap.Transition.IsPlaying
            && !bootstrap.View.Walking
            && !bootstrap.View.Clearing;

        private static IEnumerator WaitForScene(string name) =>
            SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == name,
                8f,
                name + " did not open."
            );

        // 探索の「装備」を押して、酒場の装備の人選びが「冒険 › 装備」で開くまで進める。
        private IEnumerator OpenEquipment(System.Action<GuideSceneBootstrap> found)
        {
            StartAndClearFirstRoom();
            var exploration = default(ExplorationBootstrap);
            yield return SceneTests.Load<ExplorationBootstrap>(
                SceneTests.ExplorationPath,
                ExplorationReady,
                value => exploration = value
            );
            Assert.That(exploration.View.EquipmentButton, Is.Not.Null);
            exploration.View.EquipmentButton.onClick.Invoke();
            yield return WaitForScene(SceneNames.Pub);
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.WaitUntil(
                () =>
                {
                    guide = Object.FindAnyObjectByType<GuideSceneBootstrap>();
                    return guide != null && guide.Presenter != null && !guide.Transition.IsPlaying;
                },
                8f,
                "The tavern did not get ready."
            );
            var view = guide.View;
            Assert.That(guide.Presenter.State.Page, Is.EqualTo(GuideMenuPage.List));
            Assert.That(
                guide.Presenter.State.Item,
                Is.EqualTo(view.IndexOfItem(AdventurerPickerView.GuideItemKey))
            );
            Assert.That(
                view.Trail(),
                Is.EqualTo(
                    new[]
                    {
                        ExplorationBootstrap.AdventureEquipmentTitle,
                        view.LabelOf(AdventurerPickerView.GuideItemKey),
                    }
                )
            );
            Assert.That(guide.BackSceneName, Is.EqualTo(SceneNames.Exploration));
            found(guide);
        }

        [UnityTest]
        public IEnumerator BackFromTheEquipmentReturnsToTheAdventure()
        {
            var guide = default(GuideSceneBootstrap);
            yield return OpenEquipment(value => guide = value);
            string run = AdventureSession.Current.Run.Id;

            // メニューを挟まずに、1回のもどるで探索へ戻る。冒険はそのまま続く。
            guide.Presenter.Back();
            yield return WaitForScene(SceneNames.Exploration);
            var exploration = default(ExplorationBootstrap);
            yield return SceneTests.WaitUntil(
                () =>
                {
                    exploration = Object.FindAnyObjectByType<ExplorationBootstrap>();
                    return exploration != null && ExplorationReady(exploration);
                },
                8f,
                "The exploration did not get ready."
            );
            Assert.That(AdventureSession.Current.Run.Id, Is.EqualTo(run));
            Assert.That(exploration.Flow.State.Run.Id, Is.EqualTo(run));
        }

        [UnityTest]
        public IEnumerator TheCloseButtonAlsoReturnsToTheAdventure()
        {
            var guide = default(GuideSceneBootstrap);
            yield return OpenEquipment(value => guide = value);
            guide.Presenter.Home();
            yield return WaitForScene(SceneNames.Exploration);
        }

        // ほかの画面から開いた頼みは1回だけ効き、次にHomeから開いた酒場はいつもどおりメニューから始まる。
        [UnityTest]
        public IEnumerator TheTavernOpenedLaterStartsAtItsMenu()
        {
            GuideSceneEntry.Open(
                AdventurerPickerView.GuideItemKey,
                ExplorationBootstrap.AdventureEquipmentTitle,
                SceneNames.Exploration
            );
            Assert.That(GuideSceneEntry.Take(), Is.Not.Null);
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            Assert.That(guide.Presenter.State.Page, Is.EqualTo(GuideMenuPage.Menu));
            Assert.That(guide.ReturnScene, Is.Null.Or.Empty);
            Assert.That(guide.View.Trail()[0], Is.EqualTo(guide.View.Definition.Title));
        }
    }
}
