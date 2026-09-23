using System;

namespace Baryonyx.Account
{
    public sealed class AccountSession
    {
        public string UserId { get; }
        public string Token { get; }
        public DateTimeOffset ExpiresAt { get; }

        public AccountSession(string userId, string token, DateTimeOffset expiresAt)
        {
            UserId = userId;
            Token = token;
            ExpiresAt = expiresAt;
        }
    }
}
