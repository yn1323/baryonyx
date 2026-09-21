using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Wireframe.Editor
{
    public static partial class WireframeScreenAssets
    {
        private const string ArtPath = "Assets/Baryonyx/Features/Wireframe/UI/Art/";
        private static readonly Color Gold = new(.57f, .40f, .19f);
        private static readonly Color Muted = new(.43f, .49f, .43f);
        private static Texture2D actors;
        private static Texture2D forest;
        private static Texture2D mine;
        private static Texture2D departure;
        private static Sprite frame;
        private static Sprite panelFrame;
        private static Sprite equipmentFrame;

        private static void PrepareArt()
        {
            actors = ImportArt("Adventurers.png");
            forest = ImportArt("Forest.png");
            mine = ImportArt("Mine.png");
            departure = ImportArt("Departure.png");
            frame = CreateFrame("ButtonFrame", Accent, Accent);
            panelFrame = CreateFrame(
                "PanelFrame",
                new Color(.90f, .92f, .86f),
                new Color(.90f, .92f, .86f)
            );
            equipmentFrame = CreateFrame(
                "EquipmentFrame",
                new Color(.08f, .12f, .17f),
                new Color(.65f, .49f, .23f)
            );
        }

        private static Texture2D ImportArt(string file)
        {
            string path = ArtPath + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Required UI artwork is missing: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The frame is authored on a 24px grid. Only its flat centre stretches.
        private static Sprite CreateFrame(string name, Color fill, Color edge)
        {
            string path = ArtPath + name + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(24, 24, TextureFormat.RGBA32, false) { name = name };
                AssetDatabase.CreateAsset(texture, path);
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                bool cut = (x < 2 || x > 21) && (y < 2 || y > 21);
                Color color = fill;
                float dx = Mathf.Max(5 - x, x - 18, 0);
                float dy = Mathf.Max(5 - y, y - 18, 0);
                cut = dx * dx + dy * dy > 25;
                bool border = x < 2 || x > 21 || y < 2 || y > 21;
                texture.SetPixel(
                    x,
                    y,
                    cut ? Color.clear
                        : border ? edge
                        : color
                );
            }
            texture.Apply();
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, 24, 24),
                    new Vector2(.5f, .5f),
                    100,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(6, 6, 6, 6)
                );
                sprite.name = name;
                AssetDatabase.AddObjectToAsset(sprite, texture);
            }
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssetIfDirty(texture);
            return sprite;
        }

        private static void Frame(RectTransform rect, bool button = false)
        {
            Frame(rect, button ? frame : panelFrame);
        }

        private static void Frame(RectTransform rect, Sprite sprite)
        {
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
                image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
        }

        private static UnityEngine.UI.RawImage Art(
            string name,
            RectTransform parent,
            Texture2D texture,
            Rect uv
        )
        {
            var rect = Rect(name, parent);
            Stretch(rect);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
            return image;
        }

        private static UnityEngine.UI.RawImage Actor(
            string name,
            RectTransform parent,
            int index,
            Vector2 anchor,
            Vector2 size
        )
        {
            var image = Art(name, parent, actors, WireframeArt.ActorUv(index));
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return image;
        }

        private static RectTransform Illustration(
            string name,
            RectTransform parent,
            Texture2D texture,
            float height,
            Rect uv
        )
        {
            var rect = Rect(name, parent);
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            Art(name + "Art", rect, texture, uv);
            return rect;
        }

        private static TMP_Text OverlayLabel(
            string name,
            RectTransform parent,
            string text,
            float size,
            Vector2 min,
            Vector2 max
        )
        {
            var label = Label(name, parent, text, size, 0);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(10, 4);
            rect.offsetMax = new Vector2(-10, -4);
            label.margin = new Vector4(2, 2, 2, 2);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectColor = new Color(
                0,
                0,
                0,
                .85f
            );
            return label;
        }

        private static void Eyebrow(string name, RectTransform page, string title)
        {
            var label = Label(name, page, title, 10, 18);
            label.color = Gold;
            label.characterSpacing = 3;
        }

        private static void PartyLandscape(RectTransform parent, string name, float height)
        {
            var landscape = Illustration(name, parent, departure, height, new Rect(0, 0, 1, 1));
            for (int i = 0; i < 4; i++)
                Actor(
                    name + "Actor" + i,
                    landscape,
                    i,
                    new Vector2(.17f + i * .22f, i % 2 == 0 ? .08f : .12f),
                    new Vector2(72, 110)
                );
            OverlayLabel(name + "Caption", landscape, "", 15, new Vector2(0, .76f), Vector2.one);
        }

        private static void DestinationCard(
            RectTransform page,
            int index,
            Texture2D texture,
            string title,
            string hint
        )
        {
            var button = Button("Destination" + index, page, title + "\n" + hint, 202, 18, true);
            var art = Art(
                "DestinationArt" + index,
                (RectTransform)button.transform,
                texture,
                new Rect(0, .28f, 1, .63f)
            );
            art.transform.SetAsFirstSibling();
            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontStyle = FontStyles.Bold;
            var rect = (RectTransform)label.transform;
            rect.anchorMax = new Vector2(1, .39f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var plate = Rect("DestinationPlate" + index, button.transform);
            plate.anchorMin = Vector2.zero;
            plate.anchorMax = new Vector2(1, .4f);
            plate.offsetMin = plate.offsetMax = Vector2.zero;
            Image(plate, new Color(.035f, .07f, .075f, .94f), false);
            plate.SetSiblingIndex(1);
        }

        private static void BuildExploration(RectTransform page)
        {
            Label("ExplorePlace", page, "", 20, 38);
            var map = Illustration("ExploreMap", page, forest, 350, new Rect(0, 0, 1, 1));
            map.GetComponent<UnityEngine.UI.LayoutElement>().flexibleHeight = 1;
            var party = Rect("ExplorePartyVisual", map);
            page.GetComponentInParent<WireframeArt>().ExplorationParty = party;
            party.anchorMin = party.anchorMax = new Vector2(.46f, .12f);
            party.sizeDelta = new Vector2(180, 80);
            party.pivot = new Vector2(.5f, 0);
            for (int i = 0; i < 4; i++)
                Actor(
                    "ExploreActor" + i,
                    party,
                    i,
                    new Vector2(.12f + i * .25f, 0),
                    new Vector2(43, 70)
                );
            MapExit("ExploreDoor", map, new Vector2(.09f, .51f), new Vector2(.56f, .73f));
            MapExit("ExploreChest", map, new Vector2(.59f, .48f), new Vector2(.98f, .70f));
            Label("ExplorePartyText", page, "", 12, 0).gameObject.SetActive(false);
            Label("ExploreNotice", page, "", 13, 32);
            var manage = Row(page, "ExploreManage");
            Button("ExploreParty", manage, "仲間・編成");
            Button("ExploreEquipment", manage, "装備");
            var menu = Row(page, "ExploreMenu");
            Button("ExploreSettings", menu, "設定", 48, 12);
            Button("ExploreEnd", menu, "帰還", 48, 12);
        }

        private static void MapExit(string name, RectTransform map, Vector2 min, Vector2 max)
        {
            var button = Button(name, map, "", 54, 12, true);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void PortraitSlots(RectTransform page, string prefix)
        {
            for (int rowIndex = 0; rowIndex < 1; rowIndex++)
            {
                var row = Row(page, prefix + "Row" + rowIndex);
                for (int i = 0; i < 4; i++)
                {
                    PortraitButton(prefix + i, row, i, 112);
                    var b = row.Find(prefix + i);
                    var image = b.GetComponentInChildren<UnityEngine.UI.RawImage>();
                    image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(
                        .5f,
                        .24f
                    );
                    image.rectTransform.anchoredPosition = Vector2.zero;
                    image.rectTransform.sizeDelta = new Vector2(49, 72);
                    var label = b.GetComponentInChildren<TMP_Text>();
                    label.fontSize = 12;
                    label.margin = new Vector4(0, 0, 0, 5);
                    label.alignment = TextAlignmentOptions.Bottom;
                }
            }
        }

        private static void PortraitButton(string name, RectTransform parent, int actor, int height)
        {
            var button = Button(name, parent, "", height, 15);
            Actor(
                name + "Art",
                (RectTransform)button.transform,
                actor,
                new Vector2(0, 0),
                new Vector2(40, 58)
            ).rectTransform.anchoredPosition = new Vector2(28, 3);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.margin = new Vector4(56, 4, 8, 4);
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static void CharacterFeature(
            RectTransform page,
            string artName,
            string textName,
            float height,
            bool equipmentStyle = false
        )
        {
            var card = Rect(textName + "Card", page);
            Frame(card, equipmentStyle ? equipmentFrame : panelFrame);
            card.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var size = card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            Actor(artName, card, 0, new Vector2(.15f, .13f), new Vector2(88, 126));
            var label = Label(textName, card, "", 14, 0);
            Stretch((RectTransform)label.transform);
            label.margin = new Vector4(102, 12, 12, 12);
            if (equipmentStyle)
                label.color = new Color(.96f, .94f, .86f);
        }

        private static void DecorateBattle(RectTransform page)
        {
            var field = (RectTransform)page.Find("Battlefield");
            field.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            Art("BattleBackdrop", field, forest, new Rect(0, .04f, 1, .69f))
                .transform.SetAsFirstSibling();
            for (int i = 0; i < 4; i++)
            {
                var button = field.Find("Ally" + i).GetComponent<UnityEngine.UI.Button>();
                button.image.color = new Color(.45f, .64f, .59f, .18f);
                Actor(
                    "BattleAllyArt" + i,
                    (RectTransform)button.transform,
                    i,
                    new Vector2(.5f, .08f),
                    new Vector2(59, 87)
                );
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 10;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.margin = new Vector4(50, 0, 3, 0);
                label.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectColor = Color.black;
            }
            for (int i = 0; i < 3; i++)
            {
                var button = field.Find("Enemy" + i).GetComponent<UnityEngine.UI.Button>();
                var box = Rect("EnemyPortraitBox" + i, button.transform);
                Stretch(box);
                box.offsetMin = new Vector2(4, 27);
                box.offsetMax = new Vector2(-4, -2);
                var art = Actor(
                    "BattleEnemyArt" + i,
                    box,
                    i == 1 ? 6 : 5,
                    new Vector2(.5f, .16f),
                    new Vector2(58, 58)
                );
                art.rectTransform.pivot = new Vector2(.5f, .5f);
                var aspect = art.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
                aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = .75f;
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 10;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.Bottom;
                label.margin = new Vector4(2, 0, 2, 6);
                label.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectColor = Color.black;
                label.transform.SetAsLastSibling();
            }
        }

        private static void GoalCard(string name, RectTransform page, string title, bool weekly)
        {
            var card = Rect(name + "Card", page);
            Frame(card);
            Vertical(card);
            var group = card.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            group.padding = new RectOffset(14, 14, 12, 14);
            Label(name + "Eyebrow", card, title, 11, 18).color = Gold;
            Label(name, card, "", 15, 68);
            ProgressTrack(name + "Progress", card, 0, 6);
        }

        private static void VolumeTrack(RectTransform page) =>
            ProgressTrack("VolumeProgress", page, .5f, 6);

        private static void ProgressTrack(
            string name,
            RectTransform page,
            float value,
            float height
        )
        {
            var track = Rect(name, page);
            track.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().minHeight = height;
            Image(track, new Color(.04f, .075f, .08f), false);
            var fill = Rect(name + "Fill", track);
            Stretch(fill);
            fill.anchorMax = new Vector2(value, 1);
            Image(fill, Gold, false);
        }

        private static void PixelEmblem(RectTransform parent, string name, int variant)
        {
            var icon = Rect(name, parent);
            icon.anchorMin = icon.anchorMax = new Vector2(0, .5f);
            icon.sizeDelta = new Vector2(32, 32);
            icon.anchoredPosition = new Vector2(28, 0);
            // A small geometric sword, authored as UI geometry rather than a bitmap illustration.
            for (int i = 0; i < 6; i++)
            {
                var part = Rect(name + "Pixel" + i, icon);
                part.sizeDelta = i == 4 ? new Vector2(18, 4) : new Vector2(i == 5 ? 4 : 6, 5);
                part.anchoredPosition = new Vector2(0, 13 - i * 5);
                Image(
                    part,
                    i < 4
                        ? (variant == 2 ? Gold : new Color(.86f, .83f, .72f))
                        : new Color(.49f, .32f, .19f),
                    false
                );
            }
        }

        private static void PopupArtwork(RectTransform content)
        {
            var art = Illustration(
                "PopupIllustration",
                content,
                forest,
                120,
                new Rect(0, .22f, 1, .55f)
            );
            Actor("PopupActor", art, 0, new Vector2(.5f, .04f), new Vector2(75, 110));
        }

        private static void DecorateNavigation(RectTransform nav)
        {
            foreach (var button in nav.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                var text = button.GetComponentInChildren<TMP_Text>();
                text.fontSize = 14;
                text.fontStyle = FontStyles.Bold;
            }
        }
    }
}
