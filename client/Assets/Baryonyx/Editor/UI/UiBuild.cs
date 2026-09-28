using System;
using System.Collections.Generic;
using Baryonyx.Editor.Art;
using Baryonyx.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.Editor.UI
{
    // 画面のPrefabをコードで組み立てる生成スクリプトの共通部品。座標は1920x1080の設計に合わせる。
    // Begin で開いたプレビュー用のシーンに部品を作り、開いているシーンを汚さない。
    // シーンへ直接部品を足すときは Begin を呼ばずに使う。
    public static class UiBuild
    {
        private static Scene scene;
        private static TMP_FontAsset font;
        private static Material shadowText;

        // Prefabを組み立てる間だけプレビュー用のシーンを開き、文字のフォントと影を決める。
        // Dispose でシーンと、その中に作った部品を破棄する。
        public static Session Begin(TMP_FontAsset labelFont, Material labelShadow)
        {
            if (scene.IsValid())
                throw new InvalidOperationException("A UI build session is already open.");
            scene = EditorSceneManager.NewPreviewScene();
            font = labelFont;
            shadowText = labelShadow;
            return new Session();
        }

        public readonly struct Session : IDisposable
        {
            public void Dispose()
            {
                if (scene.IsValid())
                    EditorSceneManager.ClosePreviewScene(scene);
                scene = default;
                font = null;
                shadowText = null;
            }
        }

        // 画面の高さに合わせて拡縮する、画面直描き（Screen Space - Overlay）のCanvas。
        public static RectTransform CanvasRoot(string name)
        {
            var root = Rect(name, null);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1;
            root.gameObject.AddComponent<GraphicRaycaster>();
            return root;
        }

        // 操作部品を置く、端末のSafe Areaに合わせる全面の領域。
        public static RectTransform SafeArea(RectTransform root)
        {
            var safe = Rect("SafeArea", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFollower>();
            return safe;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            if (!scene.IsValid())
            {
                var child = new GameObject(name, typeof(RectTransform));
                child.transform.SetParent(parent, false);
                return (RectTransform)child.transform;
            }
            var obj = EditorUtility.CreateGameObjectWithHideFlags(
                name,
                HideFlags.HideAndDontSave,
                typeof(RectTransform)
            );
            SceneManager.MoveGameObjectToScene(obj, scene);
            obj.hideFlags = HideFlags.None;
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static GameObject InstantiatePrefab(string path, string name, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("Prefab not found: " + path);
            var instance = scene.IsValid()
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)
                : (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        // --- 配置 ------------------------------------------------------------------------

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        public static void Corner(RectTransform rect, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        // --- 画像 ------------------------------------------------------------------------

        public static Image AddImage(RectTransform rect, Color color, bool raycast)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image SpriteImage(RectTransform rect, string path, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ArtAssets.LoadSprite(path);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // 9分割で伸ばす画像。押せる板に使うため、タップを受ける。
        public static Image Sliced(RectTransform rect, string path, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ArtAssets.LoadSprite(path);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        public static Image Picture(
            RectTransform parent,
            string name,
            Sprite sprite,
            Vector2 center,
            Vector2 size,
            float alpha
        )
        {
            var rect = Rect(name, parent);
            Place(rect, center, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(0, 0, 0, alpha);
            image.raycastTarget = false;
            return image;
        }

        // 画面の上端または下端から中央へ向かって薄くなる影。
        public static void Shade(
            RectTransform root,
            string name,
            bool top,
            float height,
            float alpha
        )
        {
            var rect = Rect(name, root);
            rect.anchorMin = new Vector2(0, top ? 1 : 0);
            rect.anchorMax = new Vector2(1, top ? 1 : 0);
            rect.pivot = new Vector2(0.5f, top ? 1 : 0);
            rect.sizeDelta = new Vector2(0, height);
            rect.anchoredPosition = Vector2.zero;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = ArtAssets.LoadTexture(UiArt.ShadePath);
            image.color = new Color(0.024f, 0.031f, 0.055f, alpha);
            image.uvRect = top ? new Rect(0, 0, 1, 1) : new Rect(0, 1, 1, -1);
            image.raycastTarget = false;
        }

        // 1ドットを dotSize ピクセルで描き、キャンバスの最下段（足元）を基準に立たせる絵。
        // 画面の大きさが変わっても、足元が影の上に立つ。
        public static RawImage PixelActor(
            RectTransform parent,
            string name,
            Texture2D texture,
            Vector2 feet,
            float dotSize
        )
        {
            var rect = Rect(name, parent);
            Place(rect, feet, new Vector2(texture.width, texture.height) * dotSize);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = feet;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            rect.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = dotSize;
            return image;
        }

        // 透明な状態で置き、スクリプトが表示するときだけ見せる（トーストなど）。
        public static CanvasGroup HiddenGroup(RectTransform rect)
        {
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        // --- 文字 ------------------------------------------------------------------------

        public static TMP_Text Label(
            RectTransform parent,
            string name,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            bool shadow = true
        )
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.text = text ?? "";
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = alignment;
            label.raycastTarget = false;
            if (shadow && shadowText != null)
                label.fontSharedMaterial = shadowText;
            return label;
        }

        // --- ボタン ----------------------------------------------------------------------

        public static Button AddButton(RectTransform rect, Graphic target) =>
            ConfigureButton(rect.gameObject.AddComponent<Button>(), target);

        // 暗い背景の上のボタン。押すとアイコンと文字も一緒に暗くなる。
        public static Button AddTintButton(RectTransform rect, Graphic target) =>
            ConfigureButton(rect.gameObject.AddComponent<TintGroupButton>(), target);

        public static Button ConfigureButton(Button button, Graphic target)
        {
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.06f, 1f);
            colors.pressedColor = new Color(0.72f, 0.70f, 0.66f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            return button;
        }

        // すべてのボタンの子を作り終えてから呼び、各ボタンが自分の画像と文字をまとめて暗くするようにする。
        public static void CollectTintGraphics(RectTransform root)
        {
            foreach (var button in root.GetComponentsInChildren<TintGroupButton>(true))
            {
                var graphics = new List<Graphic>();
                foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                    if (graphic != button.targetGraphic)
                        graphics.Add(graphic);
                button.SetTintGraphics(graphics.ToArray());
            }
        }
    }
}
