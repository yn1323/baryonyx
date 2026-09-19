using Baryonyx.Health;
using Baryonyx.Wireframe;
using UnityEngine;

namespace Baryonyx.App
{
    public sealed class WireframeBootstrap : MonoBehaviour
    {
        public WireframeData Data;
        public WireframeView View;
        public HealthConnectionSettings HealthSettings;
        private HealthRuntime health;
        private bool paused;
        private bool focused = true;

        private void Start()
        {
            if (Data == null || View == null)
            {
                Debug.LogError("Wireframe requires its sample data and screen prefab.", this);
                return;
            }
            View.Bind(new WireframeSession(Data));
            Application.targetFrameRate = 60;
            health = new HealthRuntime(HealthSettings);
            var healthView = View.GetComponent<WireframeHealthView>();
            if (healthView != null)
                healthView.Bind(health.Presenter, health.Preview);
            health.Presenter.SetForeground(!paused && focused);
            health.Initialize();
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            Foreground();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            Foreground();
        }

        private void Foreground() => health?.Presenter.SetForeground(!paused && focused);

        private void OnDestroy() => health?.Dispose();
    }
}
