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
}
