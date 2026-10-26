using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Temple
{
    /// <summary>One result's card: the art, the name, the stars and the kind, and the quality's letter.</summary>
    [Serializable]
    public sealed class TempleSummonCard
    {
        public GlassCard Card;
        public RawImage Art;
        public TMP_Text Name;
        public TMP_Text Stars;
        public TMP_Text Quality;
    }

    /// <summary>
    /// The window over the temple that shows what a summoning gave: one card, or ten in two rows.
    /// A character, ☆4 or more, and quality S light the card's gold rim. "Again" summons the same
    /// number once more; ×, the dark veil, "back" and the device back key close the window.
    /// </summary>
    public sealed class TempleSummonResults : MonoBehaviour, IGuideBackHandler, IEditModePreview
    {
        public LibraryData Data;

        // 絵を置く正方形の一辺（設計座標）。ドット絵は、収まるいちばん大きい整数倍で出す。
        public float ArtBox = 128f;

        public GameObject Window;
        public Button Veil;
        public Button Close;
        public Button Again;
        public TMP_Text Title;
        public TempleSummonCard[] Cards = Array.Empty<TempleSummonCard>();

        private IReadOnlyList<SummonResult> shown = Array.Empty<SummonResult>();

        public event Action AgainPressed;

        public bool IsOpen => Window != null && Window.activeSelf;
        public IReadOnlyList<SummonResult> Shown => shown;

        private void Awake()
        {
            if (Veil != null)
                Veil.onClick.AddListener(Hide);
            if (Close != null)
                Close.onClick.AddListener(Hide);
            if (Again != null)
                Again.onClick.AddListener(() => AgainPressed?.Invoke());
        }

        /// <summary>The mock pool: the library's characters, and the adventure's items and equipment.</summary>
        public static IReadOnlyList<SummonEntry> PoolOf(LibraryData data) =>
            TempleSummon.MockPool(
                data != null
                    ? data
                        .Entries.Where(entry => entry.Kind == LibraryKind.Character)
                        .Select(entry => entry.Id)
                    : null
            );

        public void Show(IReadOnlyList<SummonResult> results)
        {
            shown = results ?? Array.Empty<SummonResult>();
            Set(Title, shown.Count >= TempleSummon.TenCount ? "10回召喚" : "1回召喚");
            for (int i = 0; i < Cards.Length; i++)
            {
                var card = Cards[i];
                bool on = i < shown.Count;
                if (card.Card != null)
                    card.Card.gameObject.SetActive(on);
                if (on)
                    Paint(card, shown[i], Data, ArtBox);
            }
            if (Window != null)
                Window.SetActive(true);
        }

        public void Hide()
        {
            if (Window != null)
                Window.SetActive(false);
        }

        public bool HandleBack()
        {
            if (!IsOpen)
                return false;
            Hide();
            return true;
        }

        /// <summary>Draws one result on a card: its art, name, stars and kind, and quality.</summary>
        public static void Paint(
            TempleSummonCard card,
            SummonResult result,
            LibraryData data,
            float artBox
        )
        {
            var entry =
                data != null
                    ? Array.Find(
                        data.Entries,
                        e => e.Kind == result.Entry.Kind && e.Id == result.Entry.Id
                    )
                    : null;
            Set(card.Name, entry != null ? entry.Name : result.Entry.Id);
            Set(card.Stars, StarsLine(result, entry));
            Set(card.Quality, result.Quality is { } quality ? QualityPalette.Painted(quality) : "");
            if (card.Art != null)
                PaintArt(card.Art, entry, artBox);
            if (card.Card != null)
                card.Card.Selected = result.Highlight;
        }

        // ☆と分類（例：「★★★ 武器」）。キャラは☆を持たないため、加わったか、絆のしるしかかけらを得たことを書く。
        private static string StarsLine(SummonResult result, LibraryEntryData entry)
        {
            var summoned = result.Entry;
            string kind = TempleSummon
                .Categories.FirstOrDefault(category => category.Kind == summoned.Kind)
                .Name;
            if (summoned.Kind == LibraryKind.Character && result.Bond != null)
                return result.Bond.IsNew ? "新しい仲間"
                    : result.Bond.Shards > 0 ? $"かけら+{result.Bond.Shards}"
                    : "突破のしるし+1";
            if (summoned.Stars <= 0)
                return kind;
            string stars = new('★', summoned.Stars);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(UiPalette.Gold)}>{stars}</color> {kind}";
        }

        // 絵は、縦横の大きいほうが枠に収まるいちばん大きい整数倍で出す（ライブラリと同じ）。
        private static void PaintArt(RawImage image, LibraryEntryData entry, float artBox)
        {
            image.texture = entry?.Art;
            image.enabled = entry?.Art != null;
            if (entry == null || entry.Art == null)
                return;
            image.uvRect = entry.ArtUv;
            image.color = entry.ArtTint;
            var dots = entry.ArtDots;
            float dot = Mathf.Max(
                1f,
                Mathf.Floor(artBox / Mathf.Max(1f, Mathf.Max(dots.x, dots.y)))
            );
            image.rectTransform.sizeDelta = dots * dot;
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }

        // 停止中の表示の切り替え：1回と10回の結果。金の縁も確かめられるよう、キャラが出る最初の種で引く。
        public IReadOnlyList<string> PreviewStates => new[] { "1回召喚の結果", "10回召喚の結果" };

        public void ShowPreviewState(int index)
        {
            var pool = PoolOf(Data);
            int count = index == 0 ? 1 : TempleSummon.TenCount;
            IReadOnlyList<SummonResult> results = Array.Empty<SummonResult>();
            for (int seed = 1; seed <= 1000; seed++)
            {
                results = TempleSummon.Roll(pool, count, new System.Random(seed).NextDouble);
                if (results.Any(result => result.Entry.Kind == LibraryKind.Character))
                    break;
            }
            Show(results);
        }
    }
}
