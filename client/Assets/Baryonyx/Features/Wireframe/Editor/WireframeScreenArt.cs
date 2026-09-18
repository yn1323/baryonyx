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
        private static readonly Color Gold = new(.82f, .68f, .40f);
        private static readonly Color Muted = new(.60f, .69f, .66f);
        private static Texture2D actors;
        private static Texture2D forest;
        private static Texture2D mine;
        private static Sprite frame;
        private static Sprite panelFrame;

        private static void PrepareArt()
        {
            actors = ImportArt("Adventurers.png");
            forest = ImportArt("Forest.png");
            mine = ImportArt("Mine.png");
            frame = CreateFrame("ButtonFrame", new Color(.13f, .20f, .20f), Gold);
            panelFrame = CreateFrame(
                "PanelFrame",
                new Color(.09f, .14f, .15f),
                new Color(.34f, .42f, .38f)
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
                int distance = Mathf.Min(x, y, 23 - x, 23 - y);
                bool cut = (x < 2 || x > 21) && (y < 2 || y > 21);
                Color color = distance == 0 ? new Color(.025f, .04f, .045f) : fill;
                if (distance == 1)
                    color = edge;
                if (distance == 2)
                    color = Color.Lerp(edge, fill, .7f);
                if (distance == 3 && y > 12)
                    color = Color.Lerp(fill, Color.white, .09f);
                texture.SetPixel(x, y, cut ? Color.clear : color);
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
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
                image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = button ? frame : panelFrame;
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
            var landscape = Illustration(name, parent, forest, height, new Rect(0, .20f, 1, .60f));
            for (int i = 0; i < 4; i++)
                Actor(
                    name + "Actor" + i,
                    landscape,
                    i,
                    new Vector2(.18f + i * .21f, .14f),
                    new Vector2(62, 94)
                );
            OverlayLabel(
                name + "Caption",
                landscape,
                "小さな一歩が、冒険になる。",
                15,
                new Vector2(0, .76f),
                Vector2.one
            );
        }

        private static void BuildFinishedPage(
            WireScreen screen,
            RectTransform page,
            WireframeLayout layout
        )
        {
            switch (screen)
            {
                case WireScreen.Home:
                    Label("HomeEyebrow", page, "WALK INTO AN ADVENTURE", 9, 12).color = Gold;
                    Label("HomeBrand", page, "てくてくダンジョン", 24, 32);
                    PartyLandscape(page, "HomeLandscape", 112);
                    Label("HomeParty", page, "", 11, 18).alignment = TextAlignmentOptions.Center;
                    Button("HomeAdventure", page, "冒険へ出かける   →", 54, 19, true);
                    Card("HomeRunes", page, "", 32, 17);
                    Card("HomeProgress", page, "", 70, 14);
                    var home = Row(page, "HomeActions");
                    Button("HomeEquipment", home, "装備を整える", 48, 14);
                    Button("HomeSettings", home, "設定", 48, 14);
                    break;
                case WireScreen.Destination:
                    Eyebrow("DestinationEyebrow", page, "CHOOSE YOUR JOURNEY");
                    Label("DestinationIntro", page, "今日は、どこへ向かおう？", 22, 46);
                    DestinationCard(page, 0, forest, "木漏れ日の森", "獣の気配 / 武器を探す");
                    DestinationCard(page, 1, mine, "古い坑道", "硬い敵 / 宝箱を探す");
                    Label("DestinationHint", page, "探索の続きから、いつでも。", 13, 42).color =
                        Muted;
                    break;
                case WireScreen.Explore:
                    BuildExploration(page);
                    break;
                case WireScreen.Party:
                    Eyebrow("PartyEyebrow", page, "YOUR COMPANIONS");
                    Label("PartyHint", page, "枠を選んで、仲間を入れ替える", 16, 36);
                    PortraitSlots(page, "PartySlot");
                    CharacterFeature(page, "PartyPortrait", "PartyDetails", 138);
                    Eyebrow("RosterEyebrow", page, "仲間の一覧");
                    for (int i = 0; i < 5; i++)
                        PortraitButton("Character" + i, page, i, 62);
                    Button("PartyConfirm", page, "この仲間を編成", primary: true);
                    Button("PartyEquipment", page, "選択した仲間の装備へ");
                    break;
                case WireScreen.Equipment:
                    Eyebrow("EquipmentEyebrow", page, "READY FOR THE ROAD");
                    PortraitSlots(page, "EquipmentSlot");
                    CharacterFeature(page, "EquipmentPortrait", "EquipmentCompare", 184);
                    Eyebrow("WeaponEyebrow", page, "所持している武器");
                    for (int i = 0; i < 3; i++)
                    {
                        var b = Button("Equipment" + i, page, "武器", 64, 16);
                        PixelEmblem(b.transform as RectTransform, "WeaponIcon" + i, i);
                        b.GetComponentInChildren<TMP_Text>().margin = new Vector4(56, 6, 12, 6);
                        b.GetComponentInChildren<TMP_Text>().alignment =
                            TextAlignmentOptions.MidlineLeft;
                    }
                    Button("EquipmentConfirm", page, "この武器に変更", primary: true);
                    Label(
                        "EquipmentHint",
                        page,
                        "武器の性能は体験用のサンプルです。",
                        12,
                        36
                    ).color = Muted;
                    break;
                case WireScreen.Battle:
                    BuildBattle(page, layout);
                    DecorateBattle(page);
                    break;
                case WireScreen.Defeat:
                    Eyebrow("DefeatEyebrow", page, "TAKE A BREATH");
                    var rest = Illustration(
                        "RestLandscape",
                        page,
                        forest,
                        138,
                        new Rect(0, .1f, 1, .55f)
                    );
                    rest.GetComponentInChildren<UnityEngine.UI.RawImage>().color = new Color(
                        .48f,
                        .56f,
                        .61f
                    );
                    Actor("RestActor", rest, 0, new Vector2(.5f, .08f), new Vector2(75, 112));
                    Label("DefeatTitle", page, "もう一度、挑める。", 26, 44);
                    Card("DefeatSummary", page, "", 106, 14);
                    Button("DefeatRetry", page, "無料で再戦する", primary: true);
                    Button("DefeatParty", page, "仲間の編成を見直す");
                    Button("DefeatPath", page, "別の道を探す");
                    Button("DefeatEnd", page, "ホームへ帰る");
                    Button("DefeatRevive", page, "ルーンで復活", fontSize: 14);
                    break;
                case WireScreen.Result:
                    Eyebrow("ResultEyebrow", page, "JOURNEY COMPLETE");
                    PartyLandscape(page, "ResultLandscape", 146);
                    var cleared = Label("ResultTitle", page, "冒険のひと区切り", 25, 48);
                    cleared.color = Gold;
                    Card("ResultSummary", page, "", 224, 16);
                    Button("ResultHome", page, "ホームへ帰る", primary: true);
                    break;
                case WireScreen.Goals:
                    Eyebrow("GoalsEyebrow", page, "EVERY STEP COUNTS");
                    Label("GoalsTitle", page, "日々の歩みを、冒険の力に。", 20, 48);
                    GoalCard("GoalsDaily", page, "TODAY", false);
                    GoalCard("GoalsWeekly", page, "THIS WEEK", true);
                    Button("GoalsEdit", page, "目標を設定・変更する", primary: true);
                    Button("GoalsHistory", page, "これまでの歩み");
                    Button("GoalsCancel", page, "変更予約を取り消す");
                    Button("GoalsClear", page, "目標を外す", fontSize: 14);
                    break;
                case WireScreen.Settings:
                    Eyebrow("SettingsEyebrow", page, "MAKE YOURSELF AT HOME");
                    Label("SettingsIntro", page, "自分のペースで、冒険しよう。", 21, 64);
                    Card(
                        "SettingsGuide",
                        page,
                        "音と運動データ\nいつでもここから見直せます。",
                        82,
                        16
                    );
                    Button("SettingsVolume", page, "音量 50%", 70);
                    VolumeTrack(page);
                    Button("SettingsData", page, "運動データの連携案内   →", 70);
                    Label(
                        "SettingsNote",
                        page,
                        "この体験版は表示と操作を確認するためのものです。設定は終了すると元に戻ります。",
                        13,
                        110
                    ).color = Muted;
                    break;
            }
        }

        private static void DestinationCard(
            RectTransform page,
            int index,
            Texture2D texture,
            string title,
            string hint
        )
        {
            var button = Button("Destination" + index, page, title + "\n" + hint, 192, 18, true);
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
            Eyebrow("ExploreEyebrow", page, "FOLLOW THE PATH");
            Label("ExplorePlace", page, "", 20, 38);
            var map = Illustration("ExploreMap", page, forest, 258, new Rect(0, 0, 1, 1));
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
            Label("ExplorePartyText", page, "", 12, 24).alignment = TextAlignmentOptions.Center;
            Card("ExploreNotice", page, "", 54, 14);
            var manage = Row(page, "ExploreManage");
            Button("ExploreParty", manage, "仲間・編成");
            Button("ExploreEquipment", manage, "装備");
            var menu = Row(page, "ExploreMenu");
            Button("ExploreSettings", menu, "設定", fontSize: 14);
            Button("ExploreEnd", menu, "冒険を終える", fontSize: 14);
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
            for (int rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                var row = Row(page, prefix + "Row" + rowIndex);
                for (int i = 0; i < 2; i++)
                    PortraitButton(prefix + (rowIndex * 2 + i), row, rowIndex * 2 + i, 64);
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
            float height
        )
        {
            var card = Rect(textName + "Card", page);
            Frame(card);
            card.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var size = card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            Actor(artName, card, 0, new Vector2(.15f, .13f), new Vector2(88, 126));
            var label = Label(textName, card, "", 14, 0);
            Stretch((RectTransform)label.transform);
            label.margin = new Vector4(102, 12, 12, 12);
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
                    new Vector2(.38f, 0),
                    new Vector2(34, 48)
                );
                var label = button.GetComponentInChildren<TMP_Text>();
                label.fontSize = 10;
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
            Label(name, card, "", 15, 112);
            ProgressTrack(name + "Progress", card, weekly ? .728f : .764f, 6);
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
                    i < 4 ? (variant == 2 ? Gold : Ink) : new Color(.49f, .32f, .19f),
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
