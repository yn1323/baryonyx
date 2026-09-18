using Baryonyx.Wireframe;
using UnityEngine;

namespace Baryonyx.App
{
    public sealed class WireframeBootstrap : MonoBehaviour
    {
        public WireframeData Data;
        public WireframeView View;

        private void Start()
        {
            if (Data == null || View == null)
            {
                Debug.LogError("Wireframe requires its sample data and screen prefab.", this);
                return;
            }
            View.Bind(new WireframeSession(Data));
        }
    }
}
