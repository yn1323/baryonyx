using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// Remembers what each card of a person's slots showed, and turns a card over
    /// (<see cref="GlassCardTurn"/>) when it is given something else for the same person: the
    /// equipment and skill tabs' slots on the left. Showing another person, or showing the slots
    /// for the first time after loading, turns nothing. The lines are drawn on a layer over the
    /// panel, tinted with the colour of the new contents' art.
    /// </summary>
    public sealed class GlassCardTurns
    {
        private const string LayerName = "TurnLines";

        private readonly Transform panel;
        private readonly Func<Texture, Color> colorOf;
        private readonly Dictionary<int, string> shown = new();
        private string person;
        private RectTransform layer;

        /// <param name="panel">The panel the lines are drawn over.</param>
        /// <param name="colorOf">The colour of a picture, for the lines' light.</param>
        public GlassCardTurns(Transform panel, Func<Texture, Color> colorOf)
        {
            this.panel = panel;
            this.colorOf = colorOf;
        }

        /// <summary>Forgets what the cards showed, so the next drawing turns nothing.</summary>
        public void Forget()
        {
            person = null;
            shown.Clear();
        }

        /// <summary>Starts drawing <paramref name="personId"/>'s slots; another person's turns nothing.</summary>
        public void Begin(string personId)
        {
            if (personId != person)
                shown.Clear();
            person = personId;
        }

        /// <summary>
        /// Before <paramref name="card"/> (slot <paramref name="index"/>) shows <paramref name="key"/>
        /// (what it holds, or how it is empty or locked), turns it over when it showed something
        /// else for the same person. <paramref name="art"/> is the new contents' picture.
        /// </summary>
        public void Show(int index, GlassCard card, string key, Texture art)
        {
            if (
                person != null
                && shown.TryGetValue(index, out var was)
                && was != key
                && card != null
            )
                GlassCardTurn.Turn(card, Layer(), GlassCardTurn.TintOf(colorOf(art)));
            shown[index] = key;
        }

        // パネルの上にかぶせる、線を描く面。並べ方には入れず、押されない。
        private RectTransform Layer()
        {
            if (!Application.isPlaying || panel == null)
                return null;
            if (layer != null)
                return layer;
            var found = panel.Find(LayerName);
            if (found != null)
                return layer = (RectTransform)found;
            layer = new GameObject(LayerName, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(panel, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            layer.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            return layer;
        }
    }
}
