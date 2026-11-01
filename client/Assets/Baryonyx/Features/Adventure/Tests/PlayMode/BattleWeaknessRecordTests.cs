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
        public IEnumerator OnlyTheWeaknessesRevealedBeforeShowFromTheStart()
        {
            var encounter = EnterTheFirstBattle();
            // 編成のいちばん手前の敵と、いちばん奥の敵（種類はみな違う）。
            Assert.That(encounter.Enemies, Has.Length.GreaterThanOrEqualTo(2));
            int front = encounter.Enemies[0];
            int back = encounter.Enemies[^1];
            var frontKind = CombatEnemies.Find(encounter.Kinds[front]);
            var backKind = CombatEnemies.Find(encounter.Kinds[back]);
            RevealedWeaknessSession.Record(frontKind.Id, frontKind.Weaknesses[0]);
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;

            // 手前の敵は記録した1つ目の弱点だけが見え、奥の敵はどれも伏せる。
            Assert.That(
                Shown(battle.Enemies[front]),
                Is.EqualTo(frontKind.Weaknesses.Select((_, i) => i == 0).ToArray())
            );
            Assert.That(
                Shown(battle.Enemies[back]),
                Is.EqualTo(backKind.Weaknesses.Select(_ => false).ToArray())
            );
        }

        [UnityTest]
        public IEnumerator TheEnemiesCarryTheirWeaknessesAndAHitOneShowsInTheNextBattle()
        {
            var encounter = EnterTheFirstBattle();
            int back = encounter.Enemies[^1];
            var backKind = CombatEnemies.Find(encounter.Kinds[back]);
            var first = backKind.Weaknesses[0];
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;
            // The battle has the room's kinds (enemies of the forest in the places of the room's
            // formation), each with its own weaknesses, all hidden at first.
            var area = AdventureCatalog.Area(AdventureCatalog.ForestRuins);
            for (int i = 0; i < battle.Enemies.Length; i++)
            {
                if (!encounter.Enemies.Contains(i))
                {
                    Assert.That(battle.Enemies[i].Alive, Is.False);
                    continue;
                }
                var kind = CombatEnemies.Find(encounter.Kinds[i]);
                Assert.That(kind, Is.Not.Null);
                Assert.That(battle.Enemies[i].Id, Is.EqualTo(kind.Id));
                Assert.That(area.Small.Concat(area.Medium), Does.Contain(kind.Id));
                Assert.That(
                    battle
                        .Enemies[i]
                        .Weaknesses.Select(weakness => BattleCardText.ElementOf(weakness.Element)),
                    Is.EqualTo(kind.Weaknesses)
                );
            }
            Assert.That(
                Shown(battle.Enemies[back]),
                Is.EqualTo(backKind.Weaknesses.Select(_ => false).ToArray())
            );

            // 奥の敵の伏せた1つ目の弱点を突く。
            battle.LabPlay(back, EnemyLabAction.WeakHit);
            yield return SceneTests.WaitUntil(
                () => RevealedWeaknessSession.IsRevealed(backKind.Id, first),
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
            Assert.That(kept, Is.EqualTo(new[] { new RevealedWeakness(backKind.Id, first) }));

            // 同じ部屋の戦闘を開き直すと、その弱点を最初から見せる。
            yield return LoadBattle(value => bootstrap = value);
            Assert.That(
                Shown(bootstrap.Battle.Enemies[back]),
                Is.EqualTo(backKind.Weaknesses.Select((_, i) => i == 0).ToArray())
            );
        }
    }
}
