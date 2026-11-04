using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview.Editor;
using UnityEditor;
using UnityEngine;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.WorldMap.Editor
{
    /// <summary>
    /// Builds the fireworks shot up from a town when its dungeon opens (<see cref="DungeonFireworks"/>),
    /// loaded from Resources by the nested world map and laid under the window that tells of it.
    /// The boards are drawn by the Battle Vfx Shape shader's materials, with no picture.
    /// </summary>
    public static class DungeonFireworksAssets
    {
        public const string Folder = WorldMapScreenAssets.Folder + "/Vfx/Resources";
        public const string PrefabPath = Folder + "/" + DungeonFireworks.ResourcePath + ".prefab";

        // 1発ずつの板（開いた花火・打ち上げの尾・光の頭）。
        private const int Shells = 4;

        [MenuItem("Baryonyx/World Map/Create Dungeon Fireworks")]
        public static GameObject CreateAssets()
        {
            EditorGuard.RequireEditMode();
            AssetFolders.Ensure(Folder);
            var shapes = BattleSkillVfxAssets.EnsureShapeMaterials();
            AssetDatabase.SaveAssets();
            using (Begin(null, null))
            {
                var root = CanvasRoot("DungeonFireworks");
                root.sizeDelta = new Vector2(1920f, 1080f);
                var fireworks = root.gameObject.AddComponent<DungeonFireworks>();

                var dim = Rect("Dim", root);
                Stretch(dim);
                fireworks.Dim = AddImage(dim, WithAlpha(UiPalette.Veil, 0f), false);
                fireworks.DimAlpha = UiPalette.Veil.a;

                // 奥から：打ち上げの尾、開いた花火、打ち上げの光の頭。
                fireworks.Trails = Boards(root, "Trail", shapes[(int)BattleVfxShape.Streak]);
                fireworks.Bursts = Boards(root, "Burst", shapes[(int)BattleVfxShape.Firework]);
                fireworks.Heads = Boards(root, "Head", shapes[(int)BattleVfxShape.Spark]);

                // 展示室のプレビューには、開いた花火を写して保存する（起きたときに隠す）。
                fireworks.ShowPreview();
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            }
        }

        private static BattleVfxImage[] Boards(RectTransform root, string name, Material material)
        {
            var boards = new BattleVfxImage[Shells];
            for (int i = 0; i < Shells; i++)
            {
                var rect = Rect(name + i, root);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                var board = rect.gameObject.AddComponent<BattleVfxImage>();
                board.material = material;
                board.raycastTarget = false;
                rect.gameObject.SetActive(false);
                boards[i] = board;
            }
            return boards;
        }

        private static Color WithAlpha(Color color, float alpha) =>
            new(color.r, color.g, color.b, alpha);
    }

    /// <summary>The dungeon fireworks' inspector, with buttons that show its beats while stopped.</summary>
    [CustomEditor(typeof(DungeonFireworks))]
    public sealed class DungeonFireworksEditor : EditModePreviewEditor { }
}
