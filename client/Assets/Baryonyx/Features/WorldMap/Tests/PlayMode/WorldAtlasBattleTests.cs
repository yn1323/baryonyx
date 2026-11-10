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
    // 入れ子の地図の戦闘のマスの「たたかう」は、マスの設定から引いた敵で戦闘の画面を開き、
    // 勝つと世界地図へ戻る（doc/features/world-atlas.md#戦闘の設定）。
    public sealed class WorldAtlasBattleTests
    {
        // 世界地図の、始まりから7マス先の戦闘のマス。
        private const string Square = "world-38";

        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            WorldAtlasSession.Reset();
            WorldAtlasNavigator.Forget();
            WorldAtlasBattle.Pending = null;
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            WorldAtlasBattle.Pending = null;
            WorldAtlasSession.Reset();
            WorldAtlasNavigator.Forget();
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(WorldAtlasBattleTests));
        }

        [UnityTest]
        public IEnumerator FightingDrawsTheSquaresEnemiesAndAWinGoesBackToTheWorldMap()
        {
            var world = default(WorldBootstrap);
            yield return SceneTests.LoadWorld(value => world = value);
            Assert.That(world.Navigator.BattleAt(Square, 1), Is.Not.Null);

            world.Navigator.StartBattle(Square);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Battle,
                10f,
                "The battle scene did not open."
            );
            var pending = WorldAtlasBattle.Pending;
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.Square, Is.EqualTo(Square));
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
            // マスの戦闘の窓は初めて止まったときにだけ開くため、後で挑み直せるとは言わない。
            Assert.That(
                bootstrap.WorldMapFlow.DefeatText,
                Is.EqualTo(WorldMapBattleFlow.OnceDefeatBody)
            );
            Assert.That(
                bootstrap.WorldMapFlow.MenuText,
                Is.EqualTo(WorldMapBattleFlow.OnceMenuBody)
            );
            Assert.That(
                bootstrap.WorldMapFlow.MenuTitleText,
                Is.EqualTo(WorldMapBattleFlow.OnceMenuTitle)
            );
            var kinds = pending.Encounter.Enemies.Kinds;
            for (int i = 0; i < kinds.Count; i++)
                if (kinds[i] != null)
                    Assert.That(bootstrap.Battle.Enemies[i].Id, Is.EqualTo(kinds[i]));

            var battle = bootstrap.Battle;
            foreach (var enemy in battle.Enemies)
                enemy.Hp = 0;
            battle.EndTurn();
            var dialog = bootstrap.Overlay.Dialog;
            yield return SceneTests.WaitUntil(
                () =>
                    dialog.IsShown
                    && !dialog.Busy
                    && dialog.TitleText == WorldMapBattleFlow.VictoryTitle,
                30f,
                "The victory did not show."
            );
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            dialog.Choose(WorldMapBattleFlow.MapChoice);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.World,
                10f,
                "The world map did not open again."
            );
            Assert.That(WorldAtlasBattle.Pending, Is.Null);
        }
    }
}
