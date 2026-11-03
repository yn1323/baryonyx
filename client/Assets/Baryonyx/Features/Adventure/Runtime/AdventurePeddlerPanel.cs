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
    /// at the top right. A tap on a ware picks it with the gold rim and lays its detail (the
    /// shop's face) over the merchant, and "買う" at the bottom right
    /// asks to buy it (<see cref="Pressed"/>), as the shop buys: the flow buys it on the server
    /// and marks it sold. When the runes cannot cover the ware picked, "買う" cannot be pressed
    /// and the runes short show beside it (doc/rules/ui-design.md#操作の決まり). The × leaves.
    /// </summary>
    public sealed class AdventurePeddlerPanel : WorldEventPanel
    {
        public EventBubbles Talk = new();
        public Button[] Goods = Array.Empty<Button>();
        public CanvasGroup[] Faces = Array.Empty<CanvasGroup>();
        public Image[] Icons = Array.Empty<Image>();
        public TMP_Text[] Names = Array.Empty<TMP_Text>();
        // 名前の下の☆。分類の文字（「武器」など）は書かない（doc/rules/ui-design.md の装備の見せ方）。
        public TMP_Text[] Stars = Array.Empty<TMP_Text>();
        public TMP_Text[] Grades = Array.Empty<TMP_Text>();
        public TMP_Text[] Prices = Array.Empty<TMP_Text>();
        public RectTransform[] SoldMarks = Array.Empty<RectTransform>();
        public TMP_Text Runes;

        [Tooltip("選んだ品の金の縁（Goodsと同じ順）。")]
        public GameObject[] Picks = Array.Empty<GameObject>();

        [Tooltip("選んでいる品の詳しい面。行商人と吹き出しの上に重ねる（商会の詳しい面と同じ中身）。")]
        public GameObject DetailRoot;
        public ItemDetail Detail;

        [Tooltip("詳しい面を出す間は隠す、吹き出し。")]
        public CanvasGroup[] TalkGroups = Array.Empty<CanvasGroup>();

        [Tooltip("詳しい面の中身を引く図鑑。")]
        public Library.LibraryData DropLibrary;

        [Tooltip("選んだ品を買うボタンと、その左の値段・足りないルーン。")]
        public Button Buy;
        public GameObject Cost;
        public TMP_Text CostLabel;
        public TMP_Text Short;

        public const string Greeting = "旅の品をそろえているよ。";
        private const float CountPerSecond = 2400f;

        private readonly List<AdventureWare> wares = new();
        private bool[] sold = Array.Empty<bool>();
        private float[] soldAt = Array.Empty<float>();
        private int picked = -1;
        private bool buying;
        private long balance;
        private float shown;
        private bool wired;

        /// <summary>"買う" was pressed on the ware picked, which the runes cover.</summary>
        public event Action<AdventureWare> Pressed;

        public long Balance => balance;

        /// <summary>The ware picked (its place in <see cref="Goods"/>), or -1.</summary>
        public int Picked => picked;
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
                Goods[i].onClick.AddListener(() => Pick(good));
            }
            if (Buy != null)
                Buy.onClick.AddListener(PressBuy);
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
            picked = -1;
            buying = false;
            for (int i = 0; i < Goods.Length; i++)
            {
                bool used = i < wares.Count;
                Goods[i].gameObject.SetActive(used);
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
            Refresh();
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
            Set(Stars, i, ItemText.Stars(ware.AsDrop("").Stars));
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

        /// <summary>Picks the ware at <paramref name="good"/> (its place in <see cref="Goods"/>), as a tap on it does.</summary>
        public void Pick(int good)
        {
            if (good < 0 || good >= wares.Count || sold[good])
                return;
            picked = good;
            Refresh();
        }

        private void PressBuy()
        {
            if (buying || picked < 0 || sold[picked] || balance < wares[picked].Price)
                return;
            buying = true;
            Refresh();
            Pressed?.Invoke(wares[picked]);
        }

        /// <summary>The ware at <paramref name="index"/> (its place in the list) was bought: the runes count down and it dims as sold.</summary>
        public void MarkSold(int index, long runes)
        {
            balance = runes;
            buying = false;
            int good = wares.FindIndex(ware => ware.Index == index);
            if (good >= 0 && good < Goods.Length)
            {
                sold[good] = true;
                soldAt[good] = Clock;
                Goods[good].interactable = false;
                if (picked == good)
                    picked = -1;
            }
            Refresh();
        }

        /// <summary>The buying failed: "買う" takes taps again, with the ware still picked.</summary>
        public void Resume()
        {
            buying = false;
            Refresh();
        }

        // 選んだ品の金の縁と、「買う」・値段・足りないルーン。
        private void Refresh()
        {
            for (int i = 0; i < Picks.Length; i++)
                if (Picks[i] != null)
                    Picks[i].SetActive(i == picked);
            bool chosen = picked >= 0 && picked < wares.Count && !sold[picked];
            long price = chosen ? wares[picked].Price : 0;
            if (Buy != null)
                Buy.interactable = chosen && !buying && balance >= price;
            if (Cost != null)
                Cost.SetActive(chosen);
            if (CostLabel != null)
                CostLabel.text = price.ToString("N0");
            if (Short != null)
                Short.text = chosen && balance < price ? UiText.RunesShort(price - balance) : "";
            // 選んでいる間は、行商人の上に詳しい面を出す（2026-11-03、ユーザーが案Bを選んだ）。
            var face = chosen ? AdventureDropFaces.Of(DropLibrary, wares[picked].AsDrop("")) : null;
            if (Detail != null)
                Detail.Show(face);
            if (DetailRoot != null)
                DetailRoot.SetActive(face != null);
            foreach (var talk in TalkGroups)
                if (talk != null)
                    talk.alpha = face != null ? 0f : 1f;
        }

        protected override void Restart()
        {
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
