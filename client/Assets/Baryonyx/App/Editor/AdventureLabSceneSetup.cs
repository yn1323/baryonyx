using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Networking;
using Baryonyx.Showcase.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds AdventureLab.unity, the adventure debugging room (<see cref="AdventureLab"/>): its
    /// controls on a plain dark screen, the room's name at the top, a row for what the adventure
    /// starts from (the destination, the room next and the party's HP) and a row for how the
    /// requests go and the start, with a line for what could not be done.
    /// </summary>
    public static class AdventureLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/AdventureLab.unity";
        public const string PrefabPath = "Assets/Baryonyx/App/UI/AdventureLabPanel.prefab";

        [MenuItem("Baryonyx/App/Create Adventure Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            CreatePanel();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "Camera", ScreenScenes.CameraColor);
                    ScreenScenes.AddScreen(scene, PrefabPath, "AdventureLabPanel");
                    ScreenScenes.AddEventSystem(scene);
                }
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }

        private static void CreatePanel()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Baryonyx/App/UI"))
                AssetDatabase.CreateFolder("Assets/Baryonyx/App", "UI");
            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            using (Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("AdventureLabPanel");
                var lab = root.gameObject.AddComponent<AdventureLab>();
                var safe = SafeArea(root);
                EnemyLabAssets.Title(safe, "冒険のデバッグルーム");

                var setup = Row(safe, "Setup", 560f);
                (lab.DestinationButton, lab.DestinationLabel) = EnemyLabAssets.ControlButton(
                    setup,
                    "Destination",
                    "行き先：ミストラ遺跡",
                    420f
                );
                (lab.NextButton, lab.NextLabel) = EnemyLabAssets.ControlButton(
                    setup,
                    "Next",
                    "次のマス：怪しい宝箱（ミミック）",
                    560f
                );
                (lab.HpButton, lab.HpLabel) = EnemyLabAssets.ControlButton(
                    setup,
                    "Hp",
                    "HP：ひとり倒れている",
                    400f
                );

                var go = Row(safe, "Go", 400f);
                (lab.NetworkButton, lab.NetworkLabel) = EnemyLabAssets.ControlButton(
                    go,
                    "Network",
                    DebugNetwork.Label(DebugNetwork.Mode.VerySlow),
                    460f
                );
                lab.StartButton = EnemyLabAssets.ControlButton(go, "Start", "出発", 300f).button;

                lab.Notice = Label(
                    safe,
                    "Notice",
                    "",
                    32,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                );
                Corner(
                    lab.Notice.rectTransform,
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 300f),
                    new Vector2(1400f, 60f)
                );
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        // A row of buttons across the screen, its bottom <paramref name="bottom"/> up from the safe area's.
        private static RectTransform Row(RectTransform safe, string name, float bottom) =>
            EnemyLabAssets.Row(safe, name, bottom);
    }
}
