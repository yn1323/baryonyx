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
        public HomeStepReading(HomeStepResult result, HomeStepLink link, int steps)
        {
            Result = result;
            Link = link;
            Steps = steps;
        }

        public HomeStepResult Result { get; }
        public HomeStepLink Link { get; }
        public int Steps { get; }
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
