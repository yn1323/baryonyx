using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Runs the particle systems of a 3D stage (embers, motes, fireflies, dust) outside Play Mode
    /// too, so the scene looks while it is edited as it does when played: on enable each system
    /// is run ahead until it is full, then advanced with the editor's clock. The editor only
    /// simulates the particle system that is selected, so without this the stage would show none.
    /// Nothing is saved; in Play Mode the systems play by themselves.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class Hd2dParticlePreview : MonoBehaviour
    {
        [Tooltip("停止中に表示を始めるとき、粒子が行き渡るまで先に進める秒数。")]
        [Min(0f)]
        public float WarmUpSeconds = 8f;

#if UNITY_EDITOR
        private ParticleSystem[] systems = System.Array.Empty<ParticleSystem>();
        private double lastTime;

        private void OnEnable()
        {
            if (Application.isPlaying)
                return;
            systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var system in systems)
                system.Simulate(WarmUpSeconds, false, true, false);
            lastTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update += Advance;
        }

        private void OnDisable()
        {
            UnityEditor.EditorApplication.update -= Advance;
        }

        // The particles move on whenever the views redraw (the Scene view's "Always Refresh",
        // or a change in the scene); between redraws only the clock advances.
        private void Advance()
        {
            if (this == null || Application.isPlaying)
            {
                UnityEditor.EditorApplication.update -= Advance;
                return;
            }
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float delta = Mathf.Min((float)(now - lastTime), 0.1f);
            lastTime = now;
            if (delta <= 0f)
                return;
            foreach (var system in systems)
                if (system != null)
                    system.Simulate(delta, false, false, false);
        }
#endif
    }
}
