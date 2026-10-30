using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the showcase's sample of the card back (<see cref="GlassCardBackPreview"/>): a
    /// canvas with one glass card of each list that lays its cards face down while it reads, at
    /// that list's size, turning over and over (doc/features/screens/guide.md#カードの裏面).
    /// </summary>
    public static class GlassCardBackAssets
    {
        public const string PreviewPath =
            "Assets/Baryonyx/Shared/UI/GlassCard/GlassCardBackPreview.prefab";

        // 一覧ごとのカードの大きさ（設計座標）と、見本での左上の位置。Empty は出す物がない場所の見本、
        // Vertical は上下にめくる見本（カードスキルの行）。
        private static readonly (
            string Name,
            Vector2 Position,
            Vector2 Size,
            bool Empty,
            bool Vertical
        )[] Cards =
        {
            ("CardSkill", new Vector2(64, 96), new Vector2(812, 140), false, true),
            ("CardSkillEmpty", new Vector2(64, 252), new Vector2(812, 140), true, true),
            ("Adventurer", new Vector2(940, 84), new Vector2(588, 168), false, false),
            ("AdventurerEmpty", new Vector2(940, 268), new Vector2(588, 168), true, false),
            ("Picker", new Vector2(64, 470), new Vector2(597, 291), false, false),
            ("Buy", new Vector2(725, 470), new Vector2(256, 272), false, false),
            ("Sell", new Vector2(1045, 470), new Vector2(200, 200), false, false),
            ("SellEmpty", new Vector2(1045, 692), new Vector2(200, 200), true, false),
            ("Library", new Vector2(1309, 470), new Vector2(265, 300), false, false),
        };

        // 案内人の画面の面に近い、暗い紺の背景。
        private static readonly Color Backdrop = new(0.06f, 0.07f, 0.13f, 1f);

        [MenuItem("Baryonyx/UI/Create Glass Card Back Preview")]
        public static void CreatePreview()
        {
            // 開いているシーンを汚さないよう、プレビュー用のシーンで組み立てる。
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("GlassCardBackPreview", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1;

                var backdrop = Child("Backdrop", root.transform);
                backdrop.anchorMin = Vector2.zero;
                backdrop.anchorMax = Vector2.one;
                backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
                backdrop.gameObject.AddComponent<Image>().color = Backdrop;

                var stage = Child("Cards", root.transform);
                stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
                stage.sizeDelta = new Vector2(1920, 1080);
                var cards = new List<GameObject>();
                var empty = new List<GameObject>();
                foreach (var (name, position, size, isEmpty, vertical) in Cards)
                {
                    var rect = Child(name, stage);
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = size;
                    rect.anchoredPosition = new Vector2(
                        position.x + size.x / 2f,
                        -position.y - size.y / 2f
                    );
                    var (card, _) = GlassCardAssets.Build(rect);
                    GlassCardAssets.AddBack(card, vertical);
                    (isEmpty ? empty : cards).Add(rect.gameObject);
                }
                var preview = root.AddComponent<GlassCardBackPreview>();
                preview.Cards = cards.ToArray();
                preview.Empty = empty.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, PreviewPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }
    }
}
