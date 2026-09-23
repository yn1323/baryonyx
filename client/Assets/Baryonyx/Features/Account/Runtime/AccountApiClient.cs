using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;

namespace Baryonyx.Account
{
    public sealed class AccountApiClient
    {
        private readonly ServerApi server;

        public AccountApiClient(ServerApi server) =>
            this.server = server ?? throw new ArgumentNullException(nameof(server));

        public async Task<AccountSession> LoginAsync(string idToken, CancellationToken token)
        {
            var reply = await server.SendAsync<LoginReply>(
                "/v1/auth/google",
                "POST",
                new LoginRequest { idToken = idToken },
                null,
                token
            );
            return new AccountSession(
                reply.userId,
                reply.token,
                DateTimeOffset.Parse(reply.expiresAt)
            );
        }

        public Task LogoutAsync(AccountSession session, CancellationToken token) =>
            server.SendAsync<EmptyReply>("/v1/auth/logout", "POST", null, session.Token, token);

        [Serializable]
        private sealed class LoginRequest
        {
            public string idToken;
        }

        [Serializable]
        private sealed class LoginReply
        {
            public string token;
            public string userId;
            public string expiresAt;
        }

        [Serializable]
        private sealed class EmptyReply { }
    }
}
