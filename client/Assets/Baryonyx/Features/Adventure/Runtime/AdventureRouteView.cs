using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The whole route of the adventure, opened from the adventure's menu
    /// (doc/features/stage-progression.md): the rooms floor by floor from the entrance at the
    /// bottom to the deepest at the top, the ways between them, the rooms passed through and
    /// where the party is now. The rooms and lines are made from templates each time it opens,
    /// as the route comes from the server.
    /// </summary>
    public sealed class AdventureRouteView : MonoBehaviour
    {
        public const string HereText = "いまここ";

        public CanvasGroup Group;
        public RectTransform Area;

        // 部屋の見本。枠・アイコン・名前を持ち、非表示のまま置く。
        public RectTransform RoomTemplate;
        public Image LineTemplate;
        public TMP_Text FloorTemplate;
        public Button CloseButton;

        // 部屋の種類ごとのアイコン（AdventureRoomKind の順）。入口はなし。
        public Sprite[] KindIcons = Array.Empty<Sprite>();
        public Sprite Frame;
        public Sprite FrameHere;

        public Color Passed = Color.white;
        public Color Ahead = new(0.55f, 0.56f, 0.62f, 0.75f);
        public Color PassedLine = new(1f, 0.843f, 0.4f, 0.9f);
        public Color AheadLine = new(0.6f, 0.62f, 0.7f, 0.45f);

        private readonly List<GameObject> made = new();
        private bool wired;

        public bool IsShown => Group != null && Group.blocksRaycasts;

        // 表示中の部屋の数。テストで確かめる。
        public int RoomCount { get; private set; }
        public string HereRoom { get; private set; } = "";

        public event Action Closed;

        private void Awake()
        {
            Wire();
            if (!IsShown)
                Hide();
        }

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            if (CloseButton != null)
                CloseButton.onClick.AddListener(Close);
            foreach (var template in new Component[] { RoomTemplate, LineTemplate, FloorTemplate })
                if (template != null)
                    template.gameObject.SetActive(false);
        }

        public void Show(AdventureRun run)
        {
            Wire();
            Clear();
            if (run == null || Area == null)
                return;
            Group.alpha = 1f;
            Group.blocksRaycasts = true;
            Group.interactable = true;
            transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            Lay(run);
        }

        public void Hide()
        {
            if (Group == null)
                return;
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            Group.interactable = false;
        }

        public bool HandleBack()
        {
            if (!IsShown)
                return false;
            Close();
            return true;
        }

        private void Close()
        {
            Hide();
            Closed?.Invoke();
        }

        private void Clear()
        {
            foreach (var item in made)
                if (item != null)
                    Destroy(item);
            made.Clear();
            RoomCount = 0;
            HereRoom = "";
        }

        private void Lay(AdventureRun run)
        {
            var size = Area.rect.size;
            int floors = run.DeepestFloor;
            float rowHeight = size.y / Mathf.Max(1, floors);
            var centers = new Dictionary<string, Vector2>();
            foreach (var floor in run.Rooms.GroupBy(room => room.Floor).OrderBy(group => group.Key))
            {
                var rooms = floor.ToArray();
                float y = -size.y / 2f + rowHeight * (floor.Key - 0.5f);
                for (int i = 0; i < rooms.Length; i++)
                {
                    float x = -size.x / 2f + 160f + (size.x - 160f) * (i + 1) / (rooms.Length + 1);
                    centers[rooms[i].Id] = new Vector2(x, y);
                }
                if (FloorTemplate != null)
                {
                    var label = Instantiate(FloorTemplate, Area);
                    label.gameObject.SetActive(true);
                    label.text = AdventureCatalog.FloorText(floor.Key);
                    var rect = label.rectTransform;
                    rect.anchoredPosition = new Vector2(-size.x / 2f + 80f, y);
                    made.Add(label.gameObject);
                }
            }

            var route = run.Route;
            foreach (var room in run.Rooms)
            foreach (var next in room.Next)
                if (centers.ContainsKey(next))
                    Line(centers[room.Id], centers[next], Walked(route, room.Id, next));
            foreach (var room in run.Rooms)
                Room(room, centers[room.Id], route.Contains(room.Id), room.Id == run.RoomId);
        }

        // 通った道：ルートの中で続けて通った2つの部屋。
        private static bool Walked(IReadOnlyList<string> route, string from, string to)
        {
            for (int i = 0; i + 1 < route.Count; i++)
                if (route[i] == from && route[i + 1] == to)
                    return true;
            return false;
        }

        private void Line(Vector2 from, Vector2 to, bool walked)
        {
            if (LineTemplate == null)
                return;
            var line = Instantiate(LineTemplate, Area);
            line.gameObject.SetActive(true);
            var rect = line.rectTransform;
            var delta = to - from;
            rect.anchoredPosition = (from + to) / 2f;
            rect.sizeDelta = new Vector2(delta.magnitude, walked ? 12f : 8f);
            rect.localRotation = Quaternion.Euler(
                0,
                0,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg
            );
            line.color = walked ? PassedLine : AheadLine;
            line.transform.SetAsFirstSibling();
            made.Add(line.gameObject);
        }

        private void Room(AdventureRoom room, Vector2 center, bool passed, bool here)
        {
            var node = Instantiate(RoomTemplate, Area);
            node.gameObject.SetActive(true);
            node.anchoredPosition = center;
            var frame = node.GetComponent<Image>();
            if (frame != null)
            {
                frame.sprite = here ? FrameHere : Frame;
                frame.color = passed || here ? Passed : Ahead;
            }
            var iconRect = node.Find("Icon");
            var icon = iconRect != null ? iconRect.GetComponent<Image>() : null;
            int kind = (int)room.Kind;
            var sprite = kind < KindIcons.Length ? KindIcons[kind] : null;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = passed || here ? Color.white : Ahead;
            }
            // 絵のない部屋（入口）は、名前を枠の中央に置く。
            var nameRect = node.Find("Name");
            var label = nameRect != null ? nameRect.GetComponent<TMP_Text>() : null;
            if (label != null && sprite == null)
                label.rectTransform.anchoredPosition = Vector2.zero;
            if (label != null)
                label.text =
                    here ? HereText
                    : room.Kind == AdventureRoomKind.Start ? "入口"
                    : AdventureCatalog.Hint(room.Kind);
            made.Add(node.gameObject);
            RoomCount++;
            if (here)
                HereRoom = room.Id;
        }
    }
}
