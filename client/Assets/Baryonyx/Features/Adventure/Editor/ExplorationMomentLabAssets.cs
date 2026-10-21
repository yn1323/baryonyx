using System;
using System.Collections.Generic;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Inventory;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// The exploration moment lab's overlay (ExplorationMomentLabPanel.prefab), laid over the
    /// exploration screen: the moments' parts (<see cref="ExplorationMoments"/>: the veils, the
    /// light, the "！", the band, the plate of what a chest held and the wipe into the battle),
    /// the room's name and, along the top under the place and the menu, two rows of buttons
    /// (<see cref="ExplorationMomentLab"/>).
    /// </summary>
    public static class ExplorationMomentLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Adventure/UI/ExplorationMomentLabPanel.prefab";
        public const string LightShaderPath =
            "Assets/Baryonyx/Features/Adventure/Vfx/ExplorationLight.shader";
        public const string LightMaterialPath =
            "Assets/Baryonyx/Features/Adventure/Vfx/ExplorationLight.mat";

        // The words on the moment buttons, in ExplorationMoment's order.
        private static readonly string[] MomentLabels =
        {
            "魔物が現れた",
            "強い魔物が現れた",
            "最奥の主が現れた",
            "宝箱を開ける",
            "部屋へ進む",
        };

        // The wipe into the battle closes in this many bands, from the left and the right by turns.
        private const int WipeBandCount = 10;

        // The plate of what a chest held: over the map's middle, above the party and its chest.
        private static readonly Vector2 PlateCenter = new(0f, -40f);

        [MenuItem("Baryonyx/Adventure/Create Exploration Moment Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            if (MomentLabels.Length != Enum.GetValues(typeof(ExplorationMoment)).Length)
                throw new InvalidOperationException("Every exploration moment needs a label.");

            UiArt.EnsureAll();
            var material = EnsureLightMaterial();
            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("ExplorationMomentLabPanel");
                // Over the exploration screen and its dialog.
                root.GetComponent<Canvas>().sortingOrder = 10;
                var moments = root.gameObject.AddComponent<ExplorationMoments>();
                var lab = root.gameObject.AddComponent<ExplorationMomentLab>();
                lab.Moments = moments;
                moments.LightMaterial = material;

                moments.Dim = Veil(root, "Dim", Color.black);
                moments.LightLayer = Rect("Light", root);
                Stretch(moments.LightLayer);
                var safe = SafeArea(root);
                moments.Mark = BuildMark(safe);
                BuildBand(safe, moments);
                moments.Reveal = RewardRevealAssets.Build(safe, PlateCenter);
                moments.WipeBands = BuildWipe(root);

                // The lab's own controls, over everything but the wipe.
                var controls = Rect("Controls", root);
                Stretch(controls);
                var controlsSafe = SafeArea(controls);
                BuildTitle(controlsSafe);
                BuildStrip(controlsSafe);
                BuildRows(controlsSafe, lab);
                lab.Samples = BattleMomentLabAssets.BonusSamples();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        private static Material EnsureLightMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LightMaterialPath);
            if (material != null)
                return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(LightShaderPath);
            if (shader == null)
                throw new InvalidOperationException(
                    "The light's shader is missing: " + LightShaderPath
                );
            material = new Material(shader) { name = "ExplorationLight" };
            AssetDatabase.CreateAsset(material, LightMaterialPath);
            return material;
        }

        /// <summary>A full-screen veil, see-through until a moment darkens or lights it.</summary>
        private static Image Veil(RectTransform parent, string name, Color color)
        {
            var rect = Rect(name, parent);
            Stretch(rect);
            var image = AddImage(rect, new Color(color.r, color.g, color.b, 0f), false);
            image.enabled = false;
            return image;
        }

        /// <summary>The "！" over the party, hidden until monsters are found.</summary>
        private static TMP_Text BuildMark(RectTransform safe)
        {
            var mark = Label(
                safe,
                "Mark",
                "！",
                150,
                new Color(1f, 0.86f, 0.36f),
                TextAlignmentOptions.Bottom
            );
            // Kiwi Maru's "！" is thin; bold, it reads over the forest.
            mark.fontStyle = FontStyles.Bold;
            Place(mark.rectTransform, Vector2.zero, new Vector2(180f, 180f));
            mark.rectTransform.pivot = new Vector2(0.5f, 0f);
            mark.gameObject.SetActive(false);
            return mark;
        }

        /// <summary>
        /// The band of the room's words: across the middle on a dark band between two lines one dot
        /// thick that take the room's colour, as the battle's turn band.
        /// </summary>
        private static void BuildBand(RectTransform safe, ExplorationMoments moments)
        {
            var instance = InstantiatePrefab(TranslucentTextPanelAssets.PrefabPath, "Band", safe);
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -20f);
            rect.sizeDelta = new Vector2(1400f, 120f);
            var panel = instance.GetComponent<TranslucentTextPanel>();
            panel.Backdrop.color = new Color(0.02f, 0.025f, 0.04f);
            panel.SetBackdropSize(new Vector2(2600f, 190f));
            panel.SetBackdropAlpha(0.85f);
            panel.SetFontSize(72);
            panel.SetText(ExplorationRoom.BattlePrompt);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Backdrop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.BackdropCanvas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Label);
            var lines = new List<Graphic>();
            foreach (float y in new[] { 68f, -68f })
            {
                var line = Rect("Line", rect);
                line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
                line.sizeDelta = new Vector2(1100f, 4f);
                line.anchoredPosition = new Vector2(0f, y);
                lines.Add(
                    AddImage(
                        line,
                        ExplorationMoments.ColorsOf(AdventureRoomKind.Battle).line,
                        false
                    )
                );
            }
            moments.Band = panel;
            moments.BandLines = lines.ToArray();
            moments.BandGroup = instance.AddComponent<CanvasGroup>();
            moments.BandGroup.blocksRaycasts = false;
            moments.BandGroup.interactable = false;
            instance.SetActive(false);
        }

        /// <summary>The bands of the wipe, each a tenth of the screen's height, hidden until used.</summary>
        private static RectTransform[] BuildWipe(RectTransform root)
        {
            var holder = Rect("Wipe", root);
            Stretch(holder);
            var bands = new RectTransform[WipeBandCount];
            for (int i = 0; i < WipeBandCount; i++)
            {
                var band = Rect("Band" + i, holder);
                band.anchorMin = new Vector2(0f, i / (float)WipeBandCount);
                band.anchorMax = new Vector2(1f, (i + 1) / (float)WipeBandCount);
                // A dot of overlap, so no line of the map shows between two closed bands.
                band.offsetMin = new Vector2(0f, -2f);
                band.offsetMax = new Vector2(0f, 2f);
                AddImage(band, Color.black, false);
                band.gameObject.SetActive(false);
                bands[i] = band;
            }
            return bands;
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
