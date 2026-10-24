using Baryonyx.UI;
using Baryonyx.WorldMap;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the world map scene (the highway prototype): the button at the top left returns to
    /// Home, and the one at the bottom right opens the list of dungeons (for now, the travel
    /// office) behind the shared shutter.
    /// </summary>
    public sealed class WorldMapBootstrap : MonoBehaviour
    {
        [SerializeField]
        private WorldMapView view;

        [SerializeField]
        private SceneTransitionController transition;

        public WorldMapView View => view;
        public SceneTransitionController Transition => transition;

        private void Start()
        {
            if (view == null)
            {
                Debug.LogError("WorldMapBootstrap requires a view.", this);
                return;
            }
            view.BackPressed += () => SceneLoader.Load(SceneNames.Home, transition, this);
            view.DungeonsPressed += () =>
                SceneLoader.Load(SceneNames.TravelOffice, transition, this);
        }
    }
}
