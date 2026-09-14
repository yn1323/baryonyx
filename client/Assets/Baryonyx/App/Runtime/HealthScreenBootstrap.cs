using Baryonyx.Health;
using UnityEngine;

namespace Baryonyx.App
{
    public sealed class HealthScreenBootstrap : MonoBehaviour
    {
        public HealthConnectionSettings Settings;
        public HealthScreenView Screen;
        private HealthScreenPresenter presenter;
        private bool paused;
        private bool focused = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        private UmothGoogleSignInProvider authentication;
#endif

        private void Start()
        {
            Application.targetFrameRate = 60;

#if UNITY_ANDROID && !UNITY_EDITOR
            authentication = new UmothGoogleSignInProvider(
                Settings != null ? Settings.GoogleWebClientId : ""
            );
            presenter = new HealthScreenPresenter(authentication, new HealthConnectProvider());
            Screen.Bind(presenter);
#else
            var preview = new HealthScreenPreviewProvider();
            presenter = new HealthScreenPresenter(preview, preview);
            Screen.Bind(presenter, preview: true);
            _ = presenter.ConnectAsync();
#endif
            ApplyForeground();
#if UNITY_ANDROID && !UNITY_EDITOR
            _ = presenter.InitializeAsync();
#endif
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            ApplyForeground();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            ApplyForeground();
        }

        private void ApplyForeground() => presenter?.SetForeground(!paused && focused);

        private void OnDestroy()
        {
            presenter?.Dispose();
#if UNITY_ANDROID && !UNITY_EDITOR
            authentication?.Dispose();
#endif
        }
    }
}
