using System;
using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// The enemy lab's controls (EnemyLabPanel.prefab), laid over the battle screen in the enemy
    /// lab's scene where the hand and its controls are put away: the room's name at the top
    /// right, and along the bottom the chosen enemy with the buttons to choose another, to repeat
    /// and to put the battlefield back, over a row of a button for each thing the enemy can do
    /// (<see cref="EnemyLab"/>).
    /// </summary>
    public static class EnemyLabAssets
    {
        public const string PrefabPath = "Assets/Baryonyx/Features/Combat/UI/EnemyLabPanel.prefab";

        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);
        private static readonly Color ButtonColor = new(0.06f, 0.07f, 0.12f, 0.88f);
        private static readonly Color StripColor = new(0.02f, 0.03f, 0.06f, 0.72f);

        // The enemies' names in the lab, by their art's name.
        private static readonly Dictionary<string, string> EnemyNames = new()
        {
            ["MossSlime"] = "苔スライム",
            ["MossWolf"] = "苔むした狼",
            ["ForestGuardian"] = "森の守り手",
        };

        // The words on the action buttons, in EnemyLabAction's order.
        private static readonly string[] ActionLabels =
        {
            "攻撃",
            "被弾",
            "弱点に被弾",
            "撃破",
            "凍結中に攻撃",
            "麻痺中に攻撃",
            "出血中に攻撃",
            "反射に攻撃",
            "敵のターン",
        };

        // Two rows of buttons a finger can press (128 on the 1920x1080 design, as the UI rules
        // ask), kept under the lowest HP bars of the party and the enemies.
        private const float RowHeight = 128f;
        private const float RowGap = 8f;
        private const float ActionsBottom = 12f;
        private const float ChooserBottom = ActionsBottom + RowHeight + RowGap;

        /// <summary>The enemy's name in the lab, from the name of its body on the battle screen ("EnemyMossSlime").</summary>
        public static string NameOf(string bodyName)
        {
            var art = bodyName.StartsWith("Enemy", StringComparison.Ordinal)
                ? bodyName.Substring("Enemy".Length)
                : bodyName;
            return EnemyNames.TryGetValue(art, out var label) ? label : art;
        }

        [MenuItem("Baryonyx/Combat/Create Enemy Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            if (ActionLabels.Length != Enum.GetValues(typeof(EnemyLabAction)).Length)
                throw new InvalidOperationException("Every enemy lab action needs a label.");

            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("EnemyLabPanel");
                // Over the battle screen's own overlay (sorting order 1).
                root.GetComponent<Canvas>().sortingOrder = 2;
                var lab = root.gameObject.AddComponent<EnemyLab>();
                var safe = SafeArea(root);
                BuildTitle(safe);
                BuildStrip(safe);
                BuildChooser(safe, lab);
                BuildActions(safe, lab);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        private static void BuildTitle(RectTransform safe)
        {
            var title = Label(
                safe,
                "Title",
                "敵の挙動デバッグルーム",
                34,
                Gold,
                TextAlignmentOptions.Right
            );
            Corner(
                title.rectTransform,
                Vector2.one,
                new Vector2(-40f, -24f),
                new Vector2(700f, 48f)
            );
        }

        /// <summary>A dark band behind the two rows, so they read over the stage.</summary>
        private static void BuildStrip(RectTransform safe)
        {
            var strip = Rect("Strip", safe);
            strip.anchorMin = Vector2.zero;
            strip.anchorMax = new Vector2(1f, 0f);
            strip.pivot = new Vector2(0.5f, 0f);
            strip.offsetMin = new Vector2(16f, ActionsBottom - 8f);
            strip.offsetMax = new Vector2(-16f, ChooserBottom + RowHeight + 6f);
            Sliced(strip, UiArt.RoundedRectPath, StripColor).raycastTarget = false;
        }

        /// <summary>
        /// The upper row: the chosen enemy (name, attack, HP, statuses) between the buttons to the
        /// one before and after, then the repeat and the reset.
        /// </summary>
        private static void BuildChooser(RectTransform safe, EnemyLab lab)
        {
            var row = Row(safe, "Chooser", ChooserBottom);
            // DotGothic16 has no arrows (◀▶), so the neighbours are named in words.
            lab.PreviousButton = ControlButton(row, "Previous", "前の敵", 170f).button;
            var label = Label(
                row,
                "Enemy",
                "",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 34f;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            lab.EnemyLabel = label;
            lab.NextButton = ControlButton(row, "Next", "次の敵", 170f).button;
            (lab.RepeatButton, lab.RepeatLabel) = ControlButton(
                row,
                "Repeat",
                "くり返し：切",
                250f
            );
            lab.ResetButton = ControlButton(row, "Reset", "元に戻す", 210f).button;
        }

        /// <summary>The lower row: a button for each thing the enemy can do, sharing the width.</summary>
        private static void BuildActions(RectTransform safe, EnemyLab lab)
        {
            var row = Row(safe, "Actions", ActionsBottom);
            var buttons = new Button[ActionLabels.Length];
            for (int i = 0; i < ActionLabels.Length; i++)
            {
                var (button, label) = ControlButton(
                    row,
                    ((EnemyLabAction)i).ToString(),
                    ActionLabels[i],
                    0f
                );
                button.GetComponent<LayoutElement>().flexibleWidth = 1f;
                label.enableAutoSizing = true;
                label.fontSizeMin = 20f;
                label.fontSizeMax = 34f;
                buttons[i] = button;
            }
            lab.ActionButtons = buttons;
        }

        private static RectTransform Row(RectTransform safe, string name, float bottom)
        {
            var row = Rect(name, safe);
            row.anchorMin = Vector2.zero;
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.offsetMin = new Vector2(40f, bottom);
            row.offsetMax = new Vector2(-40f, bottom + RowHeight);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return row;
        }

        private static (Button button, TMP_Text label) ControlButton(
            RectTransform parent,
            string name,
            string text,
            float width
        )
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            var image = Sliced(rect, UiArt.RoundedRectPath, ButtonColor);
            var button = AddButton(rect, image);
            var label = Label(
                rect,
                "Label",
                text,
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            return (button, label);
        }
    }
}
