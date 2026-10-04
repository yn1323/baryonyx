using System;
using System.Linq;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Health;
using Baryonyx.Health.Editor;
using Baryonyx.Home;
using Baryonyx.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Connects the title and home scenes to the startup health sync: the link modal, the
    /// settings button on the title screen, and the shared connection settings.
    /// </summary>
    public static class TopStartupSyncSetup
    {
        public const string SettingsButtonName = "TopSettingsButton";
        public const string LinkModalName = "HealthLinkModal";

        [MenuItem("Baryonyx/App/Connect Startup Sync")]
        public static void ConnectScenes()
        {
            EditorGuard.RequireEditMode();
            var top = EditorSceneManager.OpenScene(
                TopHomeSceneSetup.TopScenePath,
                OpenSceneMode.Single
            );
            EnsureTop(top);
            EditorSceneManager.SaveScene(top);
            var home = EditorSceneManager.OpenScene(
                TopHomeSceneSetup.HomeScenePath,
                OpenSceneMode.Single
            );
            EnsureHome(home);
            EditorSceneManager.SaveScene(home);
            EditorSceneManager.OpenScene(TopHomeSceneSetup.TopScenePath, OpenSceneMode.Single);
        }

        public static HealthConnectionSettings LoadSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<HealthConnectionSettings>(
                HealthScreenAssets.SettingsPath
            );
            if (settings == null)
                throw new InvalidOperationException(
                    "Run Baryonyx > Health > Create Screen Assets first."
                );
            return settings;
        }

        public static void EnsureTop(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var canvas = roots.FirstOrDefault(root => root.name == "TopCanvas");
            var controller = roots
                .SelectMany(root => root.GetComponentsInChildren<TopSceneController>(true))
                .FirstOrDefault();
            if (canvas == null || controller == null)
                throw new InvalidOperationException("TopCanvas or TopSceneController is missing.");
            var safeArea = controller.transform.Find("TopSafeArea");
            if (safeArea == null)
                throw new InvalidOperationException("TopSafeArea is missing.");

            var button = safeArea.Find(SettingsButtonName);
            if (button != null)
                UnityEngine.Object.DestroyImmediate(button.gameObject);
            var settingsButton = CreateSettingsButton(safeArea);

            // 開始操作の全面ボタンより手前に置き、暗幕のタップを全面ボタンへ伝えない。
            var modal = canvas.transform.Find(LinkModalName);
            if (modal != null)
                UnityEngine.Object.DestroyImmediate(modal.gameObject);
            var prefab = HealthLinkModalAssets.CreatePrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = LinkModalName;
            instance.transform.SetParent(canvas.transform, false);
            instance.transform.SetAsLastSibling();
            instance.SetActive(false);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("settings").objectReferenceValue = LoadSettings();
            serialized.FindProperty("linkModal").objectReferenceValue =
                instance.GetComponent<HealthLinkModalView>();
            serialized.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void EnsureHome(Scene scene)
        {
            var bootstrap = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HomeBootstrap>(true))
                .FirstOrDefault();
            if (bootstrap == null)
                throw new InvalidOperationException("HomeBootstrap is missing.");
            SetHomeSettings(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void SetHomeSettings(HomeBootstrap bootstrap)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("settings").objectReferenceValue = LoadSettings();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Homeの設定ボタンと同じ見た目・位置。押しても何もしない（設定画面は未実装）。
        private static Button CreateSettingsButton(Transform safeArea)
        {
            var rect = Rect(SettingsButtonName, safeArea);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-64, -40);
            rect.sizeDelta = new Vector2(88, 88);

            var hit = Rect("HitArea", rect);
            hit.anchorMin = Vector2.zero;
            hit.anchorMax = Vector2.one;
            hit.offsetMin = new Vector2(-20, -20);
            hit.offsetMax = new Vector2(20, 20);
            var hitImage = hit.gameObject.AddComponent<Image>();
            hitImage.color = Color.clear;

            var spot = rect.gameObject.AddComponent<Image>();
            spot.sprite = ArtAssets.LoadSprite(UiArt.SoftSpotPath);
            spot.color = new Color(0.012f, 0.02f, 0.04f, 0.8f);
            spot.raycastTarget = true;

            var icon = Rect("SettingsIcon", rect);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(40, 40);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = ArtAssets.LoadSprite(UiArt.IconSettingsPath);
            iconImage.color = UiPalette.TextMain;
            iconImage.raycastTarget = false;

            var button = (TintGroupButton)AddTintButton(rect, spot);
            button.SetTintGraphics(new Graphic[] { iconImage });
            return button;
        }
    }
}
