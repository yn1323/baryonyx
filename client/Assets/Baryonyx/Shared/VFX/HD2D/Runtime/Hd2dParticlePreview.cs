using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Shows the particle systems of a 3D stage (embers, motes, fireflies, dust) outside Play Mode
    /// too, so the scene looks while it is edited as it does when played: on enable each system
    /// is run ahead until it is full. The editor only simulates the particle system that is
    /// selected, so without this the stage would show none. When
    /// <see cref="AnimateWhileStopped"/> is on (menu Baryonyx > HD-2D), the systems are also
    /// advanced with the editor's clock, about 30 times a second, while Unity is the active app.
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
        private const string AnimateWhileStoppedKey = "Baryonyx.Hd2d.AnimateStageWhileStopped";

        // The views draw the particles only when they redraw, so a finer step would be wasted.
        private const double StepSeconds = 1.0 / 30.0;

        private static bool? animateWhileStopped;

        /// <summary>
        /// Whether the stage moves outside Play Mode (the particles here, and the Scene view's
        /// "Always Refresh" set by the stage's Scene view setup). Off by default, since redrawing
        /// the stage all the time keeps the editor busy. Kept per user in UserSettings.
        /// </summary>
        public static bool AnimateWhileStopped
        {
            get
            {
                animateWhileStopped ??=
                    UnityEditor.EditorUserSettings.GetConfigValue(AnimateWhileStoppedKey) == "1";
                return animateWhileStopped.Value;
            }
            set
            {
                animateWhileStopped = value;
                UnityEditor.EditorUserSettings.SetConfigValue(
                    AnimateWhileStoppedKey,
                    value ? "1" : "0"
                );
            }
        }

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

        // The editor calls this on every tick; the particles are advanced only once a step has
        // passed, and the views show them when they redraw (the Scene view's "Always Refresh",
        // or a change in the scene).
        private void Advance()
        {
            if (this == null || Application.isPlaying)
            {
                UnityEditor.EditorApplication.update -= Advance;
                return;
            }
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            // Turned off, or Unity is behind another app: the particles stay where they are.
            if (
                !AnimateWhileStopped
                || !UnityEditorInternal.InternalEditorUtility.isApplicationActive
            )
            {
                lastTime = now;
                return;
            }
            double elapsed = now - lastTime;
            if (elapsed < StepSeconds)
                return;
            lastTime = now;
            float delta = Mathf.Min((float)elapsed, 0.1f);
            foreach (var system in systems)
                if (system != null)
                    system.Simulate(delta, false, false, false);
        }
#endif
    }
}
