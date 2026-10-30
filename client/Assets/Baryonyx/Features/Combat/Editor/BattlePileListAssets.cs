using Baryonyx.Combat.Presentation;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// Builds the list of a pile's cards (<see cref="BattlePileList"/>) on a screen's controls
    /// canvas: a dark veil over the whole battle that closes the list on a tap, and over it a
    /// scrolling block of a header (the pile's picture, its name and count, and a cross) and
    /// the battle's card to copy for each card of the pile.
    /// </summary>
    public static class BattlePileListAssets
    {
        // Linear colour makes a thin veil look thinner than its alpha: this one hides the battle.
        private static readonly Color Veil = new(0.01f, 0.02f, 0.05f, 0.96f);

        private const float HeaderHeight = 96f;
        private const float CrossHit = 96f;
        private const float CrossSize = 64f;

        // The pictures of the counters, in the order of BattlePile.
        private static readonly string[] IconArt = { "DeckIcon", "DiscardIcon", "ExhaustIcon" };

        /// <summary>The list over <paramref name="screen"/>, hidden until opened.</summary>
        public static BattlePileList Build(RectTransform screen)
        {
            var root = Rect("PileList", screen);
            Stretch(root);
            var veil = AddImage(root, Veil, true);
            var list = root.gameObject.AddComponent<BattlePileList>();
            list.Panel = root.gameObject;
            list.Backdrop = root.gameObject.AddComponent<Button>();
            list.Backdrop.targetGraphic = veil;
            list.Backdrop.transition = Selectable.Transition.None;
            list.Backdrop.navigation = new Navigation { mode = Navigation.Mode.None };

            // The whole safe area scrolls; a drag on it scrolls rather than closes.
            var safe = SafeArea(root);
            var scroll = Rect("Scroll", safe);
            Stretch(scroll);
            AddImage(scroll, Color.clear, true);
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.viewport = scroll;
            scrollRect.scrollSensitivity = 40f;
            var content = Rect("Content", scroll);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 1080f);
            scrollRect.content = content;
            list.Scroll = scrollRect;
            list.Content = content;

            var cardSize = new Vector2(70f, 98f) * 3f * list.CardScale;
            float rowWidth = list.Columns * (cardSize.x + list.Gap.x) - list.Gap.x;
            BuildHeader(content, list, rowWidth);
            BuildTemplate(content, list);

            root.gameObject.SetActive(false);
            return list;
        }

        // The pile's picture (one for each pile, the shown one only), its name and count, and the
        // cross at the right end of the rows.
        private static void BuildHeader(RectTransform content, BattlePileList list, float width)
        {
            var header = Rect("Header", content);
            Corner(header, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, HeaderHeight));
            list.Header = header;

            list.Icons = new GameObject[IconArt.Length];
            for (int i = 0; i < IconArt.Length; i++)
            {
                var art = BattleInspectAssets.Art(IconArt[i]);
                // Pixel art is drawn a dot to DotSize pixels; the flat pieces (PNG) at their own size.
                float dot = AssetDatabase.GetAssetPath(art).EndsWith(".png")
                    ? 1f
                    : BattleInspectAssets.DotSize;
                var size = new Vector2(art.width, art.height) * dot;
                var icon = Rect(IconArt[i], header);
                Corner(icon, new Vector2(0f, 0.5f), Vector2.zero, size);
                BattleInspectAssets.AddRaw(icon, art);
                list.Icons[i] = icon.gameObject;
            }

            var title = Label(header, "Title", "", 50, Color.white, TextAlignmentOptions.MidlineLeft);
            Corner(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(600f, HeaderHeight));
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.richText = true;
            title.raycastTarget = false;
            list.Title = title;

            var cross = Rect("Close", header);
            Corner(cross, new Vector2(1f, 0.5f), Vector2.zero, Vector2.one * CrossHit);
            list.CloseButton = AddTintButton(cross, AddImage(cross, Color.clear, true));
            var mark = Rect("Icon", cross);
            Place(mark, Vector2.zero, Vector2.one * CrossSize);
            SpriteImage(mark, GuideMenuAssets.IconClosePath, Color.white);
        }

        // The battle's card the list copies for each card, face up and taking no taps (a tap on
        // it closes the list like one anywhere else).
        private static void BuildTemplate(RectTransform content, BattlePileList list)
        {
            var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Card", content);
            var card = (RectTransform)instance.transform;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.localRotation = Quaternion.identity;
            card.localScale = Vector3.one * list.CardScale;
            var input = instance.GetComponent<BattleInspectCardInput>();
            if (input != null)
                Object.DestroyImmediate(input);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            var back = card.Find("Back");
            if (back != null)
                back.gameObject.SetActive(false);
            list.Template = instance.GetComponent<BattleInspectCardView>();
            instance.SetActive(false);
        }
    }
}
