using System.Collections.Generic;
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
    /// prefab and those rooms over before the room loads; once a room's scene is loaded, a copy
    /// is put in it. It goes with the room when another scene loads, and a room moved to from
    /// it (the moment rooms open each other) gets a copy too, until a scene that is not one of
    /// those rooms loads.
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
        private static IReadOnlyDictionary<string, DebugRoomCorner> pendingRooms;

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
        /// Puts a copy of <paramref name="prefab"/> in each of <paramref name="rooms"/> loaded
        /// from now on, at the room's corner, until a scene that is not one of them loads.
        /// </summary>
        public static void ShowInRooms(
            DebugRoomReturn prefab,
            IReadOnlyDictionary<string, DebugRoomCorner> rooms
        )
        {
            if (prefab == null || rooms == null || rooms.Count == 0)
                return;
            pending = prefab;
            pendingRooms = rooms;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
                return;
            if (pending == null || !pendingRooms.TryGetValue(scene.name, out var corner))
            {
                Reset();
                return;
            }
            var copy = Instantiate(pending);
            copy.Place(corner);
            SceneManager.MoveGameObjectToScene(copy.gameObject, scene);
        }

        // Play Modeに入るたびに初期化する（ドメインの再読み込みを省く設定でも残さない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= Loaded;
            pending = null;
            pendingRooms = null;
        }
    }
}
