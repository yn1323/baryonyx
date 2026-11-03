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
        public ItemTile[] Cards = Array.Empty<ItemTile>();

        // 札を押すと出す、詳しい面と同じ中身の小さな窓。
        public ItemDetailWindow DetailWindow;

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
            for (int i = 0; i < Cards.Length; i++)
            {
                int index = i;
                if (Cards[i] != null && Cards[i].Card != null)
                    Cards[i].Card.Clicked += () => ShowDetail(index);
            }
        }

        /// <summary>Opens the small detail window on the result at <paramref name="index"/>.</summary>
        public void ShowDetail(int index)
        {
            if (DetailWindow == null || index < 0 || index >= shown.Count)
                return;
            DetailWindow.Show(FaceOf(shown[index], Data));
        }

        /// <summary>The detail's face of a result: equipment with its quality and multiplier.</summary>
        public static ItemFace FaceOf(SummonResult result, LibraryData data)
        {
            var entry = EntryOf(result, data);
            return result.Quality is { } quality
                ? LibraryFaces.Of(entry, quality, result.Multiplier)
                : LibraryFaces.Of(entry);
        }

        private static LibraryEntryData EntryOf(SummonResult result, LibraryData data) =>
            data != null
                ? Array.Find(
                    data.Entries,
                    e => e.Kind == result.Entry.Kind && e.Id == result.Entry.Id
                )
                : null;

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
            if (DetailWindow != null)
                DetailWindow.Hide();
            if (Window != null)
                Window.SetActive(false);
        }

        // 詳しい窓を開いていれば、それだけを閉じる。
        public bool HandleBack()
        {
            if (DetailWindow != null && DetailWindow.HandleBack())
                return true;
            if (!IsOpen)
                return false;
            Hide();
            return true;
        }

        /// <summary>Draws one result on a card: its art, name, stars and quality.</summary>
        public static void Paint(ItemTile card, SummonResult result, LibraryData data, float artBox)
        {
            var entry = EntryOf(result, data);
            Set(card.Name, entry != null ? entry.Name : result.Entry.Id);
            Set(card.Stars, StarsLine(result, entry));
            Set(card.Quality, result.Quality is { } quality ? QualityPalette.Painted(quality) : "");
            if (card.Art != null)
                PaintArt(card.Art, entry, artBox);
            if (card.Card != null)
                card.Card.Selected = result.Highlight;
        }

        // ☆（札には分類を書かない）。キャラは☆を持たないため、加わったか、絆のしるしかかけらを得たことを書く。
        private static string StarsLine(SummonResult result, LibraryEntryData entry)
        {
            var summoned = result.Entry;
            if (summoned.Kind == LibraryKind.Character && result.Bond != null)
                return result.Bond.IsNew ? "新しい仲間"
                    : result.Bond.Shards > 0 ? $"かけら+{result.Bond.Shards}"
                    : "突破のしるし+1";
            return ItemText.GoldStars(summoned.Stars);
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

        // 停止中の表示の切り替え：1回と10回の結果。金の縁も確かめられるよう、光る札が出る最初の種で引く。
        public IReadOnlyList<string> PreviewStates => new[] { "1回召喚の結果", "10回召喚の結果" };

        public void ShowPreviewState(int index)
        {
            var pool = PoolOf(Data);
            int count = index == 0 ? 1 : TempleSummon.TenCount;
            IReadOnlyList<SummonResult> results = Array.Empty<SummonResult>();
            for (int seed = 1; seed <= 1000; seed++)
            {
                results = TempleSummon.Roll(pool, count, new System.Random(seed).NextDouble);
                if (results.Any(result => result.Highlight))
                    break;
            }
            Show(results);
        }
    }
}
