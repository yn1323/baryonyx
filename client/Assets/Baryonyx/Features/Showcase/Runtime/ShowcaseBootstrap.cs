using UnityEngine;

namespace Baryonyx.Showcase
{
    public sealed class ShowcaseBootstrap : MonoBehaviour
    {
        public ShowcaseCatalog Catalog;
        public bool BuildOnStart = true;

        private ShowcaseRuntimeView view;

        private void Start()
        {
            if (!BuildOnStart)
                return;

            view = gameObject.AddComponent<ShowcaseRuntimeView>();
            view.Catalog = Catalog;
            view.Build();
        }

        private void OnDestroy()
        {
            if (view != null)
                Destroy(view);
        }
    }
}
