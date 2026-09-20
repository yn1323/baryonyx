using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Health
{
    public enum GoogleSignInStatus
    {
        Success,
        Incomplete,
        NotConfigured,
        Unsupported,
    }

    public interface IGoogleSignInProvider
    {
        Task<GoogleSignInStatus> SignInAsync(CancellationToken token);
        Task<bool> SignOutAsync(CancellationToken token);
    }

    // Google接続とサーバー認証を同じOSログイン結果から組み立てるための境界。
    // 通常の画面テスト用認証はこのインターフェースを実装しなくてよい。
    public interface IGoogleCredentialProvider
    {
        string ServerIdToken { get; }
    }
}
