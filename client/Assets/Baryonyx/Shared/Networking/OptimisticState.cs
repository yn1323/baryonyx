using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Networking
{
    /// <summary>
    /// A player's data as the server last confirmed it, with the changes still being saved laid
    /// over it, so a screen shows a change at once (optimistic update, doc/rules/data-storage.md).
    /// <see cref="Current"/> is <see cref="Confirmed"/> with every pending change applied in
    /// order. Each change is sent through <see cref="SaveQueue"/>; when the server answers, its
    /// state becomes the confirmed one and the changes still pending are laid over it again.
    /// When a change fails, it and the changes made after it are dropped, the screen goes back
    /// to the confirmed state, and the failure is told on the notice band. A refusal (409)
    /// means the confirmed state is out of date, so it is read again (behind the queued writes)
    /// when a reader is given, before the change returns.
    /// </summary>
    public sealed class OptimisticState<T>
        where T : class
    {
        private sealed class Change
        {
            public Func<T, T> Apply;
            public bool Dropped;
        }

        private readonly List<Change> pending = new();
        private readonly Func<CancellationToken, Task<T>> reload;

        public OptimisticState(T confirmed, Func<CancellationToken, Task<T>> reload = null)
        {
            Confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
            Current = confirmed;
            this.reload = reload;
        }

        // サーバーが最後に確かめた状態。
        public T Confirmed { get; private set; }

        // 確かめた状態に、保存を待っている変更を順に当てた、画面に出す状態。
        public T Current { get; private set; }

        // 保存を待っている変更がある。
        public bool Pending => pending.Count > 0;

        // Current が変わった（変更した・サーバーが答えた・失敗して戻した）。
        public event Action Changed;

        /// <summary>Replaces the confirmed state with what the server read, keeping pending changes.</summary>
        public void Confirm(T state)
        {
            if (state == null)
                return;
            Confirmed = state;
            Recompute();
        }

        /// <summary>
        /// Shows <paramref name="apply"/> at once and saves it with <paramref name="send"/> after
        /// the writes before it. True once saved; false when it failed (told with
        /// <paramref name="failedMessage"/>) or was dropped because an earlier change failed.
        /// </summary>
        public async Task<bool> ChangeAsync(
            Func<T, T> apply,
            Func<CancellationToken, Task<T>> send,
            Func<Exception, string> failedMessage
        )
        {
            if (apply == null)
                throw new ArgumentNullException(nameof(apply));
            if (send == null)
                throw new ArgumentNullException(nameof(send));
            var change = new Change { Apply = apply };
            pending.Add(change);
            Recompute();
            T saved;
            try
            {
                // 前の変更が失敗して取り消されていたら、送らない。
                saved = await SaveQueue.Enqueue(token =>
                    change.Dropped ? Task.FromResult<T>(null) : send(token)
                );
            }
            catch (OperationCanceledException)
            {
                // アプリを止めたとき。表示は作り直されるので戻さない。
                return false;
            }
            catch (Exception exception)
            {
                if (change.Dropped)
                    return false;
                Debug.LogWarning("保存できませんでした。" + exception.Message);
                Drop(change);
                SaveQueue.Report(failedMessage?.Invoke(exception));
                // 読み直し終えてから返し、画面が読み直した状態で描き直せるようにする。
                if (reload != null && exception is ServerApiException { StatusCode: 409 })
                    await ReloadAsync();
                return false;
            }
            if (change.Dropped)
                return false;
            pending.Remove(change);
            if (saved != null)
                Confirmed = saved;
            Recompute();
            return true;
        }

        // 失敗した変更と、そのあとに出した変更を取り消して、確かめた状態へ戻す。
        private void Drop(Change failed)
        {
            int index = pending.IndexOf(failed);
            if (index < 0)
                return;
            for (int i = index; i < pending.Count; i++)
                pending[i].Dropped = true;
            pending.RemoveRange(index, pending.Count - index);
            Recompute();
        }

        // 断られたときは、手元の確かめた状態が古いので、書き込みの後ろで読み直す。
        private async Task ReloadAsync()
        {
            try
            {
                Confirm(await SaveQueue.Enqueue(reload));
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogWarning("保存を断られたあとに読み直せませんでした。" + exception.Message);
            }
        }

        private void Recompute()
        {
            var state = Confirmed;
            foreach (var change in pending)
                state = change.Apply(state) ?? state;
            Current = state;
            Changed?.Invoke();
        }
    }
}
