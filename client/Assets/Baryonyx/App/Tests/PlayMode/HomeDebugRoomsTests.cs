using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Combat.Presentation;
using Baryonyx.Home;
using Baryonyx.Networking;
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
        public IEnumerator TheListShowsEveryRoomAndARoomGetsAButtonBackToHome()
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
                        .Prepend(DebugNetwork.Label(DebugNetwork.Mode.Normal))
                        .Prepend(HomeBootstrap.DebugRunesChoice)
                        .Append(HomeBootstrap.CloseChoice)
                        .ToArray()
                )
            );
            // 通信の遅れと失敗は、押すたびに次へ替わり、一覧を開いたまま名前が変わる。最後はふつうへ戻す。
            try
            {
                Assert.That(view.Dialog.Choose(DebugNetwork.Label(DebugNetwork.Mode.Normal)), Is.True);
                Assert.That(DebugNetwork.Current, Is.EqualTo(DebugNetwork.Mode.Slow));
                Assert.That(view.Dialog.IsShown, Is.True);
                Assert.That(
                    view.Dialog.ChoiceLabels,
                    Has.Member(DebugNetwork.Label(DebugNetwork.Mode.Slow))
                );
            }
            finally
            {
                DebugNetwork.Reset();
            }
            view.Dialog.Choose(HomeBootstrap.CloseChoice);
            view.DebugRoomsButton.onClick.Invoke();
            foreach (var room in SceneNames.DebugRooms)
                Assert.That(Application.CanStreamedLevelBeLoaded(room.Scene), Is.True, room.Scene);

            // 閉じると、Homeのまま、ほかのボタンも押せる。
            Assert.That(view.Dialog.Choose(HomeBootstrap.CloseChoice), Is.True);
            Assert.That(view.Dialog.IsShown, Is.False);
            Assert.That(home.Presenter.ScreenOpened, Is.False);
            view.DebugRoomsButton.onClick.Invoke();
            Assert.That(view.Dialog.IsShown, Is.True);

            // 自分の戻り方を持たないルームには、Homeへ戻るボタンを重ねる。
            Assert.That(view.Dialog.Choose("戦闘の演出デバッグルーム"), Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.BattleMomentLab,
                10f,
                "The battle moment lab did not open."
            );
            var first = default(DebugRoomReturn);
            yield return SceneTests.WaitUntil(
                () => (first = Object.FindAnyObjectByType<DebugRoomReturn>()) != null,
                5f,
                "The button back to Home was not put over the battle moment lab."
            );
            SceneTests.AssertTouchSize(first.Button.transform);

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
            // Homeへ戻ったら、戻るボタンはルームと一緒に消えている。
            Assert.That(Object.FindAnyObjectByType<DebugRoomReturn>(), Is.Null);
        }
    }
}
