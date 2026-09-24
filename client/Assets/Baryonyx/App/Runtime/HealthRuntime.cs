using System;
using System.Threading.Tasks;
using Baryonyx.Health;
#if UNITY_ANDROID && !UNITY_EDITOR
using Baryonyx.Account;
using Baryonyx.ExerciseRewards;
using Baryonyx.Networking;
#endif

namespace Baryonyx.App
{
    // Selects the platform providers and owns their lifetime for any scene that shows
    // health data. No scene uses it while the game screens are being rebuilt.
    public sealed class HealthRuntime : IDisposable
    {
        public HealthScreenPresenter Presenter { get; }
        public bool Preview { get; }
        private readonly IDisposable authentication;
        private readonly HealthServerSync rewards;

        public HealthRuntime(HealthConnectionSettings settings)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var google = new UmothGoogleSignInProvider(
                settings != null ? settings.GoogleWebClientId : ""
            );
            authentication = google;
            var url = ServerEndpoint.Resolve(settings);
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var server = new ServerApi(url);
                    rewards = new HealthServerSync(
                        new AccountApiClient(server),
                        new HealthApiClient(server),
                        new ExerciseRewardsApiClient(server)
                    );
                }
                catch (ArgumentException exception)
                {
                    UnityEngine.Debug.LogWarning(
                        "運動報酬APIのURLが無効なため、サーバー連携を無効にします。"
                            + exception.Message
                    );
                }
            }
            Presenter = new HealthScreenPresenter(google, new HealthConnectProvider(), rewards);
#else
            var preview = new HealthScreenPreviewProvider();
            Presenter = new HealthScreenPresenter(preview, preview);
            Preview = true;
#endif
        }

        public async Task InitializeAsync()
        {
            // Check the platform requirements first, then start the same connection
            // flow used by the Health Connect button. This keeps the calling screen
            // immediately usable while the permission/read operation runs in the
            // background.
            await Presenter.InitializeAsync();
            await Presenter.ConnectAsync();
        }

        public void Initialize() => _ = InitializeAsync();

        public void Dispose()
        {
            Presenter.Dispose();
            rewards?.Dispose();
            authentication?.Dispose();
        }
    }
}
