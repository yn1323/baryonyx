using UnityEngine;

namespace Baryonyx.Wireframe
{
    public sealed class WireframeSafeAreaFollower : MonoBehaviour
    {
        public RectTransform Source;

        private void LateUpdate() => Apply();

        public void Apply()
        {
            if (Source == null)
                return;
            var target = (RectTransform)transform;
            if (target.anchorMin == Source.anchorMin && target.anchorMax == Source.anchorMax)
                return;
            target.anchorMin = Source.anchorMin;
            target.anchorMax = Source.anchorMax;
            target.offsetMin = target.offsetMax = Vector2.zero;
        }
    }
}
