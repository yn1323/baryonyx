using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// The clock that the computed ambient shaders (<c>Baryonyx/HD2D/UI Ambient</c>,
    /// <c>Baryonyx/HD2D/UI Fog</c>) read their motion from: the drift of the shafts' dust, the
    /// shimmer of the glows and the haze. The components publish their time in Play Mode; outside
    /// it the effects stand still at 0, and a capture can show any moment with <see cref="Set"/>.
    /// </summary>
    public static class Hd2dAmbientClock
    {
        private static readonly int TimeId = Shader.PropertyToID("_Hd2dAmbientTime");

        public static void Set(float time) => Shader.SetGlobalFloat(TimeId, time);

        /// <summary>Publishes the time of a component that plays in scaled or unscaled time.</summary>
        public static void Publish(bool unscaled) =>
            Set(unscaled ? Time.unscaledTime : Time.time);
    }
}
