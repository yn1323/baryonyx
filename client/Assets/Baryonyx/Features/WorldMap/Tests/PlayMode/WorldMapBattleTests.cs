using System.Collections;
using Baryonyx.App;
using Baryonyx.Combat.Presentation;
using Baryonyx.UI;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 街道の戦闘のマス（doc/features/stage-progression.md の街道のマスと移動）を、実際のシーンで通す。
    // 進み具合は、最初の街だけをクリアしたアプリ内の保存先から始める。
    public sealed class WorldMapBattleTests
    {
        // 最初の街の隣の、戦闘のマス。
        private const string Square = "n14-10";

        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            WorldMapSession.Reset();
            WorldMapSession.Source = new WorldMapMemorySource(WorldMapProgress.Start);
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            WorldMapSession.Reset();
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(WorldMapBattleTests));
        }

        private static IEnumerator WaitForScene(string name) =>
            SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == name,
                10f,
                name + " did not open."
            );

        private static IEnumerator WaitForDialog(GameDialog dialog, string title) =>
            SceneTests.WaitUntil(
                () => dialog.IsShown && !dialog.Busy && dialog.TitleText == title,
                30f,
                $"The dialog \"{title}\" did not show (it shows \"{dialog.TitleText}\")."
            );

        // 地図で戦闘のマスを押して歩き、着くと戦闘の画面を開く。戦闘の敵は、そのマスの決まった敵。
        private static IEnumerator FightAt(string square, System.Action<BattleBootstrap> found)
        {
            var map = default(WorldMapBootstrap);
            yield return SceneTests.LoadWorldMap(value => map = value);
            int index = map.View.Graph.IndexOf(square);
            Assert.That(map.View.States[index], Is.EqualTo(WorldNodeState.Open));
            Assert.That(map.View.Tap(index), Is.True);
            yield return WaitForScene(SceneNames.Battle);
            var bootstrap = Object.FindAnyObjectByType<BattleBootstrap>();
            yield return SceneTests.WaitUntil(
                () =>
                    !bootstrap.Transition.IsPlaying
                    && !bootstrap.Transition.IsCovered
                    && !bootstrap.Battle.Dealing
                    && !bootstrap.Battle.Acting,
                10f,
                "The battle did not get ready."
            );
            Assert.That(bootstrap.WorldMapFlow, Is.Not.Null);
            Assert.That(bootstrap.Flow, Is.Null);
            var kinds = WorldMapEncounters.Of(square).Enemies.Kinds;
            for (int i = 0; i < kinds.Count; i++)
                if (kinds[i] != null)
                    Assert.That(bootstrap.Battle.Enemies[i].Id, Is.EqualTo(kinds[i]));
            found(bootstrap);
        }

        private static IEnumerator BackOnTheMap(System.Action<WorldMapBootstrap> found)
        {
            yield return WaitForScene(SceneNames.WorldMap);
            var map = Object.FindAnyObjectByType<WorldMapBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.WorldMapReady(map));
            found(map);
        }

        // 勝つとマスをクリアして、そのマスに立った地図へ戻る。
        [UnityTest]
        public IEnumerator AWinClearsTheSquareAndReturnsToTheMap()
        {
            var bootstrap = default(BattleBootstrap);
            yield return FightAt(Square, value => bootstrap = value);
            Assert.That(WorldMapSession.Known.Cleared, Has.No.Member(Square));

            var battle = bootstrap.Battle;
            foreach (var enemy in battle.Enemies)
                enemy.Hp = 0;
            battle.EndTurn();
            var dialog = bootstrap.Overlay.Dialog;
            yield return WaitForDialog(dialog, WorldMapBattleFlow.VictoryTitle);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(WorldMapSession.Known.Cleared, Has.Member(Square));

            dialog.Choose(WorldMapBattleFlow.MapChoice);
            var map = default(WorldMapBootstrap);
            yield return BackOnTheMap(value => map = value);
            Assert.That(WorldMapSession.Battle, Is.Null);
            Assert.That(map.View.Graph.Nodes[map.View.PartyAt].Id, Is.EqualTo(Square));
            Assert.That(
                map.View.States[map.View.Graph.IndexOf(Square)],
                Is.EqualTo(WorldNodeState.Cleared)
            );
        }

        // 全滅すると、もう一度挑むか地図へ戻るかを選ぶ。マスはクリアにならず、パーティは元のマスへ戻る。
        [UnityTest]
        public IEnumerator AWipeoutLeavesTheSquareToFightAgain()
        {
            var bootstrap = default(BattleBootstrap);
            yield return FightAt(Square, value => bootstrap = value);
            var battle = bootstrap.Battle;
            foreach (var ally in battle.Allies)
                ally.Hp = 0;
            battle.EndTurn();
            var dialog = bootstrap.Overlay.Dialog;
            yield return WaitForDialog(dialog, WorldMapBattleFlow.DefeatTitle);
            Assert.That(
                dialog.ChoiceLabels,
                Is.EqualTo(
                    new[] { WorldMapBattleFlow.RetryChoice, WorldMapBattleFlow.ReturnChoice }
                )
            );

            // 挑み直すと、同じマスの戦闘を始めから開き直す。
            dialog.Choose(WorldMapBattleFlow.RetryChoice);
            yield return SceneTests.WaitUntil(
                () =>
                {
                    var again = Object.FindAnyObjectByType<BattleBootstrap>();
                    return again != null
                        && again != bootstrap
                        && again.WorldMapFlow != null
                        && !again.Transition.IsPlaying
                        && !again.Transition.IsCovered;
                },
                10f,
                "The battle did not open again."
            );
            bootstrap = Object.FindAnyObjectByType<BattleBootstrap>();
            Assert.That(bootstrap.WorldMapFlow.SquareId, Is.EqualTo(Square));

            // メニューから地図へ戻る。マスはクリアにならない。
            bootstrap.Overlay.MenuButton.onClick.Invoke();
            dialog = bootstrap.Overlay.Dialog;
            yield return WaitForDialog(dialog, WorldMapBattleFlow.MenuTitle);
            dialog.Choose(WorldMapBattleFlow.ReturnChoice);
            var map = default(WorldMapBootstrap);
            yield return BackOnTheMap(value => map = value);
            Assert.That(
                map.View.Graph.Nodes[map.View.PartyAt].Id,
                Is.EqualTo(WorldMapCatalog.Start)
            );
            Assert.That(
                map.View.States[map.View.Graph.IndexOf(Square)],
                Is.EqualTo(WorldNodeState.Open)
            );
        }
    }
}
