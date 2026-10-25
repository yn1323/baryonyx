using UnityEngine;

namespace Baryonyx.TempLab
{
    /// <summary>
    /// TempLab, the room for quick trials (doc/features/screens/debug-rooms.md): the battle's
    /// dusk highland with its camera, bloom, lens and grade, and an empty canvas over it. A trial
    /// asked for in chat is built here, captured and sent back, and taken out again once it is
    /// settled. A trial's parts find the stage and the canvas through this.
    /// </summary>
    public sealed class TempLabRoom : MonoBehaviour
    {
        [Tooltip("舞台を写すカメラ。")]
        public Camera StageCamera;

        [Tooltip("3Dの舞台（夕暮れの高原）。")]
        public Transform Stage;

        [Tooltip("試作のUIを置く、Safe Areaに合わせた全面の領域。")]
        public RectTransform Sandbox;
    }
}
