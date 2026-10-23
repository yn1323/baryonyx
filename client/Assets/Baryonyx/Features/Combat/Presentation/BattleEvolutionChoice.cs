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
    /// stage (it rises; a thin rim of light runs round it and wisps of pale gold light rise from
    /// its edges like steam over a soft light, motes drifting up through them; the others darken),
    /// and the button under the row confirms it. With one stage to go to, there is nothing to choose:
    /// it is shown picked for a moment, with no button, and goes on by itself. Nothing else is
    /// pressed while open.
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
            "選んでいる候補を包む光の霧（Slotsと同じ順）。どの候補のカードより奥に置き、子に光（Glow）・縁の光（Rim）・立ちのぼる霧（Wisp）・光の粒（Mote）を持つ。"
        )]
        public RectTransform[] Auras = Array.Empty<RectTransform>();

        [Tooltip("選んでいない候補を暗くする幕（Slotsと同じ順）。")]
        public Graphic[] Veils = Array.Empty<Graphic>();

        [Tooltip("取得済みの段階に出す札（Slotsと同じ順）。")]
        public GameObject[] AcquiredTags = Array.Empty<GameObject>();

        [Tooltip("浮いているカードから各候補へ伸びる光の枝（Slotsと同じ順）。")]
        public BattleVfxImage[] Branches = Array.Empty<BattleVfxImage>();

        public Button ConfirmButton;

        [Tooltip("候補の並びの中心の高さ（画面の中央から）。")]
        public float RowY = -64f;

        [Tooltip("隣の候補との間（中心どうし）。")]
        public float Spacing = 490f;

        [Tooltip("候補が1つのとき、見せてから自動で進むまでの間（秒、開いてから）。")]
        [Min(0f)]
        public float SingleShow = 0.9f;

        private static readonly Color BranchColor = new(1f, 0.84f, 0.42f);

        private readonly List<Option> options = new();
        private Action<int> chosen;
        private Vector2 origin;
        private float age;
        private int selected = -1;
        private float pickedAt;
        private BattleVfxImage[][] rims = Array.Empty<BattleVfxImage[]>();
        private BattleVfxImage[][] wisps = Array.Empty<BattleVfxImage[]>();
        private BattleVfxImage[][] motes = Array.Empty<BattleVfxImage[]>();
        private BattleVfxImage[] glows = Array.Empty<BattleVfxImage>();

        // The rising wisps' pale gold, the rim's gold and the soft light behind it all.
        private static readonly Color WispColor = new(1f, 0.8f, 0.42f, 0.5f);
        private static readonly Color RimColor = new(1f, 0.86f, 0.46f, 1f);
        private static readonly Color AuraColor = new(1f, 0.8f, 0.4f, 0.6f);

        // How long one wisp lives (s), and the rim's beams: how thick, how far outside the
        // edge their core runs and how far past the corners they reach.
        private const float WispLife = 1.6f;
        private const float RimWidth = 40f;
        private const float RimOut = 5f;
        private const float RimReach = 24f;

        /// <summary>True from <see cref="Open"/> until a stage is confirmed or it is closed.</summary>
        public bool IsOpen => Panel != null && Panel.gameObject.activeSelf;

        /// <summary>The stage picked now (its place in the options), or -1.</summary>
        public int Selected => selected;

        /// <summary>How many stages are offered.</summary>
        public int Count => options.Count;

        private void Awake()
        {
            CollectAuras();
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
            if (options.Count == 1)
                Pick(0);
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
            if (options.Count == 1 && age >= SingleShow)
                Confirm();
        }

        // The stages rise into their places one after another as the branches are drawn down
        // to them; the one picked stands a little higher in its glow.
        private void Step()
        {
            if (Panel == null)
                return;
            Panel.alpha = Mathf.Clamp01(age / 0.15f);
            Panel.blocksRaycasts = age >= 0.2f;
            int count = options.Count;
            for (int i = 0; i < count; i++)
            {
                float k = Mathf.Clamp01((age - 0.08f * i) / 0.32f);
                float rise = 1f - Mathf.Pow(1f - k, 3f);
                bool picked = i == selected;
                var place = SlotPlace(i, count);
                var slot = (RectTransform)Slots[i].transform;
                slot.anchoredPosition =
                    place + Vector2.down * (70f * (1f - rise)) + Vector2.up * (picked ? 18f : 0f);
                slot.localEulerAngles = new Vector3(0f, 0f, SlotTilt(i, count));
                float scale = picked ? 1.06f : 1f;
                slot.localScale = new Vector3(scale, scale, 1f);
                if (i < Auras.Length && Auras[i] != null)
                {
                    if (Auras[i].gameObject.activeSelf != picked)
                        Auras[i].gameObject.SetActive(picked);
                    if (picked)
                    {
                        // It stands where the card does, behind every card.
                        Auras[i].anchoredPosition = slot.anchoredPosition;
                        Auras[i].localEulerAngles = slot.localEulerAngles;
                        Auras[i].localScale = slot.localScale;
                        Aura(i, slot.rect.size);
                    }
                }
                if (i < Veils.Length && Veils[i] != null)
                {
                    var veil = new Color(0f, 0f, 0f, selected >= 0 && !picked ? 0.45f : 0f);
                    if (Veils[i].color != veil)
                        Veils[i].color = veil;
                }
                if (i < Branches.Length && Branches[i] != null)
                    Branch(Branches[i], slot.anchoredPosition, rise, picked);
            }
            if (ConfirmButton != null)
            {
                ConfirmButton.interactable = selected >= 0;
                // With one stage there is nothing to confirm; it goes on by itself.
                bool shown = count > 1;
                if (ConfirmButton.gameObject.activeSelf != shown)
                    ConfirmButton.gameObject.SetActive(shown);
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

        // Each aura's parts, by their names, so the builder alone decides how many there are.
        private void CollectAuras()
        {
            int count = Auras.Length;
            rims = new BattleVfxImage[count][];
            wisps = new BattleVfxImage[count][];
            motes = new BattleVfxImage[count][];
            glows = new BattleVfxImage[count];
            for (int i = 0; i < count; i++)
            {
                var rim = new List<BattleVfxImage>();
                var wisp = new List<BattleVfxImage>();
                var mote = new List<BattleVfxImage>();
                if (Auras[i] != null)
                    foreach (var part in Auras[i].GetComponentsInChildren<BattleVfxImage>(true))
                    {
                        if (part.name.StartsWith("Rim", StringComparison.Ordinal))
                            rim.Add(part);
                        else if (part.name.StartsWith("Wisp", StringComparison.Ordinal))
                            wisp.Add(part);
                        else if (part.name.StartsWith("Mote", StringComparison.Ordinal))
                            mote.Add(part);
                        else
                            glows[i] = part;
                    }
                rims[i] = rim.ToArray();
                wisps[i] = wisp.ToArray();
                motes[i] = mote.ToArray();
            }
        }

        // The light round the stage picked, all behind it: a soft light that breathes, a thin
        // bright rim of flowing light just outside its edges, wisps of light rising from its top
        // and sides like steam, and motes drifting up through them. It comes in over a quarter of
        // a second.
        private void Aura(int slot, Vector2 size)
        {
            if (slot >= wisps.Length)
                return;
            float t = age - pickedAt;
            float fade = Mathf.Clamp01(t / 0.25f);
            var half = size * 0.5f;
            if (glows[slot] != null)
            {
                var glow = glows[slot];
                glow.rectTransform.sizeDelta = new Vector2(size.x * 1.5f, size.y * 1.3f);
                var tint = AuraColor;
                tint.a *= fade * (0.8f + 0.2f * Mathf.Sin(t * 2.2f));
                glow.color = tint;
                glow.SetShape(0f, 0f, 0f, 1f);
            }
            Rims(rims[slot], half, t, fade);
            var rising = wisps[slot];
            for (int i = 0; i < rising.Length; i++)
                Wisp(rising[i], i, rising.Length, half, t, fade);
            var lights = motes[slot];
            for (int i = 0; i < lights.Length; i++)
            {
                // Each rises from under the card to above it, again and again, at its own place.
                float span = size.y + 160f;
                float climb = (t * 80f + i * span / lights.Length) % span;
                float x = (Mathf.PerlinNoise(i * 3.1f, 0.5f) - 0.5f) * size.x * 1.3f;
                var rect = lights[i].rectTransform;
                rect.anchoredPosition = new Vector2(
                    x + Mathf.Sin(t * 1.6f + i) * 12f,
                    -half.y - 40f + climb
                );
                float through = climb / span;
                var tint = Color.white;
                tint.a = fade * Mathf.Sin(through * Mathf.PI);
                lights[i].color = tint;
                lights[i].SetShape(i * 0.29f % 1f, through, 0f, 1.5f);
            }
        }

        // The rim: a beam of light along each edge, a little outside it, its light flowing.
        private void Rims(BattleVfxImage[] edges, Vector2 half, float t, float fade)
        {
            for (int i = 0; i < edges.Length && i < 4; i++)
            {
                bool across = i < 2;
                float side = i % 2 == 0 ? 1f : -1f;
                var rect = edges[i].rectTransform;
                // Top and bottom run left to right; the sides run up.
                rect.anchoredPosition = across
                    ? new Vector2(-half.x - RimReach, side * (half.y + RimOut))
                    : new Vector2(side * (half.x + RimOut), -half.y - RimReach);
                rect.sizeDelta = new Vector2(
                    (across ? half.x : half.y) * 2f + RimReach * 2f,
                    RimWidth
                );
                rect.localEulerAngles = new Vector3(0f, 0f, across ? 0f : 90f);
                var tint = RimColor;
                tint.a *= fade;
                edges[i].color = tint;
                edges[i].SetShape(0.21f * i, 0.28f + (t * 0.25f + i * 0.17f) % 0.7f, 0f, 1.4f);
            }
        }

        // One wisp of the steam: it is born at a place on the top or a side of the card, rises
        // and swells as it flickers, and fades out before it is born again somewhere else.
        private void Wisp(BattleVfxImage wisp, int i, int count, Vector2 half, float t, float fade)
        {
            float life = WispLife * (0.8f + 0.4f * Hash(i, 0));
            float clock = t + life * i / count;
            int born = Mathf.FloorToInt(clock / life);
            float k = clock / life - born;
            // Where along the top and the sides it rises from, new at each birth.
            float place = Hash(i, born + 1);
            Vector2 root;
            float width = half.x * 2f;
            float height = half.y * 2f;
            float along = place * (width + height * 2f * 0.75f);
            if (along < width)
                root = new Vector2(along - half.x, half.y - 30f);
            else
            {
                along -= width;
                bool right = along >= height * 0.75f;
                float up = right ? along - height * 0.75f : along;
                root = new Vector2((right ? 1f : -1f) * half.x, half.y - up - 10f);
            }
            var rect = wisp.rectTransform;
            // Wide and soft, so a wisp reads as steam, not a flame or a needle of light.
            float tall = 130f + 100f * Hash(i, born + 7);
            rect.anchoredPosition = root + Vector2.up * (k * 40f);
            rect.sizeDelta = new Vector2(tall * 1.1f, tall * (0.9f + 0.3f * k));
            var tint = WispColor;
            tint.a *= fade * Mathf.Sin(k * Mathf.PI);
            wisp.color = tint;
            wisp.SetShape(Hash(i, born + 3), k * 0.9f, 0f, 1f);
        }

        // A steady number from 0 to 1 for a wisp and its birth.
        private static float Hash(int a, int b)
        {
            float h = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.547f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Where a stage's card stands among <paramref name="count"/>: side by side round the middle.</summary>
        public Vector2 SlotPlace(int slot, int count) =>
            new(
                (slot - (count - 1) * 0.5f) * Spacing,
                RowY - Mathf.Abs(slot - (count - 1) * 0.5f) * 14f
            );

        // The cards lean out a little, as a hand fanned out.
        private static float SlotTilt(int slot, int count) => -(slot - (count - 1) * 0.5f) * 4f;
    }
}
