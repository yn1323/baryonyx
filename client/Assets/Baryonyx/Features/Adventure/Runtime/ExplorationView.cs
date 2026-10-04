using System;
using System.Collections;
using Baryonyx.StepBonus;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>One door out of the room, standing where the battle's enemies stand.</summary>
    [Serializable]
    public sealed class ExplorationDoorWidget
    {
        // 扉の足元。3Dの舞台では、この位置の地面に扉の板が立つ。
        public RectTransform Body;
        public CanvasGroup Group;
        public Button Button;

        // 扉の上の札：部屋の種類のアイコンと短い手掛かり。
        public Image Icon;
        public TMP_Text Hint;
    }

    /// <summary>
    /// The exploration screen (doc/features/screens.md): the party on the left and up to two
    /// doors on the right of the room, on the battle's 3D stage of the destination, the place and
    /// floor at the top, a prompt at the bottom, the chest of a treasure room, and the menu at the
    /// top left. A door tapped sends the party walking to it. ExplorationAssets builds it; the
    /// adventure's flow (<see cref="ExplorationFlow"/>) drives it.
    /// </summary>
    public sealed class ExplorationView : MonoBehaviour
    {
        // 入口へ歩く時間と、扉の手前で止まる位置（扉の足元からの設計座標）。
        public const float WalkSeconds = 0.8f;
        public static readonly Vector2 DoorStop = new(-150f, -40f);

        [Header("舞台")]
        public RectTransform[] Party = Array.Empty<RectTransform>();
        public ExplorationDoorWidget[] Doors = Array.Empty<ExplorationDoorWidget>();
        public RectTransform Chest;
        public CanvasGroup ChestGroup;
        public Button ChestButton;
        public RawImage ChestSprite;
        public Texture ChestClosed;
        public Texture ChestOpen;

        [Header("手前")]
        public TMP_Text Location;
        public TMP_Text Prompt;
        public Button MenuButton;
        public GameDialog Dialog;
        public NoticeBand Notice;
        public AdventureRouteView Route;

        // 部屋の種類ごとのアイコン（AdventureRoomKind の順）。
        public Sprite[] KindIcons = Array.Empty<Sprite>();

        // 報酬のボーナスの名前とアイコン。
        public StepBonusMockData Bonuses;

        private Vector2[] partyBase = Array.Empty<Vector2>();
        private Coroutine walking;
        private bool wired;

        public event Action<int> DoorPressed;
        public event Action ChestPressed;
        public event Action MenuPressed;

        // 端末の戻るキー。開いているルート・ダイアログが先に受け取り、なければメニューを開く。
        public event Action BackPressed;

        public bool Walking => walking != null;
        public string LocationText => Location != null ? Location.text : "";
        public string PromptText => Prompt != null ? Prompt.text : "";

        private void Awake()
        {
            partyBase = new Vector2[Party.Length];
            for (int i = 0; i < Party.Length; i++)
                partyBase[i] = Party[i].anchoredPosition;
            Wire();
        }

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            for (int i = 0; i < Doors.Length; i++)
            {
                int index = i;
                if (Doors[i].Button != null)
                    Doors[i].Button.onClick.AddListener(() => DoorPressed?.Invoke(index));
            }
            if (ChestButton != null)
                ChestButton.onClick.AddListener(() => ChestPressed?.Invoke());
            if (MenuButton != null)
                MenuButton.onClick.AddListener(() => MenuPressed?.Invoke());
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                PressBack();
        }

        /// <summary>The device's back key, as <see cref="Update"/> reads it.</summary>
        public void PressBack()
        {
            if (Route != null && Route.HandleBack())
                return;
            if (Dialog != null && Dialog.HandleBack())
                return;
            BackPressed?.Invoke();
        }

        /// <summary>Shows the room: its place, its doors once the room is done, and its chest.</summary>
        public void Render(ExplorationRoom room)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));
            ResetParty();
            if (Location != null)
                Location.text = room.Location;
            if (Prompt != null)
                Prompt.text = room.Prompt;
            for (int i = 0; i < Doors.Length; i++)
            {
                var door = Doors[i];
                bool shown = i < room.Exits.Count;
                door.Group.alpha = shown ? 1f : 0f;
                door.Group.blocksRaycasts = shown;
                door.Group.interactable = shown;
                // 押せる範囲は手前の画面にあるため、扉と一緒に出し入れする。
                if (door.Button != null)
                    door.Button.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var exit = room.Exits[i];
                int kind = (int)exit.Kind;
                if (door.Icon != null)
                {
                    door.Icon.sprite = kind < KindIcons.Length ? KindIcons[kind] : null;
                    door.Icon.enabled = door.Icon.sprite != null;
                }
                if (door.Hint != null)
                    door.Hint.text = exit.Hint;
            }
            if (ChestGroup != null)
            {
                bool chest = room.Chest != ExplorationChest.None;
                ChestGroup.alpha = chest ? 1f : 0f;
                ChestGroup.blocksRaycasts = room.Chest == ExplorationChest.Closed;
                ChestGroup.interactable = room.Chest == ExplorationChest.Closed;
            }
            if (ChestButton != null)
                ChestButton.gameObject.SetActive(room.Chest == ExplorationChest.Closed);
            if (ChestSprite != null && room.Chest != ExplorationChest.None)
                ChestSprite.texture = room.Chest == ExplorationChest.Open ? ChestOpen : ChestClosed;
        }

        /// <summary>The party walks to the door's front, then <paramref name="arrived"/> runs.</summary>
        public void WalkTo(int door, Action arrived)
        {
            if (walking != null)
                StopCoroutine(walking);
            walking = StartCoroutine(Walk(door, arrived));
        }

        private IEnumerator Walk(int door, Action arrived)
        {
            var target = Doors[door].Body.anchoredPosition + DoorStop;
            var from = new Vector2[Party.Length];
            for (int i = 0; i < Party.Length; i++)
                from[i] = Party[i].anchoredPosition;
            // 隊列のまま進む：先頭（扉に最も近い者）が扉の手前に着く分だけ全員が動く。
            var lead = from[0];
            for (int i = 1; i < from.Length; i++)
                if (from[i].x > lead.x)
                    lead = from[i];
            var shift = target - lead;
            for (float t = 0f; t < WalkSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / WalkSeconds);
                // 1ドット（4px）ずつ進み、ドット絵の歩みに見せる。
                for (int i = 0; i < Party.Length; i++)
                {
                    var p = from[i] + shift * k;
                    Party[i].anchoredPosition = new Vector2(
                        Mathf.Round(p.x / 4f) * 4f,
                        Mathf.Round(p.y / 4f) * 4f
                    );
                }
                yield return null;
            }
            for (int i = 0; i < Party.Length; i++)
                Party[i].anchoredPosition = from[i] + shift;
            walking = null;
            arrived?.Invoke();
        }

        public void ResetParty()
        {
            if (walking != null)
            {
                StopCoroutine(walking);
                walking = null;
            }
            for (int i = 0; i < Party.Length && i < partyBase.Length; i++)
                Party[i].anchoredPosition = partyBase[i];
        }

        public void ShowNotice(string message)
        {
            if (Notice != null)
                Notice.Show(message);
        }

        public Sprite KindIcon(AdventureRoomKind kind) =>
            (int)kind < KindIcons.Length ? KindIcons[(int)kind] : null;
    }
}
