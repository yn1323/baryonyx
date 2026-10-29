using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// The runes on a map of the nested world map (doc/features/world-atlas.md#マスを開くルーン):
    /// the balance under the place's name at the top right, with the runes taken for each square
    /// rising from it and fading, a price tag under each next square, the window that asks before a walk of several squares that
    /// costs runes (with the choice not to be asked again until the app starts again), the button at the
    /// bottom left that stops a walk where the party stands, and, in development builds and the
    /// Editor, the switch that turns charging off.
    /// </summary>
    public sealed class WorldAtlasToll : MonoBehaviour
    {
        public WorldMapView View;

        public TMP_Text Balance;

        // 1マス払うたびに残高の下から浮かんで消える「-200」の札の見本（暗い地に文字）。
        public RectTransform SpendTemplate;

        // 開発用のビルドとEditorだけに出す、消費のオンオフ。
        public GameObject SwitchRoot;
        public Button Switch;
        public Image SwitchTrack;
        public RectTransform SwitchKnob;
        public TMP_Text SwitchLabel;

        // 次に行けるマスの下の値札。地図の中に置き、地図と一緒に動く。
        public RectTransform Tags;
        public RectTransform TagTemplate;

        // 複数マスを進む道でルーンを払うときに確かめる窓（後ろの地図は幕で押せなくする）。
        public CanvasGroup Confirm;
        public TMP_Text Message;
        public Button Go;
        public Button Cancel;
        public Button Skip;
        public GameObject SkipCheck;

        // 歩いている間に左下に出す、文字のない止めるボタン。
        public GameObject StopRoot;
        public Button Stop;

        public static readonly Color OnColor = new(0.43f, 0.67f, 0.35f, 1f);
        public static readonly Color OffColor = new(0.35f, 0.33f, 0.4f, 1f);
        private const float KnobTravel = 22f;

        private readonly List<(RectTransform Rect, TMP_Text Label)> tags = new();
        private readonly List<(RectTransform Rect, float Age)> spends = new();
        private Vector2 spendFrom;

        // 「-200」が浮かぶ時間と高さ。
        public const float SpendSeconds = 1.1f;
        private const float SpendRise = 40f;
        private IReadOnlyList<int> asked;
        private bool skipChecked;
        private bool dirty = true;

        public bool Asking => asked != null;

        private void Awake()
        {
            if (TagTemplate != null)
                TagTemplate.gameObject.SetActive(false);
            spendFrom = SpendTemplate.anchoredPosition;
            SpendTemplate.gameObject.SetActive(false);
            Go.onClick.AddListener(Accept);
            Cancel.onClick.AddListener(Hide);
            Skip.onClick.AddListener(ToggleSkip);
            Stop.onClick.AddListener(() => View.Stop());
            Switch.onClick.AddListener(ToggleCharging);
            ShowConfirm(false);
        }

        private void OnEnable()
        {
            View.MoveAsked += Ask;
            View.Refreshed += MarkDirty;
            WorldAtlasSession.Changed += MarkDirty;
            WorldAtlasSession.Spent += ShowSpent;
        }

        private void OnDisable()
        {
            View.MoveAsked -= Ask;
            View.Refreshed -= MarkDirty;
            WorldAtlasSession.Changed -= MarkDirty;
            WorldAtlasSession.Spent -= ShowSpent;
        }

        private void MarkDirty() => dirty = true;

        private void Update()
        {
            Balance.text = WorldAtlasSession.Loaded ? WorldAtlasSession.Runes.ToString("N0") : "…";
            SwitchRoot.SetActive(WorldAtlasSession.CanSwitch);
            StopRoot.SetActive(View.CanStop && !Asking);
            if (dirty)
            {
                dirty = false;
                PlaceTags();
                PaintSwitch();
            }
            FloatSpends(Time.unscaledDeltaTime);
        }

        /// <summary>Shows "-<paramref name="runes"/>" rising from under the balance.</summary>
        public void ShowSpent(int runes)
        {
            int index = spends.FindIndex(spend => !spend.Rect.gameObject.activeSelf);
            if (index < 0)
            {
                var copy = Instantiate(SpendTemplate, SpendTemplate.parent);
                copy.name = "Spend" + spends.Count;
                copy.gameObject.AddComponent<CanvasGroup>();
                spends.Add((copy, 0f));
                index = spends.Count - 1;
            }
            var rect = spends[index].Rect;
            spends[index] = (rect, 0f);
            rect.GetComponentInChildren<TMP_Text>(true).text = $"-{runes:N0}";
            rect.gameObject.SetActive(true);
            FloatSpends(0f);
        }

        // 出てすぐ少しふくらみ、上へ昇りながら消える。
        private void FloatSpends(float dt)
        {
            for (int i = 0; i < spends.Count; i++)
            {
                var (rect, age) = spends[i];
                if (!rect.gameObject.activeSelf)
                    continue;
                age += dt;
                spends[i] = (rect, age);
                float x = Mathf.Clamp01(age / SpendSeconds);
                if (x >= 1f)
                {
                    rect.gameObject.SetActive(false);
                    continue;
                }
                float rise = 1f - (1f - x) * (1f - x);
                rect.anchoredPosition = spendFrom + new Vector2(0f, SpendRise * rise);
                float pop = x < 0.15f ? 1f + 0.25f * Mathf.Sin(Mathf.PI * x / 0.15f) : 1f;
                rect.localScale = Vector3.one * pop;
                rect.GetComponent<CanvasGroup>().alpha = x < 0.6f ? 1f : 1f - (x - 0.6f) / 0.4f;
            }
        }

        /// <summary>
        /// Asks before walking <paramref name="path"/>, which costs <paramref name="total"/>
        /// runes, when it goes more than one square; one square shows its price tag already.
        /// </summary>
        public void Ask(IReadOnlyList<int> path, int total)
        {
            int runes = WorldAtlasSession.Runes;
            // 1歩で1ルーンになるため、足りない分は歩数で出す。
            int shortBy = total - runes;
            bool single = path.Count <= 2;
            if (shortBy <= 0 && (single || WorldAtlasSession.SkipConfirm))
            {
                View.Go(path);
                return;
            }
            if (single)
            {
                View.ShowNotice($"{WorldAtlasSession.ShortMessage}（あと{shortBy:N0}歩）");
                return;
            }
            asked = path;
            Message.text =
                shortBy > 0
                    ? $"合計{total:N0}ルーンかかります。\n{WorldAtlasSession.ShortMessage}（あと{shortBy:N0}歩）"
                    : $"合計{total:N0}ルーンかかります。\nよろしいですか？";
            Go.interactable = shortBy <= 0;
            skipChecked = false;
            SkipCheck.SetActive(false);
            View.Highlight(path);
            ShowConfirm(true);
        }

        private void Accept()
        {
            var path = asked;
            if (path == null)
                return;
            if (skipChecked)
                WorldAtlasSession.SkipConfirm = true;
            Hide();
            View.Go(path);
        }

        public void Hide()
        {
            asked = null;
            View.Highlight(null);
            ShowConfirm(false);
        }

        private void ShowConfirm(bool shown)
        {
            Confirm.alpha = shown ? 1f : 0f;
            Confirm.blocksRaycasts = shown;
            Confirm.interactable = shown;
        }

        private void ToggleSkip()
        {
            skipChecked = !skipChecked;
            SkipCheck.SetActive(skipChecked);
        }

        private void ToggleCharging()
        {
            WorldAtlasSession.Charging = !WorldAtlasSession.Charging;
            if (Asking)
                Hide();
            View.Refresh();
        }

        private void PaintSwitch()
        {
            bool on = WorldAtlasSession.Charging;
            SwitchTrack.color = on ? OnColor : OffColor;
            SwitchKnob.anchoredPosition = new Vector2(on ? KnobTravel : -KnobTravel, 0f);
            // 文字は丸の反対側に出す。
            ((RectTransform)SwitchLabel.transform).anchoredPosition = new Vector2(
                on ? -14f : 14f,
                0f
            );
            SwitchLabel.text = on ? "ON" : "OFF";
        }

        // 次に行けるマス（開けたマスの隣）のうち、払うマスの下に値札を出す。
        private void PlaceTags()
        {
            int used = 0;
            if (View.Charging && View.Graph != null)
            {
                var states = View.States;
                for (int i = 0; i < states.Count && i < View.Nodes.Length; i++)
                {
                    if (states[i] != WorldNodeState.Open)
                        continue;
                    int price = View.PriceOf(i);
                    if (price <= 0)
                        continue;
                    var (rect, label) = TagAt(used++);
                    var node = View.Nodes[i];
                    rect.anchoredPosition =
                        node.anchoredPosition + new Vector2(0f, -node.sizeDelta.y * 0.42f);
                    label.text = price.ToString("N0");
                    rect.gameObject.SetActive(true);
                }
            }
            for (int i = used; i < tags.Count; i++)
                tags[i].Rect.gameObject.SetActive(false);
        }

        private (RectTransform, TMP_Text) TagAt(int index)
        {
            while (tags.Count <= index)
            {
                var rect = Instantiate(TagTemplate, Tags);
                rect.name = "Tag" + tags.Count;
                tags.Add((rect, rect.GetComponentInChildren<TMP_Text>(true)));
            }
            return tags[index];
        }
    }
}
