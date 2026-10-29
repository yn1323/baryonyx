using System;
using System.Collections.Generic;
using Baryonyx.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Where the next stage of an evolving unique skill is picked
    /// (doc/features/skill-evolution.md#覚醒先を選ぶ画面), over the battle while the cracked card
    /// hovers above: a branch of light runs down from the card to each stage it can evolve into,
    /// shown as a battle card in a row below, one stage already acquired marked so. A tap picks a
    /// stage (it rises a little larger in a haze of pale gold light breathing behind it; the others
    /// step back and darken), and the check button in the bottom-right corner, where the
    /// end of the turn is, confirms it. With one stage to go to, it is picked and confirmed the same
    /// way, so that what it does can be read before it is taken. Nothing else is pressed while open.
    /// </summary>
    public sealed class BattleEvolutionChoice : MonoBehaviour
    {
        /// <summary>A stage that can be picked, as its card shows it.</summary>
        public struct Option
        {
            public string Name;
            public string Owner;
            public string Kind;
            public string Description;
            public Texture Art;
            public Texture Frame;
            public Texture Element;
            public int Cost;
            public ItemQuality Quality;
            public int FoilStyle;
            public bool Acquired;
        }

        public CanvasGroup Panel;

        [Tooltip("候補のカード（押せる枠）。左から並べ、使う数だけ出す。")]
        public Button[] Slots = Array.Empty<Button>();

        [Tooltip("候補のカードの絵（Slotsと同じ順）。")]
        public BattleInspectCardView[] Cards = Array.Empty<BattleInspectCardView>();

        [Tooltip(
            "選んでいる候補の後ろに立つ白金の後光の靄（Slotsと同じ順）。どの候補のカードより奥に置き、靄のシェーダー（Battle Card Aura）を掛けた板。"
        )]
        public RawImage[] Auras = Array.Empty<RawImage>();

        [Tooltip("選んでいない候補を暗くする幕（Slotsと同じ順）。")]
        public Graphic[] Veils = Array.Empty<Graphic>();

        [Tooltip("取得済みの段階に出す札（Slotsと同じ順）。")]
        public GameObject[] AcquiredTags = Array.Empty<GameObject>();

        [Tooltip("浮いているカードから各候補へ伸びる光の枝（Slotsと同じ順）。")]
        public BattleVfxImage[] Branches = Array.Empty<BattleVfxImage>();

        [Tooltip("決めるボタン（右下のチェック）。選んでいない間は押せない。")]
        public Button ConfirmButton;

        [Tooltip("決めるボタンの後ろの光。押せる間だけ息づく。")]
        public Graphic ConfirmGlow;

        [Tooltip("候補の並びの中心の高さ（画面の中央から）。")]
        public float RowY = -42f;

        [Tooltip("隣の候補との間（中心どうし）。")]
        public float Spacing = 560f;

        private static readonly Color BranchColor = new(1f, 0.84f, 0.42f);

        private readonly List<Option> options = new();
        private Action<int> chosen;
        private Vector2 origin;
        private float age;
        private int selected = -1;
        private float pickedAt;

        // The soft light behind the check while it can be pressed.
        private static readonly Color AuraColor = new(1f, 0.8f, 0.4f, 0.75f);

        // The stage picked rises this far, this much larger; the others step back this small.
        private const float PickedRise = 16f;
        private const float PickedScale = 1.04f;
        private const float OtherScale = 0.96f;

        /// <summary>True from <see cref="Open"/> until a stage is confirmed or it is closed.</summary>
        public bool IsOpen => Panel != null && Panel.gameObject.activeSelf;

        /// <summary>The stage picked now (its place in the options), or -1.</summary>
        public int Selected => selected;

        /// <summary>How many stages are offered.</summary>
        public int Count => options.Count;

        private void Awake()
        {
            SetUpAuras();
            for (int i = 0; i < Slots.Length; i++)
            {
                int slot = i;
                if (Slots[i] != null)
                    Slots[i].onClick.AddListener(() => Select(slot));
            }
            if (ConfirmButton != null)
                ConfirmButton.onClick.AddListener(Confirm);
            if (Panel != null && options.Count == 0)
                Panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Offers <paramref name="offered"/> (one to three), the branches running from
        /// <paramref name="originScreen"/> (a point on the screen, under the hovering card).
        /// <paramref name="onChosen"/> is called with the stage confirmed.
        /// </summary>
        public void Open(IReadOnlyList<Option> offered, Vector2 originScreen, Action<int> onChosen)
        {
            if (Panel == null || offered == null || offered.Count == 0)
                return;
            options.Clear();
            for (int i = 0; i < offered.Count && i < Slots.Length; i++)
                options.Add(offered[i]);
            chosen = onChosen;
            age = 0f;
            Panel.gameObject.SetActive(true);
            var rect = (RectTransform)Panel.transform;
            var canvas = rect.GetComponentInParent<Canvas>();
            var eye =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                originScreen,
                eye,
                out origin
            );
            for (int i = 0; i < Slots.Length; i++)
            {
                bool shown = i < options.Count;
                if (Slots[i] != null)
                    Slots[i].gameObject.SetActive(shown);
                if (i < Branches.Length && Branches[i] != null)
                    Branches[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;
                var option = options[i];
                if (i < Cards.Length && Cards[i] != null)
                {
                    Cards[i]
                        .Show(
                            option.Name,
                            option.Owner,
                            option.Kind,
                            option.Description,
                            option.Art,
                            option.Frame,
                            option.Element,
                            option.Cost,
                            option.Quality,
                            option.FoilStyle
                        );
                    Cards[i].SetPlayable(true, 0f);
                }
                if (i < AcquiredTags.Length && AcquiredTags[i] != null)
                    AcquiredTags[i].SetActive(option.Acquired);
            }
            selected = -1;
            Step();
        }

        /// <summary>Picks the stage at <paramref name="slot"/>; the button then confirms it.</summary>
        public void Select(int slot)
        {
            if (!IsOpen || slot < 0 || slot >= options.Count)
                return;
            if (slot != selected)
                Pick(slot);
            Step();
        }

        private void Pick(int slot)
        {
            selected = slot;
            pickedAt = age;
            // The stage picked stands in front of its neighbours, which it may overlap.
            if (slot < Slots.Length && Slots[slot] != null)
                Slots[slot].transform.SetAsLastSibling();
        }

        /// <summary>Confirms the stage picked and closes; nothing happens while none is picked.</summary>
        public void Confirm()
        {
            if (!IsOpen || selected < 0)
                return;
            int picked = selected;
            var then = chosen;
            Close();
            then?.Invoke(picked);
        }

        /// <summary>Closes at once without a pick (an evolution cut short).</summary>
        public void Close()
        {
            chosen = null;
            options.Clear();
            selected = -1;
            if (Panel != null)
                Panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!IsOpen)
                return;
            age += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Step();
        }

        // The stages rise into their places one after another as the branches are drawn down
        // to them; the one picked stands a little higher and larger in its light, the others
        // step back.
        private void Step()
        {
            if (Panel == null)
                return;
            Panel.alpha = Mathf.Clamp01(age / 0.15f);
            Panel.blocksRaycasts = age >= 0.2f;
            int count = options.Count;
            // How far the pick has come in (0 to 1), so the others step back with it.
            float picking = selected >= 0 ? EaseOut(Mathf.Clamp01((age - pickedAt) / 0.18f)) : 0f;
            for (int i = 0; i < count; i++)
            {
                float k = Mathf.Clamp01((age - 0.08f * i) / 0.32f);
                float rise = 1f - Mathf.Pow(1f - k, 3f);
                bool picked = i == selected;
                var slot = (RectTransform)Slots[i].transform;
                slot.anchoredPosition =
                    SlotPlace(i, count)
                    + Vector2.down * (70f * (1f - rise))
                    + Vector2.up * (picked ? PickedRise * picking : 0f);
                float scale = picked
                    ? Mathf.Lerp(1f, PickedScale, picking)
                    : Mathf.Lerp(1f, OtherScale, picking);
                slot.localScale = new Vector3(scale, scale, 1f);
                if (i < Auras.Length && Auras[i] != null)
                {
                    var aura = Auras[i];
                    if (aura.gameObject.activeSelf != picked)
                        aura.gameObject.SetActive(picked);
                    if (picked)
                    {
                        // It stands where the card does, behind every card, and comes in with the pick.
                        aura.rectTransform.anchoredPosition = slot.anchoredPosition;
                        aura.rectTransform.localScale = slot.localScale;
                        var tint = Color.white;
                        tint.a = picking;
                        if (aura.color != tint)
                            aura.color = tint;
                    }
                }
                if (i < Veils.Length && Veils[i] != null)
                {
                    var veil = new Color(0f, 0f, 0f, picked ? 0f : 0.5f * picking);
                    if (Veils[i].color != veil)
                        Veils[i].color = veil;
                }
                if (i < Branches.Length && Branches[i] != null)
                    Branch(Branches[i], slot.anchoredPosition, rise, picked);
            }
            if (ConfirmButton != null)
                ConfirmButton.interactable = selected >= 0;
            if (ConfirmGlow != null)
            {
                // The check breathes once there is something to confirm.
                var tint = AuraColor;
                tint.a *= picking * (0.7f + 0.3f * Mathf.Sin(age * 3f));
                ConfirmGlow.color = tint;
            }
        }

        // A branch of light from under the card to the top of a stage's card, drawn down to it.
        private void Branch(BattleVfxImage branch, Vector2 slot, float drawn, bool picked)
        {
            var top = slot + Vector2.up * (((RectTransform)Slots[0].transform).rect.height * 0.5f);
            var along = top - origin;
            var rect = branch.rectTransform;
            rect.anchoredPosition = origin;
            rect.sizeDelta = new Vector2(along.magnitude, picked ? 70f : 52f);
            rect.localEulerAngles = new Vector3(
                0f,
                0f,
                Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg
            );
            var tint = BranchColor;
            tint.a = selected >= 0 && !picked ? 0.45f : 1f;
            branch.color = tint;
            // The beam is drawn in by a little over a quarter of its progress, then its light
            // keeps flowing down it.
            float flow = drawn >= 1f ? (age * 0.3f) % 0.7f : 0f;
            branch.SetShape(0.37f, 0.28f * drawn + flow, 0f, picked ? 1.6f : 1f);
        }

        // Each aura gets its own copy of the haze's material, told its board and card sizes, its
        // own seed (so neighbouring stages do not breathe alike) and the noise it reads.
        private void SetUpAuras()
        {
            for (int i = 0; i < Auras.Length; i++)
            {
                var aura = Auras[i];
                if (aura == null || aura.material == null)
                    continue;
                var material = new Material(aura.material)
                {
                    name = aura.material.name + " (Aura)",
                    hideFlags = HideFlags.DontSave,
                };
                var card =
                    i < Slots.Length && Slots[i] != null
                        ? ((RectTransform)Slots[i].transform).rect.size
                        : Vector2.zero;
                material.SetVector("_Size", aura.rectTransform.rect.size);
                material.SetVector("_CardHalf", card * 0.5f);
                material.SetVector("_Seed", new Vector4(i * 17.3f, i * 5.1f, i * 2.9f, 0f));
                // The haze reads its noise from a small square made once in code.
                material.SetTexture("_Noise", BattleCardAuraNoise.Texture);
                aura.material = material;
            }
        }

        private static float EaseOut(float k) => 1f - (1f - k) * (1f - k);

        /// <summary>
        /// Where a stage's card stands among <paramref name="count"/>: upright, side by side round
        /// the middle, so each reads as it is.
        /// </summary>
        public Vector2 SlotPlace(int slot, int count) =>
            new((slot - (count - 1) * 0.5f) * Spacing, RowY);
    }
}
