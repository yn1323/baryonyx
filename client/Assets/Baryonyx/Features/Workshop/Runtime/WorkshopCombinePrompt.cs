using System;
using System.Collections.Generic;
using Baryonyx.Enhancement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The enhance's prompt when the chosen stones are short but can be made from the colours
    /// below (doc/features/enhancement.md#磨き石の合成): the screen dimmed, one sentence, each
    /// change as small stone art with counts and an arrow ("白×6 ▶ 青×2"), one line a colour,
    /// the runes of the changes and the enhance together, and the battle's round check at the
    /// bottom right. The check makes the stones and enhances; tapping the dim closes without either.
    /// </summary>
    public sealed class WorkshopCombinePrompt : MonoBehaviour
    {
        public const string Message = "足りない磨き石は、下の色から変換して使います";

        public float ArtBox = 64f;
        public Button Dim;
        public Button Confirm;

        // 変換の行（下の色から順に、多くて3行）：使う石と作る石の、絵と数。
        public GameObject[] Steps = Array.Empty<GameObject>();
        public WorkshopGoodRow[] From = Array.Empty<WorkshopGoodRow>();
        public WorkshopGoodRow[] To = Array.Empty<WorkshopGoodRow>();
        public TMP_Text Runes;

        // 磨き石の絵（白・青・紫・金の順）。生成で ItemArt から写す。
        public Texture[] StoneArts = Array.Empty<Texture>();

        private readonly HashSet<Component> bound = new();
        private Action confirmed;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Shows <paramref name="plan"/> and the <paramref name="runes"/> to pay; <paramref name="onConfirm"/> runs when the check is pressed.</summary>
        public void Open(IReadOnlyList<CombineStep> plan, int runes, Action onConfirm)
        {
            Bind();
            confirmed = onConfirm;
            int count = plan?.Count ?? 0;
            for (int i = 0; i < Steps.Length; i++)
            {
                bool visible = i < count;
                if (Steps[i] != null)
                    Steps[i].SetActive(visible);
                if (!visible)
                    continue;
                var step = plan[i];
                Paint(From[i], step.From, step.Uses);
                Paint(To[i], step.Made, step.Count);
            }
            if (Runes != null)
                Runes.text = runes.ToString("N0");
            gameObject.SetActive(true);
        }

        /// <summary>Closes, then makes the stones and enhances.</summary>
        public void Take()
        {
            var take = confirmed;
            Close();
            take?.Invoke();
        }

        public void Close()
        {
            confirmed = null;
            gameObject.SetActive(false);
        }

        // 石の絵と、使う・作る数。
        private void Paint(WorkshopGoodRow row, EnhancementMaterial stone, int count)
        {
            if (row == null || stone == null)
                return;
            var art = ArtOf(stone);
            if (row.Art != null)
            {
                row.Art.texture = art;
                row.Art.enabled = art != null;
                if (art != null)
                {
                    float dot = Mathf.Max(
                        1f,
                        Mathf.Floor(ArtBox / Mathf.Max(art.width, art.height))
                    );
                    row.Art.rectTransform.sizeDelta = new Vector2(art.width, art.height) * dot;
                }
            }
            if (row.Count != null)
                row.Count.text = WorkshopEnhanceView.CountText(count);
            if (row.Name != null)
                row.Name.text = stone.Name;
        }

        private Texture ArtOf(EnhancementMaterial stone)
        {
            for (int i = 0; i < EnhancementMaterials.Polishes.Count && i < StoneArts.Length; i++)
                if (EnhancementMaterials.Polishes[i] == stone)
                    return StoneArts[i];
            return null;
        }

        private void Bind()
        {
            if (Dim != null && bound.Add(Dim))
                Dim.onClick.AddListener(Close);
            if (Confirm != null && bound.Add(Confirm))
                Confirm.onClick.AddListener(Take);
        }
    }
}
