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
        public ItemTile Template;
        public List<ItemTile> Rows = new();

        private readonly HashSet<Component> bound = new();
        // 開いている間は隠す、後ろの合成の決めるボタンの段（ボタン・値段・足りない物の文字）。
        // 1つの画面に決めるボタンの形を2つ見せない（右下の丸いチェックだけにする）。
        public GameObject[] Behind = Array.Empty<GameObject>();
        private readonly List<GameObject> hidden = new();

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
            HideBehind();
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
            foreach (var part in hidden)
                if (part != null)
                    part.SetActive(true);
            hidden.Clear();
        }

        private void HideBehind()
        {
            foreach (var part in Behind)
                if (part != null && part.activeSelf)
                {
                    part.SetActive(false);
                    hidden.Add(part);
                }
        }

        /// <summary>
        /// Writes a piece's card like every other card: the quality's letter, the art, the name
        /// and the stars (the effect is not written on cards, doc/rules/ui-design.md の装備の見せ方).
        /// </summary>
        public static void Paint(
            ItemTile row,
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
            Set(row.Name, entry != null ? entry.Name : piece.Name);
            Set(row.Stars, ItemText.Stars(piece.Stars));
            // 使っている物は影と「E」で示し、押しても選ばない（Choose）。札を薄くすると透けるため、押せるままにする。
            if (row.Mark != null)
                row.Mark.SetActive(piece.User != null);
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
