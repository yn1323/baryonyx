using System;
using Baryonyx.Health;

namespace Baryonyx.App
{
    // The two entry scenes share platform selection and provider lifetime.
    public sealed class HealthRuntime : IDisposable
    {
        public HealthScreenPresenter Presenter { get; }
        public bool Preview { get; }
        private readonly IDisposable authentication;

        public HealthRuntime(HealthConnectionSettings settings)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var google = new UmothGoogleSignInProvider(
                settings != null ? settings.GoogleWebClientId : ""
            );
            authentication = google;
            Presenter = new HealthScreenPresenter(google, new HealthConnectProvider());
#else
            var preview = new HealthScreenPreviewProvider();
            Presenter = new HealthScreenPresenter(preview, preview);
            Preview = true;
#endif
        }

        public void Initialize()
        {
            if (Preview)
                _ = Presenter.ConnectAsync();
            else
                _ = Presenter.InitializeAsync();
        }

        public void Dispose()
        {
            Presenter.Dispose();
            authentication?.Dispose();
        }
    }
}
