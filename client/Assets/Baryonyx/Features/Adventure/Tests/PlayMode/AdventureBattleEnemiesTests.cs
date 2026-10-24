using System.Collections;
using System.Linq;
using System.Threading;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 冒険の戦闘では、ターン順を速度から決め、敵が敵の定義の技と行動パターンで動く（doc/features/combat.md）。
    public sealed class AdventureBattleEnemiesTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices() => services = TestGameServices.Use();

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(AdventureBattleEnemiesTests));
        }

        [UnityTest]
        public IEnumerator EveryEnemyActsAfterEachPartyTurnWithItsOwnSkills()
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
            var bootstrap = default(BattleBootstrap);
            yield return SceneTests.Load(
                SceneTests.BattlePath,
                (System.Func<BattleBootstrap, bool>)(_ => true),
                value => bootstrap = value
            );
            var view = bootstrap.Battle;
            yield return SceneTests.WaitUntil(
                () =>
                    !bootstrap.Transition.IsPlaying
                    && !bootstrap.Transition.IsCovered
                    && !view.Dealing
                    && !view.Acting,
                8f,
                "The battle did not get ready."
            );

            // The room's two enemies have speeds and skills, and act once in each round, the
            // faster first: one faster than the party before its turn (it already acted when the
            // battle began), a slower one after it. Not two party turns in a row as the mock's
            // order had.
            var standing = Enumerable
                .Range(0, view.Enemies.Length)
                .Where(i => view.Enemies[i].Alive)
                .ToArray();
            Assert.That(standing, Has.Length.EqualTo(2));
            foreach (int i in standing)
            {
                Assert.That(view.Enemies[i].Speed, Is.GreaterThan(0));
                Assert.That(view.Enemies[i].Brain, Is.Not.Null);
            }
            var faster = standing.OrderByDescending(i => view.Enemies[i].Speed).ToArray();
            double partySpeed = view.PartySpeed();
            var first = faster.Where(i => view.Enemies[i].Speed > partySpeed).ToArray();
            var after = faster.Where(i => view.Enemies[i].Speed <= partySpeed).ToArray();
            Assert.That(
                first,
                Is.EqualTo(view.OpeningEnemies()),
                "The enemies faster than the party act first."
            );
            const int party = BattleInspectView.PartyTurn;
            var round = new[] { party }.Concat(after).Concat(first).ToArray();
            Assert.That(view.UpcomingTurns(6), Is.EqualTo(round.Concat(round).Take(6).ToArray()));

            view.EndTurn();
            yield return SceneTests.WaitUntil(() => !view.EnemyTurn, 20f, "The enemies acted.");
            foreach (int i in standing)
                if (view.Enemies[i].Alive)
                    Assert.That(
                        view.Enemies[i].Brain.History.Count == (first.Contains(i) ? 2 : 1)
                            || view.IsCharging(i),
                        Is.True,
                        $"{view.Enemies[i].Id} used its skills, or gathers for one."
                    );
            Assert.That(view.BattleTurn, Is.EqualTo(2));
        }
    }
}
