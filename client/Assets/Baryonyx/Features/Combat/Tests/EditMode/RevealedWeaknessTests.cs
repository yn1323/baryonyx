using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Combat;
using Baryonyx.Combat.Records;
using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class RevealedWeaknessTests
    {
        [SetUp]
        public void ResetSession()
        {
            RevealedWeaknessSession.Reset();
            SaveQueue.Reset();
        }

        [TearDown]
        public void ClearSession()
        {
            RevealedWeaknessSession.Reset();
            SaveQueue.Reset();
        }

        [Test]
        public void EveryEnemyHasItsOwnIdAndWeaknessesTheServerKnows()
        {
            var ids = CombatEnemies.All.Select(enemy => enemy.Id).ToArray();
            Assert.That(ids, Is.Unique);
            Assert.That(ids, Is.EqualTo(new[] { "moss-slime", "moss-wolf", "forest-guardian" }));
            foreach (var enemy in CombatEnemies.All)
            {
                Assert.That(CombatEnemies.Find(enemy.Id), Is.SameAs(enemy));
                Assert.That(enemy.Weaknesses, Is.Not.Empty.And.Unique);
                Assert.That(enemy.Weaknesses, Has.None.EqualTo(CardElement.None));
            }
            Assert.That(CombatEnemies.Find("dragon"), Is.Null);
        }

        [Test]
        public void EveryElementHasTheServersName()
        {
            var elements = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Where(element => element != CardElement.None)
                .ToArray();
            Assert.That(
                elements.Select(CardElementKeys.Key),
                Is.EqualTo(new[] { "slash", "fire", "ice", "thunder", "blunt", "pierce" })
            );
            foreach (var element in elements)
            {
                Assert.That(CardElementKeys.TryParse(CardElementKeys.Key(element), out var back));
                Assert.That(back, Is.EqualTo(element));
            }
            Assert.That(CardElementKeys.TryParse("water", out _), Is.False);
            Assert.That(CardElementKeys.TryParse(null, out _), Is.False);
        }

        [Test]
        public void AWeaknessIsAddedOnceInTheOrderItWasRevealed()
        {
            var known = new RevealedWeaknesses();
            Assert.That(known.Add("moss-wolf", CardElement.Ice), Is.True);
            Assert.That(known.Add("moss-slime", CardElement.Fire), Is.True);
            Assert.That(known.Add("moss-wolf", CardElement.Ice), Is.False);
            Assert.That(known.Add("", CardElement.Ice), Is.False);
            Assert.That(known.Add("moss-wolf", CardElement.None), Is.False);
            Assert.That(
                known.All,
                Is.EqualTo(
                    new[]
                    {
                        new RevealedWeakness("moss-wolf", CardElement.Ice),
                        new RevealedWeakness("moss-slime", CardElement.Fire),
                    }
                )
            );
            Assert.That(known.Contains("moss-wolf", CardElement.Ice), Is.True);
            Assert.That(known.Contains("moss-wolf", CardElement.Fire), Is.False);
        }

        [Test]
        public void ServerAnswersLeaveOutUnknownElementsAndRowsWithoutAnEnemy()
        {
            var list = RevealedWeaknessServerSource.ToList(
                new RevealedWeaknessApiClient.State
                {
                    weaknesses = new[]
                    {
                        new RevealedWeaknessApiClient.Weakness
                        {
                            enemyId = "moss-slime",
                            element = "thunder",
                        },
                        new RevealedWeaknessApiClient.Weakness
                        {
                            enemyId = "moss-slime",
                            element = "water",
                        },
                        new RevealedWeaknessApiClient.Weakness { enemyId = "", element = "fire" },
                        null,
                    },
                }
            );
            Assert.That(
                list,
                Is.EqualTo(new[] { new RevealedWeakness("moss-slime", CardElement.Thunder) })
            );
            Assert.That(RevealedWeaknessServerSource.ToList(null), Is.Empty);
        }

        [Test]
        public async Task TheLoadAddsThoseRevealedBeforeOnceWhileTheAppRuns()
        {
            var server = new FakeSource();
            server.Saved.Add(new RevealedWeakness("forest-guardian", CardElement.Slash));
            RevealedWeaknessSession.Source = server;
            int changed = 0;
            RevealedWeaknessSession.Changed += () => changed++;

            await RevealedWeaknessSession.LoadAsync(CancellationToken.None);
            await RevealedWeaknessSession.LoadAsync(CancellationToken.None);

            Assert.That(
                RevealedWeaknessSession.IsRevealed("forest-guardian", CardElement.Slash),
                Is.True
            );
            Assert.That(server.Loads, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(1));

            await RevealedWeaknessSession.LoadAsync(CancellationToken.None, refresh: true);
            Assert.That(server.Loads, Is.EqualTo(2));
        }

        [Test]
        public async Task ARevealedWeaknessShowsAtOnceAndIsSaved()
        {
            var server = new FakeSource();
            RevealedWeaknessSession.Source = server;

            Assert.That(RevealedWeaknessSession.Record("moss-slime", CardElement.Thunder), Is.True);
            Assert.That(
                RevealedWeaknessSession.IsRevealed("moss-slime", CardElement.Thunder),
                Is.True
            );
            Assert.That(
                RevealedWeaknessSession.Record("moss-slime", CardElement.Thunder),
                Is.False
            );
            await RevealedWeaknessSession.Work;

            Assert.That(
                server.Saved,
                Is.EqualTo(new[] { new RevealedWeakness("moss-slime", CardElement.Thunder) })
            );
            Assert.That(server.Sends, Is.EqualTo(1));
            Assert.That(SaveQueue.Busy, Is.False);
        }

        [Test]
        public async Task AFailedSaveIsNotToldAsAFailure()
        {
            var told = new List<string>();
            SaveQueue.Failed += told.Add;
            RevealedWeaknessSession.Source = new FakeSource { Fails = true };

            RevealedWeaknessSession.Record("moss-slime", CardElement.Fire);
            await RevealedWeaknessSession.Work;

            Assert.That(told, Is.Empty);
            Assert.That(
                RevealedWeaknessSession.IsRevealed("moss-slime", CardElement.Fire),
                Is.True
            );
        }

        [Test]
        public async Task AWeaknessTheServerDidNotTakeStaysShownAndIsSentAgainWithTheNext()
        {
            var server = new FakeSource { Fails = true };
            RevealedWeaknessSession.Source = server;

            RevealedWeaknessSession.Record("moss-slime", CardElement.Thunder);
            await RevealedWeaknessSession.Work;
            Assert.That(server.Saved, Is.Empty);
            Assert.That(
                RevealedWeaknessSession.IsRevealed("moss-slime", CardElement.Thunder),
                Is.True
            );

            server.Fails = false;
            RevealedWeaknessSession.Record("moss-wolf", CardElement.Ice);
            await RevealedWeaknessSession.Work;
            Assert.That(
                server.Saved,
                Is.EquivalentTo(
                    new[]
                    {
                        new RevealedWeakness("moss-slime", CardElement.Thunder),
                        new RevealedWeakness("moss-wolf", CardElement.Ice),
                    }
                )
            );
        }

        [Test]
        public async Task AWeaknessTheServerDidNotTakeIsSentAgainWithTheNextLoad()
        {
            var server = new FakeSource { Fails = true };
            RevealedWeaknessSession.Source = server;
            RevealedWeaknessSession.Record("moss-wolf", CardElement.Fire);
            await RevealedWeaknessSession.Work;

            server.Fails = false;
            await RevealedWeaknessSession.LoadAsync(CancellationToken.None);
            await RevealedWeaknessSession.Work;
            Assert.That(
                server.Saved,
                Is.EqualTo(new[] { new RevealedWeakness("moss-wolf", CardElement.Fire) })
            );
        }

        [Test]
        public async Task WithoutAServerTheWeaknessesStayWhileTheAppRuns()
        {
            RevealedWeaknessSession.Record("moss-wolf", CardElement.Ice);
            await RevealedWeaknessSession.Work;
            var kept = await RevealedWeaknessSession.SourceOrLocal.LoadAsync(
                CancellationToken.None
            );
            Assert.That(
                kept,
                Is.EqualTo(new[] { new RevealedWeakness("moss-wolf", CardElement.Ice) })
            );
        }

        private sealed class FakeSource : IRevealedWeaknessSource
        {
            public readonly List<RevealedWeakness> Saved = new();
            public bool Fails;
            public int Loads;
            public int Sends;

            public async Task<IReadOnlyList<RevealedWeakness>> LoadAsync(CancellationToken token)
            {
                await Task.Yield();
                Loads++;
                if (Fails)
                    throw new InvalidOperationException("offline");
                return Saved.ToArray();
            }

            public async Task<IReadOnlyList<RevealedWeakness>> RevealAsync(
                IReadOnlyList<RevealedWeakness> weaknesses,
                CancellationToken token
            )
            {
                await Task.Yield();
                Sends++;
                if (Fails)
                    throw new InvalidOperationException("offline");
                foreach (var weakness in weaknesses)
                    if (!Saved.Contains(weakness))
                        Saved.Add(weakness);
                return Saved.ToArray();
            }
        }
    }
}
