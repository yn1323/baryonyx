using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Uralstech.UMoth.GoogleSignIn;
#endif

namespace Baryonyx.Health
{
    public sealed class UmothGoogleSignInProvider :
        IGoogleSignInProvider,
        IGoogleCredentialProvider,
        IDisposable
    {
        private readonly string clientId;
#if UNITY_ANDROID && !UNITY_EDITOR
        private GoogleSignInManager manager;
#endif

        public UmothGoogleSignInProvider(string clientId) => this.clientId = clientId?.Trim();

        public string ServerIdToken { get; private set; }

        public async Task<GoogleSignInStatus> SignInAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (
                string.IsNullOrWhiteSpace(clientId)
                || !clientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal)
            )
                return GoogleSignInStatus.NotConfigured;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (manager == null)
            {
                manager = new GameObject("GoogleSignIn").AddComponent<GoogleSignInManager>();
                manager.ServerClientId = clientId.Trim();
            }
            var (credential, _) = await manager.SignInAsync(
                filterByAuthorizedAccount: false,
                autoSelectSignIn: false,
                token: token
            );
            // Credentials never leave this adapter or enter the screen's state.
            ServerIdToken = credential?.IdToken;
            return credential != null ? GoogleSignInStatus.Success : GoogleSignInStatus.Incomplete;
#else
            await Task.CompletedTask;
            ServerIdToken = null;
            return GoogleSignInStatus.Unsupported;
#endif
        }

        public async Task<bool> SignOutAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
#if UNITY_ANDROID && !UNITY_EDITOR
            bool result = manager == null || await manager.SignOutAsync(token);
            if (result)
                ServerIdToken = null;
            return result;
#else
            await Task.CompletedTask;
            ServerIdToken = null;
            return true;
#endif
        }

        public void Dispose()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (manager != null)
                UnityEngine.Object.Destroy(manager.gameObject);
#endif
        }
    }
}
