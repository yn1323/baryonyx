using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    // 歩数の保存先。サーバーURLがない実行環境とテストでは端末内の実装へ差し替える。
    public interface IHealthStepServer
    {
        // 起動時に接続とゲストのセッションを確認する。失敗は例外で返す。
        Task ConnectAsync(CancellationToken token);

        // Health Connectから読んだ直近7日分を保存する。ルーンの請求は行わない。
        Task SaveAsync(HealthDay[] days, CancellationToken token);

        // 保存済みの日別歩数を返す。まだ保存していなければ空の配列を返す。
        Task<HealthDay[]> ReadAsync(CancellationToken token);

        // 所持ルーンの残高を返す。
        Task<long> ReadRunesAsync(CancellationToken token);

        // 保存済みの歩数のうち未変換の分をルーンへ変換し、今回の付与量と付与後の残高を返す。
        Task<HealthRuneClaim> ClaimRunesAsync(CancellationToken token);
    }

    public readonly struct HealthRuneClaim
    {
        public HealthRuneClaim(long granted, long balance)
        {
            Granted = granted;
            Balance = balance;
        }

        public long Granted { get; }
        public long Balance { get; }
    }
}
