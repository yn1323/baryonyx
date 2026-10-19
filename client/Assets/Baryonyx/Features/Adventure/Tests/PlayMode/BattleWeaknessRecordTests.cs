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
        private const int Slime = AdventureCatalog.MossSlime;
        private const int Wolf = AdventureCatalog.MossWolf;

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

        // 冒険を始め、入口の次の部屋（戦闘）へ進んだ状態にする。
        private void EnterTheFirstBattle()
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
            EnterTheFirstBattle();
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var enemies = bootstrap.Battle.Enemies;
            // The battle here has the three kinds the battle mock was made with, not every kind.
            var kinds = new[]
            {
                CombatEnemies.MossSlime,
                CombatEnemies.MossWolf,
                CombatEnemies.ForestGuardian,
            };
            Assert.That(
                enemies.Select(enemy => enemy.Id),
                Is.EqualTo(kinds.Select(kind => kind.Id))
            );
            for (int i = 0; i < enemies.Length; i++)
                Assert.That(
                    enemies[i]
                        .Weaknesses.Select(weakness => BattleCardText.ElementOf(weakness.Element)),
                    Is.EqualTo(kinds[i].Weaknesses)
                );
        }

        [UnityTest]
        public IEnumerator OnlyTheWeaknessesRevealedBeforeShowFromTheStart()
        {
            RevealedWeaknessSession.Record(CombatEnemies.MossSlime.Id, CardElement.Thunder);
            EnterTheFirstBattle();
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;

            // 苔スライムは雷だけが見え、モックで見せていた苔むした狼の氷も伏せる。
            Assert.That(Shown(battle.Enemies[Slime]), Is.EqualTo(new[] { true, false }));
            Assert.That(Shown(battle.Enemies[Wolf]), Is.EqualTo(new[] { false, false }));
        }

        [UnityTest]
        public IEnumerator AWeaknessHitIsRecordedAndShowsInTheNextBattle()
        {
            EnterTheFirstBattle();
            var bootstrap = default(BattleBootstrap);
            yield return LoadBattle(value => bootstrap = value);
            var battle = bootstrap.Battle;
            Assert.That(Shown(battle.Enemies[Wolf]), Is.EqualTo(new[] { false, false }));

            // 苔むした狼の伏せた弱点（氷）を突く。
            battle.LabPlay(Wolf, EnemyLabAction.WeakHit);
            yield return SceneTests.WaitUntil(
                () =>
                    RevealedWeaknessSession.IsRevealed(CombatEnemies.MossWolf.Id, CardElement.Ice),
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
                    new[] { new RevealedWeakness(CombatEnemies.MossWolf.Id, CardElement.Ice) }
                )
            );

            // 同じ部屋の戦闘を開き直すと、氷を最初から見せる。
            yield return LoadBattle(value => bootstrap = value);
            Assert.That(Shown(bootstrap.Battle.Enemies[Wolf]), Is.EqualTo(new[] { true, false }));
        }
    }
}
