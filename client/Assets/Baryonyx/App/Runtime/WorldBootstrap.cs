using Baryonyx.UI;
using Baryonyx.WorldMap;
using UnityEngine;

namespace Baryonyx.App
{
    /// <summary>
    /// Runs the adventure's world map scene, the nested world map (doc/features/world-atlas.md):
    /// on the world map, the button at the top left returns to Home and the one at the bottom
    /// right opens the list of dungeons (for now, the travel office), behind the shared shutter.
    /// Squares with an event open its sample window, as on the debug room's prototype; a battle's
    /// "たたかう" draws the square's enemies (<see cref="WorldAtlasBattle"/>) and opens the battle
    /// scene, which comes back here.
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
            navigator.StartBattle = Fight;
        }

        // マスの設定から敵を引いて戦闘へ移る。設定が読めないマスは、戦わずに地図へ戻る。
        private void Fight(string square)
        {
            var battle = navigator.BattleAt(square, System.Environment.TickCount);
            if (battle == null)
            {
                Debug.LogWarning($"The battle of {square} cannot be read.", this);
                return;
            }
            WorldAtlasBattle.Pending = battle;
            SceneLoader.Load(SceneNames.Battle, transition, this);
        }
    }
}
