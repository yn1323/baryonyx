using System.Collections.Generic;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// Builds the boons after a won battle (<see cref="AdventureBoonPicker"/>): a dark veil over
    /// the screen, the title, three battle cards side by side (the card's own Prefab, larger),
    /// each with the member's figure (or the four) in its art window and a light behind it when
    /// picked, and the button that takes the card picked. The battle's overlay and the
    /// exploration screen both have it. Coordinates follow the 1920x1080 design.
    /// </summary>
    public static class AdventureBoonPickerAssets
    {
        private static readonly Color Veil = new(0.012f, 0.02f, 0.04f, 0.78f);

        // カードの倍率と間隔、中心の高さ。
        private const float CardScale = 1.3f;
        private const float CardGap = 72f;
        private const float CardY = 30f;
        private static readonly Vector2 CardSize = new(210, 294);

        // 編成の枠ごとの、見本の仲間（AdventureAssets の Party と同じ割り当て）。
        private static readonly string[] SampleParty = { "Toma", "Luka", "Aria", "Mina" };
        private const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";

        public static AdventureBoonPicker Build(RectTransform parent)
        {
            var root = Rect("BoonPicker", parent);
            Stretch(root);
            var picker = root.gameObject.AddComponent<AdventureBoonPicker>();
            picker.Panel = root;
            AddImage(root, Veil, true);
            // 白は銀の枠（斬の枠）、金と虹は属性のない枠に、金の線と虹を CardFrameFoil で重ねる。
            var plain = BattleInspectAssets.Art("CardFrameNone");
            picker.Frames = new Texture[]
            {
                BattleInspectAssets.Art("CardFrameSlash"),
                plain,
                plain,
            };
            picker.Backdrop = ArtAssets.LoadSprite(UiArt.SoftSpotPath).texture;
            var safe = SafeArea(root);

            var title = Label(
                safe,
                "Title",
                AdventureTexts.BoonTitle,
                64,
                UiPalette.GoldLight,
                TextAlignmentOptions.Center
            );
            Place((RectTransform)title.transform, new Vector2(0, 420), new Vector2(600, 90));

            var shown = CardSize * CardScale;
            var cards = new List<Button>();
            var faces = new List<BattleInspectCardView>();
            var rims = new List<Graphic>();
            var members = new List<RawImage>();
            for (int i = 0; i < AdventureBoons.OfferCount; i++)
            {
                float x = (i - (AdventureBoons.OfferCount - 1) / 2f) * (shown.x + CardGap);
                var body = Rect($"Card{i + 1}", safe);
                Place(body, new Vector2(x, CardY), shown);
                var rim = Rect("Rim", body);
                Place(rim, Vector2.zero, shown + new Vector2(64, 64));
                var glow = SpriteImage(rim, UiArt.SoftSpotPath, new Color(1f, 0.86f, 0.5f, 0.95f));
                glow.raycastTarget = false;
                rim.gameObject.SetActive(false);
                rims.Add(glow);
                var hit = AddImage(body, Color.clear, true);
                cards.Add(AddButton(body, hit));
                var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Face", body);
                var face = (RectTransform)instance.transform;
                face.anchorMin = face.anchorMax = face.pivot = new Vector2(0.5f, 0.5f);
                face.anchoredPosition = Vector2.zero;
                face.localScale = Vector3.one * CardScale;
                foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
                var view = instance.GetComponent<BattleInspectCardView>();
                faces.Add(view);
                AddFigures(view.Art.rectTransform, members);
            }
            picker.Cards = cards.ToArray();
            picker.Faces = faces.ToArray();
            picker.Rims = rims.ToArray();
            picker.Members = members.ToArray();

            var confirm = Rect("Confirm", safe);
            Place(confirm, new Vector2(0, -400), new Vector2(480, 100));
            var (button, _, content) = GlassCardAssets.Choice(confirm, pattern: false);
            var label = Label(
                content,
                "Label",
                AdventureTexts.BoonChoice,
                46,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            button.interactable = false;
            picker.Confirm = button;
            root.gameObject.SetActive(false);
            return picker;
        }

        // 挿絵の窓の下寄りに、1人の絵（大きく）と、全員の4人の絵（小さく横に並べる）を立てる。
        // 見本は、1人の絵にトーマ、全員の絵に4人を入れておく。
        private static void AddFigures(RectTransform art, List<RawImage> members)
        {
            float width = art.rect.width;
            members.Add(Figure(art, "Member", SampleParty[0], 0f, 3f));
            for (int k = 0; k < AdventureBoonPicker.Figures; k++)
            {
                float x = (k - (AdventureBoonPicker.Figures - 1) / 2f) * width / 4.4f;
                var figure = Figure(art, $"Party{k + 1}", SampleParty[k], x, 2f);
                figure.gameObject.SetActive(false);
                members.Add(figure);
            }
        }

        private static RawImage Figure(
            RectTransform art,
            string name,
            string sample,
            float x,
            float dot
        )
        {
            var rect = Rect(name, art);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 2f);
            var texture = ArtAssets.LoadTexture($"{CharacterArtFolder}/Battle{sample}.aseprite");
            rect.sizeDelta = new Vector2(texture.width, texture.height) * dot;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }
    }
}
