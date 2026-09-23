using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.Showcase
{
    public sealed class ShowcaseBootstrap : MonoBehaviour
    {
        public ShowcaseCatalog Catalog;
        public bool BuildOnStart = true;

        private ShowcaseRuntimeView view;

        public ShowcaseRuntimeView View => view;

        private void Awake()
        {
            EnsureCamera();
        }

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

        private void EnsureCamera()
        {
            var scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentsInChildren<Camera>(true).Length > 0)
                    return;
            }

            var cameraObject = new GameObject("ShowcaseCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.047f, 0.075f, 1f);
            camera.tag = "MainCamera";
        }
    }
}
