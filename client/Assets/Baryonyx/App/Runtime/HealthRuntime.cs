using System;
using System.Threading.Tasks;
using Baryonyx.Health;

namespace Baryonyx.App
{
    // The two entry scenes share platform selection and provider lifetime.
    public sealed class HealthRuntime : IDisposable
    {
        public HealthScreenPresenter Presenter { get; }
        public bool Preview { get; }
        private readonly IDisposable authentication;
        private readonly ExerciseRewardService rewards;

        public HealthRuntime(HealthConnectionSettings settings)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var google = new UmothGoogleSignInProvider(
                settings != null ? settings.GoogleWebClientId : ""
            );
            authentication = google;
            if (!string.IsNullOrWhiteSpace(settings?.ServerBaseUrl))
            {
                try
                {
                    rewards = new ExerciseRewardService(
                        new HealthApiClient(settings.ServerBaseUrl)
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
            // flow used by the Health Connect button. This keeps the home screen
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
