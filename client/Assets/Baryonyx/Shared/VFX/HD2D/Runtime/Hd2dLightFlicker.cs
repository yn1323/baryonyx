using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Makes a real light of the 3D stage flicker like a flame: its intensity and range follow two
    /// layers of smooth noise, so nearby walls, floor and characters brighten and dim together.
    /// Each light has its own seed, so torches do not flicker in step.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class Hd2dLightFlicker : MonoBehaviour
    {
        [Tooltip("明るさの揺れ幅（元の明るさに対する割合）。炎は0.15〜0.3、魔法の光は0.05前後。")]
        [Range(0f, 1f)]
        public float IntensityAmount = 0.2f;

        [Tooltip("照らす範囲の揺れ幅（元の範囲に対する割合）。")]
        [Range(0f, 0.5f)]
        public float RangeAmount = 0.05f;

        [Tooltip("揺れの速さ。炎は2〜3。")]
        [Min(0f)]
        public float Speed = 2.4f;

        [Tooltip("揺れ方を変える種。光ごとに変える。")]
        public float Seed;

        private Light target;
        private float baseIntensity;
        private float baseRange;

        private void OnEnable()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            baseRange = target.range;
        }

        private void OnDisable()
        {
            if (target == null)
                return;
            target.intensity = baseIntensity;
            target.range = baseRange;
        }

        private void Update()
        {
            float wave = Flicker(Time.time, Speed, Seed);
            target.intensity = baseIntensity * (1f + IntensityAmount * wave);
            target.range = baseRange * (1f + RangeAmount * wave);
        }

        /// <summary>A flame-like wobble from -1 to 1: slow swells with quick flutters on top.</summary>
        public static float Flicker(float time, float speed, float seed)
        {
            float slow = Mathf.PerlinNoise(seed * 13.1f, time * speed * 0.35f) * 2f - 1f;
            float fast = Mathf.PerlinNoise(time * speed * 1.7f, seed * 7.3f + 3f) * 2f - 1f;
            return Mathf.Clamp(slow * 0.65f + fast * 0.35f, -1f, 1f);
        }
    }
}
