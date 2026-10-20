using System.Collections;
using System.Linq;
using System.Threading;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using Baryonyx.Combat.Records;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 冒険の戦闘で、開示した敵の弱点（doc/features/combat.md の弱点の開示）を記録し、以後の戦闘で最初から見せる。
    // 記録はテストごとに空のアプリ内の記録（RevealedWeaknessSession）から始める。
    public sealed class BattleWeaknessRecordTests
    {
        // 戦闘画面の手前の小さな敵と、中くらいの敵の場所。
        private const int Slime = AdventureCatalog.SmallPlace;
        private const int Wolf = AdventureCatalog.MediumPlace;

        private TestGameServices services;

        [SetUp]
        public void UseTestServices() => services = TestGameServices.Use();

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(BattleWeaknessRecordTests));
        }

        // 冒険を始め、入口の次の部屋（戦闘）へ進んだ状態にする。その部屋の敵の組み合わせを返す。
        private AdventureEncounter EnterTheFirstBattle()
        {
            var source = services.Adventure;
            var state = AdventureSession.Use(
                source
                    .StartAsync(AdventureCatalog.ForestRuins, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
            );
            AdventureSession.Use(
                source
                    .MoveAsync(state.Run.Exits[0], CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
            );
            return AdventureCatalog.Encounter(AdventureSession.Current.Run.Room.Encounter);
        }

        private static IEnumerator LoadBattle(System.Action<BattleBootstrap> found)
        {
            var bootstrap = default(BattleBootstrap);
            yield return SceneTests.Load(
                SceneTests.BattlePath,
                (System.Func<BattleBootstrap, bool>)(_ => true),
                value => bootstrap = value
            );
            yield return SceneTests.WaitUntil(
                () =>
                    !bootstrap.Transition.IsPlaying
                    && !bootstrap.Transition.IsCovered
                    && !bootstrap.Battle.Dealing
                    && !bootstrap.Battle.Acting,
                8f,
                "The battle did not get ready."
            );
            found(bootstrap);
        }

        private static bool[] Shown(BattleInspectEnemy enemy) =>
            enemy.Weaknesses.Select(weakness => weakness.Revealed).ToArray();

        [UnityTest]
        public IEnumerator TheEnemiesCarryTheirKindsAndWeaknesses()
        {
            var encounter = EnterTheFirstBattle();
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var enemies = bootstrap.Battle.Enemies;
            // The battle has the room's kinds (a small and a middle-sized enemy of the forest),
            // each with its own weaknesses.
            var kinds = encounter.Kinds.Take(2).Select(CombatEnemies.Find).ToArray();
            Assert.That(kinds, Has.None.Null);
            Assert.That(
                enemies.Take(2).Select(enemy => enemy.Id),
                Is.EqualTo(kinds.Select(kind => kind.Id))
            );
            Assert.That(enemies[2].Alive, Is.False);
            var area = AdventureCatalog.Area(AdventureCatalog.ForestRuins);
            Assert.That(area.Small, Does.Contain(kinds[0].Id));
            Assert.That(area.Medium, Does.Contain(kinds[1].Id));
            for (int i = 0; i < kinds.Length; i++)
                Assert.That(
                    enemies[i]
                        .Weaknesses.Select(weakness => BattleCardText.ElementOf(weakness.Element)),
                    Is.EqualTo(kinds[i].Weaknesses)
                );
        }

        [UnityTest]
        public IEnumerator OnlyTheWeaknessesRevealedBeforeShowFromTheStart()
        {
            var encounter = EnterTheFirstBattle();
            var small = CombatEnemies.Find(encounter.Kinds[Slime]);
            var medium = CombatEnemies.Find(encounter.Kinds[Wolf]);
            RevealedWeaknessSession.Record(small.Id, small.Weaknesses[0]);
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;

            // 小さな敵は記録した1つ目の弱点だけが見え、中くらいの敵はどれも伏せる。
            Assert.That(
                Shown(battle.Enemies[Slime]),
                Is.EqualTo(small.Weaknesses.Select((_, i) => i == 0).ToArray())
            );
            Assert.That(
                Shown(battle.Enemies[Wolf]),
                Is.EqualTo(medium.Weaknesses.Select(_ => false).ToArray())
            );
        }

        [UnityTest]
        public IEnumerator AWeaknessHitIsRecordedAndShowsInTheNextBattle()
        {
            var encounter = EnterTheFirstBattle();
            var medium = CombatEnemies.Find(encounter.Kinds[Wolf]);
            var first = medium.Weaknesses[0];
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;
            Assert.That(
                Shown(battle.Enemies[Wolf]),
                Is.EqualTo(medium.Weaknesses.Select(_ => false).ToArray())
            );

            // 中くらいの敵の伏せた1つ目の弱点を突く。
            battle.LabPlay(Wolf, EnemyLabAction.WeakHit);
            yield return SceneTests.WaitUntil(
                () => RevealedWeaknessSession.IsRevealed(medium.Id, first),
                8f,
                "The weakness hit was not recorded."
            );
            yield return SceneTests.WaitUntil(
                () => RevealedWeaknessSession.Work.IsCompleted,
                8f,
                "The weakness was not saved."
            );
            var kept = RevealedWeaknessSession
                .SourceOrLocal.LoadAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert.That(
                kept,
                Is.EqualTo(
                    new[] { new RevealedWeakness(medium.Id, first) }
                )
            );

            // 同じ部屋の戦闘を開き直すと、その弱点を最初から見せる。
            yield return LoadBattle(value => bootstrap = value);
            Assert.That(
                Shown(bootstrap.Battle.Enemies[Wolf]),
                Is.EqualTo(medium.Weaknesses.Select((_, i) => i == 0).ToArray())
            );
        }
    }
}
