using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Baryonyx.Home
{
    /// <summary>
    /// Keeps the real step source for steps and linking, but shows a mock rune balance and
    /// grants a fixed mock amount on every tap, so the gain can be tried without a server
    /// or new steps. Nothing it grants reaches the server.
    /// </summary>
    public sealed class HomeMockRuneSource : IHomeStepSource
    {
        private readonly IHomeStepSource steps;
        private readonly long granted;
        private long balance;
        private HomeStepReading last = new(HomeStepResult.Failed, HomeStepLink.Linked, 0);

        public HomeMockRuneSource(IHomeStepSource steps, long balance, long granted)
        {
            this.steps = steps ?? throw new ArgumentNullException(nameof(steps));
            this.balance = Math.Max(0, balance);
            this.granted = Math.Max(0, granted);
        }

        public async Task<HomeStepReading> LoadAsync(CancellationToken token)
        {
            try
            {
                last = await steps.LoadAsync(token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // 歩数を読めなくても、仮の所持ルーンは表示する。
                Debug.LogWarning("歩数を更新できませんでした。" + exception.Message);
                last = new HomeStepReading(HomeStepResult.Failed, last.Link, 0);
            }
            return WithRunes(last, 0);
        }

        public async Task<HomeStepReading> SyncAsync(CancellationToken token)
        {
            try
            {
                last = await steps.SyncAsync(token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // 歩数の同期に失敗しても、仮のルーンは付与する。
                Debug.LogWarning("歩数を同期できませんでした。" + exception.Message);
            }
            // 未連携のままなら、連携の案内を優先してルーンは付与しない。
            if (last.Link != HomeStepLink.Linked)
                return WithRunes(last, 0);
            balance += granted;
            return WithRunes(last, granted);
        }

        private HomeStepReading WithRunes(HomeStepReading reading, long grantedRunes) =>
            new(reading.Result, reading.Link, reading.Steps, reading.Day, balance, grantedRunes);
    }
}
