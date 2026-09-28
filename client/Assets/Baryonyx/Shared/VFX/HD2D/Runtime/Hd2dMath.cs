using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    // HD-2Dの演出が共通で使う計算。
    internal static class Hd2dMath
    {
        // 0〜1へ丸めた値を、両端でなめらかに止まる曲線（smoothstep）へ写す。
        public static float SmoothUnit(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
