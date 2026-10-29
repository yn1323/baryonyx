using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.WorldMap.Editor
{
    /// <summary>
    /// Builds the <see cref="WorldAtlasToll"/> into a map of the nested world map: the balance
    /// under the place's name with the label that rises from it on each payment, the switch
    /// next to the back button that turns charging off, a price tag to copy under each next
    /// square, the window that asks before a walk of several squares that costs runes, and the
    /// button at the bottom left that stops a walk.
    /// </summary>
    public static class WorldAtlasTollAssets
    {
        private const string RunePath = "Assets/Baryonyx/Shared/Art/GameResources/IconRune.aseprite";

        // 画面の端の部品の位置（WorldMapScreenAssets のもどる・場所の札に合わせる）。
        private const float Margin = 24f;
        private const float BackSize = 128f;

        private static readonly Color Pill = new(0.02f, 0.03f, 0.07f, 0.86f);
        private static readonly Color Box = new(0.95f, 0.91f, 0.82f, 1f);
        private static readonly Color GoFill = new(0.78f, 0.55f, 0.24f, 0.95f);
        // 確認の窓は、文と選ぶところの間を広く取る。イベントの窓（1600×860）よりは小さくする。
        private static readonly Vector2 ConfirmSize = new(1100f, 500f);

        public static WorldAtlasToll Build(RectTransform root, WorldMapView view)
        {
            var safe = (RectTransform)root.Find("SafeArea");
            var toll = root.gameObject.AddComponent<WorldAtlasToll>();
            toll.View = view;

            toll.Balance = Balance(safe, toll);
            BuildSwitch(safe, toll);
            BuildTags(view, toll);
            BuildStop(safe, toll);
            BuildConfirm(root, toll);
            return toll;
        }

        // 右上の場所の札の下に、持っているルーン。払うたびに、その下から「-200」が浮かんで消える。
        private static readonly Vector2 BalanceSize = new(290f, 72f);
        private const float RowY = -Margin - 88f - 12f;
        public static readonly Color SpendColor = new(1f, 0.42f, 0.32f, 1f);

        private static TMP_Text Balance(RectTransform safe, WorldAtlasToll toll)
        {
            var pill = Rect("Runes", safe);
            Corner(pill, Vector2.one, new Vector2(-Margin, RowY), BalanceSize);
            var inside = GlassCardAssets.Plaque(pill, pattern: false, fill: GlassCardAssets.SceneFillColor);
            Icon(inside, new Vector2(-100f, 0f), 48f);
            var text = Label(
                inside,
                "Count",
                "",
                42,
                UiPalette.GoldLight,
                TextAlignmentOptions.MidlineRight
            );
            Place((RectTransform)text.transform, new Vector2(26f, 0f), new Vector2(200f, 60f));

            var spend = Rect("SpendTemplate", safe);
            Corner(spend, Vector2.one, new Vector2(-Margin - 20f, RowY - BalanceSize.y - 10f), new Vector2(170f, 58f));
            Sliced(spend, UiArt.CapsulePath, new Color(0.08f, 0.02f, 0.03f, 0.92f)).raycastTarget = false;
            var amount = Label(spend, "Amount", "-200", 40, SpendColor, TextAlignmentOptions.Center);
            Stretch((RectTransform)amount.transform);
            toll.SpendTemplate = spend;
            return text;
        }

        // もどるの右に、開発用のビルドとEditorだけで出す消費のオンオフ。
        private static void BuildSwitch(RectTransform safe, WorldAtlasToll toll)
        {
            var rect = Rect("ChargingSwitch", safe);
            Corner(
                rect,
                new Vector2(0f, 1f),
                new Vector2(Margin + BackSize + 20f, -Margin - 32f),
                new Vector2(250f, 64f)
            );
            var plate = Sliced(rect, UiArt.CapsulePath, Pill);
            toll.Switch = AddButton(rect, plate);
            var label = Label(rect, "Label", "消費", 30, UiPalette.TextMain, TextAlignmentOptions.MidlineLeft);
            Place((RectTransform)label.transform, new Vector2(-55f, 0f), new Vector2(100f, 44f));
            var track = Rect("Track", rect);
            Place(track, new Vector2(60f, 0f), new Vector2(104f, 44f));
            toll.SwitchTrack = Sliced(track, UiArt.CapsulePath, WorldAtlasToll.OnColor);
            toll.SwitchTrack.raycastTarget = false;
            var state = Label(track, "State", "ON", 20, Color.white, TextAlignmentOptions.Center);
            Place((RectTransform)state.transform, new Vector2(-14f, 0f), new Vector2(60f, 40f));
            toll.SwitchLabel = state;
            var knob = Rect("Knob", track);
            Place(knob, new Vector2(22f, 0f), Vector2.one * 34f);
            SpriteImage(knob, UiArt.CirclePath, Color.white).raycastTarget = false;
            toll.SwitchKnob = knob;
            toll.SwitchRoot = rect.gameObject;
        }

        // 値札の置き場は、地図の中のマスの上の層。値札の見本を1つ置き、出すときに写す。
        private static void BuildTags(WorldMapView view, WorldAtlasToll toll)
        {
            var layer = Rect("Tags", view.Map);
            Stretch(layer);
            // 駒より奥、マスより手前。
            layer.SetSiblingIndex(view.Party.parent.GetSiblingIndex());
            toll.Tags = layer;

            var tag = Rect("TagTemplate", layer);
            tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f);
            tag.pivot = new Vector2(0.5f, 1f);
            tag.sizeDelta = new Vector2(96f, 34f);
            Sliced(tag, UiArt.CapsulePath, new Color(0.02f, 0.03f, 0.07f, 0.9f)).raycastTarget = false;
            Icon(tag, new Vector2(-28f, 0f), 24f);
            var text = Label(tag, "Price", "", 24, UiPalette.GoldLight, TextAlignmentOptions.MidlineRight);
            Place((RectTransform)text.transform, new Vector2(10f, 0f), new Vector2(64f, 30f));
            toll.TagTemplate = tag;
        }

        // 地図を幕で覆い、画面の中央に「合計○ルーンかかります。よろしいですか？」・「次回起動まで確認しない」・
        // やめる・OK を出す。道は幕の下でも光って見える。
        private static void BuildConfirm(RectTransform root, WorldAtlasToll toll)
        {
            var rect = Rect("Confirm", root);
            Stretch(rect);
            toll.Confirm = rect.gameObject.AddComponent<CanvasGroup>();
            var veil = Rect("Veil", rect);
            Stretch(veil);
            AddImage(veil, new Color(0.01f, 0.02f, 0.05f, 0.35f), true);

            var safe = Rect("SafeArea", rect);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFollower>();
            var window = Rect("Window", safe);
            Place(window, Vector2.zero, ConfirmSize);
            var inside = GlassCardAssets.Plaque(window, pattern: false, fill: GlassCardAssets.SceneFillColor);
            toll.Message = Label(inside, "Message", "", 46, UiPalette.TextMain, TextAlignmentOptions.Center);
            toll.Message.lineSpacing = 18f;
            Place((RectTransform)toll.Message.transform, new Vector2(0f, 110f), new Vector2(960f, 150f));

            var skip = Rect("Skip", inside);
            Place(skip, new Vector2(0f, -30f), new Vector2(560f, 72f));
            toll.Skip = AddButton(skip, AddImage(skip, Color.clear, true));
            var box = Rect("Box", skip);
            Place(box, new Vector2(-200f, 0f), Vector2.one * 44f);
            AddImage(box, Box, false);
            var hole = Rect("Hole", box);
            Stretch(hole);
            hole.offsetMin = Vector2.one * 4f;
            hole.offsetMax = -Vector2.one * 4f;
            AddImage(hole, new Color(0.12f, 0.12f, 0.16f, 1f), false);
            var check = Rect("Check", box);
            Stretch(check);
            check.offsetMin = Vector2.one * 9f;
            check.offsetMax = -Vector2.one * 9f;
            AddImage(check, UiPalette.Gold, false);
            toll.SkipCheck = check.gameObject;
            var label = Label(
                skip,
                "Label",
                "次回起動まで確認しない",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Place((RectTransform)label.transform, new Vector2(90f, 0f), new Vector2(500f, 48f));

            toll.Cancel = TextButton(inside, "Cancel", new Vector2(-180f, -160f), "やめる", GlassCardAssets.SceneFillColor);
            toll.Go = TextButton(inside, "Go", new Vector2(180f, -160f), "OK", GoFill);
        }

        // 歩いている間だけ、左下の隅に出す。文字を置かず、止まる印（角の丸い四角）だけにする。
        private static void BuildStop(RectTransform safe, WorldAtlasToll toll)
        {
            var rect = Rect("Stop", safe);
            Corner(rect, Vector2.zero, new Vector2(Margin, Margin), Vector2.one * BackSize);
            var (button, _, content) = GlassCardAssets.Choice(
                rect,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            var icon = Rect("Icon", content);
            Place(icon, Vector2.zero, Vector2.one * 40f);
            AddImage(icon, Color.white, false);
            toll.Stop = button;
            toll.StopRoot = rect.gameObject;
        }

        private static Button TextButton(RectTransform parent, string name, Vector2 center, string text, Color fill)
        {
            var rect = Rect(name, parent);
            Place(rect, center, new Vector2(300f, 96f));
            var (button, _, content) = GlassCardAssets.Choice(rect, pattern: false, fill: fill);
            var label = Label(content, "Label", text, 40, UiPalette.TextMain, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            return button;
        }

        private static void Icon(RectTransform parent, Vector2 center, float size)
        {
            var icon = Rect("Rune", parent);
            Place(icon, center, Vector2.one * size);
            var image = icon.gameObject.AddComponent<RawImage>();
            image.texture = ArtAssets.LoadTexture(RunePath);
            image.raycastTarget = false;
        }
    }
}
