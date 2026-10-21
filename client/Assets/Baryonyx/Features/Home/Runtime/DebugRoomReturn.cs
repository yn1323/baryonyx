using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.Home
{
    /// <summary>Where the button back to Home goes in a room: a corner its own controls leave free.</summary>
    public enum DebugRoomCorner
    {
        // 右上のルームの名前の下（戦闘の画面は、左上に行動順のアイコンがある）。
        TopRight,

        // 左下（探索の画面は、上に名前と操作盤がある）。
        BottomLeft,
    }

    /// <summary>
    /// The "ホームへ" button put over a debugging room opened from Home's list when the room has
    /// no way back of its own (the battle mock, the enemy and moment rooms). Home hands the
    /// prefab over before the room loads; once the room's scene is loaded, a copy is put in it.
    /// It goes with the room when another scene loads.
    /// </summary>
    public sealed class DebugRoomReturn : MonoBehaviour
    {
        // 安全領域の端からの間（設計座標）。上は、ルームの名前の1行の下に置く。
        public static readonly Vector2 Margin = new(60f, 12f);
        public const float BelowTitle = 72f;

        public Button Button;

        // 置き場所を動かす、ボタンの外枠（安全領域の子）。
        public RectTransform Frame;

        // 戻る先のシーン（Home）。
        public string HomeScene = "Home";

        private static DebugRoomReturn pending;
        private static string pendingScene;
        private static DebugRoomCorner pendingCorner;

        private void Awake()
        {
            if (Button != null)
                Button.onClick.AddListener(GoHome);
        }

        public void GoHome()
        {
            if (Button != null)
                Button.interactable = false;
            if (!Baryonyx.UI.SceneLoader.Load(HomeScene, null, this) && Button != null)
                Button.interactable = true;
        }

        /// <summary>Moves the button to <paramref name="corner"/> of the safe area.</summary>
        public void Place(DebugRoomCorner corner)
        {
            if (Frame == null)
                return;
            bool top = corner == DebugRoomCorner.TopRight;
            var anchor = top ? Vector2.one : Vector2.zero;
            Frame.anchorMin = Frame.anchorMax = Frame.pivot = anchor;
            Frame.anchoredPosition = top
                ? new Vector2(-Margin.x, -BelowTitle)
                : new Vector2(Margin.x, Margin.y);
        }

        /// <summary>
        /// Puts a copy of <paramref name="prefab"/> at <paramref name="corner"/> of
        /// <paramref name="scene"/> once it is loaded.
        /// </summary>
        public static void ShowWhenLoaded(
            DebugRoomReturn prefab,
            string scene,
            DebugRoomCorner corner
        )
        {
            if (prefab == null || string.IsNullOrEmpty(scene))
                return;
            pending = prefab;
            pendingScene = scene;
            pendingCorner = corner;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != pendingScene)
                return;
            SceneManager.sceneLoaded -= Loaded;
            var prefab = pending;
            pending = null;
            pendingScene = null;
            if (prefab == null)
                return;
            var copy = Instantiate(prefab);
            copy.Place(pendingCorner);
            SceneManager.MoveGameObjectToScene(copy.gameObject, scene);
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= Loaded;
            pending = null;
            pendingScene = null;
        }
    }
}
