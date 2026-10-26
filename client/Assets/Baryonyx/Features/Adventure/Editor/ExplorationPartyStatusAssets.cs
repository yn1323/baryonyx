using System.Collections.Generic;
using Baryonyx.Editor.Art;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// Builds the party's HP (<see cref="ExplorationPartyStatus"/>): the row at the top of the
    /// exploration map, and the bars under the four at the campfire. Coordinates follow the
    /// 1920x1080 design.
    /// </summary>
    public static class ExplorationPartyStatusAssets
    {
        private const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";

        // 見本の仲間（AdventureAssets の Party と同じ割り当て）。
        private static readonly string[] SampleParty = { "Toma", "Luka", "Aria", "Mina" };

        // 地図の上の札：4人を横に並べる。1人の幅、高さ、間隔、絵の倍率。左上のメニューの右から並べ、
        // 右上の行き先と階（長い名前でも）に重ならない幅にする。
        private const float CellWidth = 200f;
        private const float CellHeight = 120f;
        private const float CellGap = 10f;
        private const float RowLeft = 210f;
        private const float RowDot = 2f;

        // HPのバーの太さと、数字の大きさ。
        private const float BarHeight = 16f;
        private const float NumberSize = 30f;

        private static readonly Color BarBack = new(0.02f, 0.03f, 0.06f, 0.9f);

        /// <summary>The row at the top centre of the map, a card of glass under each member.</summary>
        public static ExplorationPartyStatus BuildRow(RectTransform safe)
        {
            var row = Rect("PartyStatus", safe);
            float width = CellWidth * SampleParty.Length + CellGap * (SampleParty.Length - 1);
            Corner(
                row,
                new Vector2(0f, 1f),
                new Vector2(RowLeft, -24),
                new Vector2(width, CellHeight)
            );
            var status = row.gameObject.AddComponent<ExplorationPartyStatus>();
            var slots = new List<ExplorationPartyStatus.Slot>();
            for (int slot = 0; slot < SampleParty.Length; slot++)
            {
                var cell = Rect($"Member{slot + 1}", row);
                Place(
                    cell,
                    new Vector2(-width / 2f + CellWidth / 2f + slot * (CellWidth + CellGap), 0f),
                    new Vector2(CellWidth, CellHeight)
                );
                var plate = Rect("Plate", cell);
                Stretch(plate);
                GlassCardAssets.Plaque(plate, pattern: false, fill: GlassCardAssets.SceneFillColor);
                var art = ArtAssets.LoadTexture(
                    $"{CharacterArtFolder}/Battle{SampleParty[slot]}.aseprite"
                );
                var sprite = PixelActor(
                    cell,
                    "Sprite",
                    art,
                    new Vector2(-CellWidth / 2f + 50f, -CellHeight / 2f + 8f),
                    RowDot
                );
                var (fill, fillImage, number) = Gauge(
                    cell,
                    new Vector2(38f, -22f),
                    104f,
                    new Vector2(38f, 16f)
                );
                slots.Add(
                    new ExplorationPartyStatus.Slot
                    {
                        Root = cell.gameObject,
                        Sprite = sprite,
                        Fill = fill,
                        FillImage = fillImage,
                        Number = number,
                    }
                );
            }
            status.Slots = slots.ToArray();
            return status;
        }

        /// <summary>
        /// Bars under the members standing at <paramref name="feet"/> (the campfire's places, in
        /// its world), whose sprites the campfire paints itself.
        /// </summary>
        public static ExplorationPartyStatus BuildUnder(
            RectTransform world,
            IReadOnlyList<Vector2> feet
        )
        {
            var layer = Rect("PartyStatus", world);
            Stretch(layer);
            var status = layer.gameObject.AddComponent<ExplorationPartyStatus>();
            var slots = new List<ExplorationPartyStatus.Slot>();
            for (int slot = 0; slot < feet.Count; slot++)
            {
                var cell = Rect($"Member{slot + 1}", layer);
                Place(cell, feet[slot] + new Vector2(0f, -44f), new Vector2(200f, 70f));
                var (fill, fillImage, number) = Gauge(
                    cell,
                    new Vector2(0f, -14f),
                    170f,
                    new Vector2(0f, 18f)
                );
                slots.Add(
                    new ExplorationPartyStatus.Slot
                    {
                        Root = cell.gameObject,
                        Fill = fill,
                        FillImage = fillImage,
                        Number = number,
                    }
                );
            }
            status.Slots = slots.ToArray();
            return status;
        }

        // 細いHPのバー（暗い溝と、左から伸びる色）と、その上の数字。
        private static (RectTransform Fill, Image FillImage, TMP_Text Number) Gauge(
            RectTransform parent,
            Vector2 barCenter,
            float barWidth,
            Vector2 numberCenter
        )
        {
            var bar = Rect("Bar", parent);
            Place(bar, barCenter, new Vector2(barWidth, BarHeight));
            AddImage(bar, BarBack, false);
            var fill = Rect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            fill.pivot = new Vector2(0f, 0.5f);
            var fillImage = AddImage(fill, ExplorationPartyStatus.HealthyFill, false);
            var number = Label(
                parent,
                "Number",
                "297/356",
                NumberSize,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Place((RectTransform)number.transform, numberCenter, new Vector2(barWidth + 40f, 40f));
            return (fill, fillImage, number);
        }
    }
}
