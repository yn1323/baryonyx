using Baryonyx.Editor;
using Baryonyx.Showcase.Editor;
using Baryonyx.WorldMap;
using Baryonyx.WorldMap.Editor;
using UnityEditor;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Rebuilds WorldEventLab.unity, the debugging room for what happens on the nested world
    /// map's squares: the nested world map (<see cref="WorldAtlasAssets"/>) with no adventure
    /// behind it, and the lab's list (<see cref="WorldEventLabAssets"/>) over it to open each
    /// event's window (<see cref="WorldEventLab"/>).
    /// </summary>
    public static class WorldEventLabSceneSetup
    {
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Debug/WorldEventLab.unity";

        [MenuItem("Baryonyx/App/Create World Event Lab Scene")]
        public static void CreateScene()
        {
            EditorGuard.RequireEditMode();
            // 地図は入れ子の地図の物。ないときだけ、ここで作る。
            if (
                AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                    WorldAtlasAssets.NavigatorPath
                ) == null
            )
                WorldAtlasAssets.CreateAssets();
            WorldEventLabAssets.CreateAssets();
            ScreenScenes.Rebuild(
                ScenePath,
                scene =>
                {
                    ScreenScenes.AddCamera(scene, "WorldEventLabCamera", ScreenScenes.CameraColor);
                    var atlas = ScreenScenes.AddScreen(
                        scene,
                        WorldAtlasAssets.NavigatorPath,
                        "WorldAtlas"
                    );
                    var panel = ScreenScenes.AddScreen(
                        scene,
                        WorldEventLabAssets.PrefabPath,
                        "WorldEventLabPanel"
                    );
                    var lab = panel.GetComponent<WorldEventLab>();
                    lab.Navigator = atlas.GetComponent<WorldAtlasNavigator>();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lab);
                    ScreenScenes.AddEventSystem(scene);
                }
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ScreenScenes.AddToBuildSettings(ScenePath);
            ShowcaseCatalogBuilder.RefreshCatalog();
        }
    }
}
