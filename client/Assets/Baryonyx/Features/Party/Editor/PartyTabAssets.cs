using System.Collections.Generic;
using System.Linq;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Party.Editor
{
    /// <summary>
    /// Bakes the people's tabs of the formation's per-person screens (skills, equipment): a
    /// square tab per mock character with the face and the name, in a strip that scrolls
    /// sideways, with a divider after the party.
    /// </summary>
    public static class PartyTabAssets
    {
        // 64×64の戦闘のドット絵のうち、タブに出す顔のあたり（ドット、絵の左上が原点）。2倍で出す。
        public static readonly RectInt Face = new(18, 14, 28, 28);

        public const float TabSize = 128f;
        private const float FaceDot = 2f;
        private const float DividerWidth = 4f;
        private static readonly Color DividerColor = new(0.52f, 0.59f, 0.68f, 0.7f);
        private static readonly Color BadgeColor = new(0.02f, 0.03f, 0.06f, 0.85f);

        /// <summary>
        /// Builds the strip across the top of <paramref name="panel"/>, <paramref name="top"/>
        /// below its top edge.
        /// </summary>
        public static PartyTabStrip Build(
            RectTransform panel,
            IReadOnlyList<PartyMember> people,
            int partyCount,
            float top
        )
        {
            var viewport = Rect("People", panel);
            viewport.anchorMin = new Vector2(0, 1);
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 1);
            viewport.offsetMin = new Vector2(24, -(top + TabSize));
            viewport.offsetMax = new Vector2(-24, -top);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);

            var content = Rect("Tabs", viewport);
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var group = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 10;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            var strip = new PartyTabStrip
            {
                Tabs = people.Select(member => BuildTab(content, member)).ToArray(),
            };

            // パーティとほかの仲間の間の区切り。
            var line = Rect("Divider", content);
            var size = line.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = DividerWidth;
            var stroke = Rect("Line", line);
            Stretch(stroke);
            stroke.offsetMin = new Vector2(0, 24);
            stroke.offsetMax = new Vector2(0, -24);
            AddImage(stroke, DividerColor, false);
            line.SetSiblingIndex(partyCount);
            strip.Divider = line.gameObject;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            strip.Scroll = scroll;
            return strip;
        }

        // A square tab, as large as a touch needs: the face above, the name below.
        private static PartyTab BuildTab(RectTransform content, PartyMember member)
        {
            var tab = Rect("Person_" + member.Id, content);
            var width = tab.gameObject.AddComponent<LayoutElement>();
            width.minWidth = width.preferredWidth = TabSize;
            var widget = new PartyTab
            {
                Id = member.Id,
                Button = Guide.AddButton(tab, Guide.Frame(tab, Guide.FramePath, Color.white)),
            };

            // Under the face and the name, so the gold frame's fill does not cover them.
            var selected = Rect("Selected", tab);
            Stretch(selected);
            Guide.Frame(selected, Guide.FrameSelectedPath, Color.white).raycastTarget = false;
            selected.gameObject.SetActive(false);
            widget.Selected = selected.gameObject;

            var face = Rect("Face", tab);
            Corner(
                face,
                new Vector2(0.5f, 1),
                new Vector2(0, -14),
                new Vector2(Face.width, Face.height) * FaceDot + Vector2.one * 4
            );
            AddImage(face, BadgeColor, false);
            var picture = Rect("Picture", face);
            Place(picture, Vector2.zero, new Vector2(Face.width, Face.height) * FaceDot);
            var image = picture.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            PaintFace(image, member);
            picture.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = FaceDot;

            var name = Label(
                tab,
                "Name",
                member.Name,
                26,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Guide.Band((RectTransform)name.transform, top: false, 12, 34, 8, 8);
            Guide.Shrink(name, 18);
            return widget;
        }

        // The face part of the 64x64 sprite, with the member's tint and facing.
        private static void PaintFace(RawImage image, PartyMember member)
        {
            image.texture = member.Art;
            image.color = member.Tint;
            if (member.Art == null)
                return;
            float width = member.Art.width;
            float height = member.Art.height;
            var uv = new Rect(
                Face.x / width,
                1f - Face.yMax / height,
                Face.width / width,
                Face.height / height
            );
            if (member.Flip)
                uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            image.uvRect = uv;
        }
    }
}
