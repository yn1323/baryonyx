using System.Collections.Generic;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI.GuideMenu.Editor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.WorldMap.Editor
{
    /// <summary>
    /// Builds the <see cref="WorldMapEventWindow"/> into a map's screen: the dimming veil that
    /// skips the opening when tapped, the square's glow and rising lines of light, the window in
    /// the guide frame with the meadow scenery inside its gold line, one panel for each kind of
    /// event (<see cref="WorldEventPanelAssets"/>), a cross at the top right that closes it, and
    /// the white flash that leads to a battle. The window is placed by hand, without layout
    /// groups, since it is scaled and moved every frame while it opens.
    /// </summary>
    public static class WorldMapEventWindowAssets
    {
        // 見本の窓の後ろに敷く景色。
        public const string SceneryPath =
            "Assets/Baryonyx/Shared/Art/Stages/MeadowRoad/MeadowBackdrop.aseprite";

        private const int RayCount = 12;

        // 窓は画面の中央に大きく置く。右上の今いる場所の札には重なってよく、下の端の下に出来事のマスが見える。
        // 景色は金の線の内側いっぱいに敷き、題も説明の文も置かない。拾い物だけは小さな窓にする。
        public static readonly Vector2 WindowSize = new(1600f, 860f);
        public const float Inset = 6f;
        private const float CloseHit = 128f;
        private static readonly Color Veil = new(0.012f, 0.02f, 0.04f, 0f);
        public static readonly Color Ink = new(0.1f, 0.12f, 0.18f, 1f);

        /// <summary>Adds the window under <paramref name="root"/>, which spans the whole screen.</summary>
        public static WorldMapEventWindow Build(RectTransform root)
        {
            var rect = Rect("EventWindow", root);
            Stretch(rect);
            var window = rect.gameObject.AddComponent<WorldMapEventWindow>();
            window.Group = rect.gameObject.AddComponent<CanvasGroup>();

            var veil = Rect("Veil", rect);
            Stretch(veil);
            window.Veil = AddImage(veil, Veil, true);
            window.Skip = veil.gameObject.AddComponent<Button>();
            window.Skip.transition = Selectable.Transition.None;

            var glow = Rect("Glow", rect);
            Place(glow, Vector2.zero, new Vector2(220f, 220f));
            window.Glow = glow.gameObject.AddComponent<RawImage>();
            window.Glow.raycastTarget = false;

            var rays = new List<RawImage>();
            for (int i = 0; i < RayCount; i++)
            {
                var ray = Rect("Ray" + i, rect);
                Place(ray, Vector2.zero, new Vector2(i % 3 == 0 ? 4f : 3f, 90f));
                // 線の下の端をマスに合わせ、上へ伸ばす。
                ray.pivot = new Vector2(0.5f, 0f);
                var image = ray.gameObject.AddComponent<RawImage>();
                image.raycastTarget = false;
                rays.Add(image);
            }
            window.Rays = rays.ToArray();

            var frame = Rect("Window", rect);
            Place(frame, WorldMapEventWindow.Home, WindowSize);
            GuideFrameAssets.WindowFrame(frame);
            window.Window = frame;
            window.WindowGroup = frame.gameObject.AddComponent<CanvasGroup>();

            // 中身は窓の大きさに合わせて伸びる。押すと出来事が進む（見えない板で受ける）。
            var stage = Rect("Stage", frame);
            Stretch(stage);
            stage.offsetMin = Vector2.one * Inset;
            stage.offsetMax = -Vector2.one * Inset;
            // 金の線とひし形を中身の上に出す。
            stage.SetSiblingIndex(0);
            window.Next = stage.gameObject.AddComponent<Button>();
            window.Next.targetGraphic = AddImage(stage, Color.clear, true);
            window.Next.transition = Selectable.Transition.None;
            window.Scenery = Scenery(stage, WindowSize - Vector2.one * (Inset * 2f));

            window.Panels = WorldEventPanelAssets.Build(stage);

            // 閉じるは右上の×にして、窓の中を中身に使う。×は小さく描き、押せる範囲は指の大きさにする。
            // 明るい空の上でも見えるよう、×の下に暗い円を敷く。
            var close = Rect("Close", frame);
            Corner(close, Vector2.one, new Vector2(-8f, -4f), Vector2.one * CloseHit);
            var button = AddTintButton(close, AddImage(close, Color.clear, true));
            var plate = Rect("Plate", close);
            Place(plate, Vector2.zero, Vector2.one * 88f);
            SpriteImage(plate, UiArt.CirclePath, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            var cross = Rect("Icon", close);
            Place(cross, Vector2.zero, Vector2.one * 64f);
            SpriteImage(cross, GuideMenuAssets.IconClosePath, Color.white);
            window.Close = button;
            window.CloseGroup = close.gameObject.AddComponent<CanvasGroup>();

            // 戦闘へ切り替わるときの白。窓も含めて画面ごと覆う。
            var flash = Rect("Flash", rect);
            Stretch(flash);
            window.Flash = AddImage(flash, new Color(1f, 0.98f, 0.94f, 0f), false);
            window.Flash.enabled = false;
            return window;
        }

        // 景色のドット絵を整数倍で窓いっぱいに広げ、足元の草原が見えるよう下の端に合わせる。
        private static RawImage Scenery(RectTransform stage, Vector2 size)
        {
            var rect = Rect("Scenery", stage);
            Stretch(rect);
            var texture = ArtAssets.LoadTexture(SceneryPath);
            float dot = Mathf.Ceil(Mathf.Max(size.x / texture.width, size.y / texture.height));
            float w = size.x / (texture.width * dot);
            float h = size.y / (texture.height * dot);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = new Rect((1f - w) * 0.5f, 0f, w, h);
            image.raycastTarget = false;
            return image;
        }
    }
}
