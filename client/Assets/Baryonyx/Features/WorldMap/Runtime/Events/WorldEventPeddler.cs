using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A peddler: the merchant greets the party and the day's goods lie in cards beside them,
    /// each with its price in runes. A tap buys a card the runes cover: the runes count down and
    /// the card dims as sold. One the runes cannot cover shakes.
    /// </summary>
    public sealed class WorldEventPeddler : WorldEventPanel
    {
        public EventBubbles Talk = new();
        public Button[] Goods = System.Array.Empty<Button>();
        public CanvasGroup[] Faces = System.Array.Empty<CanvasGroup>();
        public RectTransform[] SoldMarks = System.Array.Empty<RectTransform>();
        public TMP_Text Runes;

        private const float CountPerSecond = 2400f;

        private bool[] sold;
        private float[] soldAt;
        private float[] shookAt;
        private Vector2[] homes;
        private float shown;

        public int Balance { get; private set; }

        private void Awake()
        {
            for (int i = 0; i < Goods.Length; i++)
            {
                int good = i;
                Goods[i].onClick.AddListener(() => Buy(good));
            }
        }

        protected override void Restart()
        {
            if (homes == null)
            {
                homes = new Vector2[Goods.Length];
                for (int i = 0; i < homes.Length; i++)
                    homes[i] = ((RectTransform)Goods[i].transform).anchoredPosition;
            }
            sold = new bool[Goods.Length];
            soldAt = new float[Goods.Length];
            shookAt = new float[Goods.Length];
            for (int i = 0; i < Goods.Length; i++)
            {
                shookAt[i] = -1f;
                Faces[i].alpha = 1f;
                SoldMarks[i].gameObject.SetActive(false);
                Goods[i].interactable = true;
            }
            Balance = WorldEventSamples.PeddlerRunes;
            shown = Balance;
            Runes.text = Balance.ToString("N0");
            Talk.Start(new[] { new WorldEventLine(0, WorldEventSamples.PeddlerGreeting) }, false);
            Done = true;
        }

        /// <summary>Buys good <paramref name="good"/> when the runes cover it.</summary>
        public void Buy(int good)
        {
            if (sold[good])
                return;
            int price = WorldEventSamples.PeddlerPrices[good];
            if (Balance < price)
            {
                shookAt[good] = Clock;
                return;
            }
            Balance -= price;
            sold[good] = true;
            soldAt[good] = Clock;
            Goods[good].interactable = false;
        }

        public bool Sold(int good) => sold[good];

        public override void Advance() => Talk.Complete();

        protected override void Draw(float dt)
        {
            Talk.Tick(dt, true);
            shown = Mathf.MoveTowards(shown, Balance, CountPerSecond * dt);
            Runes.text = Mathf.RoundToInt(shown).ToString("N0");
            for (int i = 0; i < Goods.Length; i++)
            {
                var rect = (RectTransform)Goods[i].transform;
                float shake = shookAt[i] < 0f ? 1f : Mathf.Clamp01((Clock - shookAt[i]) / 0.3f);
                rect.anchoredPosition =
                    homes[i]
                    + new Vector2(Mathf.Sin(shake * Mathf.PI * 6f) * 12f * (1f - shake), 0f);
                if (!sold[i])
                    continue;
                float t = Mathf.Clamp01((Clock - soldAt[i]) / 0.25f);
                Faces[i].alpha = Mathf.Lerp(1f, 0.4f, t);
                SoldMarks[i].gameObject.SetActive(true);
                SoldMarks[i].localScale = Vector3.one * EventMotion.EaseOutBack(t, 2f);
            }
        }
    }
}
