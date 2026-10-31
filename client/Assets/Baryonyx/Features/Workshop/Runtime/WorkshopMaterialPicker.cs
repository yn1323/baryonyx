using System;
using System.Collections.Generic;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The craft's picker of which held piece a special material is (doc/features/screens/shop.md#使う物を選ぶ画面):
    /// the screen dimmed, the pieces of the kind as cards four to a row, scrolling the whole
    /// height when there are many, and the battle's round check at the bottom right. Pieces in
    /// use show "E", shaded, and cannot be chosen. The check takes the chosen piece; tapping the
    /// dim closes without changing it.
    /// </summary>
    public sealed class WorkshopMaterialPicker : MonoBehaviour
    {
        public float ArtBox = 96f;
        public Button Dim;
        public Button Confirm;
        public ScrollRect List;
        public RectTransform Content;
        public WorkshopGoodRow Template;
        public List<WorkshopGoodRow> Rows = new();

        private readonly HashSet<Component> bound = new();
        private IReadOnlyList<WorkshopOwned> pieces = Array.Empty<WorkshopOwned>();
        private Action<WorkshopOwned> chosen;
        private int selected = -1;

        public bool IsOpen => gameObject.activeSelf;
        public IReadOnlyList<WorkshopOwned> Pieces => pieces;
        public int Selected => selected;

        /// <summary>
        /// Shows <paramref name="all"/> with <paramref name="current"/> chosen, and calls
        /// <paramref name="onChosen"/> with the chosen piece when the check is pressed.
        /// </summary>
        public void Open(
            IReadOnlyList<WorkshopOwned> all,
            WorkshopOwned current,
            LibraryEntryData entry,
            Action<WorkshopOwned> onChosen
        )
        {
            Bind();
            pieces = all ?? Array.Empty<WorkshopOwned>();
            chosen = onChosen;
            selected = -1;
            for (int i = 0; i < pieces.Count; i++)
                if (current != null && pieces[i].RowId == current.RowId)
                    selected = i;
            EnsureRows(pieces.Count);
            for (int i = 0; i < Rows.Count; i++)
            {
                bool visible = i < pieces.Count;
                Rows[i].gameObject.SetActive(visible);
                if (visible)
                    Paint(Rows[i], pieces[i], entry, ArtBox);
            }
            gameObject.SetActive(true);
            PaintSelection();
            if (List != null)
                List.verticalNormalizedPosition = 1f;
        }

        public void Choose(int index)
        {
            if (index < 0 || index >= pieces.Count || pieces[index].User != null)
                return;
            selected = index;
            PaintSelection();
        }

        /// <summary>Takes the chosen piece and closes.</summary>
        public void Take()
        {
            if (selected < 0 || selected >= pieces.Count)
                return;
            var piece = pieces[selected];
            var take = chosen;
            Close();
            take?.Invoke(piece);
        }

        public void Close()
        {
            chosen = null;
            gameObject.SetActive(false);
        }

        /// <summary>Writes a piece's card: the quality's letter, the art, the multiplier and the effect at it.</summary>
        public static void Paint(
            WorkshopGoodRow row,
            WorkshopOwned piece,
            LibraryEntryData entry,
            float artBox
        )
        {
            LibraryCatalogView.PaintArt(row.Art, entry, true, artBox);
            if (row.Quality != null)
            {
                row.Quality.text = Inventory.Quality.Letter(piece.Quality);
                row.Quality.color = QualityPalette.Of(piece.Quality);
            }
            Set(row.Price, Inventory.Quality.Times(piece.Multiplier));
            Set(row.Detail, WorkshopSellView.GearEffectOf(piece) ?? "");
            // 使っている物は影と「E」で示し、押しても選ばない（Choose）。札を薄くすると透けるため、押せるままにする。
            if (row.InUse != null)
                row.InUse.SetActive(piece.User != null);
        }

        private void PaintSelection()
        {
            for (int i = 0; i < pieces.Count && i < Rows.Count; i++)
                if (Rows[i].Card != null)
                    Rows[i].Card.Selected = i == selected;
            if (Confirm != null)
                Confirm.interactable = selected >= 0;
        }

        private void Bind()
        {
            if (Dim != null && bound.Add(Dim))
                Dim.onClick.AddListener(Close);
            if (Confirm != null && bound.Add(Confirm))
                Confirm.onClick.AddListener(Take);
        }

        private void EnsureRows(int count)
        {
            Rows.RemoveAll(row => row == null);
            while (Rows.Count < count && Template != null && Content != null)
                Rows.Add(Instantiate(Template, Content));
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                row.name = "Piece" + i;
                if (row.Card == null || !bound.Add(row))
                    continue;
                int index = i;
                row.Card.Clicked += () => Choose(index);
            }
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
