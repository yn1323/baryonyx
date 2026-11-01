using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A bet played by opening chests one after another: paying the stake lets the chests in the
    /// row be tapped. Each chest holds runes that pile up, more for each one opened, but one of
    /// them is a mimic that takes the whole pile. After a good chest the player may stop and take
    /// the pile. A party member good at it may peek into one closed chest, once.
    /// </summary>
    public sealed class WorldEventChestRun : WorldEventPanel
    {
        public RawImage[] Chests = System.Array.Empty<RawImage>();
        public Button[] Taps = System.Array.Empty<Button>();
        public TMP_Text[] Gains = System.Array.Empty<TMP_Text>();

        // のぞいた宝箱の上に出す印（中身がルーン・ミミック）。
        public GameObject[] SafeMarks = System.Array.Empty<GameObject>();
        public GameObject[] MimicMarks = System.Array.Empty<GameObject>();
        public Texture2D ClosedArt;
        public Texture2D OpenArt;
        public Texture2D MimicArt;
        public Vector2 ChestSize;
        public Vector2 MimicSize;
        public TMP_Text Runes;
        public TMP_Text Pot;
        public RectTransform PotPlate;
        public Button Play;
        public CanvasGroup PlayGroup;
        public Button Stop;
        public CanvasGroup StopGroup;
        public Button Peek;
        public CanvasGroup PeekGroup;
        public RectTransform Win;
        public TMP_Text WinLabel;
        public RectTransform Lose;

        private const float ShakeSeconds = 0.3f;
        private const float GainSeconds = 0.9f;
        private const float GainRise = 110f;

        private enum Step
        {
            Ready,
            Picking,
            Opening,
            Won,
            Lost,
        }

        private System.Random random;
        private int rigged = -1;
        private Step step;
        private float stepAt;
        private int opening;
        private bool[] opened;
        private float[] openedAt;
        private bool peeked;
        private float shownBalance;
        private float shownPot;

        public int Balance { get; private set; }

        /// <summary>The runes piled up by this bet (what the opened chests held).</summary>
        public int PotRunes { get; private set; }

        /// <summary>The chest that is a mimic.</summary>
        public int Mimic { get; private set; }

        /// <summary>The good chests opened so far.</summary>
        public int Found { get; private set; }

        /// <summary>A chest can be tapped now.</summary>
        public bool Picking => step == Step.Picking;

        public bool Lost => step == Step.Lost;

        private void Awake()
        {
            Play.onClick.AddListener(Begin);
            Stop.onClick.AddListener(Quit);
            Peek.onClick.AddListener(PeekOne);
            for (int i = 0; i < Taps.Length; i++)
            {
                int chest = i;
                Taps[i].onClick.AddListener(() => Open(chest));
            }
        }

        /// <summary>The next bet hides the mimic in chest <paramref name="chest"/> (tests).</summary>
        public void Rig(int chest) => rigged = chest;

        protected override void Restart()
        {
            random ??= new System.Random(System.Environment.TickCount);
            opened = new bool[Chests.Length];
            openedAt = new float[Chests.Length];
            for (int i = 0; i < Chests.Length; i++)
            {
                SetArt(i, ClosedArt, ChestSize);
                Chests[i].rectTransform.localRotation = Quaternion.identity;
                Chests[i].rectTransform.anchoredPosition = Vector2.zero;
                Taps[i].interactable = false;
                Gains[i].gameObject.SetActive(false);
                SafeMarks[i].SetActive(false);
                MimicMarks[i].SetActive(false);
            }
            step = Step.Ready;
            stepAt = 0f;
            Mimic = -1;
            Found = 0;
            peeked = false;
            Balance = WorldEventSamples.PeddlerRunes;
            shownBalance = Balance;
            PotRunes = 0;
            shownPot = 0f;
            Runes.text = Balance.ToString("N0");
            Pot.text = "0";
            Pot.color = UiPalette.GoldLight;
            PotPlate.gameObject.SetActive(false);
            PotPlate.localRotation = Quaternion.identity;
            Show(PlayGroup, true);
            Show(StopGroup, false);
            Show(PeekGroup, false);
            Win.gameObject.SetActive(false);
            Lose.gameObject.SetActive(false);
        }

        /// <summary>Pays the stake and lets the chests be opened (the play button).</summary>
        public void Begin()
        {
            if (step != Step.Ready)
                return;
            Balance -= WorldEventSamples.BetStake;
            Mimic = rigged >= 0 ? rigged : random.Next(Chests.Length);
            rigged = -1;
            PotPlate.gameObject.SetActive(true);
            Show(PlayGroup, false);
            Pick();
        }

        /// <summary>Opens chest <paramref name="chest"/> while the chests can be tapped.</summary>
        public void Open(int chest)
        {
            if (step != Step.Picking || opened[chest])
                return;
            opening = chest;
            opened[chest] = true;
            foreach (var tap in Taps)
                tap.interactable = false;
            Show(StopGroup, false);
            Show(PeekGroup, false);
            Go(Step.Opening);
        }

        /// <summary>Takes the pile (only after a good chest).</summary>
        public void Quit()
        {
            if (step != Step.Picking || Found == 0)
                return;
            Cash();
        }

        /// <summary>The party member's help: shows what one closed chest holds, once.</summary>
        public void PeekOne()
        {
            if (step != Step.Picking || peeked || !Host.Helped)
                return;
            peeked = true;
            int closed = 0;
            for (int i = 0; i < opened.Length; i++)
                if (!opened[i])
                    closed++;
            int pick = random.Next(closed);
            for (int i = 0; i < opened.Length; i++)
            {
                if (opened[i] || pick-- > 0)
                    continue;
                SafeMarks[i].SetActive(i != Mimic);
                MimicMarks[i].SetActive(i == Mimic);
                break;
            }
            Show(PeekGroup, false);
        }

        private void Pick()
        {
            Go(Step.Picking);
            for (int i = 0; i < Taps.Length; i++)
                Taps[i].interactable = !opened[i];
            Show(StopGroup, Found > 0);
            Show(PeekGroup, Host.Helped && !peeked);
        }

        private void Reveal()
        {
            SafeMarks[opening].SetActive(false);
            MimicMarks[opening].SetActive(false);
            if (opening == Mimic)
            {
                SetArt(opening, MimicArt, MimicSize);
                PotRunes = 0;
                Pot.color = new Color(0.95f, 0.38f, 0.32f, 1f);
                Lose.gameObject.SetActive(true);
                Go(Step.Lost);
                return;
            }
            SetArt(opening, OpenArt, ChestSize);
            int gain = WorldEventSamples.ChestRunGains[
                Mathf.Min(Found, WorldEventSamples.ChestRunGains.Length - 1)
            ];
            Found++;
            PotRunes += gain;
            openedAt[opening] = Clock;
            Gains[opening].text = $"+{gain}";
            Gains[opening].gameObject.SetActive(true);
            // ミミックのほかを全部開けたら、自動で降りる。
            if (Found >= Chests.Length - 1)
                Cash();
            else
                Pick();
        }

        private void Cash()
        {
            Balance += PotRunes;
            WinLabel.text = $"{PotRunes:N0}ルーン";
            foreach (var tap in Taps)
                tap.interactable = false;
            Show(StopGroup, false);
            Show(PeekGroup, false);
            Go(Step.Won);
        }

        private void Go(Step next)
        {
            step = next;
            stepAt = Clock;
        }

        protected override void Draw(float dt)
        {
            float t = Clock - stepAt;
            shownBalance = Mathf.MoveTowards(shownBalance, Balance, 2400f * dt);
            Runes.text = Mathf.RoundToInt(shownBalance).ToString("N0");
            shownPot = Mathf.MoveTowards(shownPot, PotRunes, Mathf.Max(800f, PotRunes * 3f) * dt);
            Pot.text = Mathf.RoundToInt(shownPot).ToString("N0");
            DrawGains();
            switch (step)
            {
                case Step.Picking:
                    // 開けられる宝箱は、順にときどき小さく揺れる。
                    for (int i = 0; i < Chests.Length; i++)
                    {
                        if (opened[i])
                            continue;
                        float p = (Clock + i * 0.37f) % 1.8f;
                        float rock =
                            p < 0.4f ? Mathf.Sin(p / 0.4f * Mathf.PI * 3f) * (1f - p / 0.4f) : 0f;
                        Chests[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, rock * 4f);
                    }
                    break;
                case Step.Opening:
                    var shaking = Chests[opening].rectTransform;
                    shaking.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Sin(t * 70f) * 6f * (t / ShakeSeconds)
                    );
                    if (t >= ShakeSeconds)
                    {
                        shaking.localRotation = Quaternion.identity;
                        Reveal();
                    }
                    break;
                case Step.Won:
                    Done = EventMotion.PopRow(Win, t - 0.3f);
                    break;
                case Step.Lost:
                    // ミミックが2度跳ね、積んだルーンの札が揺れて赤い0になる。
                    float hop = Mathf.Abs(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.6f) * 2f));
                    Chests[opening].rectTransform.anchoredPosition = new Vector2(0f, 50f * hop);
                    PotPlate.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Sin(t * 60f) * 5f * Mathf.Clamp01(1f - t / 0.4f)
                    );
                    Done = EventMotion.PopRow(Lose, t - 0.5f);
                    break;
            }
        }

        // 開けた宝箱の上に、足したルーンが浮かんで消える。
        private void DrawGains()
        {
            for (int i = 0; i < Gains.Length; i++)
            {
                if (!Gains[i].gameObject.activeSelf)
                    continue;
                float x = Mathf.Clamp01((Clock - openedAt[i]) / GainSeconds);
                Gains[i].rectTransform.anchoredPosition = new Vector2(
                    0f,
                    GainRise * EventMotion.Decelerate(x)
                );
                Gains[i].alpha = 1f - Mathf.Clamp01((x - 0.6f) / 0.4f);
                if (x >= 1f)
                    Gains[i].gameObject.SetActive(false);
            }
        }

        private void SetArt(int chest, Texture2D art, Vector2 size)
        {
            Chests[chest].texture = art;
            Chests[chest].rectTransform.sizeDelta = size;
        }

        private static void Show(CanvasGroup group, bool shown)
        {
            group.alpha = shown ? 1f : 0f;
            group.interactable = group.blocksRaycasts = shown;
        }
    }
}
