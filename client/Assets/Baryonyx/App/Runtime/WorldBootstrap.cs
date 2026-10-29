using Baryonyx.UI;
using Baryonyx.WorldMap;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the adventure's world map scene, the nested world map (doc/features/world-atlas.md):
    /// on the world map, the button at the top left returns to Home and the one at the bottom
    /// right opens the list of dungeons (for now, the travel office), behind the shared shutter.
    /// Squares with an event open its sample window, as on the debug room's prototype.
    /// </summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        [SerializeField]
        private WorldAtlasNavigator navigator;

        [SerializeField]
        private SceneTransitionController transition;

        public WorldAtlasNavigator Navigator => navigator;
        public SceneTransitionController Transition => transition;

        // 地図を開く前に渡し、世界地図にもどるとダンジョン一覧のボタンを出させる。
        private void Awake()
        {
            if (navigator == null)
            {
                Debug.LogError("WorldBootstrap requires the navigator.", this);
                return;
            }
            navigator.Leave = () => SceneLoader.Load(SceneNames.Home, transition, this);
            navigator.OpenDungeons = () =>
                SceneLoader.Load(SceneNames.TravelOffice, transition, this);
        }
    }
}
