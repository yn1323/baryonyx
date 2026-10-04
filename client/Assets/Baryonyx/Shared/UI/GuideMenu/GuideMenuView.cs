using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// A guide screen: the guide's upper body on the left, and the menu, a full list or a world
    /// map on the right. GuideMenuAssets bakes every menu row, list and map pin into the prefab
    /// from the definition, so the text can be read in the editor without Play Mode; this view
    /// only wires them up and switches which one is visible. Without a presenter from the scene
    /// (the showcase preview), the view makes its own so the menus still open.
    /// </summary>
    public sealed class GuideMenuView : MonoBehaviour, IGuideMenuView
    {
        public GuideMenuDefinition Definition;

        public Button Back;
        public GameObject MenuPanel;
        public Button[] MenuItems = Array.Empty<Button>();

        public GameObject ListPanel;
        public TMP_Text ListTitle;
        public ScrollRect ListScroll;

        // One baked list per menu item; only the open one is active.
        public RectTransform[] Lists = Array.Empty<RectTransform>();

        // A feature's own panel that replaces the list for an item (null for a plain list),
        // e.g. the tavern's bonus settings. The panel handles its own input.
        public GameObject[] ItemPanels = Array.Empty<GameObject>();
        public Button Confirm;
        public TMP_Text ConfirmLabel;

        public GameObject MapPanel;
        public Button[] Pins = Array.Empty<Button>();
        public TMP_Text MapDetail;
        public Button Depart;

        public CanvasGroup Toast;
        public TMP_Text ToastLabel;

        private readonly List<Button[]> entries = new();
        private GuideMenuState state = new(GuideMenuPage.Menu, -1, -1);
        private FadingMessage toastFade;
        private GuideMenuPresenter ownPresenter;
        private bool bound;

        public event Action<int> ItemPressed;
        public event Action<int> EntryPressed;
        public event Action ConfirmPressed;
        public event Action BackPressed;

        /// <summary>The rows of the open list (empty on the menu page).</summary>
        public IReadOnlyList<Button> Entries =>
            state.Page == GuideMenuPage.List && state.Item < entries.Count
                ? entries[state.Item]
                : Array.Empty<Button>();

        public string ToastMessage => ToastLabel != null ? ToastLabel.text : "";

        private void Awake()
        {
            if (Back != null)
                Back.onClick.AddListener(() => BackPressed?.Invoke());
            if (Confirm != null)
                Confirm.onClick.AddListener(() => ConfirmPressed?.Invoke());
            if (Depart != null)
                Depart.onClick.AddListener(() => ConfirmPressed?.Invoke());
            for (int i = 0; i < MenuItems.Length; i++)
            {
                int index = i;
                MenuItems[i].onClick.AddListener(() => ItemPressed?.Invoke(index));
            }
            foreach (var list in Lists)
            {
                var rows = list.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < rows.Length; i++)
                {
                    int index = i;
                    rows[i].onClick.AddListener(() => EntryPressed?.Invoke(index));
                }
                entries.Add(rows);
            }
            for (int i = 0; i < Pins.Length; i++)
            {
                int index = i;
                Pins[i].onClick.AddListener(() => EntryPressed?.Invoke(index));
            }
            if (Toast != null)
                Toast.alpha = 0f;
        }

        private void Start()
        {
            // The scene binds a presenter before Start; a bare prefab (the showcase) drives itself.
            if (!bound && Definition != null)
                ownPresenter = new GuideMenuPresenter(this, Definition, null);
        }

        private void OnDestroy() => ownPresenter?.Dispose();

        /// <summary>Called by the scene before the view starts, so it does not make its own presenter.</summary>
        public void MarkBound() => bound = true;

        private void Update()
        {
            // The Android back key arrives as Escape.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                BackPressed?.Invoke();
        }

        public void Render(GuideMenuState next)
        {
            state = next;
            bool map = Definition != null && Definition.Layout == GuideMenuLayout.Map;
            bool open = !map && state.Page == GuideMenuPage.List;
            var custom = open ? PanelFor(state.Item) : null;
            bool list = open && custom == null;
            for (int i = 0; i < ItemPanels.Length; i++)
                if (ItemPanels[i] != null && ItemPanels[i] != custom)
                    ItemPanels[i].SetActive(false);
            if (custom != null)
                custom.SetActive(true);
            if (MenuPanel != null)
                MenuPanel.SetActive(!map && !open);
            if (ListPanel != null)
                ListPanel.SetActive(list);
            if (MapPanel != null)
                MapPanel.SetActive(map);
            if (list)
                RenderList();
            if (map)
                RenderMap();
        }

        /// <summary>The feature panel shown for an item instead of the list, if it has one.</summary>
        public GameObject PanelFor(int item) =>
            item >= 0 && item < ItemPanels.Length ? ItemPanels[item] : null;

        private void RenderList()
        {
            for (int i = 0; i < Lists.Length; i++)
                Lists[i].gameObject.SetActive(i == state.Item);
            if (state.Item < 0 || state.Item >= Lists.Length)
                return;
            var item = Definition.Items[state.Item];
            SetText(ListTitle, item.Label);
            SetText(ConfirmLabel, item.ConfirmLabel);
            if (ListScroll != null && ListScroll.content != Lists[state.Item])
            {
                ListScroll.content = Lists[state.Item];
                ListScroll.verticalNormalizedPosition = 1f;
            }
            var rows = entries[state.Item];
            for (int i = 0; i < rows.Length; i++)
                SetSelected(rows[i], i == state.Selected);
            if (Confirm != null)
                Confirm.interactable = state.CanConfirm;
        }

        private void RenderMap()
        {
            for (int i = 0; i < Pins.Length; i++)
                SetSelected(Pins[i], i == state.Selected);
            var point =
                state.Selected >= 0 && state.Selected < Definition.MapPoints.Length
                    ? Definition.MapPoints[state.Selected]
                    : null;
            SetText(
                MapDetail,
                point != null ? point.Name + "　" + point.Detail : "行き先を選んでください"
            );
            if (Depart != null)
                Depart.interactable = state.CanConfirm;
        }

        public void ShowToast(string message) =>
            (toastFade ??= new FadingMessage(this)).Show(Toast, ToastLabel, message, 1.4f, 0.3f);

        private static void SetSelected(Button button, bool selected)
        {
            var mark = button.transform.Find("Selected");
            if (mark != null)
                mark.gameObject.SetActive(selected);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
