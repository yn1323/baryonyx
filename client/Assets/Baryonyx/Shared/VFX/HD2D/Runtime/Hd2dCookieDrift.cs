using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Moves the sun's cookie (the leaves' dapples or the clouds' shadows) over the stage in Play
    /// Mode: clouds drift with the wind, and leaves sway a little, so the light on the ground is
    /// never frozen. Outside Play Mode the cookie stays where it is saved, so nothing in the scene
    /// changes while it is edited.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class Hd2dCookieDrift : MonoBehaviour
    {
        [Tooltip("風で流れる速さ（m/秒、ライトクッキーの面のx・y）。雲の影に使う。")]
        public Vector2 Drift;

        [Tooltip("葉の揺れで木漏れ日がずれる幅（m）。")]
        [Min(0f)]
        public float Sway;

        [Tooltip("葉の揺れの周期（秒）。")]
        [Min(0.1f)]
        public float SwayPeriod = 3.4f;

        private UniversalAdditionalLightData data;
        private Vector2 start;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            data = GetComponent<Light>().GetUniversalAdditionalLightData();
            start = data.lightCookieOffset;
        }

        private void OnDisable()
        {
            if (data != null)
                data.lightCookieOffset = start;
            data = null;
        }

        private void Update()
        {
            if (data == null)
                return;
            float t = Time.time;
            float turn = t * 2f * Mathf.PI / SwayPeriod;
            var sway = new Vector2(Mathf.Sin(turn), 0.6f * Mathf.Sin(turn * 0.73f + 1.3f)) * Sway;
            var offset = start + Drift * t + sway;
            // The cookie repeats every tile, so the offset is kept within one tile.
            var size = data.lightCookieSize;
            if (size.x > 0f)
                offset.x = Mathf.Repeat(offset.x, size.x);
            if (size.y > 0f)
                offset.y = Mathf.Repeat(offset.y, size.y);
            data.lightCookieOffset = offset;
        }
    }
}
