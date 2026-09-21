using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    // Owns operation lifetime; the presenter decides which health action to run next.
    internal sealed class HealthScreenOperations : IDisposable
    {
        private readonly CancellationTokenSource lifetime = new();
        private CancellationTokenSource cancellation;
        private TaskCompletionSource<bool> foregroundSignal;
        private int generation;

        public bool IsBusy { get; private set; }
        public bool IsForeground { get; private set; } = true;
        public bool IsSystemDialog { get; private set; }
        public bool IsDisposed { get; private set; }
        public Task Active { get; private set; } = Task.CompletedTask;
        public CancellationToken LifetimeToken => lifetime.Token;

        public bool IsCurrent(int version) => !IsDisposed && generation == version;

        public Task RunAsync(Func<int, CancellationToken, Task> action, Action completed)
        {
            if (IsDisposed)
                return Task.CompletedTask;
            if (IsBusy)
                return Active;
            IsBusy = true;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            // Publish before invoking action: synchronous completion may start the next operation.
            Active = completion.Task;
            _ = ExecuteAsync(action, completed, ++generation, cancellation, completion);
            return completion.Task;
        }

        private async Task ExecuteAsync(
            Func<int, CancellationToken, Task> action,
            Action completed,
            int version,
            CancellationTokenSource current,
            TaskCompletionSource<bool> completion
        )
        {
            try
            {
                try
                {
                    await action(version, current.Token);
                }
                finally
                {
                    IsSystemDialog = false;
                    IsBusy = false;
                    cancellation = null;
                    current.Dispose();
                    completed();
                }
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        public async Task<T> RunSystemDialogAsync<T>(Func<CancellationToken, Task<T>> action)
        {
            BeginSystemDialog();
            try
            {
                var result = await action(LifetimeToken);
                await WaitForForegroundAsync();
                return result;
            }
            finally
            {
                IsSystemDialog = false;
            }
        }

        public void BeginSystemDialog() => IsSystemDialog = true;

        public bool SetForeground(bool value)
        {
            if (IsDisposed || IsForeground == value)
                return false;
            IsForeground = value;
            if (value)
                foregroundSignal?.TrySetResult(true);
            else
                foregroundSignal = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
            return true;
        }

        public void CancelBackgroundOperation()
        {
            if (!IsForeground && !IsSystemDialog)
                cancellation?.Cancel();
        }

        private Task WaitForForegroundAsync() =>
            IsForeground || IsDisposed ? Task.CompletedTask : foregroundSignal.Task;

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;
            generation++;
            lifetime.Cancel();
            foregroundSignal?.TrySetResult(true);
            lifetime.Dispose();
        }
    }
}
