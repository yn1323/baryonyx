using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;

namespace Baryonyx.Account
{
    /// <summary>
    /// The player's one server session, shared by every feature that calls the game server
    /// (health, the party, equipment and the adventure). It signs in as the guest when
    /// there is no session or it is about to expire, and when the server rejects the session it
    /// signs in again once and retries the call. Calls made while a sign-in is running wait for
    /// that sign-in instead of signing in again, so the features can read the server at once.
    /// </summary>
    public sealed class AccountSessionRunner : IAccountSessionRunner
    {
        // 期限の直前に送った要求が途中で失効しないよう、早めにセッションを取り直す。
        internal static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

        private readonly Func<CancellationToken, Task<AccountSession>> signInAsGuest;
        private readonly Func<DateTimeOffset> now;

        // 実行中または直前のゲストのログイン。同時に届いた要求は、このログインを待つ。
        private Task signingIn = Task.CompletedTask;

        public AccountSessionRunner(AccountApiClient accounts, Func<string> guestSecret = null)
            : this(GuestSignIn(accounts, guestSecret ?? GuestCredential.GetOrCreate)) { }

        // テストではゲストのログインと時計を差し替える。
        internal AccountSessionRunner(
            Func<CancellationToken, Task<AccountSession>> signInAsGuest,
            Func<DateTimeOffset> now = null
        )
        {
            this.signInAsGuest =
                signInAsGuest ?? throw new ArgumentNullException(nameof(signInAsGuest));
            this.now = now ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>Raised with each new session, after a sign-in or a renewal.</summary>
        public event Action<AccountSession> Started;

        public AccountSession Current { get; private set; }

        public bool IsSignedIn => Current != null && Current.ExpiresAt > now();

        /// <summary>Uses a session signed in another way (such as Google).</summary>
        public void Start(AccountSession session)
        {
            Current = session ?? throw new ArgumentNullException(nameof(session));
            Started?.Invoke(session);
        }

        // ログインは、待っている要求のどれかが取り消されても止めない。ほかの要求も同じログインを待つため。
        public async Task ConnectAsync(CancellationToken token)
        {
            if (Current != null && Current.ExpiresAt - RenewBefore > now())
                return;
            if (signingIn.IsCompleted)
                signingIn = SignInAsync();
            await signingIn;
            token.ThrowIfCancellationRequested();
        }

        private async Task SignInAsync() => Start(await signInAsGuest(CancellationToken.None));

        // 失効・取り消し済みのセッションは一度だけゲストで取り直して再実行する。
        public async Task<T> WithSessionAsync<T>(
            Func<AccountSession, Task<T>> operation,
            CancellationToken token
        )
        {
            await ConnectAsync(token);
            var session = Current;
            try
            {
                return await operation(session);
            }
            catch (ServerApiException exception) when (exception.StatusCode == 401)
            {
                // 同時に断られたほかの要求が取り直したセッションは、捨てずに使う。
                if (Current == session)
                    Current = null;
                await ConnectAsync(token);
                return await operation(Current);
            }
        }

        /// <summary>Forgets the session and returns it, so the caller can sign it out.</summary>
        public AccountSession End()
        {
            var ended = Current;
            Current = null;
            return ended;
        }

        private static Func<CancellationToken, Task<AccountSession>> GuestSignIn(
            AccountApiClient accounts,
            Func<string> guestSecret
        )
        {
            if (accounts == null)
                throw new ArgumentNullException(nameof(accounts));
            return token => accounts.GuestLoginAsync(guestSecret(), token);
        }
    }
}
