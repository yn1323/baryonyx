using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using UnityEngine;

namespace Baryonyx.Combat.Records
{
    /// <summary>
    /// The weaknesses the player has revealed (doc/features/combat.md, revealing weaknesses),
    /// kept while the app runs and saved where <see cref="Source"/> says: the app sets it to the
    /// game server; without it they stay only while the app runs. A weakness hit in a battle is
    /// shown at once and saved behind the battle in the <see cref="SaveQueue"/>; one the server
    /// has not taken yet is sent again with the next one revealed or the next load, and is never
    /// hidden again nor told as a failure.
    /// </summary>
    public static class RevealedWeaknessSession
    {
        private static readonly RevealedWeaknesses known = new();
        private static readonly List<RevealedWeakness> unsent = new();
        private static RevealedWeaknessMemorySource local;
        private static Task loading;
        private static bool loaded;
        private static bool sending;

        // Play Modeをまたいで、前の読み込みや送信の結果を混ぜないための世代。
        private static int generation;

        /// <summary>The server that keeps the revealed weaknesses. Without it, they stay in the app.</summary>
        public static IRevealedWeaknessSource Source { get; set; }

        public static IRevealedWeaknessSource SourceOrLocal =>
            Source ?? (local ??= new RevealedWeaknessMemorySource());

        /// <summary>Those revealed so far: those read from the source and those revealed since.</summary>
        public static RevealedWeaknesses Known => known;

        /// <summary>Raised when <see cref="Known"/> gains a weakness, revealed or read.</summary>
        public static event Action Changed;

        // 実行中または直前の送信。テストで完了を待つために公開する。
        public static Task Work { get; private set; } = Task.CompletedTask;

        public static bool IsRevealed(string enemyId, CardElement element) =>
            known.Contains(enemyId, element);

        /// <summary>
        /// Reads those revealed, once while the app runs (again with <paramref name="refresh"/>),
        /// and sends again those not yet saved. A failed read leaves what is known as it is.
        /// </summary>
        public static Task LoadAsync(CancellationToken token, bool refresh = false)
        {
            if (loaded && !refresh)
                return Task.CompletedTask;
            if (loading == null || loading.IsCompleted)
                loading = ReadAsync(generation);
            return token.CanBeCanceled ? WaitAsync(loading, token) : loading;
        }

        /// <summary>
        /// Records a weakness a card has hit. True when it was not known before; it is then
        /// saved behind the caller.
        /// </summary>
        public static bool Record(string enemyId, CardElement element)
        {
            if (!known.Add(enemyId, element))
                return false;
            unsent.Add(new RevealedWeakness(enemyId, element));
            Changed?.Invoke();
            Send();
            return true;
        }

        private static async Task ReadAsync(int at)
        {
            IReadOnlyList<RevealedWeakness> read;
            try
            {
                read = await SourceOrLocal.LoadAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("開示した弱点を読み込めませんでした。" + exception.Message);
                return;
            }
            if (at != generation)
                return;
            loaded = true;
            Merge(read);
            Send();
        }

        private static void Send()
        {
            if (sending || unsent.Count == 0)
                return;
            sending = true;
            Work = SendAsync(generation);
        }

        private static async Task SendAsync(int at)
        {
            try
            {
                while (at == generation && unsent.Count > 0)
                {
                    var batch = unsent.ToArray();
                    IReadOnlyList<RevealedWeakness> saved;
                    try
                    {
                        // ほかの保存と同じ列に並べて順に送る。表示は戻さないため、失敗を知らせない。
                        var source = SourceOrLocal;
                        saved = await SaveQueue.Enqueue(token => source.RevealAsync(batch, token));
                    }
                    catch (Exception exception)
                    {
                        // 表示はそのままにし、次に開示したときか次の読み込みで送り直す。
                        Debug.LogWarning(
                            "開示した弱点を保存できませんでした。" + exception.Message
                        );
                        return;
                    }
                    if (at != generation)
                        return;
                    unsent.RemoveAll(weakness => batch.Contains(weakness));
                    Merge(saved);
                }
            }
            finally
            {
                if (at == generation)
                    sending = false;
            }
        }

        private static void Merge(IEnumerable<RevealedWeakness> weaknesses)
        {
            int before = known.Count;
            known.AddRange(weaknesses);
            if (known.Count != before)
                Changed?.Invoke();
        }

        private static async Task WaitAsync(Task task, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>();
            using (token.Register(() => cancelled.TrySetResult(true)))
                if (await Task.WhenAny(task, cancelled.Task) != task)
                    token.ThrowIfCancellationRequested();
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            generation++;
            known.Clear();
            unsent.Clear();
            local = null;
            loading = null;
            loaded = false;
            sending = false;
            Work = Task.CompletedTask;
            Source = null;
            Changed = null;
        }
    }
}
