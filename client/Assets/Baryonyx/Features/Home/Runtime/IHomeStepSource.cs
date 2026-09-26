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
            DateTime? day = null,
            long? runes = null,
            long grantedRunes = 0
        )
        {
            Result = result;
            Link = link;
            Steps = steps;
            Day = day;
            Runes = runes;
            GrantedRunes = grantedRunes;
        }

        public HomeStepResult Result { get; }
        public HomeStepLink Link { get; }
        public int Steps { get; }

        // 歩数を集計した日。日付をまたいでHomeを開いたままでも、表示の日付を歩数に合わせる。
        public DateTime? Day { get; }

        // サーバーの所持ルーン。読めなかったときはnull。
        public long? Runes { get; }

        // 今回の同期で付与されたルーン。同期しない読み取りでは0。
        public long GrantedRunes { get; }
    }

    /// <summary>
    /// Supplies today's steps without making Home depend on health or server code. The App
    /// layer implements it; the mock scene runs without one.
    /// </summary>
    public interface IHomeStepSource
    {
        // サーバーに保存済みの今日の歩数を読む。
        Task<HomeStepReading> LoadAsync(CancellationToken token);

        // 端末の歩数をサーバーへ同期してルーンへ変換し、保存済みの今日の歩数を読む。
        Task<HomeStepReading> SyncAsync(CancellationToken token);
    }
}
