using System.Collections;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.App;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 冒険のデバッグルーム（doc/features/screens/debug-rooms.md#冒険のデバッグルーム）。
    public sealed class AdventureLabTests
    {
        private const string LabPath = SceneTests.ScenesFolder + "/Debug/AdventureLab.unity";

        private TestGameServices services;

        // シーンを開いたテストだけ閉じる。開かずに閉じると、テストを動かす最初のシーンまで閉じて実行が止まる。
        private bool opened;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            AdventureVitals.UseStore(new MemoryAdventureVitalsStore());
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            AdventureLab.End();
            AdventureVitals.UseStore(null);
            services?.Dispose();
            services = null;
            if (opened)
                yield return SceneTests.UnloadAll(nameof(AdventureLabTests));
            opened = false;
        }

        // どの行き先でも、選べる種類のマスはどれも、その手前まで進んだ冒険を作れる。
        [Test]
        public void EveryKindOfRoomCanBeReachedInEveryDestination()
        {
            foreach (var area in AdventureCatalog.Areas)
            foreach (var room in AdventureLab.NextRooms)
            {
                var source = AdventureLab.Prepare(area.Id, room, out var state);
                Assert.That(source, Is.Not.Null, area.Id + " " + room.Label);
                Assert.That(state.Run.RoomCleared, Is.True);
                Assert.That(state.Run.Exits.Select(exit => exit.Kind), Has.Member(room.Kind));
            }
        }

        // 出発すると、選んだマスの手前に立った探索が代役の冒険で開き、ホームへ戻るとサーバーの冒険へ戻る。
        [UnityTest]
        public IEnumerator TheExplorationOpensBeforeTheRoomChosenAndHomeTakesTheServerBack()
        {
            var server = AdventureSession.Source;
            var lab = default(AdventureLab);
            opened = true;
            yield return SceneTests.Load<AdventureLab>(LabPath, _ => true, value => lab = value);
            int rest = System.Array.FindIndex(
                AdventureLab.NextRooms,
                room => room.Kind == AdventureRoomKind.Rest
            );
            lab.Choose(0, rest, 1);
            Assert.That(lab.Go(), Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Exploration,
                10f,
                "The exploration did not open."
            );
            Assert.That(AdventureLab.Active, Is.True);
            Assert.That(AdventureSession.Source, Is.Not.SameAs(server));
            Assert.That(
                AdventureSession.Current.Run.Exits.Select(exit => exit.Kind),
                Has.Member(AdventureRoomKind.Rest)
            );
            Assert.That(
                AdventureVitals.Of(AdventureSession.Current.Run.Id),
                Has.All.EqualTo(0.5f).Within(0.001f)
            );

            AdventureLab.End();
            Assert.That(AdventureLab.Active, Is.False);
            Assert.That(AdventureSession.Source, Is.SameAs(server));
        }
    }
}
