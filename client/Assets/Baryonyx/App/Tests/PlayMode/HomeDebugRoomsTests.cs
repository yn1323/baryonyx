using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Combat.Presentation;
using Baryonyx.Home;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // Homeの右上のデバッグルームの一覧。どのルームもビルドに入っていて開け、戻るボタンのないルームには
    // 「ホームへ」を重ねる。
    public sealed class HomeDebugRoomsTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices() => services = TestGameServices.Use();

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(HomeDebugRoomsTests));
        }

        [UnityTest]
        public IEnumerator TheListShowsEveryRoomAndClosesBackToHome()
        {
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            var view = home.View;
            Assert.That(view.Dialog.IsShown, Is.False);
            // ボタンは88の丸で、押せる範囲（HitArea）を広げて指の大きさにしている。
            SceneTests.AssertTouchSize(view.DebugRoomsButton.transform.Find("HitArea"));

            view.DebugRoomsButton.onClick.Invoke();
            Assert.That(view.Dialog.IsShown, Is.True);
            Assert.That(view.Dialog.TitleText, Is.EqualTo(HomeBootstrap.DebugRoomsTitle));
            Assert.That(
                view.Dialog.ChoiceLabels,
                Is.EqualTo(
                    SceneNames
                        .DebugRooms.Select(room => room.Name)
                        .Prepend(HomeBootstrap.DebugRunesChoice)
                        .Append(HomeBootstrap.CloseChoice)
                        .ToArray()
                )
            );
            foreach (var room in SceneNames.DebugRooms)
                Assert.That(Application.CanStreamedLevelBeLoaded(room.Scene), Is.True, room.Scene);

            // 閉じると、Homeのまま、ほかのボタンも押せる。
            Assert.That(view.Dialog.Choose(HomeBootstrap.CloseChoice), Is.True);
            Assert.That(view.Dialog.IsShown, Is.False);
            Assert.That(home.Presenter.ScreenOpened, Is.False);
            view.DebugRoomsButton.onClick.Invoke();
            Assert.That(view.Dialog.IsShown, Is.True);
        }

        [UnityTest]
        public IEnumerator ARoomWithoutItsOwnWayBackGetsAButtonToHome()
        {
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            home.View.DebugRoomsButton.onClick.Invoke();
            Assert.That(home.View.Dialog.Choose("敵の挙動デバッグルーム"), Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.EnemyLab,
                10f,
                "The enemy lab did not open."
            );
            var back = default(DebugRoomReturn);
            yield return SceneTests.WaitUntil(
                () => (back = Object.FindAnyObjectByType<DebugRoomReturn>()) != null,
                5f,
                "The button back to Home was not put over the room."
            );
            Assert.That(back.gameObject.scene.name, Is.EqualTo(SceneNames.EnemyLab));
            SceneTests.AssertTouchSize(back.Button.transform);

            back.Button.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                10f,
                "Home did not open again."
            );
            home = Object.FindAnyObjectByType<HomeBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.HomeReady(home));
            // Homeへ戻ったら、戻るボタンはルームと一緒に消えている。
            Assert.That(Object.FindAnyObjectByType<DebugRoomReturn>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator TheButtonFollowsTheMomentRoomsIntoEachOther()
        {
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            home.View.DebugRoomsButton.onClick.Invoke();
            Assert.That(home.View.Dialog.Choose("戦闘の演出デバッグルーム"), Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.BattleMomentLab,
                10f,
                "The battle moment lab did not open."
            );
            yield return SceneTests.WaitUntil(
                () => Object.FindAnyObjectByType<DebugRoomReturn>() != null,
                5f,
                "The button back to Home was not put over the battle moment lab."
            );

            // 「探索の演出へ」で移った先にも、その角（左下）に重ねる。
            Object.FindAnyObjectByType<BattleMomentLab>().OtherLabButton.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.ExplorationMomentLab,
                10f,
                "The exploration moment lab did not open."
            );
            var back = default(DebugRoomReturn);
            yield return SceneTests.WaitUntil(
                () => (back = Object.FindAnyObjectByType<DebugRoomReturn>()) != null,
                5f,
                "The button back to Home did not follow into the exploration moment lab."
            );
            Assert.That(back.gameObject.scene.name, Is.EqualTo(SceneNames.ExplorationMomentLab));
            Assert.That(back.Frame.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(
                Object.FindObjectsByType<DebugRoomReturn>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1)
            );

            back.Button.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                10f,
                "Home did not open again."
            );
            home = Object.FindAnyObjectByType<HomeBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.HomeReady(home));
            Assert.That(Object.FindAnyObjectByType<DebugRoomReturn>(), Is.Null);
        }
    }
}
