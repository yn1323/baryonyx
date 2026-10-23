using System.IO;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Temple.Editor
{
    /// <summary>
    /// Builds the summoning-once presentation (<see cref="TempleSummonShow"/>) as a prefab: the
    /// cathedral's key art with the layers of the breaking rose window over it, each on a material
    /// of the Temple Summon Glass shader. The key art and the window are pictures drawn with
    /// Codex CLI (output/temple-summon/); the cracks, shards, light and dust are worked out by
    /// the shader. The prefab's canvas is an overlay; the summoning scene draws it with a camera
    /// for Bloom.
    /// </summary>
    public static class TempleSummonShowAssets
    {
        public const string Folder = TempleScreenAssets.Folder;
        public const string PrefabPath = Folder + "/UI/TempleSummonShow.prefab";
        public const string KeyArtPath = Folder + "/UI/Art/SummonKeyArt.png";
        public const string WindowArtPath = Folder + "/UI/Art/SummonWindow.png";
        public const string WallArtPath = Folder + "/UI/Art/SummonWall.png";
        public const string ShaderPath = Folder + "/Vfx/Shaders/TempleSummonGlass.shader";
        public const string MaterialFolder = Folder + "/Vfx/Materials";

        // Codexの絵（output/temple-summon/）。取り込み元。
        private const string SourceFolder = "../output/temple-summon";

        // キャッチイラスト（1536×1024）の中の薔薇窓。画面いっぱいの幅（1.25倍）で、上端をそろえて置く。
        private static readonly Vector2 ArtSize = new(1536f, 1024f);
        private static readonly Vector2 ArtWindowCenter = new(771f, 254f);
        private const float ArtWindowRadius = 228f;
        private const float ArtScale = 1920f / 1536f;

        private static Vector2 WindowCenter => ArtWindowCenter * ArtScale;
        private static float WindowRadius => ArtWindowRadius * ArtScale;

        // 札の大きさ（結果の窓と同じ）と、降りて止まる高さ。
        private static readonly Vector2 CardSize = new(236f, 300f);
        private const float CardRestY = 700f;

        [MenuItem("Baryonyx/Temple/Create Summon Show")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            var keyArt = ImportArt("key-art/key-art.png", KeyArtPath);
            ImportArt("window/window.png", WindowArtPath);
            ImportArt("ten-wall/ten-wall.png", WallArtPath);
            Directory.CreateDirectory(MaterialFolder);
            var glow = EnsureMaterial("SummonGlow", 0, additive: true);
            var window = EnsureMaterial("SummonWindow", 1, additive: true);
            var shard = EnsureMaterial("SummonShard", 2, additive: false);
            var rays = EnsureMaterial("SummonRays", 3, additive: true);
            var glitter = EnsureMaterial("SummonGlitter", 4, additive: true);
            var hole = EnsureMaterial("SummonHole", 5, additive: false);
            var windowRect = WindowUv();
            window.SetVector(
                "_UvRect",
                new Vector4(windowRect.x, windowRect.y, windowRect.width, windowRect.height)
            );
            shard.SetVector(
                "_UvRect",
                new Vector4(windowRect.x, windowRect.y, windowRect.width, windowRect.height)
            );
            AssetDatabase.SaveAssets();

            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("TempleSummonShow");
                var show = root.gameObject.AddComponent<TempleSummonShow>();
                show.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(
                    LibraryScreenAssets.DataPath
                );

                // 16:9より横長の画面の左右は、同じ絵を暗くして覆う。
                var fill = Rect("BackFill", root);
                Stretch(fill);
                var fillImage = fill.gameObject.AddComponent<RawImage>();
                fillImage.texture = keyArt;
                fillImage.color = new Color(0.35f, 0.35f, 0.4f, 1f);
                fillImage.raycastTarget = false;
                var fitter = fill.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = ArtSize.x / ArtSize.y;

                var layer = Layout.Layer(root, "Layer");
                var stage = Rect("Stage", layer);
                Stretch(stage);
                stage.pivot = new Vector2(
                    WindowCenter.x / Layout.Design.x,
                    1f - WindowCenter.y / Layout.Design.y
                );
                show.Stage = stage;

                var art = Layout.Box(
                    stage,
                    "KeyArt",
                    0f,
                    0f,
                    Layout.Design.x,
                    ArtSize.y * ArtScale
                );
                var artImage = art.gameObject.AddComponent<RawImage>();
                artImage.texture = keyArt;
                artImage.raycastTarget = false;

                var dim = Rect("Dim", stage);
                Stretch(dim);
                show.Dim = AddImage(dim, new Color(0f, 0f, 0f, 0f), false);

                show.FloorPool = Vfx(
                    stage,
                    "FloorPool",
                    glow,
                    WindowCenter.x - 560f,
                    820f,
                    1120f,
                    240f
                );
                show.Window = Square(stage, "Window", window, WindowRadius, keyArt, windowRect);
                show.Hole = Square(stage, "Hole", hole, WindowRadius, null, new Rect(0, 0, 1, 1));
                show.BackGlow = Square(
                    stage,
                    "BackGlow",
                    glow,
                    1000f / 2f,
                    null,
                    new Rect(0, 0, 1, 1)
                );
                show.Rays = Vfx(
                    stage,
                    "Rays",
                    rays,
                    WindowCenter.x - 800f,
                    WindowCenter.y,
                    1600f,
                    760f
                );
                show.Pillar = Vfx(
                    stage,
                    "Pillar",
                    rays,
                    WindowCenter.x - 200f,
                    WindowCenter.y,
                    400f,
                    700f
                );

                var shards = Layout.Box(
                    stage,
                    "Shards",
                    WindowCenter.x - WindowRadius,
                    WindowCenter.y - WindowRadius,
                    WindowRadius * 2f,
                    WindowRadius * 2f
                );
                shards.gameObject.AddComponent<CanvasRenderer>();
                show.Shards = shards.gameObject.AddComponent<TempleGlassShards>();
                show.Shards.material = shard;
                show.Shards.Picture = keyArt;
                show.Shards.PictureRect = windowRect;
                show.Shards.raycastTarget = false;

                show.Glitter = Vfx(
                    stage,
                    "Glitter",
                    glitter,
                    0f,
                    0f,
                    Layout.Design.x,
                    Layout.Design.y
                );
                show.Flash = Square(stage, "Flash", glow, 1700f / 2f, null, new Rect(0, 0, 1, 1));

                // 札と、その後ろの光。札の位置は左上を原点に、中心で動かす。
                show.CardGlow = Vfx(stage, "CardGlow", glow, 0f, 0f, 760f, 760f);
                Centered(show.CardGlow.rectTransform);
                // 札は光の中に置くため、後ろの光が透けすぎないよう、場面の上に置く濃い地にする。
                var card = TempleSummonAssets.BuildCard(
                    stage,
                    "Card",
                    GlassCardAssets.SceneFillColor
                );
                card.Card.transform.localScale = Vector3.one;
                var cardRect = (RectTransform)card.Card.transform;
                cardRect.sizeDelta = CardSize;
                Centered(cardRect);
                show.Card = cardRect;
                show.CardGroup = cardRect.GetComponent<CanvasGroup>();
                show.CardFace = card;
                show.CardTop = new Vector2(WindowCenter.x, -WindowCenter.y);
                show.CardRest = new Vector2(WindowCenter.x, -CardRestY);

                // 札には、キャラの結果（虹）を描いておく。演出の層は、始まる前の姿（絵だけ）で保存する。
                // 層の見た目は、停止中の表示の切り替えで各拍のコマを確かめる。
                var sample = System.Array.Find(
                    show.Data != null ? show.Data.Entries : new LibraryEntryData[0],
                    entry => entry.Kind == LibraryKind.Character
                );
                if (sample != null)
                    TempleSummonResults.Paint(
                        card,
                        new SummonResult(
                            new SummonEntry(LibraryKind.Character, sample.Id, 0),
                            null
                        ),
                        show.Data,
                        show.CardArtBox
                    );
                foreach (
                    var layerImage in new Graphic[]
                    {
                        show.FloorPool,
                        show.Window,
                        show.Hole,
                        show.BackGlow,
                        show.Rays,
                        show.Pillar,
                        show.Shards,
                        show.Glitter,
                        show.Flash,
                        show.CardGlow,
                    }
                )
                    layerImage.gameObject.SetActive(false);
                cardRect.gameObject.SetActive(false);

                show.TapArea = TapArea(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
                Debug.Log("[gen] summon show " + (prefab != null));
            }
        }

        /// <summary>A button over the whole screen that takes the taps but draws nothing.</summary>
        internal static Button TapArea(RectTransform root)
        {
            var rect = Rect("TapArea", root);
            Stretch(rect);
            var image = AddImage(rect, Color.clear, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        // 絵の中の窓の範囲（UV、左下が原点）。窓の円がこの正方形に内接する。
        public static Rect WindowUv() =>
            new(
                (ArtWindowCenter.x - ArtWindowRadius) / ArtSize.x,
                1f - (ArtWindowCenter.y + ArtWindowRadius) / ArtSize.y,
                ArtWindowRadius * 2f / ArtSize.x,
                ArtWindowRadius * 2f / ArtSize.y
            );

        internal static Texture2D ImportArt(string source, string path)
        {
            string from = Path.Combine(SourceFolder, source);
            if (File.Exists(from))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(from, path, true);
            }
            return ArtAssets.ImportTexture(path, FilterMode.Bilinear);
        }

        internal static Material EnsureMaterial(string name, int mode, bool additive)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_Mode", mode);
            material.SetFloat("_Intensity", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat(
                "_DstBlend",
                (float)(
                    additive
                        ? UnityEngine.Rendering.BlendMode.One
                        : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                )
            );
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static BattleVfxImage Vfx(
            RectTransform parent,
            string name,
            Material material,
            float x,
            float y,
            float width,
            float height
        )
        {
            var rect = Layout.Box(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<BattleVfxImage>();
            image.material = material;
            image.raycastTarget = false;
            return image;
        }

        // 窓の中心に置く正方形の層（半径 half）。
        private static BattleVfxImage Square(
            RectTransform parent,
            string name,
            Material material,
            float half,
            Texture texture,
            Rect uv
        )
        {
            var image = Vfx(
                parent,
                name,
                material,
                WindowCenter.x - half,
                WindowCenter.y - half,
                half * 2f,
                half * 2f
            );
            image.texture = texture;
            image.uvRect = uv;
            return image;
        }

        // 左上を原点にして、中心の位置で動かす。
        internal static void Centered(RectTransform rect)
        {
            var size = rect.sizeDelta;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }
    }
}
