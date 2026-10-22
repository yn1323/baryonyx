using System;
using System.Collections.Generic;
using Baryonyx.Editor.UI;
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
    /// The exploration screen's turning points' parts (<see cref="ExplorationMoments"/>): the
    /// veils, the light, the "！", the band, the plate of what a chest held and the wipe into the
    /// battle, made inside the screen (ExplorationScreen.prefab) over the map and under its notice
    /// and dialog, so the adventure's exploration and the exploration moment lab play the same.
    /// </summary>
    public static class ExplorationMomentsAssets
    {
        public const string LightShaderPath =
            "Assets/Baryonyx/Features/Adventure/Vfx/ExplorationLight.shader";
        public const string LightMaterialPath =
            "Assets/Baryonyx/Features/Adventure/Vfx/ExplorationLight.mat";

        // The wipe into the battle closes in this many bands, from the left and the right by turns.
        private const int WipeBandCount = 10;

        // The plate of what a chest held: over the map's middle, above the party and its chest.
        private static readonly Vector2 PlateCenter = new(0f, -40f);

        /// <summary>
        /// Makes the moments' parts in <paramref name="root"/> (the screen's canvas), over what is
        /// in it already, for <paramref name="view"/>. Inside a <see cref="UiBuild.Begin"/>.
        /// </summary>
        public static ExplorationMoments Build(RectTransform root, ExplorationView view)
        {
            var holder = Rect("Moments", root);
            Stretch(holder);
            var moments = holder.gameObject.AddComponent<ExplorationMoments>();
            moments.View = view;
            moments.LightMaterial = EnsureLightMaterial();

            moments.Dim = Veil(holder, "Dim", Color.black);
            moments.LightLayer = Rect("Light", holder);
            Stretch(moments.LightLayer);
            var safe = SafeArea(holder);
            moments.Mark = BuildMark(safe);
            BuildBand(safe, moments);
            moments.Reveal = RewardRevealAssets.Build(safe, PlateCenter);
            moments.WipeBands = BuildWipe(holder);
            return moments;
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
    }
}
