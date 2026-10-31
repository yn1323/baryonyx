using System.Collections.Generic;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Cards;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// Builds the boons after a won battle (<see cref="AdventureBoonPicker"/>): a dark veil over
    /// the screen split into four glass columns side by side, one a member, with the member's
    /// battle figure standing large on a soft light at the top and the member's two boons as
    /// cards below, framed the way a card is (the frame's light and the sparkle show the step),
    /// a pillar of light hidden behind each member for the take,
    /// and the battle's round check at the bottom right that takes the boon
    /// picked. The battle's overlay and the exploration screen both have it. Coordinates follow
    /// the 1920x1080 design.
    /// </summary>
    public static class AdventureBoonPickerAssets
    {
        private static readonly Color Veil = new(0.012f, 0.02f, 0.04f, 0.78f);

        // 列の板。後ろの戦闘が透けすぎないよう、ガラスの札より濃くする。
        private static readonly Color LaneFill = new(0.03f, 0.045f, 0.1f, 0.92f);

        // 列の大きさと間隔、中心の高さ。
        private static readonly Vector2 LaneSize = new(396, 720);
        private const float LaneGap = 30f;
        private const float LaneY = 60f;

        // 選んだ札の縁と光。金の枠と見分けるため、金ではなく淡い青白にする。
        private static readonly Color PickedColor = new(0.78f, 0.92f, 1f, 1f);

        // 札の板。キラキラが映えるよう、場面の札より少し濃くする。
        private static readonly Color CardFill = new(0.035f, 0.05f, 0.12f, 0.9f);

        // 列の中の、足元の高さと、強化の札の大きさ・中心の高さ（列の中心から）。
        private const float FloorY = 70f;
        private static readonly Vector2 CardSize = new(352, 150);
        private static readonly float[] CardYs = { -88f, -260f };

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
            var safe = SafeArea(root);

            var lanes = new List<CanvasGroup>();
            var members = new List<RawImage>();
            var floors = new List<Graphic>();
            var cards = new List<Button>();
            var texts = new List<TMP_Text>();
            var selections = new List<GameObject>();
            var frames = new List<CardFrameFoil>();
            var foils = new List<CardFoil>();
            var pillars = new List<AdventureBoonPillar>();
            for (int column = 0; column < AdventureBoonPicker.Columns; column++)
            {
                float x = (column - (AdventureBoonPicker.Columns - 1) / 2f) * (LaneSize.x + LaneGap);
                var lane = Rect($"Lane{column + 1}", safe);
                Place(lane, new Vector2(x, LaneY), LaneSize);
                lanes.Add(lane.gameObject.AddComponent<CanvasGroup>());
                GlassCardAssets.Plaque(lane, pattern: false, fill: LaneFill);

                var floor = Rect("Floor", lane);
                Place(floor, new Vector2(0, FloorY), new Vector2(300, 80));
                floors.Add(SpriteImage(floor, UiArt.SoftSpotPath, Color.white));
                pillars.Add(Pillar(lane));
                members.Add(Figure(lane, SampleParty[column], picker.Dot));

                for (int k = 0; k < AdventureBoons.PerMember; k++)
                {
                    var card = Rect($"Boon{k + 1}", lane);
                    Place(card, new Vector2(0, CardYs[k]), CardSize);
                    var (button, selected, content) = GlassCardAssets.Choice(
                        card,
                        pattern: false,
                        fill: CardFill
                    );
                    cards.Add(button);
                    selections.Add(selected);
                    Repaint(selected, card);
                    // ガラスの縁の代わりに、カードと同じ作りの枠を置き、板の上にキラキラを重ねる。
                    var rim = GlassCardAssets.RimOf(card);
                    rim.gameObject.SetActive(false);
                    var fill = GlassCardAssets.FillOf(card);
                    foils.Add(Layer<CardFoil>("Foil", fill.transform, CardFoilAssets.Material()));
                    frames.Add(Layer<CardFrameFoil>("Frame", rim.transform, CardFoilAssets.FrameMaterial()));
                    var text = Label(
                        content,
                        "Text",
                        "",
                        34,
                        UiPalette.TextMain,
                        TextAlignmentOptions.Center
                    );
                    var textRect = (RectTransform)text.transform;
                    Stretch(textRect);
                    textRect.offsetMin = new Vector2(18, 12);
                    textRect.offsetMax = new Vector2(-18, -12);
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 22;
                    text.fontSizeMax = 34;
                    text.lineSpacing = -8f;
                    text.richText = true;
                    texts.Add(text);
                }
            }
            picker.Lanes = lanes.ToArray();
            picker.Members = members.ToArray();
            picker.Floors = floors.ToArray();
            picker.Cards = cards.ToArray();
            picker.Texts = texts.ToArray();
            picker.Selections = selections.ToArray();
            picker.Frames = frames.ToArray();
            picker.Foils = foils.ToArray();
            picker.Pillars = pillars.ToArray();

            // 決めるのは、戦闘のターン終了と同じ丸いチェック（同じ場所）。
            var confirm = Rect("Confirm", safe);
            var face = BattleInspectAssets.EndTurnCircle(confirm);
            var tickArt = BattleInspectAssets.Art("EndTurnCheck");
            var tick = Rect("Check", confirm);
            Place(tick, BattleInspectAssets.EndTurnTickAt, new Vector2(tickArt.width, tickArt.height));
            BattleInspectAssets.AddRaw(tick, tickArt).raycastTarget = false;
            var check = (TintGroupButton)AddTintButton(confirm, face);
            check.SetTintGraphics(new Graphic[] { tick.GetComponent<RawImage>() });
            check.interactable = false;
            picker.Confirm = check;
            root.gameObject.SetActive(false);
            return picker;
        }

        // 決めたときに足元から立つ光の柱（仲間の奥）。広い光・細い芯・昇る細い線で、ふだんは隠しておく。
        private const int PillarLines = 8;
        private const float PillarLineWidth = 4f;

        private static AdventureBoonPillar Pillar(RectTransform lane)
        {
            var rect = Rect("Pillar", lane);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, FloorY - 10f);
            var pillar = rect.gameObject.AddComponent<AdventureBoonPillar>();
            rect.sizeDelta = new Vector2(pillar.HaloWidth, pillar.Height);
            pillar.Halo = Rising("Halo", rect, UiArt.SoftSpotPath);
            pillar.Core = Rising("Core", rect, UiArt.SoftSpotPath);
            var lines = new Image[PillarLines];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = Rising($"Line{i + 1}", rect, null);
                lines[i].rectTransform.sizeDelta = new Vector2(PillarLineWidth, 0);
            }
            pillar.Lines = lines;
            rect.gameObject.SetActive(false);
            return pillar;
        }

        // 柱の下端から上へ伸ばす層。path がなければ線（無地の四角）。
        private static Image Rising(string name, RectTransform parent, string path)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return path != null
                ? SpriteImage(rect, path, Color.clear)
                : AddImage(rect, Color.clear, false);
        }

        // after のすぐ後ろの兄弟として、札いっぱいに枠やキラキラの層を置く。
        private static T Layer<T>(string name, Transform after, Material material)
            where T : Component
        {
            var rect = Rect(name, (RectTransform)after.parent);
            Stretch(rect);
            rect.SetSiblingIndex(after.GetSiblingIndex() + 1);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.material = material;
            image.raycastTarget = false;
            return rect.gameObject.AddComponent<T>();
        }

        // 選んだときの縁・光・板の色を、金から淡い青白に変える。
        private static void Repaint(GameObject selected, RectTransform card)
        {
            foreach (var graphic in selected.GetComponentsInChildren<Graphic>(true))
                graphic.color = new Color(PickedColor.r, PickedColor.g, PickedColor.b, graphic.color.a);
            var tint = card.Find("Body/SelectedTint").GetComponent<Graphic>();
            tint.color = new Color(PickedColor.r, PickedColor.g, PickedColor.b, tint.color.a);
        }

        // 足元の光の上に、仲間の戦闘のドット絵を立てる。見本は枠ごとの仲間を入れておく。
        private static RawImage Figure(RectTransform lane, string sample, float dot)
        {
            var rect = Rect("Member", lane);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, FloorY - 16f);
            var texture = ArtAssets.LoadTexture($"{CharacterArtFolder}/Battle{sample}.aseprite");
            rect.sizeDelta = new Vector2(texture.width, texture.height) * dot;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }
    }
}
