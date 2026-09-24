using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Home
{
    public enum HomeStepResult
    {
        Updated,
        Unlinked,

        // 許可画面を表示できず、Health Connectの設定を開いた。
        SettingsOpened,
        Failed,
    }

    public readonly struct HomeStepReading
    {
        public HomeStepReading(
            HomeStepResult result,
            HomeStepLink link,
            int steps,
            DateTime? day = null
        )
        {
            Result = result;
            Link = link;
            Steps = steps;
            Day = day;
        }

        public HomeStepResult Result { get; }
        public HomeStepLink Link { get; }
        public int Steps { get; }

        // 歩数を集計した日。日付をまたいでHomeを開いたままでも、表示の日付を歩数に合わせる。
        public DateTime? Day { get; }
    }

    /// <summary>
    /// Supplies today's steps without making Home depend on health or server code. The App
    /// layer implements it; the mock scene runs without one.
    /// </summary>
    public interface IHomeStepSource
    {
        // サーバーに保存済みの今日の歩数を読む。
        Task<HomeStepReading> LoadAsync(CancellationToken token);

        // 端末の歩数をサーバーへ同期してから、保存済みの今日の歩数を読む。
        Task<HomeStepReading> SyncAsync(CancellationToken token);
    }
}
