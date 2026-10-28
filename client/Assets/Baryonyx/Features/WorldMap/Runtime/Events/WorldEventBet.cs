using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A bet: three cards lie face down. Paying the stake turns them up to show the winning one,
    /// turns them down again and shuffles them; the card tapped then turns up. The winner gives
    /// the prize and the others a small thing, so no bet ends with nothing.
    /// </summary>
    public sealed class WorldEventBet : WorldEventPanel
    {
        public RectTransform[] Cards = System.Array.Empty<RectTransform>();
        public GameObject[] Backs = System.Array.Empty<GameObject>();
        public GameObject[] Winners = System.Array.Empty<GameObject>();
        public GameObject[] Losers = System.Array.Empty<GameObject>();
        public Button[] Picks = System.Array.Empty<Button>();
        public Button Play;
        public CanvasGroup PlayGroup;
        public TMP_Text Runes;
        public RectTransform Win;
        public RectTransform Lose;

        private const float FlipSeconds = 0.3f;
        private const float PeekSeconds = 0.7f;
        private const float SwapSeconds = 0.32f;
        private const int Swaps = 5;
        private const int WinningCard = 1;

        private enum Step
        {
            Ready,
            Peek,
            Shuffle,
            Pick,
            Reveal,
        }

        private Step step;
        private float stepAt;
        private Vector2[] slots;
        private int[] slotOf;
        private readonly int[,] swaps = new int[Swaps, 2];
        private int picked;
        private float shown;

        public int Balance { get; private set; }

        /// <summary>The cards can be picked (the shuffle is over).</summary>
        public bool Picking => step == Step.Pick;

        /// <summary>The card that wins.</summary>
        public int Winner => WinningCard;

        private void Awake()
        {
            Play.onClick.AddListener(Begin);
            for (int i = 0; i < Picks.Length; i++)
            {
                int card = i;
                Picks[i].onClick.AddListener(() => Pick(card));
            }
        }

        protected override void Restart()
        {
            if (slots == null)
            {
                slots = new Vector2[Cards.Length];
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = Cards[i].anchoredPosition;
            }
            slotOf = new int[Cards.Length];
            for (int i = 0; i < Cards.Length; i++)
            {
                slotOf[i] = i;
                Cards[i].anchoredPosition = slots[i];
                Cards[i].localScale = Vector3.one;
                Face(i, false);
                Picks[i].interactable = false;
            }
            step = Step.Ready;
            stepAt = 0f;
            picked = -1;
            Balance = WorldEventSamples.PeddlerRunes;
            shown = Balance;
            Runes.text = Balance.ToString("N0");
            PlayGroup.alpha = 1f;
            PlayGroup.interactable = PlayGroup.blocksRaycasts = true;
            Win.gameObject.SetActive(false);
            Lose.gameObject.SetActive(false);
        }

        /// <summary>Pays the stake and starts (the play button).</summary>
        public void Begin()
        {
            if (step != Step.Ready)
                return;
            Balance -= WorldEventSamples.BetStake;
            var random = new System.Random(System.Environment.TickCount);
            for (int i = 0; i < Swaps; i++)
            {
                int a = random.Next(Cards.Length);
                int b = (a + 1 + random.Next(Cards.Length - 1)) % Cards.Length;
                swaps[i, 0] = a;
                swaps[i, 1] = b;
            }
            Go(Step.Peek);
            PlayGroup.interactable = PlayGroup.blocksRaycasts = false;
        }

        /// <summary>Turns up card <paramref name="card"/> once the shuffle is over.</summary>
        public void Pick(int card)
        {
            if (step != Step.Pick)
                return;
            picked = card;
            if (card == WinningCard)
                Balance += WorldEventSamples.BetPrize;
            foreach (var pick in Picks)
                pick.interactable = false;
            Go(Step.Reveal);
        }

        private void Go(Step next)
        {
            step = next;
            stepAt = Clock;
        }

        protected override void Draw(float dt)
        {
            float t = Clock - stepAt;
            shown = Mathf.MoveTowards(shown, Balance, 2400f * dt);
            Runes.text = Mathf.RoundToInt(shown).ToString("N0");
            PlayGroup.alpha =
                step == Step.Ready ? 1f : Mathf.MoveTowards(PlayGroup.alpha, 0f, dt / 0.15f);
            switch (step)
            {
                case Step.Peek:
                    // 表を見せ、また伏せる。
                    for (int i = 0; i < Cards.Length; i++)
                    {
                        if (t < FlipSeconds)
                            Turn(i, t / FlipSeconds, true);
                        else if (t < FlipSeconds + PeekSeconds)
                            Turn(i, 1f, true);
                        else
                            Turn(i, (t - FlipSeconds - PeekSeconds) / FlipSeconds, false);
                    }
                    if (t >= FlipSeconds * 2f + PeekSeconds)
                        Go(Step.Shuffle);
                    break;
                case Step.Shuffle:
                    Shuffle(t);
                    break;
                case Step.Pick:
                    // 選べる間、カードは小さく上下する。
                    for (int i = 0; i < Cards.Length; i++)
                        Cards[i].anchoredPosition =
                            slots[slotOf[i]] + new Vector2(0f, 6f * Mathf.Sin(t * 4f + i * 1.3f));
                    break;
                case Step.Reveal:
                    Turn(picked, t / FlipSeconds, true);
                    for (int i = 0; i < Cards.Length; i++)
                        if (i != picked)
                            Turn(i, (t - 0.5f) / FlipSeconds, true);
                    var gift = picked == WinningCard ? Win : Lose;
                    Done = EventMotion.PopRow(gift, t - 0.25f);
                    break;
            }
        }

        // 2枚ずつ、1枚は上・1枚は下を通る弧で場所を入れ替える。
        private void Shuffle(float t)
        {
            int at = Mathf.Min(Swaps - 1, Mathf.FloorToInt(t / SwapSeconds));
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - at * SwapSeconds) / SwapSeconds));
            // ここまでに済んだ入れ替えを、伏せ直したときの並びに重ねる。
            var now = (int[])slotOf.Clone();
            for (int i = 0; i < at; i++)
                (now[swaps[i, 0]], now[swaps[i, 1]]) = (now[swaps[i, 1]], now[swaps[i, 0]]);
            int a = swaps[at, 0];
            int b = swaps[at, 1];
            for (int i = 0; i < Cards.Length; i++)
            {
                if (i != a && i != b)
                {
                    Cards[i].anchoredPosition = slots[now[i]];
                    continue;
                }
                int other = i == a ? b : a;
                float arc = (i == a ? 1f : -1f) * 70f * Mathf.Sin(Mathf.PI * k);
                Cards[i].anchoredPosition =
                    Vector2.Lerp(slots[now[i]], slots[now[other]], k) + new Vector2(0f, arc);
            }
            if (t < Swaps * SwapSeconds)
                return;
            for (int i = 0; i < Swaps; i++)
                Exchange(swaps[i, 0], swaps[i, 1]);
            for (int i = 0; i < Cards.Length; i++)
            {
                Cards[i].anchoredPosition = slots[slotOf[i]];
                Picks[i].interactable = true;
            }
            Go(Step.Pick);
        }

        private void Exchange(int a, int b) => (slotOf[a], slotOf[b]) = (slotOf[b], slotOf[a]);

        // 縦の軸で回し、真横を向いたところで面を替える。
        private void Turn(int card, float x, bool up)
        {
            x = Mathf.Clamp01(x);
            float width = Mathf.Abs(Mathf.Cos(Mathf.PI * x));
            Cards[card].localScale = new Vector3(Mathf.Max(0.001f, width), 1f, 1f);
            Face(card, x >= 0.5f ? up : !up);
        }

        private void Face(int card, bool up)
        {
            Backs[card].SetActive(!up);
            Winners[card].SetActive(up && card == WinningCard);
            Losers[card].SetActive(up && card != WinningCard);
        }
    }
}
