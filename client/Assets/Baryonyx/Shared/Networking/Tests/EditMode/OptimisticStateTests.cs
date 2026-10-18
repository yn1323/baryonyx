using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class OptimisticStateTests
    {
        // 変更を順に足していく値。サーバーの答えは、送った変更までを足した値にする。
        private sealed class Numbers
        {
            public Numbers(params int[] values) => Values = values;

            public int[] Values { get; }

            public Numbers With(int value)
            {
                var next = new int[Values.Length + 1];
                Values.CopyTo(next, 0);
                next[^1] = value;
                return new Numbers(next);
            }
        }

        private readonly List<string> failures = new();

        [SetUp]
        public void ResetQueue()
        {
            SaveQueue.Reset();
            failures.Clear();
            SaveQueue.Failed += failures.Add;
        }

        [TearDown]
        public void ClearQueue() => SaveQueue.Reset();

        [Test]
        public void AChangeShowsAtOnceAndTheServersAnswerBecomesConfirmed()
        {
            var state = new OptimisticState<Numbers>(new Numbers());
            var answer = new TaskCompletionSource<Numbers>();
            int changes = 0;
            state.Changed += () => changes++;

            var saved = state.ChangeAsync(n => n.With(1), _ => answer.Task, _ => "失敗");
            Assert.That(state.Current.Values, Is.EqualTo(new[] { 1 }));
            Assert.That(state.Confirmed.Values, Is.Empty);
            Assert.That(state.Pending, Is.True);
            Assert.That(SaveQueue.Busy, Is.True);
            Assert.That(changes, Is.EqualTo(1));

            answer.SetResult(new Numbers(1));
            Assert.That(saved.IsCompleted && saved.Result, Is.True);
            Assert.That(state.Pending, Is.False);
            Assert.That(state.Confirmed.Values, Is.EqualTo(new[] { 1 }));
            Assert.That(state.Current, Is.SameAs(state.Confirmed));
            Assert.That(SaveQueue.Busy, Is.False);
            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void ChangesAreSentInOrderAndPendingOnesStayOverTheAnswer()
        {
            var state = new OptimisticState<Numbers>(new Numbers());
            var first = new TaskCompletionSource<Numbers>();
            var second = new TaskCompletionSource<Numbers>();
            var sent = new List<int>();

            state.ChangeAsync(
                n => n.With(1),
                _ =>
                {
                    sent.Add(1);
                    return first.Task;
                },
                null
            );
            state.ChangeAsync(
                n => n.With(2),
                _ =>
                {
                    sent.Add(2);
                    return second.Task;
                },
                null
            );
            // 前の書き込みが終わるまで、次は送らない。
            Assert.That(sent, Is.EqualTo(new[] { 1 }));
            Assert.That(state.Current.Values, Is.EqualTo(new[] { 1, 2 }));

            // 先の答えが届いても、保存を待っている変更は消えない。
            first.SetResult(new Numbers(1));
            Assert.That(sent, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(state.Confirmed.Values, Is.EqualTo(new[] { 1 }));
            Assert.That(state.Current.Values, Is.EqualTo(new[] { 1, 2 }));

            second.SetResult(new Numbers(1, 2));
            Assert.That(state.Current.Values, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(state.Pending, Is.False);
        }

        [Test]
        public void AFailedChangeGoesBackWithTheLaterOnesAndIsTold()
        {
            var state = new OptimisticState<Numbers>(new Numbers(7));
            var first = new TaskCompletionSource<Numbers>();
            int laterSends = 0;

            var failed = state.ChangeAsync(
                n => n.With(1),
                _ => first.Task,
                _ => "保存できませんでした"
            );
            var dropped = state.ChangeAsync(
                n => n.With(2),
                _ =>
                {
                    laterSends++;
                    return Task.FromResult(new Numbers(7, 1, 2));
                },
                _ => "出さない"
            );
            Assert.That(state.Current.Values, Is.EqualTo(new[] { 7, 1, 2 }));

            first.SetException(new InvalidOperationException("offline"));
            Assert.That(failed.IsCompleted && !failed.Result, Is.True);
            Assert.That(dropped.IsCompleted && !dropped.Result, Is.True);
            // あとの変更も取り消して送らず、確かめた状態へ戻す。知らせるのは1回だけ。
            Assert.That(laterSends, Is.EqualTo(0));
            Assert.That(state.Current.Values, Is.EqualTo(new[] { 7 }));
            Assert.That(state.Pending, Is.False);
            Assert.That(failures, Is.EqualTo(new[] { "保存できませんでした" }));
        }

        [Test]
        public void ARefusalReadsTheConfirmedStateAgain()
        {
            var state = new OptimisticState<Numbers>(
                new Numbers(1),
                _ => Task.FromResult(new Numbers(1, 5))
            );

            state.ChangeAsync(
                n => n.With(2),
                _ => Task.FromException<Numbers>(new ServerApiException(409)),
                _ => "断られました"
            );
            Assert.That(state.Current.Values, Is.EqualTo(new[] { 1, 5 }));
            Assert.That(failures, Is.EqualTo(new[] { "断られました" }));
        }

        [Test]
        public void AReadKeepsThePendingChanges()
        {
            var state = new OptimisticState<Numbers>(new Numbers());
            var answer = new TaskCompletionSource<Numbers>();
            state.ChangeAsync(n => n.With(3), _ => answer.Task, null);

            state.Confirm(new Numbers(9));
            Assert.That(state.Current.Values, Is.EqualTo(new[] { 9, 3 }));
        }

        [Test]
        public async Task AQueuedWriteKeepsGoingAfterAFailureAndIdleWaitsForAll()
        {
            var first = new TaskCompletionSource<int>();
            var failed = SaveQueue.Enqueue(_ => first.Task);
            var next = SaveQueue.Enqueue(_ => Task.FromResult(2));
            var idle = SaveQueue.IdleAsync(CancellationToken.None);
            Assert.That(idle.IsCompleted, Is.False);

            first.SetException(new InvalidOperationException());
            await idle;
            Assert.That(failed.IsFaulted, Is.True);
            Assert.That(next.Result, Is.EqualTo(2));
            // 失敗を知らせるのは、表示を戻す側（OptimisticState）だけ。
            Assert.That(failures, Is.Empty);
        }
    }
}
