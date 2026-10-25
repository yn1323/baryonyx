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

        [TestCase(0)]
        [TestCase(409)]
        [TestCase(500)]
        public void AFailureOtherThanARejectedSessionReachesTheCallerWithoutSigningInAgain(
            int status
        )
        {
            var runner = Runner();
            int calls = 0;

            var exception = Assert.Throws<ServerApiException>(() =>
                runner
                    .WithSessionAsync<string>(
                        _ =>
                        {
                            calls++;
                            throw new ServerApiException(status);
                        },
                        CancellationToken.None
                    )
                    .GetAwaiter()
                    .GetResult()
            );

            Assert.That(exception.StatusCode, Is.EqualTo(status));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(issued, Has.Count.EqualTo(1));
            Assert.That(runner.Current, Is.SameAs(issued[0]), "セッションは捨てない。");
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

    // 同時に届いた要求は、実行中のゲストのログインを待つ。
    public sealed class AccountSessionRunnerSharedSignInTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

        private readonly List<TaskCompletionSource<AccountSession>> signIns = new();
        private SynchronizationContext context;

        // ログインの答えを返したその場で、待っている要求の続きを走らせる。
        [SetUp]
        public void RunContinuationsInline()
        {
            signIns.Clear();
            context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void RestoreContext() => SynchronizationContext.SetSynchronizationContext(context);

        private AccountSessionRunner Runner() =>
            new(
                _ =>
                {
                    var signIn = new TaskCompletionSource<AccountSession>();
                    signIns.Add(signIn);
                    return signIn.Task;
                },
                () => Now
            );

        private static AccountSession Session(string token) => new("guest", token, Now.AddHours(1));

        [Test]
        public void CallsMadeDuringASignInShareIt()
        {
            var runner = Runner();

            var party = runner.WithSessionAsync(
                session => Task.FromResult("party:" + session.Token),
                CancellationToken.None
            );
            var equipment = runner.WithSessionAsync(
                session => Task.FromResult("equipment:" + session.Token),
                CancellationToken.None
            );
            Assert.That(signIns, Has.Count.EqualTo(1));
            signIns[0].SetResult(Session("token0"));

            Assert.That(party.Result, Is.EqualTo("party:token0"));
            Assert.That(equipment.Result, Is.EqualTo("equipment:token0"));
            Assert.That(signIns, Has.Count.EqualTo(1));
        }

        [Test]
        public void AFailedSignInReachesEveryWaiterAndTheNextCallSignsInAgain()
        {
            var runner = Runner();
            var first = runner.ConnectAsync(CancellationToken.None);
            var second = runner.ConnectAsync(CancellationToken.None);

            signIns[0].SetException(new ServerApiException(503));

            Assert.That(first.IsFaulted && second.IsFaulted, Is.True);
            var again = runner.ConnectAsync(CancellationToken.None);
            Assert.That(signIns, Has.Count.EqualTo(2));
            signIns[1].SetResult(Session("token1"));
            Assert.That(again.IsCompletedSuccessfully, Is.True);
            Assert.That(runner.Current.Token, Is.EqualTo("token1"));
        }

        // 画面を閉じて取り消した要求があっても、同じログインを待つほかの要求は続く。
        [Test]
        public void ACancelledCallerDoesNotStopTheSharedSignIn()
        {
            var runner = Runner();
            using var closed = new CancellationTokenSource();
            var cancelled = runner.ConnectAsync(closed.Token);
            var waiting = runner.ConnectAsync(CancellationToken.None);

            closed.Cancel();
            signIns[0].SetResult(Session("token0"));

            Assert.That(cancelled.IsCanceled, Is.True);
            Assert.That(waiting.IsCompletedSuccessfully, Is.True);
            Assert.That(runner.Current.Token, Is.EqualTo("token0"));
        }

        // 同時に断られた要求は、先に取り直したセッションを捨てずに使う。
        [Test]
        public void RequestsRejectedTogetherSignInAgainOnce()
        {
            var runner = Runner();
            var started = runner.ConnectAsync(CancellationToken.None);
            signIns[0].SetResult(Session("token0"));
            Assert.That(started.IsCompletedSuccessfully, Is.True);
            var rejections = new List<TaskCompletionSource<string>>();
            Task<string> Call() =>
                runner.WithSessionAsync(
                    session =>
                    {
                        if (session.Token != "token0")
                            return Task.FromResult(session.Token);
                        var rejection = new TaskCompletionSource<string>();
                        rejections.Add(rejection);
                        return rejection.Task;
                    },
                    CancellationToken.None
                );

            var first = Call();
            var second = Call();
            rejections[0].SetException(new ServerApiException(401));
            signIns[1].SetResult(Session("token1"));
            rejections[1].SetException(new ServerApiException(401));

            Assert.That(first.Result, Is.EqualTo("token1"));
            Assert.That(second.Result, Is.EqualTo("token1"));
            Assert.That(signIns, Has.Count.EqualTo(2));
        }
    }
}
