using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;
using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class AccountSessionRunnerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        private readonly List<AccountSession> issued = new();
        private DateTimeOffset expiresAt;

        [SetUp]
        public void SetUp()
        {
            issued.Clear();
            expiresAt = Now.AddHours(1);
        }

        private AccountSessionRunner Runner() =>
            new(
                _ =>
                {
                    var session = new AccountSession("guest", $"token{issued.Count}", expiresAt);
                    issued.Add(session);
                    return Task.FromResult(session);
                },
                () => Now
            );

        [Test]
        public void SignsInOnceAndReusesTheSession()
        {
            var runner = Runner();
            var started = new List<AccountSession>();
            runner.Started += started.Add;

            string first = runner
                .WithSessionAsync(session => Task.FromResult(session.Token), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            string second = runner
                .WithSessionAsync(session => Task.FromResult(session.Token), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(first, Is.EqualTo("token0"));
            Assert.That(second, Is.EqualTo("token0"));
            Assert.That(issued, Has.Count.EqualTo(1));
            Assert.That(started, Is.EqualTo(issued));
            Assert.That(runner.IsSignedIn, Is.True);
        }

        [Test]
        public void RenewsASessionAboutToExpire()
        {
            expiresAt = Now + AccountSessionRunner.RenewBefore - TimeSpan.FromSeconds(1);
            var runner = Runner();
            runner.ConnectAsync(CancellationToken.None).GetAwaiter().GetResult();
            runner.ConnectAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(issued, Has.Count.EqualTo(2));
            Assert.That(runner.Current, Is.SameAs(issued[1]));
        }

        [Test]
        public void SignsInAgainOnceWhenTheServerRejectsTheSession()
        {
            var runner = Runner();
            var tokens = new List<string>();

            string result = runner
                .WithSessionAsync(
                    session =>
                    {
                        tokens.Add(session.Token);
                        if (tokens.Count == 1)
                            throw new ServerApiException(401);
                        return Task.FromResult("saved");
                    },
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

            Assert.That(result, Is.EqualTo("saved"));
            Assert.That(tokens, Is.EqualTo(new[] { "token0", "token1" }));
        }

        [Test]
        public void ARejectionAfterTheRetryReachesTheCaller()
        {
            var runner = Runner();
            int calls = 0;

            var exception = Assert.Throws<ServerApiException>(() =>
                runner
                    .WithSessionAsync<string>(
                        _ =>
                        {
                            calls++;
                            throw new ServerApiException(401);
                        },
                        CancellationToken.None
                    )
                    .GetAwaiter()
                    .GetResult()
            );

            Assert.That(exception.StatusCode, Is.EqualTo(401));
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(issued, Has.Count.EqualTo(2));
        }

        [Test]
        public void EndForgetsTheSession()
        {
            var runner = Runner();
            runner.ConnectAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(runner.End(), Is.SameAs(issued[0]));
            Assert.That(runner.Current, Is.Null);
            Assert.That(runner.IsSignedIn, Is.False);
        }
    }
}
