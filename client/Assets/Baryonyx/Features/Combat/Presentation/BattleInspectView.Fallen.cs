using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// An ally whose HP runs out acts no more. The moment it falls, its cards leave the hand and
    /// the deck, and a deck dealt anew leaves them out; those in hand crumble away as a beaten
    /// enemy does (BattleSkillVfx.Defeat.cs). Its cards are shuffled back into the deck when it
    /// stands up again. Only a card that revives can be used on it while it is down.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        // The hold, white-hot and trembling, and the crumble that eats the card away (s, real
        // time, so the hit stop of the blow that felled the ally does not hold it).
        private const float VanishHold = 0.12f;
        private const float VanishTime = 0.6f;

        // How far the crumbling card floats up, in the hand's units.
        private const float VanishRise = 36f;

        // The motes flown off one card.
        private const int VanishMotes = 90;

        // The white-hot light of a crumbling card.
        private static readonly Color32 VanishHot = new(255, 242, 219, 255);

        // Whose cards are out of the hand and the deck: the allies down when last looked.
        private bool[] benched = Array.Empty<bool>();

        // Over the hand, where the cards of a fallen ally crumble.
        private RectTransform vanishLayer;

        // The pixels of the card pictures, read once for the motes' colours.
        private readonly Dictionary<Texture, Color32[]> vanishPixels = new();

        /// <summary>True when the card of the deck at <paramref name="deckIndex"/> belongs to an ally who is down.</summary>
        private bool Benched(int deckIndex)
        {
            int caster = Deck[deckIndex].Caster;
            return caster >= 0 && caster < Allies.Length && Allies[caster].Down;
        }

        /// <summary>True when the card brings a fallen ally back (リザレクション), so it can be used on one.</summary>
        private bool Revives(BattleInspectCard card)
        {
            var skill = SkillOf(Deck[card.DeckIndex]);
            if (skill == null)
                return false;
            foreach (var action in skill.Actions)
                if (action.Kind == CardActionKind.Revive)
                    return true;
            return false;
        }

        /// <summary>Takes note of who is down now, as the deck is made anew without their cards.</summary>
        private void NoteFallen() => benched = Array.ConvertAll(Allies, ally => ally.Down);

        /// <summary>
        /// Takes the cards of each ally fallen since last looked out of the hand and the deck, and
        /// shuffles those of each ally up again back into the deck.
        /// </summary>
        private void RefreshFallen()
        {
            if (benched.Length != Allies.Length)
                NoteFallen();
            for (int i = 0; i < Allies.Length; i++)
            {
                bool down = Allies[i].Down;
                if (down == benched[i])
                    continue;
                benched[i] = down;
                if (down)
                    TakeOutCards(i);
                else
                    ShuffleInCards(i);
            }
        }

        /// <summary>The fallen ally's cards leave the deck, and those in hand crumble away.</summary>
        private void TakeOutCards(int ally)
        {
            drawPile.RemoveAll(deckIndex => Deck[deckIndex].Caster == ally);
            RefreshDeck();
            if (held >= 0 && Cards[held].Caster == ally)
                PutBack();
            for (int slot = hand.Count - 1; slot >= 0; slot--)
            {
                int index = hand[slot];
                if (Cards[index].Caster != ally)
                    continue;
                // A copy crumbles where the card stood, and the card goes back to be drawn again.
                // Not where the hand is put away (the labs).
                if (
                    Application.isPlaying
                    && isActiveAndEnabled
                    && Hand.gameObject.activeInHierarchy
                )
                    StartCoroutine(Crumble(CopyToVanish(index)));
                hand.RemoveAt(slot);
                Cards[index].Place = BattleInspectCardPlace.Discard;
                RefreshCard(index);
            }
            LayoutHand();
        }

        /// <summary>The ally's cards go back into the deck, each at a place at random.</summary>
        private void ShuffleInCards(int ally)
        {
            for (int i = 0; i < Deck.Length; i++)
                if (Deck[i].Caster == ally)
                    drawPile.Insert(Random.Range(0, drawPile.Count + 1), i);
            RefreshDeck();
        }

        /// <summary>
        /// A copy of the card as it stands, on a layer over the hand that the hand's sorting
        /// never puts under its cards.
        /// </summary>
        private RectTransform CopyToVanish(int index)
        {
            var body = Cards[index].Body;
            var layer = VanishLayer();
            var copy = (RectTransform)Instantiate(body.gameObject, layer, false).transform;
            copy.name = body.name + " (Vanishing)";
            BattleEvolutionGlint.StripFrom(copy.gameObject);
            copy.SetAsLastSibling();
            var input = copy.GetComponent<BattleInspectCardInput>();
            if (input != null)
                Destroy(input);
            var group = copy.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            return copy;
        }

        /// <summary>The layer over the hand, laid over it exactly, so a card keeps its place on it.</summary>
        private RectTransform VanishLayer()
        {
            if (vanishLayer == null)
            {
                vanishLayer = new GameObject(
                    "VanishingCards",
                    typeof(RectTransform)
                ).GetComponent<RectTransform>();
                vanishLayer.SetParent(Hand.parent, false);
            }
            vanishLayer.anchorMin = Hand.anchorMin;
            vanishLayer.anchorMax = Hand.anchorMax;
            vanishLayer.pivot = Hand.pivot;
            vanishLayer.anchoredPosition = Hand.anchoredPosition;
            vanishLayer.sizeDelta = Hand.sizeDelta;
            vanishLayer.localRotation = Hand.localRotation;
            vanishLayer.localScale = Hand.localScale;
            vanishLayer.SetSiblingIndex(Hand.GetSiblingIndex() + 1);
            return vanishLayer;
        }

        /// <summary>
        /// The copy of a fallen ally's card crumbles away, in the beaten enemy's three beats. The
        /// hold: it flares white-hot and trembles. The crumble: from the top, dot by dot of the
        /// card's pixel art in a ragged front that burns white, as it floats up and cools to its
        /// own colours; each dot that goes flies up as a square mote of its colour. The fall-off:
        /// the motes drift up and fade.
        /// </summary>
        private IEnumerator Crumble(RectTransform card)
        {
            var face = card.GetComponent<BattleInspectCardView>();
            var frame = face != null ? face.Frame : null;
            var art = face != null ? face.Art : null;
            var rect = card.rect;
            // The card's dots: its frame's pixels, which cover the whole card.
            int columns =
                frame != null && frame.texture != null
                    ? frame.texture.width
                    : Mathf.RoundToInt(rect.width / Dot);
            int rows =
                frame != null && frame.texture != null
                    ? frame.texture.height
                    : Mathf.RoundToInt(rect.height / Dot);
            columns = Mathf.Clamp(columns, 1, 256);
            rows = Mathf.Clamp(rows, 1, 256);
            var dots = CardDots(card, frame, art, columns, rows, out var order, out var solid);

            // The card's parts go under a mask that lets through the dots left, and the heat is
            // laid over them all.
            var maskPixels = new Color32[columns * rows];
            var heatPixels = new Color32[columns * rows];
            var mask = DotTexture("VanishMask", columns, rows);
            var heat = DotTexture("VanishHeat", columns, rows);
            var parts = new List<Transform>();
            foreach (Transform child in card)
                parts.Add(child);
            var masked = Stretched("Crumbling", card);
            var maskImage = masked.gameObject.AddComponent<RawImage>();
            maskImage.texture = mask;
            maskImage.raycastTarget = false;
            masked.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            foreach (var part in parts)
                part.SetParent(masked, false);
            var heatImage = Stretched("Heat", card).gameObject.AddComponent<RawImage>();
            heatImage.texture = heat;
            heatImage.raycastTarget = false;

            var motes = new List<VanishMote>();
            int count = Mathf.Min(dots.Count, VanishMotes);
            float share = dots.Count / (float)Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                var dot = dots[Mathf.Min(dots.Count - 1, (int)((i + Random.value) * share))];
                motes.Add(
                    new VanishMote
                    {
                        Dot = dot,
                        Delay = VanishHold + VanishTime * BattleSkillVfx.CrumbleMoment(dot.Order),
                    }
                );
            }

            var layer = (RectTransform)card.parent;
            var start = card.anchoredPosition;
            // One dot of the card in the layer's units.
            float dotSize = rect.width / columns * Mathf.Abs(card.localScale.y);
            float total = VanishHold + VanishTime;
            bool left = false;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float crumble = Mathf.Clamp01((t - VanishHold) / VanishTime);
                if (!left)
                {
                    // It quivers a dot's half through the hold, then floats up as it goes.
                    float tremble =
                        t < VanishHold
                            ? (Mathf.Repeat(t, 0.06f) < 0.03f ? 0.5f : -0.5f) * dotSize
                            : 0f;
                    float rise = 1f - (1f - crumble) * (1f - crumble);
                    card.anchoredPosition = start + new Vector2(tremble, VanishRise * rise);
                    PaintCrumble(crumble, order, solid, maskPixels, heatPixels);
                    mask.SetPixels32(maskPixels);
                    mask.Apply(false);
                    heat.SetPixels32(heatPixels);
                    heat.Apply(false);
                    if (t >= total)
                    {
                        left = true;
                        card.gameObject.SetActive(false);
                    }
                }

                bool flying = false;
                foreach (var mote in motes)
                {
                    if (mote.Waiting && t >= mote.Delay)
                        Launch(mote, card, layer, dotSize);
                    flying |= mote.Step(t, Time.unscaledDeltaTime);
                }
                if (left && !flying)
                    break;
                yield return null;
            }

            foreach (var mote in motes)
                if (mote.Image != null)
                    Destroy(mote.Image.gameObject);
            Destroy(card.gameObject);
            Destroy(mask);
            Destroy(heat);
        }

        /// <summary>
        /// The card's solid dots in the order they go: from the top, a little from one side, with
        /// clumps and grain of noise so the front is ragged. Each dot's order (0 first to 1 last)
        /// is also put in <paramref name="order"/>, by its place in the grid.
        /// </summary>
        private List<VanishDot> CardDots(
            RectTransform card,
            RawImage frame,
            RawImage art,
            int columns,
            int rows,
            out float[] order,
            out bool[] solid
        )
        {
            var rect = card.rect;
            var framePixels = PixelsOf(frame != null ? frame.texture : null);
            var artPixels = PixelsOf(art != null ? art.texture : null);
            var artArea = art != null ? AreaIn(card, art.rectTransform) : Rect.zero;
            float side = Random.value < 0.5f ? -1f : 1f;
            float seed = Random.value * 50f;
            order = new float[columns * rows];
            solid = new bool[columns * rows];
            var dots = new List<VanishDot>();
            var keys = new List<float>();
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    var local = new Vector2((x + 0.5f) / columns, (y + 0.5f) / rows);
                    var color = (Color32)Color.white;
                    if (framePixels != null)
                    {
                        color = Sample(framePixels, frame.texture, frame.uvRect, local);
                        if (color.a < 128)
                            continue;
                    }
                    var point = rect.min + Vector2.Scale(local, rect.size);
                    if (artPixels != null && artArea.Contains(point))
                    {
                        var drawn = Sample(
                            artPixels,
                            art.texture,
                            art.uvRect,
                            Rect.PointToNormalized(artArea, point)
                        );
                        if (drawn.a >= 128)
                            color = drawn;
                    }
                    float clump = Mathf.PerlinNoise(seed + x * 0.2f, seed + y * 0.2f);
                    float grain = Mathf.Repeat(
                        Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.55f,
                        1f
                    );
                    keys.Add(
                        (1f - local.y)
                            + side * (local.x - 0.5f) * 0.3f
                            + (clump - 0.5f) * 0.3f
                            + (grain - 0.5f) * 0.08f
                    );
                    solid[y * columns + x] = true;
                    dots.Add(
                        new VanishDot
                        {
                            Local = point,
                            Color = color,
                            Cell = y * columns + x,
                        }
                    );
                }
            }
            var ranks = new int[dots.Count];
            for (int i = 0; i < ranks.Length; i++)
                ranks[i] = i;
            Array.Sort(ranks, (a, b) => keys[a].CompareTo(keys[b]));
            var sorted = new List<VanishDot>(dots.Count);
            for (int rank = 0; rank < ranks.Length; rank++)
            {
                var dot = dots[ranks[rank]];
                dot.Order = rank / (float)Mathf.Max(1, ranks.Length - 1);
                order[dot.Cell] = dot.Order;
                sorted.Add(dot);
            }
            return sorted;
        }

        /// <summary>
        /// The mask and the heat <paramref name="crumble"/> (0 to 1) into the crumble: the dots
        /// past the front are gone, the band ahead of it burns white, and the whole card is
        /// white-hot through the hold, cooling early in the crumble (as the Battle Defeat shader).
        /// </summary>
        private static void PaintCrumble(
            float crumble,
            float[] order,
            bool[] solid,
            Color32[] mask,
            Color32[] heat
        )
        {
            float band = BattleSkillVfx.CrumbleBand;
            float eaten = BattleSkillVfx.Crumbled(crumble);
            float front = eaten * (1f + band);
            float cooled = Mathf.Clamp01(crumble / 0.25f);
            float cool = 1f - (1f - cooled) * (1f - cooled);
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < order.Length; i++)
            {
                if (!solid[i] || order[i] < front)
                {
                    mask[i] = clear;
                    heat[i] = clear;
                    continue;
                }
                float edge = eaten > 0f ? 1f - Mathf.Clamp01((order[i] - front) / band) : 0f;
                float glow = Mathf.Max(1f - cool, edge);
                mask[i] = new Color32(255, 255, 255, 255);
                heat[i] = new Color32(
                    VanishHot.r,
                    VanishHot.g,
                    VanishHot.b,
                    (byte)Mathf.RoundToInt(255f * glow)
                );
            }
        }

        /// <summary>A mote flies off from its dot as it goes, white-hot and cooling to the dot's colour.</summary>
        private static void Launch(
            VanishMote mote,
            RectTransform card,
            RectTransform layer,
            float dotSize
        )
        {
            var image = new GameObject(
                "Mote",
                typeof(RectTransform),
                typeof(RawImage)
            ).GetComponent<RawImage>();
            image.raycastTarget = false;
            image.rectTransform.SetParent(layer, false);
            image.rectTransform.SetAsLastSibling();
            mote.Image = image;
            mote.Position = layer.InverseTransformPoint(card.TransformPoint(mote.Dot.Local));
            mote.Velocity =
                new Vector2(Random.Range(-70f, 70f), Random.Range(60f, 200f))
                + Random.insideUnitCircle * 40f;
            mote.Lift = Random.Range(120f, 320f);
            mote.Life = Random.Range(0.45f, 0.9f);
            // Some go as two dots.
            mote.Size = dotSize * (Random.value < 0.5f ? 2f : 1f);
            var color = (Color)mote.Dot.Color;
            mote.From = Color.Lerp(color, Color.white, 0.3f);
            mote.To = new Color(color.r, color.g, color.b, 0f);
            mote.Born = mote.Delay;
        }

        /// <summary>The rectangle of <paramref name="part"/> in the card's space.</summary>
        private static Rect AreaIn(RectTransform card, RectTransform part)
        {
            var corners = new Vector3[4];
            part.GetWorldCorners(corners);
            Vector2 min = card.InverseTransformPoint(corners[0]);
            Vector2 max = card.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y)
            );
        }

        /// <summary>The pixel of a picture at <paramref name="local"/> (0 to 1) of its crop <paramref name="uv"/>.</summary>
        private static Color32 Sample(Color32[] pixels, Texture texture, Rect uv, Vector2 local)
        {
            int x = Mathf.Clamp(
                Mathf.FloorToInt((uv.x + local.x * uv.width) * texture.width),
                0,
                texture.width - 1
            );
            int y = Mathf.Clamp(
                Mathf.FloorToInt((uv.y + local.y * uv.height) * texture.height),
                0,
                texture.height - 1
            );
            return pixels[y * texture.width + x];
        }

        /// <summary>The picture's pixels, read once, or null when there is none or it cannot be read.</summary>
        private Color32[] PixelsOf(Texture texture)
        {
            if (texture == null)
                return null;
            if (!vanishPixels.TryGetValue(texture, out var pixels))
            {
                pixels = BattleSkillVfx.ReadPicture(texture);
                if (pixels != null && pixels.Length != texture.width * texture.height)
                    pixels = null;
                vanishPixels[texture] = pixels;
            }
            return pixels;
        }

        private static Texture2D DotTexture(string name, int columns, int rows) =>
            new(columns, rows, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

        /// <summary>A new child laid over the whole of <paramref name="parent"/>, turning about the same point.</summary>
        private static RectTransform Stretched(string name, RectTransform parent)
        {
            var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.pivot = parent.pivot;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
            return child;
        }

        /// <summary>One solid dot of a crumbling card.</summary>
        private struct VanishDot
        {
            /// <summary>Its middle in the card's space.</summary>
            public Vector2 Local;
            public Color32 Color;

            /// <summary>When it goes, 0 (first) to 1 (last).</summary>
            public float Order;

            /// <summary>Its place in the grid of the card's dots.</summary>
            public int Cell;
        }

        /// <summary>A mote flown off a crumbling card, drawn as a square on the layer over the hand.</summary>
        private sealed class VanishMote
        {
            public VanishDot Dot;
            public float Delay;
            public RawImage Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Lift;
            public float Life;
            public float Size;
            public float Born;
            public Color From;
            public Color To;
            public bool Done;

            /// <summary>True while the mote waits to fly off at its dot's turn.</summary>
            public bool Waiting => Image == null && !Done;

            /// <summary>Moves the mote on to <paramref name="t"/>; false once it has faded.</summary>
            public bool Step(float t, float dt)
            {
                if (Done)
                    return false;
                if (Image == null)
                    return true;
                float k = (t - Born) / Life;
                if (k >= 1f)
                {
                    Object.Destroy(Image.gameObject);
                    Image = null;
                    Done = true;
                    return false;
                }
                Velocity *= Mathf.Exp(-2.4f * dt);
                Velocity.y += Lift * dt;
                Position += Velocity * dt;
                var rect = Image.rectTransform;
                rect.localPosition = Position;
                rect.sizeDelta = Vector2.one * (Size * Mathf.Lerp(1f, 0.35f, k));
                Image.color = Color.Lerp(From, To, k);
                return true;
            }
        }
    }
}
