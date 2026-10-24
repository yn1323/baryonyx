using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Temple.Editor
{
    /// <summary>
    /// Builds the summoning-ten-times presentation (<see cref="TempleSummonTenShow"/>) as a
    /// prefab: the cathedral's wall with ten round openings (a picture drawn with Codex CLI,
    /// output/temple-summon/ten-wall/), each filled with the stained-glass window picture that
    /// lights, cracks and breaks on its own, and a card for each window.
    /// </summary>
    public static class TempleSummonTenShowAssets
    {
        public const string PrefabPath =
            TempleScreenAssets.Folder + "/UI/TempleSummonTenShow.prefab";

        // 壁の絵（1536×1024）の中の、10個の丸い穴の中心と直径（画素）。上の段の左から、下の段の左から。
        private static readonly (Vector2 Center, float Size)[] Openings =
        {
            (new Vector2(334f, 219f), 166f),
            (new Vector2(545f, 219f), 168f),
            (new Vector2(766f, 219f), 172f),
            (new Vector2(988f, 219f), 170f),
            (new Vector2(1202f, 219f), 167f),
            (new Vector2(331f, 495f), 168f),
            (new Vector2(543f, 496f), 170f),
            (new Vector2(766f, 496f), 174f),
            (new Vector2(989f, 496f), 171f),
            (new Vector2(1204f, 496f), 169f),
        };

        private static readonly Vector2 ArtSize = new(1536f, 1024f);
        private const float ArtScale = 1920f / 1536f;

        // 窓の絵は石の枠ごと描いてあるため、穴より少し大きく置き、枠を穴の縁に重ねる。
        private const float WindowOverOpening = 1.14f;

        private static readonly Vector2 CardSize = new(236f, 300f);

        [MenuItem("Baryonyx/Temple/Create Summon Ten Show")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            var wall = TempleSummonShowAssets.ImportArt(
                "ten-wall/ten-wall.png",
                TempleSummonShowAssets.WallArtPath
            );
            var window = TempleSummonShowAssets.ImportArt(
                "window/window.png",
                TempleSummonShowAssets.WindowArtPath
            );
            var glow = TempleSummonShowAssets.EnsureMaterial("SummonGlow", 0, additive: true);
            var glass = TempleSummonShowAssets.EnsureMaterial("SummonPaneGlass", 1, additive: true);
            var shard = TempleSummonShowAssets.EnsureMaterial(
                "SummonPaneShard",
                2,
                additive: false
            );
            var glitter = TempleSummonShowAssets.EnsureMaterial("SummonGlitter", 4, additive: true);
            var hole = TempleSummonShowAssets.EnsureMaterial("SummonHole", 5, additive: false);
            // 窓の絵は1枚まるごとが窓。
            glass.SetVector("_UvRect", new Vector4(0f, 0f, 1f, 1f));
            shard.SetVector("_UvRect", new Vector4(0f, 0f, 1f, 1f));
            AssetDatabase.SaveAssets();

            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("TempleSummonTenShow");
                var show = root.gameObject.AddComponent<TempleSummonTenShow>();
                show.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(
                    LibraryScreenAssets.DataPath
                );

                var fill = Rect("BackFill", root);
                Stretch(fill);
                var fillImage = fill.gameObject.AddComponent<RawImage>();
                fillImage.texture = wall;
                fillImage.color = new Color(0.35f, 0.35f, 0.4f, 1f);
                fillImage.raycastTarget = false;
                var fitter = fill.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = ArtSize.x / ArtSize.y;

                var layer = Layout.Layer(root, "Layer");
                var stage = Rect("Stage", layer);
                Stretch(stage);
                show.Stage = stage;

                var art = Layout.Box(stage, "Wall", 0f, 0f, Layout.Design.x, ArtSize.y * ArtScale);
                var artImage = art.gameObject.AddComponent<RawImage>();
                artImage.texture = wall;
                artImage.raycastTarget = false;

                var dim = Rect("Dim", stage);
                Stretch(dim);
                show.Dim = AddImage(dim, new Color(0f, 0f, 0f, 0f), false);

                var panes = new List<TempleSummonPane>();
                for (int i = 0; i < Openings.Length; i++)
                    panes.Add(BuildPane(stage, i, window, glow, glass, shard, hole));
                // 札は窓より手前に置くため、窓をすべて置いてから並べる。
                for (int i = 0; i < panes.Count; i++)
                    BuildCard(stage, panes[i], i, glow);
                show.Panes = panes.ToArray();

                show.Glitter = TempleSummonShowAssets.Vfx(
                    stage,
                    "Glitter",
                    glitter,
                    0f,
                    0f,
                    Layout.Design.x,
                    Layout.Design.y
                );
                show.Flash = TempleSummonShowAssets.Vfx(stage, "Flash", glow, 0f, 0f, 900f, 900f);
                TempleSummonShowAssets.Centered(show.Flash.rectTransform);
                show.TapArea = TempleSummonShowAssets.TapArea(root);

                // 層は、始まる前の姿（壁の絵だけ）で保存する。各拍は停止中の表示の切り替えで確かめる。
                foreach (var pane in show.Panes)
                foreach (
                    var part in new Component[]
                    {
                        pane.Glow,
                        pane.Glass,
                        pane.Hole,
                        pane.Shards,
                        pane.CardGlow,
                        pane.Card,
                    }
                )
                    part.gameObject.SetActive(false);
                show.Glitter.gameObject.SetActive(false);
                show.Flash.gameObject.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
                Debug.Log("[gen] summon ten show");
            }
        }

        // 1つの窓：後ろの光、窓の絵（ひび）、抜けた穴、破片。窓の中心を左上からの位置で置く。
        private static TempleSummonPane BuildPane(
            RectTransform stage,
            int index,
            Texture window,
            Material glow,
            Material glass,
            Material shard,
            Material hole
        )
        {
            var (center, size) = Openings[index];
            var at = center * ArtScale;
            float side = size * ArtScale * WindowOverOpening;
            BattleVfxImage Square(string name, Material material, float width, Texture texture)
            {
                var image = TempleSummonShowAssets.Vfx(
                    stage,
                    $"{name}{index}",
                    material,
                    0f,
                    0f,
                    width,
                    width
                );
                TempleSummonShowAssets.Centered(image.rectTransform);
                image.rectTransform.anchoredPosition = new Vector2(at.x, -at.y);
                image.texture = texture;
                return image;
            }
            var pane = new TempleSummonPane
            {
                Glow = Square("Glow", glow, side * 2.4f, null),
                Glass = Square("Glass", glass, side, window),
                Hole = Square("Hole", hole, side, null),
            };
            var shards = Rect($"Shards{index}", stage);
            shards.gameObject.AddComponent<CanvasRenderer>();
            pane.Shards = shards.gameObject.AddComponent<TempleGlassShards>();
            TempleSummonShowAssets.Centered(shards);
            shards.sizeDelta = Vector2.one * side;
            shards.anchoredPosition = new Vector2(at.x, -at.y);
            pane.Shards.material = shard;
            pane.Shards.Picture = window;
            pane.Shards.PictureRect = new Rect(0f, 0f, 1f, 1f);
            pane.Shards.raycastTarget = false;
            // 小さな窓のため、破片は1回の窓より狭く飛ばす。
            pane.Shards.Spread = 300f;
            pane.Shards.TowardCamera = 0.6f;
            return pane;
        }

        // 窓から出て伏せて止まる札と、その後ろの光。裏は濃い紺に金のひし形。
        private static void BuildCard(
            RectTransform stage,
            TempleSummonPane pane,
            int index,
            Material glow
        )
        {
            pane.CardGlow = TempleSummonShowAssets.Vfx(
                stage,
                $"CardGlow{index}",
                glow,
                0f,
                0f,
                420f,
                420f
            );
            TempleSummonShowAssets.Centered(pane.CardGlow.rectTransform);
            var card = TempleSummonAssets.BuildCard(
                stage,
                $"Card{index}",
                GlassCardAssets.SceneFillColor
            );
            var rect = (RectTransform)card.Card.transform;
            rect.sizeDelta = CardSize;
            TempleSummonShowAssets.Centered(rect);
            pane.Card = rect;
            pane.CardGroup = rect.GetComponent<CanvasGroup>();
            pane.CardFace = card;
        }
    }
}
