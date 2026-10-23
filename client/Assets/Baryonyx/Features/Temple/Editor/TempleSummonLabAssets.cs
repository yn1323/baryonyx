using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Temple.Editor
{
    /// <summary>
    /// The summoning moment lab's controls (TempleSummonLabPanel.prefab), laid over the
    /// summoning-once presentation (<see cref="TempleSummonLab"/>): the room's name at the top
    /// right, and one row along the bottom under the card's resting place, on a dark band.
    /// </summary>
    public static class TempleSummonLabAssets
    {
        public const string PrefabPath =
            TempleScreenAssets.Folder + "/UI/TempleSummonLabPanel.prefab";

        private const float Bottom = 12f;

        [MenuItem("Baryonyx/Temple/Create Summon Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("TempleSummonLabPanel");
                // 演出の上に重ねる。
                root.GetComponent<Canvas>().sortingOrder = 10;
                var lab = root.gameObject.AddComponent<TempleSummonLab>();
                var safe = SafeArea(root);
                EnemyLabAssets.Title(safe, "召喚の演出デバッグルーム");

                var strip = Rect("Strip", safe);
                strip.anchorMin = Vector2.zero;
                strip.anchorMax = new Vector2(1f, 0f);
                strip.pivot = new Vector2(0.5f, 0f);
                strip.offsetMin = new Vector2(16f, Bottom - 8f);
                strip.offsetMax = new Vector2(-16f, Bottom + EnemyLabAssets.RowHeight + 6f);
                GlassCardAssets.Plaque(strip, pattern: false, fill: EnemyLabAssets.StripColor);

                var row = EnemyLabAssets.Row(safe, "Controls", Bottom);
                (lab.TierButton, lab.TierLabel) = EnemyLabAssets.ControlButton(
                    row,
                    "Tier",
                    "光：虹",
                    0f
                );
                (lab.UpgradeButton, lab.UpgradeLabel) = EnemyLabAssets.ControlButton(
                    row,
                    "Upgrade",
                    "昇格：入",
                    0f
                );
                (lab.RepeatButton, lab.RepeatLabel) = EnemyLabAssets.ControlButton(
                    row,
                    "Repeat",
                    "くり返し：切",
                    0f
                );
                lab.PlayButton = EnemyLabAssets
                    .ControlButton(row, "Play", "1回召喚を再生", 0f)
                    .button;
                lab.PlayTenButton = EnemyLabAssets
                    .ControlButton(row, "PlayTen", "10回召喚を再生", 0f)
                    .button;
                foreach (
                    var button in new[]
                    {
                        lab.TierButton,
                        lab.UpgradeButton,
                        lab.RepeatButton,
                        lab.PlayButton,
                        lab.PlayTenButton,
                    }
                )
                    button.GetComponent<LayoutElement>().flexibleWidth = 1f;

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
