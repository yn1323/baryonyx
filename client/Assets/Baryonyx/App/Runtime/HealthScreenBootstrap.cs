using Baryonyx.Health;
using UnityEngine;

namespace Baryonyx.App
{
    public sealed class HealthScreenBootstrap : MonoBehaviour
    {
        public HealthConnectionSettings Settings;
        public HealthScreenView Screen;
        private HealthRuntime health;
        private HealthScreenPresenter presenter => health?.Presenter;
        private bool paused;
        private bool focused = true;

        private void Start()
        {
            Application.targetFrameRate = 60;

            health = new HealthRuntime(Settings);
            Screen.Bind(presenter, preview: health.Preview);
            ApplyForeground();
            health.Initialize();
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

        private void OnDestroy() => health?.Dispose();
    }
}
