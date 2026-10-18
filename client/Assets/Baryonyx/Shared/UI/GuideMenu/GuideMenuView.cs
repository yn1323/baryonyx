using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Economy;
using Baryonyx.UI.EditModePreview;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baryonyx.UI.GuideMenu
{
    /// <summary>
    /// A guide screen: the guide's upper body on the left, and the menu, a full list or the
    /// destinations on the right. GuideMenuAssets bakes every menu row, list row and destination
    /// into the prefab from the definition, so the text can be read in the editor without Play
    /// Mode; this view only wires them up and switches which one is visible. Without a presenter
    /// from the scene (the showcase preview), the view makes its own so the menus still open.
    /// </summary>
    public sealed class GuideMenuView : MonoBehaviour, IGuideMenuView, IEditModePreview
    {
        public GuideMenuDefinition Definition;

        public Button Back;

        // 右上の×。どの深さからでもホームへ戻る。
        public Button Home;

        // 「もどる」の右のパンくず（例：編成 › 冒険者 › アリア）と、×の左の所持ルーン。
        public TMP_Text Breadcrumb;
        public TMP_Text Runes;

        // 左の案内人。項目の専用のパネルが画面を広く使うときは隠す。
        public GameObject GuideArt;
        public GameObject MenuPanel;

        // 定義の項目と同じ順。メニューに出さない項目はnull。
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

        // 旅の案内所の行き先のリスト。メニューを通さず、開いたときから表示する。
        public GameObject DestinationPanel;
        public Button[] DestinationRows = Array.Empty<Button>();
        public Button Depart;

        // 操作を求めない通知（共通の通知の帯）。
        public NoticeBand Notice;

        // 画面いっぱいの項目のパネル（GuideFramedPanel）を開いている間だけ出す枠：ぼかした背景、
        // ヘッダーの帯と区切りの線、ヘッダーの下の線。
        public GameObject[] FrameParts = Array.Empty<GameObject>();

        // 枠を出している間は透明にする、「もどる」と×のタブ、所持ルーンの下地（押せる範囲は残す）。
        public Graphic[] TabBackdrops = Array.Empty<Graphic>();

        private readonly List<Button[]> entries = new();
        private GuideMenuState state = new(GuideMenuPage.Menu, -1, -1);
        private GuideMenuPresenter ownPresenter;
        private GameObject openPanel;
        private bool bound;
        private float[] tabAlphas = Array.Empty<float>();

        public event Action<int> ItemPressed;
        public event Action<int> EntryPressed;
        public event Action ConfirmPressed;
        public event Action BackPressed;
        public event Action HomePressed;

        public const string TrailSeparator = " › ";

        /// <summary>The rows of the open list (empty on the menu page).</summary>
        public IReadOnlyList<Button> Entries =>
            state.Page == GuideMenuPage.List && state.Item < entries.Count
                ? entries[state.Item]
                : Array.Empty<Button>();

        public string ToastMessage => Notice != null ? Notice.LastMessage : "";

        private void Awake()
        {
            if (Back != null)
                Back.onClick.AddListener(PressBack);
            if (Home != null)
                Home.onClick.AddListener(() => HomePressed?.Invoke());
            if (Confirm != null)
                Confirm.onClick.AddListener(() => ConfirmPressed?.Invoke());
            if (Depart != null)
                Depart.onClick.AddListener(() => ConfirmPressed?.Invoke());
            // メニューに出さない項目（Hidden）の位置はnull。
            for (int i = 0; i < MenuItems.Length; i++)
            {
                if (MenuItems[i] == null)
                    continue;
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
            for (int i = 0; i < DestinationRows.Length; i++)
            {
                int index = i;
                DestinationRows[i].onClick.AddListener(() => EntryPressed?.Invoke(index));
            }
            if (Notice != null)
                Notice.Hide();
            tabAlphas = TabBackdrops
                .Select(graphic => graphic != null ? graphic.color.a : 1f)
                .ToArray();
        }

        private void Start()
        {
            // The scene binds a presenter before Start; a bare prefab (the showcase) drives itself.
            if (!bound && Definition != null)
                ownPresenter = new GuideMenuPresenter(this, Definition, null);
        }

        private void OnDestroy() => ownPresenter?.Dispose();

        private void OnEnable()
        {
            RuneBalance.Changed += ShowRunes;
            ShowRunes();
        }

        private void OnDisable() => RuneBalance.Changed -= ShowRunes;

        private void ShowRunes() => SetText(Runes, RuneBalance.Text(RuneBalance.Known));

        /// <summary>Called by the scene before the view starts, so it does not make its own presenter.</summary>
        public void MarkBound() => bound = true;

        private void Update()
        {
            // The Android back key arrives as Escape.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                PressBack();
        }

        // 開いている項目のパネルが自分の中で戻れるなら（重ねた画面を閉じるなど）、そちらを先にする。
        public void PressBack()
        {
            if (
                openPanel != null
                && openPanel.TryGetComponent(out IGuideBackHandler panel)
                && panel.HandleBack()
            )
                return;
            BackPressed?.Invoke();
        }

        public void Render(GuideMenuState next)
        {
            state = next;
            bool destinations =
                Definition != null && Definition.Layout == GuideMenuLayout.Destinations;
            bool open = !destinations && state.Page == GuideMenuPage.List;
            var custom = open ? PanelFor(state.Item) : null;
            openPanel = custom;
            bool list = open && custom == null;
            for (int i = 0; i < ItemPanels.Length; i++)
                if (ItemPanels[i] != null && ItemPanels[i] != custom)
                    ItemPanels[i].SetActive(false);
            if (custom != null)
                custom.SetActive(true);
            if (GuideArt != null)
                GuideArt.SetActive(custom == null);
            // 枠は、画面いっぱいの項目のパネルと、案内人の右に出す一覧・行き先のリストで出す。
            ShowFrame(
                list
                    || destinations
                    || (custom != null && custom.GetComponent<GuideFramedPanel>() != null)
            );
            if (MenuPanel != null)
                MenuPanel.SetActive(!destinations && !open);
            if (ListPanel != null)
                ListPanel.SetActive(list);
            if (DestinationPanel != null)
                DestinationPanel.SetActive(destinations);
            if (list)
                RenderList();
            if (destinations)
                RenderDestinations();
            RefreshTrail();
        }

        /// <summary>
        /// The breadcrumb: the screen's title, then the open item's label, or what its feature
        /// panel names (<see cref="IGuideBreadcrumb"/>).
        /// </summary>
        public IReadOnlyList<string> Trail()
        {
            var parts = new List<string>();
            if (Definition == null)
                return parts;
            parts.Add(Definition.Title);
            bool open =
                Definition.Layout != GuideMenuLayout.Destinations
                && state.Page == GuideMenuPage.List
                && state.Item >= 0
                && state.Item < Definition.Items.Length;
            if (open)
            {
                if (openPanel != null && openPanel.TryGetComponent(out IGuideBreadcrumb panel))
                    parts.AddRange(panel.Trail ?? Array.Empty<string>());
                else
                    parts.Add(Definition.Items[state.Item].Label);
            }
            return parts.Where(part => !string.IsNullOrEmpty(part)).ToList();
        }

        // 項目のパネルは、出す人が変わったときなどに呼ぶ。
        public void RefreshTrail() => SetText(Breadcrumb, string.Join(TrailSeparator, Trail()));

        /// <summary>The label of the item with <paramref name="key"/>, or empty.</summary>
        public string LabelOf(string key)
        {
            int index = IndexOf(key);
            return index >= 0 ? Definition.Items[index].Label : "";
        }

        public bool HasItem(string key) => IndexOf(key) >= 0;

        /// <summary>
        /// Opens the item with <paramref name="key"/> as if its menu row were pressed, so one
        /// feature panel can move to another (e.g. the training to the card skills).
        /// </summary>
        public bool OpenItem(string key)
        {
            int index = IndexOf(key);
            if (index < 0)
                return false;
            ItemPressed?.Invoke(index);
            return true;
        }

        private int IndexOf(string key) =>
            Definition == null || string.IsNullOrEmpty(key)
                ? -1
                : Array.FindIndex(Definition.Items, item => item.Key == key);

        /// <summary>The feature panel shown for an item instead of the list, if it has one.</summary>
        public GameObject PanelFor(int item) =>
            item >= 0 && item < ItemPanels.Length ? ItemPanels[item] : null;

        // 停止中の表示の切り替え：項目ごとに、その項目を開いたときの表示。メニューに出さない項目は、
        // 同じ名前の項目と見分けるためキーを添える。項目のパネルの中に切り替えられる画面があれば、
        // 「項目 › 状態」も続けて並べる（例：育成（training） › レベルアップ）。
        public IReadOnlyList<string> PreviewStates =>
            PreviewEntries().Select(entry => entry.Label).ToArray();

        public void ShowPreviewState(int index)
        {
            var entry = PreviewEntries()[index];
            Render(new GuideMenuState(GuideMenuPage.List, entry.Item, -1));
            entry.Panel?.ShowPreviewState(entry.State);
        }

        private List<(string Label, int Item, IEditModePreview Panel, int State)> PreviewEntries()
        {
            var list = new List<(string, int, IEditModePreview, int)>();
            if (Definition == null || Definition.Layout == GuideMenuLayout.Destinations)
                return list;
            for (int i = 0; i < Definition.Items.Length; i++)
            {
                var item = Definition.Items[i];
                string label = item.Hidden ? item.Label + "（" + item.Key + "）" : item.Label;
                list.Add((label, i, null, -1));
                var panel = PanelFor(i);
                if (panel == null)
                    continue;
                foreach (var screen in panel.GetComponentsInChildren<IEditModePreview>(true))
                {
                    var states = screen.PreviewStates;
                    for (int j = 0; j < states.Count; j++)
                        list.Add((label + TrailSeparator + states[j], i, screen, j));
                }
            }
            return list;
        }

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
            // 停止中の表示の切り替え（IEditModePreview）では Awake が動かず、行を集めていない。
            if (state.Item < entries.Count)
            {
                var rows = entries[state.Item];
                for (int i = 0; i < rows.Length; i++)
                    SetSelected(rows[i], i == state.Selected);
            }
            if (Confirm != null)
                Confirm.interactable = state.CanConfirm;
        }

        private void RenderDestinations()
        {
            for (int i = 0; i < DestinationRows.Length; i++)
                SetSelected(DestinationRows[i], i == state.Selected);
            if (Depart != null)
                Depart.interactable = state.CanConfirm;
        }

        /// <summary>Whether the frame of a full-screen panel is showing.</summary>
        public bool Framed { get; private set; }

        private void ShowFrame(bool framed)
        {
            Framed = framed;
            foreach (var part in FrameParts)
                if (part != null)
                    part.SetActive(framed);
            for (int i = 0; i < TabBackdrops.Length; i++)
            {
                var graphic = TabBackdrops[i];
                if (graphic == null)
                    continue;
                var color = graphic.color;
                color.a =
                    framed ? 0f
                    : i < tabAlphas.Length ? tabAlphas[i]
                    : 1f;
                graphic.color = color;
            }
        }

        public void ShowToast(string message)
        {
            if (Notice != null)
                Notice.Show(message);
        }

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
