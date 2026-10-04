using System;
using System.Threading;
using System.Threading.Tasks;

namespace Baryonyx.Account
{
    /// <summary>
    /// Runs a server call with a valid session: signs in as the guest when needed and, if the
    /// server rejects the session, signs in again once and retries. Features that call their own
    /// APIs (such as the UPT bonus) use it instead of keeping a session of their own.
    /// </summary>
    public interface IAccountSessionRunner
    {
        Task<T> WithSessionAsync<T>(
            Func<AccountSession, Task<T>> operation,
            CancellationToken token
        );
    }
}
