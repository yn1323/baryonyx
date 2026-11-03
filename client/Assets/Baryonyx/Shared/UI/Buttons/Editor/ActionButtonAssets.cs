using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the shared buttons that decide (doc/rules/ui-design.md#決めるボタン): a button whose
    /// look comes from its <see cref="ButtonRole"/>, and the round check. The primary and danger
    /// buttons are a plate of their colour with a lighter edge along the top for its thickness
    /// and the label in white, without gold or light; the secondary is the frosted glass card
    /// unlit. A button that cannot be pressed greys out, plate and label alike.
    /// </summary>
    public static class ActionButtonAssets
    {
        // 上の縁の明るい帯の太さ（設計座標）。
        public const float EdgeWidth = 4f;

        // 丸いチェックの絵（戦闘のターン終了と同じ）。
        public const string CheckFolder = "Assets/Baryonyx/Features/Combat/UI/Art";
        public const string CirclePath = CheckFolder + "/EndTurnCircle.png";
        public const string TickPath = CheckFolder + "/EndTurnCheck.png";

        // 円の絵の中の、押せる円のまわりの淡い光の幅。
        public const float CircleGlow = 20f;

        // 円の中の白いチェックの位置：長い払いが右へ伸びるため、少し左に置く。
        public static readonly Vector2 TickAt = new(-1f, 0f);

        /// <summary>
        /// Turns <paramref name="rect"/> (already placed by the caller) into a button of
        /// <paramref name="role"/> with <paramref name="label"/> in the middle. A secondary card
        /// straight over a scene passes <see cref="GlassCardAssets.SceneFillColor"/> as
        /// <paramref name="sceneFill"/>. Returns the button, its label and the inside for more
        /// parts (a cost under the label).
        /// </summary>
        public static (Button Button, TMP_Text Label, RectTransform Content) Build(
            RectTransform rect,
            string label,
            ButtonRole role,
            float size = UiType.Title,
            Color? sceneFill = null
        )
        {
            if (role == ButtonRole.Secondary)
            {
                var (card, rim, inside) = GlassCardAssets.Choice(
                    rect,
                    pattern: false,
                    fill: sceneFill
                );
                rim.SetActive(false);
                return (card, Text(inside, label, size, UiPalette.TextMain), inside);
            }

            var (_, edge, plate) = Plate(rect, "Face", role);
            edge.raycastTarget = true;
            var content = Rect("Content", rect);
            Stretch(content);
            var text = Text(content, label, size, UiPalette.ButtonText);
            var button = (TintGroupButton)AddTintButton(rect, edge);
            Dim(button);
            button.SetTintGraphics(new Graphic[] { plate, text });
            return (button, text, content);
        }

        /// <summary>
        /// A button in a layout group whose role changes with what it is given (the shared
        /// dialog's row of choices): the faces of every role, one shown at a time by its
        /// <see cref="ButtonFaces"/>, under one label. It starts as a secondary card.
        /// </summary>
        public static (Button Button, TMP_Text Label, ButtonFaces Faces) Switchable(
            RectTransform parent,
            string name,
            float width,
            float height,
            float size = UiType.Title
        )
        {
            var rect = Rect(name, parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
                element.preferredWidth = width;
            element.preferredHeight = element.minHeight = height;
            var hit = AddImage(rect, Color.clear, true);
            var faces = rect.gameObject.AddComponent<ButtonFaces>();
            var glass = Rect("Secondary", rect);
            Stretch(glass);
            GlassCardAssets.Plaque(glass, pattern: false);
            faces.Secondary = glass.gameObject;
            faces.Primary = Plate(rect, "Primary", ButtonRole.Primary).Face.gameObject;
            faces.Danger = Plate(rect, "Danger", ButtonRole.Danger).Face.gameObject;
            var content = Rect("Content", rect);
            Stretch(content);
            var text = Text(content, "", size, UiPalette.ButtonText);
            var button = (TintGroupButton)AddTintButton(rect, hit);
            Dim(button);
            var graphics = new List<Graphic>();
            foreach (var graphic in rect.GetComponentsInChildren<Graphic>(true))
                if (graphic != hit)
                    graphics.Add(graphic);
            button.SetTintGraphics(graphics.ToArray());
            faces.Show(ButtonRole.Secondary);
            return (button, text, faces);
        }

        // 板の色の面：上の縁の明るい板と、その上端を EdgeWidth だけ残して重ねる面の板。
        private static (RectTransform Face, Image Edge, Image Plate) Plate(
            RectTransform rect,
            string name,
            ButtonRole role
        )
        {
            GlassCardAssets.EnsureArt();
            bool danger = role == ButtonRole.Danger;
            var face = Rect(name, rect);
            Stretch(face);
            var edge = Rect("Edge", face);
            Stretch(edge);
            var edgeImage = Sliced(
                edge,
                GlassCardAssets.FillPath,
                danger ? UiPalette.DangerEdge : UiPalette.PrimaryEdge
            );
            edgeImage.raycastTarget = false;
            var plate = Rect("Plate", face);
            Stretch(plate);
            plate.offsetMax = new Vector2(0f, -EdgeWidth);
            var plateImage = Sliced(
                plate,
                GlassCardAssets.FillPath,
                danger ? UiPalette.DangerFill : UiPalette.PrimaryFill
            );
            plateImage.raycastTarget = false;
            return (face, edgeImage, plateImage);
        }

        /// <summary>
        /// The same button for a parent whose layout group places it (a row of buttons under a
        /// message): <paramref name="width"/> and <paramref name="height"/> become its preferred size.
        /// </summary>
        public static (Button Button, TMP_Text Label, RectTransform Content) InLayout(
            RectTransform parent,
            string name,
            string label,
            ButtonRole role,
            float width,
            float height,
            float size = UiType.Title
        )
        {
            var rect = Rect(name, parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
                element.preferredWidth = width;
            element.preferredHeight = element.minHeight = height;
            return Build(rect, label, role, size);
        }

        /// <summary>The round check's size, its faint glow included.</summary>
        public static Vector2 RoundCheckSize
        {
            get
            {
                var circle = AssetDatabase.LoadAssetAtPath<Texture2D>(CirclePath);
                return new Vector2(circle.width, circle.height);
            }
        }

        /// <summary>
        /// Turns <paramref name="rect"/> (already placed and sized by the caller, see
        /// <see cref="RoundCheckSize"/>) into the round check that decides a pick from a list: the
        /// battle's end-turn circle, navy in a ring of muted gold with a white tick, unlit.
        /// The tick greys out with the circle when it cannot be pressed.
        /// </summary>
        public static Button RoundCheck(RectTransform rect)
        {
            var circle = AssetDatabase.LoadAssetAtPath<Texture2D>(CirclePath);
            var tickArt = AssetDatabase.LoadAssetAtPath<Texture2D>(TickPath);
            var face = rect.gameObject.AddComponent<RawImage>();
            face.texture = circle;
            face.raycastTarget = true;
            var tick = Rect("Check", rect);
            Place(tick, TickAt, new Vector2(tickArt.width, tickArt.height));
            var tickImage = tick.gameObject.AddComponent<RawImage>();
            tickImage.texture = tickArt;
            tickImage.raycastTarget = false;
            var button = (TintGroupButton)AddTintButton(rect, face);
            Dim(button);
            button.SetTintGraphics(new Graphic[] { tickImage });
            return button;
        }

        // 展示室で見る、役割ごとのボタンと丸いチェックの見本（doc/features/showcase.md）。
        public const string SamplesPath = "Assets/Baryonyx/Shared/UI/Buttons/ActionButtonSamples.prefab";

        /// <summary>
        /// Builds the showcase's samples on a window: secondary and primary, secondary and danger,
        /// a primary that cannot be pressed with the runes short beside it, and the round check
        /// pressable and not.
        /// </summary>
        [MenuItem("Baryonyx/UI/Create Action Button Samples")]
        public static GameObject CreateSamples()
        {
            EditorGuard.RequireEditMode();
            Directory.CreateDirectory(Path.GetDirectoryName(SamplesPath));
            var font = GameFontAssets.GetOrCreate();
            using (Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = Rect("ActionButtonSamples", null);
                Stretch(root);
                AddImage(root, UiPalette.Veil, false);
                var safe = SafeArea(root);
                var window = Rect("Window", safe);
                Place(window, Vector2.zero, new Vector2(UiSpace.WindowMedium, 760f));
                GuideFrameAssets.WindowFrame(window);
                Sample(window, "Cancel", "やめる", ButtonRole.Secondary, new Vector2(-330f, 250f));
                Sample(window, "Decide", "決める", ButtonRole.Primary, new Vector2(170f, 250f), 480f);
                Sample(window, "GoOn", "つづける", ButtonRole.Secondary, new Vector2(-330f, 100f));
                Sample(window, "Quit", "冒険をやめる", ButtonRole.Danger, new Vector2(170f, 100f), 480f);
                var buy = Sample(window, "Buy", "買う", ButtonRole.Primary, new Vector2(170f, -60f), 480f);
                buy.interactable = false;
                var missing = Label(
                    window,
                    "Short",
                    UiText.RunesShort(500),
                    UiType.Small,
                    UiPalette.Fall,
                    TextAlignmentOptions.MidlineRight
                );
                Place((RectTransform)missing.transform, new Vector2(-330f, -60f), new Vector2(480f, 60f));
                var size = RoundCheckSize;
                var check = Rect("Check", window);
                Place(check, new Vector2(-130f, -230f), size);
                RoundCheck(check);
                var off = Rect("CheckDisabled", window);
                Place(off, new Vector2(130f, -230f), size);
                RoundCheck(off).interactable = false;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, SamplesPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
        }

        private static Button Sample(
            RectTransform window,
            string name,
            string label,
            ButtonRole role,
            Vector2 center,
            float width = 280f
        )
        {
            var rect = Rect(name, window);
            Place(rect, center, new Vector2(width, 100f));
            return Build(rect, label, role).Button;
        }

        private static TMP_Text Text(RectTransform content, string label, float size, Color color)
        {
            var text = Label(content, "Label", label, size, color, TextAlignmentOptions.Center);
            Stretch((RectTransform)text.transform);
            GuideMenuAssets.Shrink(text, 24);
            return text;
        }

        private static void Dim(Button button)
        {
            var colors = button.colors;
            colors.disabledColor = UiPalette.ButtonDisabled;
            button.colors = colors;
        }
    }
}
