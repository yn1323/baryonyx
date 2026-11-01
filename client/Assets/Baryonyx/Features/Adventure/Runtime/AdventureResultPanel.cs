using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The end of an adventure (doc/features/screens/adventure.md, 冒険の結果): how it ended and
    /// the place in a line at the top, the party standing on a band of light below it (cheering
    /// in turn when the adventure was cleared), and what was brought back as cards of art, quality,
    /// stars, count and name, the best first, two rows at a time. "ホームへ" goes Home. The device's
    /// back key does nothing. AdventureResultAssets builds it into the battle's overlay and the
    /// exploration screen.
    /// </summary>
    public sealed class AdventureResultPanel : MonoBehaviour
    {
        // 開くとき：板が下から浮き、仲間が少しずつずれて立ち、持ち帰った物が順に現れる。
        public const float OpenSeconds = 0.24f;
        public const float RiseFrom = 28f;
        public const float MemberStagger = 0.07f;
        public const float CellsFrom = 0.3f;
        public const float CellStagger = 0.04f;
        public const float CellSeconds = 0.2f;

        // クリアしたときの仲間の喜び：順に跳ねる。
        public const float CheerFrom = 0.35f;
        public const float CheerPeriod = 1.5f;
        public const float CheerStagger = 0.16f;
        public const float HopSeconds = 0.34f;
        public const float HopHeight = 24f;

        public RectTransform Panel;
        public CanvasGroup Window;

        public TMP_Text Title;
        public TMP_Text Place;

        // 見出しの後ろの光の筋と、まわりのきらめき（クリアしたときだけ）。
        public Graphic[] Rays = Array.Empty<Graphic>();
        public CanvasGroup[] Twinkles = Array.Empty<CanvasGroup>();

        // 仲間のドット絵と、足元の光の帯。
        public RawImage[] Members = Array.Empty<RawImage>();
        public Graphic Floor;
        public float Dot = 3f;

        // 持ち帰った物の札を並べる中身と、複製する札（隠しておく）。物がなければ Empty を出す。
        public ScrollRect Scroll;
        public RectTransform Cells;
        public AdventureResultCell CellTemplate;
        public TMP_Text Empty;

        public Button Home;

        public Color FloorCleared = new(1f, 0.86f, 0.5f, 0.55f);
        public Color FloorEnded = new(0.6f, 0.7f, 1f, 0.22f);

        private readonly List<AdventureResultCell> cells = new();
        private Action home;
        private bool cleared;
        private float openedAt;
        private Vector2 windowHome;
        private Vector2[] memberHomes = Array.Empty<Vector2>();
        private bool wired;

        public bool IsOpen => Panel != null && Panel.gameObject.activeSelf;

        public string TitleText => Title != null ? Title.text : "";
        public string PlaceText => Place != null ? Place.text : "";

        /// <summary>The names on the cards shown, in order.</summary>
        public IReadOnlyList<string> Names =>
            cells
                .Where(cell => cell.gameObject.activeSelf)
                .Select(cell => cell.Name != null ? cell.Name.text : "")
                .ToArray();


        private void Awake() => Wire();

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            if (Home != null)
                Home.onClick.AddListener(GoHome);
            if (Window != null)
                windowHome = ((RectTransform)Window.transform).anchoredPosition;
            memberHomes = Members
                .Select(member => member != null ? member.rectTransform.anchoredPosition : Vector2.zero)
                .ToArray();
            if (CellTemplate != null)
                CellTemplate.gameObject.SetActive(false);
        }

        /// <summary>
        /// Opens with <paramref name="result"/>, the party (slot order, null for an empty slot)
        /// and the art of the things brought back. <paramref name="onHome"/> runs once on
        /// "ホームへ"; <see cref="Resume"/> lets it be pressed again when Home could not open.
        /// </summary>
        public void Open(
            AdventureResult result,
            IReadOnlyList<BattlePartyMember> party,
            ItemArt art,
            Action onHome
        )
        {
            if (result == null)
                return;
            Wire();
            home = onHome;
            cleared = result.Status == AdventureEndStatus.Cleared;
            if (Title != null)
                Title.text = AdventureTexts.ResultTitle(result.Status);
            if (Place != null)
                Place.text = AdventureTexts.ResultPlace(result);
            foreach (var ray in Rays)
                if (ray != null)
                    ray.gameObject.SetActive(cleared);
            foreach (var twinkle in Twinkles)
                if (twinkle != null)
                    twinkle.gameObject.SetActive(cleared);
            if (Floor != null)
                Floor.color = cleared ? FloorCleared : FloorEnded;
            PaintParty(party ?? Array.Empty<BattlePartyMember>());
            ShowThings(result.Drops, art);
            if (Home != null)
                Home.interactable = true;
            // 停止中の表示は、演出を終えた形で止める。
            openedAt = Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup - 10f;
            if (Panel != null)
                Panel.gameObject.SetActive(true);
            Animate();
        }

        public void Close()
        {
            home = null;
            if (Panel != null)
                Panel.gameObject.SetActive(false);
        }

        /// <summary>Lets "ホームへ" be pressed again (Home could not open).</summary>
        public void Resume()
        {
            if (Home != null)
                Home.interactable = true;
        }

        /// <summary>The device's back key: the result stays until "ホームへ". False when closed.</summary>
        public bool HandleBack() => IsOpen;

        /// <summary>Presses "ホームへ" (tests).</summary>
        public void GoHome()
        {
            if (!IsOpen || (Home != null && !Home.interactable))
                return;
            if (Home != null)
                Home.interactable = false;
            home?.Invoke();
        }

        // 停止中の表示と展示室の見本の名前（絵は組み立てで置いた4人）。
        private static readonly string[] SampleNames = { "トーマ", "ルカ", "アリア", "ミナ" };

        /// <summary>
        /// Opens with a sample result ending as <paramref name="status"/>, and the four figures
        /// the builder put on the band as the party (the screens' previews and the showcase).
        /// </summary>
        public void ShowSample(AdventureEndStatus status, ItemArt art)
        {
            var party = new BattlePartyMember[Members.Length];
            for (int i = 0; i < party.Length; i++)
                party[i] = new BattlePartyMember
                {
                    Name = i < SampleNames.Length ? SampleNames[i] : "",
                    Art = Members[i] != null ? Members[i].texture : null,
                };
            Open(AdventureDialogSamples.Result(status), party, art, null);
        }

        /// <summary>
        /// The things brought back as one card a kind and quality, with how many: the higher
        /// quality first, then the more stars, then by category.
        /// </summary>
        public static IReadOnlyList<(AdventureDrop Drop, int Count)> Kinds(
            IReadOnlyList<AdventureDrop> drops
        ) =>
            (drops ?? Array.Empty<AdventureDrop>())
                .Where(drop => drop != null)
                .GroupBy(drop => (drop.Category, drop.Id, drop.Quality))
                .Select(group => (Drop: group.First(), Count: group.Count()))
                .OrderByDescending(kind => kind.Drop.Quality.HasValue)
                .ThenByDescending(kind => kind.Drop.Quality ?? ItemQuality.C)
                .ThenByDescending(kind => kind.Drop.Stars)
                .ThenBy(kind => kind.Drop.Category)
                .ToArray();

        private void Update()
        {
            if (IsOpen)
                Animate();
        }

        private void PaintParty(IReadOnlyList<BattlePartyMember> party)
        {
            var present = party.Where(member => member != null && member.Art != null).ToArray();
            for (int i = 0; i < Members.Length; i++)
            {
                var image = Members[i];
                if (image == null)
                    continue;
                bool shown = i < present.Length;
                image.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                image.texture = present[i].Art;
                image.color = present[i].Tint;
                image.rectTransform.sizeDelta =
                    new Vector2(present[i].Art.width, present[i].Art.height) * Dot;
            }
        }

        private void ShowThings(IReadOnlyList<AdventureDrop> drops, ItemArt art)
        {
            var kinds = Kinds(drops);
            if (Empty != null)
                Empty.gameObject.SetActive(kinds.Count == 0);
            if (CellTemplate == null || Cells == null)
                return;
            while (cells.Count < kinds.Count)
            {
                var cell = Instantiate(CellTemplate, Cells);
                cell.name = $"Cell{cells.Count + 1}";
                cells.Add(cell);
            }
            for (int i = 0; i < cells.Count; i++)
            {
                bool used = i < kinds.Count;
                cells[i].gameObject.SetActive(used);
                if (used)
                    Paint(cells[i], kinds[i].Drop, kinds[i].Count, art);
            }
            if (Scroll != null)
                Scroll.verticalNormalizedPosition = 1f;
        }

        private static void Paint(AdventureResultCell cell, AdventureDrop drop, int count, ItemArt art)
        {
            var icon = AdventureTexts.DropIcon(drop, art);
            if (cell.Art != null)
            {
                cell.Art.sprite = icon;
                cell.Art.enabled = icon != null;
            }
            if (cell.Quality != null)
            {
                cell.Quality.text = drop.Quality is { } quality ? Quality.Letter(quality) : "";
                cell.Quality.color = QualityPalette.Of(drop.Quality ?? ItemQuality.C);
            }
            if (cell.Stars != null)
                cell.Stars.text = EquipmentCatalog.Stars(drop.Stars);
            if (cell.Count != null)
                cell.Count.text = $"×{count}";
            if (cell.Name != null)
                cell.Name.text = AdventureTexts.DropName(drop);
        }

        private void Animate()
        {
            float t = (Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup) - openedAt;

            float open = Ease(t / OpenSeconds);
            if (Window != null)
            {
                Window.alpha = open;
                ((RectTransform)Window.transform).anchoredPosition =
                    windowHome + Vector2.down * RiseFrom * (1f - open);
            }

            for (int i = 0; i < Members.Length; i++)
            {
                if (Members[i] == null || i >= memberHomes.Length)
                    continue;
                float rise = Ease((t - OpenSeconds * 0.5f - i * MemberStagger) / OpenSeconds);
                float hop = cleared ? Hop(t - CheerFrom - i * CheerStagger) : 0f;
                Members[i].rectTransform.anchoredPosition =
                    memberHomes[i] + Vector2.down * RiseFrom * (1f - rise) + Vector2.up * hop;
                var color = Members[i].color;
                Members[i].color = new Color(color.r, color.g, color.b, rise);
            }

            for (int i = 0; i < cells.Count; i++)
            {
                if (!cells[i].gameObject.activeSelf || cells[i].Group == null)
                    continue;
                float shown = Ease((t - CellsFrom - i * CellStagger) / CellSeconds);
                cells[i].Group.alpha = shown;
                cells[i].transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, shown);
            }

            if (!cleared)
                return;
            // 光の筋はゆっくり息づき、きらめきは1つずつずれて瞬く。
            for (int i = 0; i < Rays.Length; i++)
                if (Rays[i] != null)
                    Rays[i].canvasRenderer.SetAlpha(open * (0.75f + 0.25f * Mathf.Sin(t * 1.4f + i * 1.3f)));
            for (int i = 0; i < Twinkles.Length; i++)
            {
                if (Twinkles[i] == null)
                    continue;
                float wave = 0.5f + 0.5f * Mathf.Sin(t * 2.6f + i * 2.1f);
                Twinkles[i].alpha = open * wave * wave;
                Twinkles[i].transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, wave);
            }
        }

        // 跳ねる高さ：CheerPeriod ごとに1回、HopSeconds の間だけ弧を描く。
        private static float Hop(float t)
        {
            if (t < 0f)
                return 0f;
            float phase = t % CheerPeriod;
            return phase < HopSeconds ? Mathf.Sin(phase / HopSeconds * Mathf.PI) * HopHeight : 0f;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }
    }
}
