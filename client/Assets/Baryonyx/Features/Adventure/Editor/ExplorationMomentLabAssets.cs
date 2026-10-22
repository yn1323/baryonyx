using System;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Inventory;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// The exploration moment lab's controls (ExplorationMomentLabPanel.prefab), laid over the
    /// exploration screen, whose own parts play the moments (<see cref="ExplorationMomentsAssets"/>):
    /// the room's name and, along the top under the place and the menu, two rows of buttons
    /// (<see cref="ExplorationMomentLab"/>).
    /// </summary>
    public static class ExplorationMomentLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Adventure/UI/ExplorationMomentLabPanel.prefab";

        // The words on the moment buttons, in ExplorationMoment's order.
        private static readonly string[] MomentLabels =
        {
            "魔物が現れた",
            "強い魔物が現れた",
            "最奥の主が現れた",
            "宝箱を開ける",
            "マスへ進む",
        };

        [MenuItem("Baryonyx/Adventure/Create Exploration Moment Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            if (MomentLabels.Length != Enum.GetValues(typeof(ExplorationMoment)).Length)
                throw new InvalidOperationException("Every exploration moment needs a label.");

            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("ExplorationMomentLabPanel");
                // Over the exploration screen, its moments and its dialog.
                root.GetComponent<Canvas>().sortingOrder = 10;
                var lab = root.gameObject.AddComponent<ExplorationMomentLab>();
                var safe = SafeArea(root);
                BuildTitle(safe);
                BuildStrip(safe);
                BuildRows(safe, lab);
                lab.Samples = BattleMomentLabAssets.BonusSamples();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        // The party stands at the bottom of the map, so the controls go along the top, under the
        // place and floor and the menu.
        private const float ControlsTop = 128f;
        private const float RowHeight = EnemyLabAssets.RowHeight;
        private const float RowGap = EnemyLabAssets.RowGap;

        private static void BuildTitle(RectTransform safe)
        {
            var title = Label(
                safe,
                "Title",
                "探索の演出デバッグルーム",
                30,
                EnemyLabAssets.Gold,
                TextAlignmentOptions.Center
            );
            Corner(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -ControlsTop + 8f),
                new Vector2(600f, 40f)
            );
        }

        /// <summary>A dark band behind the two rows, so they read over the map.</summary>
        private static void BuildStrip(RectTransform safe)
        {
            var strip = Rect("Strip", safe);
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = Vector2.one;
            strip.pivot = new Vector2(0.5f, 1f);
            strip.offsetMin = new Vector2(16f, -(ControlsTop + 2f * RowHeight + RowGap + 14f));
            strip.offsetMax = new Vector2(-16f, -(ControlsTop - 6f));
            GlassCardAssets.Plaque(strip, pattern: false, fill: EnemyLabAssets.StripColor);
        }

        /// <summary>A row of buttons along the top, <paramref name="index"/> rows down.</summary>
        private static RectTransform TopRow(RectTransform safe, string name, int index)
        {
            var row = EnemyLabAssets.Row(safe, name, 0f);
            float top = ControlsTop + index * (RowHeight + RowGap);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(40f, -(top + RowHeight));
            row.offsetMax = new Vector2(-40f, -top);
            return row;
        }

        /// <summary>
        /// The upper row: a button for each moment. The lower row: the chest's quality, the repeat,
        /// the reset and the way to the battle moment lab.
        /// </summary>
        private static void BuildRows(RectTransform safe, ExplorationMomentLab lab)
        {
            var upper = TopRow(safe, "Moments", 0);
            var lower = TopRow(safe, "Settings", 1);
            var buttons = new Button[MomentLabels.Length];
            for (int i = 0; i < MomentLabels.Length; i++)
                buttons[i] = EnemyLabAssets.SharedWidthButton(
                    upper,
                    ((ExplorationMoment)i).ToString(),
                    MomentLabels[i]
                );
            lab.MomentButtons = buttons;
            (lab.QualityButton, lab.QualityLabel) = EnemyLabAssets.ControlButton(
                lower,
                "Quality",
                ExplorationMomentLab.QualityText(ItemQuality.S),
                0f
            );
            lab.QualityLabel.richText = true;
            (lab.RepeatButton, lab.RepeatLabel) = EnemyLabAssets.ControlButton(
                lower,
                "Repeat",
                "くり返し：切",
                0f
            );
            lab.ResetButton = EnemyLabAssets.ControlButton(lower, "Reset", "元に戻す", 0f).button;
            lab.OtherLabButton = EnemyLabAssets
                .ControlButton(lower, "OtherLab", "戦闘の演出へ", 0f)
                .button;
            foreach (
                var button in new[]
                {
                    lab.QualityButton,
                    lab.RepeatButton,
                    lab.ResetButton,
                    lab.OtherLabButton,
                }
            )
                button.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }
    }
}
