using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using NUnit.Framework;

namespace Baryonyx.Tests
{
    public sealed class HealthScreenOperationsTests
    {
        [Test]
        public async Task RepeatedRequestsShareOneOperationAndBackgroundCancelsIt()
        {
            using var operations = new HealthScreenOperations();
            var pending = new TaskCompletionSource<bool>();
            CancellationToken token = default;
            int calls = 0;
            Task Read(int version, CancellationToken current)
            {
                calls++;
                token = current;
                return pending.Task;
            }
            var first = operations.RunAsync(Read, () => { });
            Assert.That(operations.RunAsync(Read, () => { }), Is.SameAs(first));
            operations.SetForeground(false);
            operations.CancelBackgroundOperation();
            Assert.That(token.IsCancellationRequested, Is.True);
            Assert.That(calls, Is.EqualTo(1));
            pending.SetResult(true);
            await first;
            Assert.That(operations.IsBusy, Is.False);
        }

        [Test]
        public async Task SynchronousCompletionKeepsTheNextOperationActive()
        {
            using var operations = new HealthScreenOperations();
            var pending = new TaskCompletionSource<bool>();
            Task next = null;
            await operations.RunAsync(
                (_, _) => Task.CompletedTask,
                () => next = operations.RunAsync((_, _) => pending.Task, () => { })
            );
            Assert.That(operations.IsBusy, Is.True);
            Assert.That(operations.Active, Is.SameAs(next));
            pending.SetResult(true);
            await next;
        }

        [Test]
        public async Task SystemDialogSurvivesBackgroundAndWaitsForForeground()
        {
            using var operations = new HealthScreenOperations();
            var reply = new TaskCompletionSource<bool>();
            CancellationToken dialogToken = default;
            bool returned = false;
            var active = operations.RunAsync(
                async (_, _) =>
                {
                    await operations.RunSystemDialogAsync(token =>
                    {
                        dialogToken = token;
                        return reply.Task;
                    });
                    returned = true;
                },
                () => { }
            );
            operations.SetForeground(false);
            operations.CancelBackgroundOperation();
            reply.SetResult(true);
            await Task.Yield();
            Assert.That(dialogToken.IsCancellationRequested, Is.False);
            Assert.That(returned, Is.False);
            operations.SetForeground(true);
            await active;
            Assert.That(returned, Is.True);
            Assert.That(operations.IsSystemDialog, Is.False);
        }

        [Test]
        public async Task DisposalReleasesForegroundWaitAndInvalidatesDelayedResult()
        {
            using var operations = new HealthScreenOperations();
            var reply = new TaskCompletionSource<bool>();
            int version = 0;
            var active = operations.RunAsync(
                async (current, _) =>
                {
                    version = current;
                    await operations.RunSystemDialogAsync(_ => reply.Task);
                },
                () => { }
            );
            operations.SetForeground(false);
            operations.Dispose();
            reply.SetResult(true);
            await active;
            Assert.That(operations.IsCurrent(version), Is.False);
            Assert.That(operations.IsBusy, Is.False);
        }

        [Test]
        public async Task FailedOperationReleasesTheSlotForRetry()
        {
            using var operations = new HealthScreenOperations();
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await operations.RunAsync(
                    (_, _) => throw new InvalidOperationException(),
                    () => { }
                )
            );
            bool retried = false;
            await operations.RunAsync(
                (_, _) =>
                {
                    retried = true;
                    return Task.CompletedTask;
                },
                () => { }
            );
            Assert.That(retried, Is.True);
        }
    }
}
