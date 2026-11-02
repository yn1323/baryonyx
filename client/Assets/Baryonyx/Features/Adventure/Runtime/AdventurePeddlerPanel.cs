using System;
using System.Collections.Generic;
using Baryonyx.Inventory;
using Baryonyx.UI;
using Baryonyx.WorldMap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The merchant's room, in the event window like the map's peddler
    /// (doc/features/screens/adventure.md): the merchant greets the party and the wares lie in
    /// cards beside them, each with its art, stars, quality and price in runes, and the runes held
    /// at the top right. A tap on a ware asks to buy it (<see cref="Pressed"/>); the flow buys it
    /// on the server and marks it sold, or shakes it when the runes cannot cover it. The × leaves.
    /// </summary>
    public sealed class AdventurePeddlerPanel : WorldEventPanel
    {
        public EventBubbles Talk = new();
        public Button[] Goods = Array.Empty<Button>();
        public CanvasGroup[] Faces = Array.Empty<CanvasGroup>();
        public Image[] Icons = Array.Empty<Image>();
        public TMP_Text[] Names = Array.Empty<TMP_Text>();
        public TMP_Text[] Kinds = Array.Empty<TMP_Text>();
        public TMP_Text[] Grades = Array.Empty<TMP_Text>();
        public TMP_Text[] Prices = Array.Empty<TMP_Text>();
        public RectTransform[] SoldMarks = Array.Empty<RectTransform>();
        public TMP_Text Runes;

        public const string Greeting = "旅の品をそろえているよ。";
        private const float CountPerSecond = 2400f;

        private readonly List<AdventureWare> wares = new();
        private bool[] sold = Array.Empty<bool>();
        private float[] soldAt = Array.Empty<float>();
        private float[] shookAt = Array.Empty<float>();
        private Vector2[] homes;
        private long balance;
        private float shown;
        private bool wired;

        /// <summary>A ware's card was tapped: the ware, to buy if the runes cover it.</summary>
        public event Action<AdventureWare> Pressed;

        public long Balance => balance;
        public IReadOnlyList<AdventureWare> Wares => wares;

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            for (int i = 0; i < Goods.Length; i++)
            {
                int good = i;
                Goods[i].onClick.AddListener(() => Press(good));
            }
        }

        /// <summary>Lays out <paramref name="offered"/> and the runes held, before the window opens.</summary>
        public void Use(IReadOnlyList<AdventureWare> offered, long runes, ItemArt art)
        {
            Wire();
            wares.Clear();
            if (offered != null)
                wares.AddRange(offered);
            balance = runes;
            shown = runes;
            sold = new bool[Goods.Length];
            soldAt = new float[Goods.Length];
            shookAt = new float[Goods.Length];
            for (int i = 0; i < Goods.Length; i++)
            {
                bool used = i < wares.Count;
                Goods[i].gameObject.SetActive(used);
                shookAt[i] = -1f;
                if (!used)
                    continue;
                Paint(i, wares[i], art);
                sold[i] = wares[i].Sold;
                soldAt[i] = -1f;
                Faces[i].alpha = sold[i] ? SoldAlpha : 1f;
                SoldMarks[i].gameObject.SetActive(sold[i]);
                SoldMarks[i].localScale = Vector3.one;
                Goods[i].interactable = !sold[i];
            }
            if (Runes != null)
                Runes.text = balance.ToString("N0");
        }

        private const float SoldAlpha = 0.4f;

        private void Paint(int i, AdventureWare ware, ItemArt art)
        {
            var item = AdventureDropItems.From(ware.AsDrop(""), art);
            if (i < Icons.Length && Icons[i] != null)
            {
                Icons[i].sprite = item?.Icon;
                Icons[i].enabled = item?.Icon != null;
            }
            Set(Names, i, item?.Name ?? "");
            Set(Kinds, i, item?.Kind ?? "");
            if (i < Grades.Length && Grades[i] != null)
            {
                Grades[i].text = ware.Quality is { } quality ? quality.ToString() : "";
                if (ware.Quality is { } graded)
                    Grades[i].color = QualityPalette.Of(graded);
            }
            Set(Prices, i, ware.Price.ToString("N0"));
        }

        private static void Set(TMP_Text[] texts, int i, string text)
        {
            if (i < texts.Length && texts[i] != null)
                texts[i].text = text;
        }

        private void Press(int good)
        {
            if (good >= wares.Count || sold[good])
                return;
            if (balance < wares[good].Price)
            {
                Shake(good);
                return;
            }
            Pressed?.Invoke(wares[good]);
        }

        /// <summary>The ware at <paramref name="index"/> (its place in the list) was bought: the runes count down and it dims as sold.</summary>
        public void MarkSold(int index, long runes)
        {
            balance = runes;
            int good = wares.FindIndex(ware => ware.Index == index);
            if (good < 0 || good >= Goods.Length)
                return;
            sold[good] = true;
            soldAt[good] = Clock;
            Goods[good].interactable = false;
        }

        /// <summary>Shakes the card of a ware the runes cannot cover.</summary>
        public void Shake(int good)
        {
            if (good >= 0 && good < shookAt.Length)
                shookAt[good] = Clock;
        }

        protected override void Restart()
        {
            if (homes == null)
            {
                homes = new Vector2[Goods.Length];
                for (int i = 0; i < homes.Length; i++)
                    homes[i] = ((RectTransform)Goods[i].transform).anchoredPosition;
            }
            Talk.Start(new[] { new WorldEventLine(0, Greeting) }, false);
            Done = true;
        }

        public override void Advance() => Talk.Complete();

        protected override void Draw(float dt)
        {
            Talk.Tick(dt, true);
            shown = Mathf.MoveTowards(shown, balance, CountPerSecond * dt);
            if (Runes != null)
                Runes.text = Mathf.RoundToInt(shown).ToString("N0");
            for (int i = 0; i < Goods.Length && i < wares.Count; i++)
            {
                var rect = (RectTransform)Goods[i].transform;
                float shake = shookAt[i] < 0f ? 1f : Mathf.Clamp01((Clock - shookAt[i]) / 0.3f);
                rect.anchoredPosition =
                    homes[i]
                    + new Vector2(Mathf.Sin(shake * Mathf.PI * 6f) * 12f * (1f - shake), 0f);
                if (!sold[i] || soldAt[i] < 0f)
                    continue;
                float t = Mathf.Clamp01((Clock - soldAt[i]) / 0.25f);
                Faces[i].alpha = Mathf.Lerp(1f, SoldAlpha, t);
                SoldMarks[i].gameObject.SetActive(true);
                SoldMarks[i].localScale = Vector3.one * EventMotion.EaseOutBack(t, 2f);
            }
        }
    }
}
