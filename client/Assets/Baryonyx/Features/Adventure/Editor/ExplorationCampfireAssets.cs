using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.App.Editor;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Home.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// Builds the rest's campfire (<see cref="ExplorationCampfire"/>) into the exploration screen:
    /// the night camp (<see cref="CampfireSceneArt"/>) in the guide frame's window, the party and
    /// the logs standing on it where Home stands them, the book on the crate, and the three things
    /// to tap with their tags (the tent, the stump with the firewood, the book); the window that
    /// stands the party on pedestals to train one; and the window that lays out each member's card
    /// skills as battle cards to study one. Coordinates follow the 1920x1080 design.
    /// </summary>
    public static class ExplorationCampfireAssets
    {
        public const string BookPath = AdventureAssets.ArtFolder + "/CampfireBook.aseprite";
        private const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";

        // 編成の枠ごとの、見本の仲間（AdventureAssets の Party と同じ割り当て）。
        private static readonly string[] SampleParty = { "Toma", "Luka", "Aria", "Mina" };

        private static readonly Color Veil = new(0.012f, 0.02f, 0.04f, 0.72f);
        private static readonly Color Plate = new(0.03f, 0.05f, 0.1f, 0.92f);

        // 焚き火の絵の窓：野営地の絵（1920x1080）を縮めて、上下を少し切って見せる。
        private static readonly Vector2 CampWindow = new(1640, 940);
        private static readonly Vector2 CampView = new(1560, 800);
        private const float CampScale = 0.82f;

        // Homeの4人の足元と影（HomeScreenAssets の PartyPlaces に、野営地の持ち上げを足した位置）。
        private static readonly (Vector2 Feet, Vector2 Shadow, float Alpha)[] PartyPlaces =
        {
            (new Vector2(-175, -232), new Vector2(150, 24), 0.45f),
            (new Vector2(175, -232), new Vector2(150, 24), 0.45f),
            (new Vector2(-330, -281), new Vector2(164, 26), 0.5f),
            (new Vector2(330, -281), new Vector2(164, 26), 0.5f),
        };

        // 野営地の絵の中の、押せる物の範囲（絵の中央が原点）と、その札の位置（範囲の中央から）。
        private static readonly (
            string Name,
            string Label,
            Vector2 Center,
            Vector2 Size,
            Vector2 Tag
        )[] Spots =
        {
            (
                "Rest",
                AdventureTexts.RestChoice,
                new Vector2(-425, 200),
                new Vector2(300, 260),
                new Vector2(0, 190)
            ),
            (
                "Train",
                AdventureTexts.TrainChoice,
                new Vector2(505, 60),
                new Vector2(300, 320),
                new Vector2(0, 220)
            ),
            (
                "Study",
                AdventureTexts.StudyChoice,
                new Vector2(-190, 120),
                new Vector2(210, 230),
                new Vector2(0, -175)
            ),
        };

        // 木箱の上の本（32x21を4倍）の足元。
        private static readonly Vector2 BookFeet = new(-226, 136);
        private const float BookDot = 4f;

        // 鍛える：4人の台座の中心のx、台座の高さ、仲間のドット絵の倍率。
        private static readonly float[] TrainColumns = { -540, -180, 180, 540 };
        private const float PedestalY = -40f;
        private const float TrainDot = 4f;

        // 研鑽：列のx、仲間の絵の足元の高さ、カードの中心の高さと倍率と間隔。
        private static readonly float[] StudyColumns = { -630, -210, 210, 630 };
        private const float StudySpriteFeet = 170f;
        private const float StudyDot = 3f;
        private const float CardY = -40f;
        private const float CardScale = 0.74f;
        private const float CardGap = 14f;
        private static readonly Vector2 CardSize = new(210, 294);

        public static ExplorationCampfire Build(RectTransform parent)
        {
            ArtAssets.ImportTexture(CampfireSceneArt.OutputPath, FilterMode.Bilinear);
            ArtAssets.ImportTexture(BookPath, FilterMode.Point);
            var root = Rect("Campfire", parent);
            Stretch(root);
            var campfire = root.gameObject.AddComponent<ExplorationCampfire>();
            var elements = (CardElement[])Enum.GetValues(typeof(CardElement));
            campfire.CardFrames = elements
                .Select(element => (Texture)BattleInspectAssets.Art("CardFrame" + element))
                .ToArray();
            campfire.ElementIcons = elements
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : (Texture)BattleInspectAssets.Art("Element" + element)
                )
                .ToArray();
            BuildCamp(root, campfire);
            BuildTrain(root, campfire);
            BuildStudy(root, campfire);
            campfire.Camp.gameObject.SetActive(false);
            campfire.Train.gameObject.SetActive(false);
            campfire.Study.gameObject.SetActive(false);
            return campfire;
        }

        // 画面全体を覆う暗い幕と、真ん中の枠の窓。
        private static (RectTransform Panel, RectTransform Window) Panel(
            RectTransform root,
            string name,
            Vector2 size,
            string title
        )
        {
            var panel = Rect(name, root);
            Stretch(panel);
            AddImage(panel, Veil, true);
            var safe = SafeArea(panel);
            var window = Rect("Window", safe);
            Place(window, Vector2.zero, size);
            GuideFrameAssets.WindowFrame(window);
            var heading = Label(
                window,
                "Title",
                title,
                52,
                UiPalette.GoldLight,
                TextAlignmentOptions.Center
            );
            Place(
                (RectTransform)heading.transform,
                new Vector2(0, size.y / 2f - 56f),
                new Vector2(600, 72)
            );
            return (panel, window);
        }

        // --- Camp ----------------------------------------------------------------------------

        private static void BuildCamp(RectTransform root, ExplorationCampfire campfire)
        {
            var (panel, window) = Panel(root, "Camp", CampWindow, AdventureTexts.CampfireTitle);
            campfire.Camp = panel;
            var view = Rect("SceneView", window);
            Place(view, new Vector2(0, -36), CampView);
            view.gameObject.AddComponent<RectMask2D>();
            var world = Rect("World", view);
            Place(
                world,
                Vector2.zero,
                new Vector2(CampfireSceneArt.Width, CampfireSceneArt.Height)
            );
            world.localScale = Vector3.one * CampScale;

            var scene = Rect("Scene", world);
            Stretch(scene);
            campfire.Scene = scene.gameObject.AddComponent<RawImage>();
            campfire.Scene.texture = ArtAssets.LoadTexture(CampfireSceneArt.OutputPath);
            campfire.Scene.raycastTarget = false;

            var glows = new List<Graphic>();
            var glowLayer = Rect("Glows", world);
            Stretch(glowLayer);
            foreach (var spot in Spots)
            {
                var glow = Rect(spot.Name + "Glow", glowLayer);
                Place(glow, spot.Center, spot.Size * 0.95f);
                var image = SpriteImage(
                    glow,
                    UiArt.SoftSpotPath,
                    new Color(1f, 0.82f, 0.42f, 0.2f)
                );
                image.raycastTarget = false;
                glows.Add(image);
            }
            campfire.Glows = glows.ToArray();

            // 4人と薪は、Homeと同じ所に立てる。右の2人は焚き火の方を向く。
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var party = new List<RawImage>();
            var shadows = new List<Graphic>();
            for (int slot = 0; slot < PartyPlaces.Length; slot++)
            {
                var place = PartyPlaces[slot];
                shadows.Add(
                    Picture(
                        world,
                        $"PartyShadow{slot + 1}",
                        shadow,
                        place.Feet + new Vector2(0, -2),
                        place.Shadow,
                        place.Alpha
                    )
                );
                var art = ArtAssets.LoadTexture(
                    $"{CharacterArtFolder}/Battle{SampleParty[slot]}.aseprite"
                );
                var picture = PixelActor(world, $"PartyMember{slot + 1}", art, place.Feet, 4f);
                picture.color = ExplorationCampfire.NightTint;
                if (place.Feet.x > 0)
                    picture.rectTransform.localScale = new Vector3(-1, 1, 1);
                party.Add(picture);
            }
            campfire.Party = party.ToArray();
            campfire.PartyShadows = shadows.ToArray();
            var logs = Picture(
                world,
                "CampfireLogs",
                ArtAssets.LoadSprite(HomeScreenArt.CampfireLogsPath),
                new Vector2(0, -232),
                new Vector2(88, 24),
                1f
            );
            logs.color = Color.white;
            var book = PixelActor(
                world,
                "Book",
                ArtAssets.LoadTexture(BookPath),
                BookFeet,
                BookDot
            );
            book.color = ExplorationCampfire.NightTint;

            // 押せる物：絵の中の範囲と、その上の札。どちらを押しても同じ。
            var buttons = new List<Button>();
            foreach (var spot in Spots)
            {
                var area = Rect(spot.Name, world);
                Place(area, spot.Center, spot.Size);
                var hit = AddImage(area, Color.clear, true);
                var button = AddButton(area, hit);
                var tag = Rect("Tag", area);
                Place(tag, spot.Tag, new Vector2(260, 96));
                var (_, _, content) = GlassCardAssets.Choice(tag, pattern: false);
                // 札は押す物の範囲のボタンに任せ、札自身のボタンは使わない。
                UnityEngine.Object.DestroyImmediate(tag.GetComponent<Button>());
                var label = Label(
                    content,
                    "Label",
                    spot.Label,
                    52,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                );
                Stretch((RectTransform)label.transform);
                buttons.Add(button);
            }
            campfire.RestButton = buttons[0];
            campfire.TrainButton = buttons[1];
            campfire.StudyButton = buttons[2];
        }

        // --- Train ---------------------------------------------------------------------------

        private static void BuildTrain(RectTransform root, ExplorationCampfire campfire)
        {
            var (panel, window) = Panel(
                root,
                "Train",
                new Vector2(1500, 840),
                AdventureTexts.TrainChoice
            );
            campfire.Train = panel;
            var circle = ArtAssets.LoadSprite(UiArt.CirclePath);
            var slots = new List<Button>();
            var sprites = new List<RawImage>();
            var pedestals = new List<Graphic>();
            var names = new List<TMP_Text>();
            for (int slot = 0; slot < TrainColumns.Length; slot++)
            {
                var body = Rect($"Member{slot + 1}", window);
                Place(body, new Vector2(TrainColumns[slot], 0), new Vector2(320, 520));
                var hit = AddImage(body, Color.clear, true);
                slots.Add(AddButton(body, hit));
                var pedestal = Rect("Pedestal", body);
                Place(pedestal, new Vector2(0, PedestalY), new Vector2(280, 80));
                var ring = pedestal.gameObject.AddComponent<Image>();
                ring.sprite = circle;
                ring.raycastTarget = false;
                ring.color = new Color(0.5f, 0.7f, 1f, 0.3f);
                pedestals.Add(ring);
                var art = ArtAssets.LoadTexture(
                    $"{CharacterArtFolder}/Battle{SampleParty[slot]}.aseprite"
                );
                sprites.Add(
                    PixelActor(body, "Sprite", art, new Vector2(0, PedestalY - 10), TrainDot)
                );
                var name = Label(
                    body,
                    "Name",
                    SampleParty[slot],
                    40,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                );
                Place(
                    (RectTransform)name.transform,
                    new Vector2(0, PedestalY - 90),
                    new Vector2(300, 56)
                );
                names.Add(name);
            }
            campfire.TrainSlots = slots.ToArray();
            campfire.TrainSprites = sprites.ToArray();
            campfire.TrainPedestals = pedestals.ToArray();
            campfire.TrainNames = names.ToArray();

            var tag = Rect("Tag", window);
            Place(tag, new Vector2(TrainColumns[0], 270), new Vector2(360, 68));
            AddImage(tag, Plate, false);
            var tagLabel = Label(
                tag,
                "Label",
                AdventureTexts.TrainEffect,
                38,
                UiPalette.GoldLight,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)tagLabel.transform);
            tag.gameObject.SetActive(false);
            campfire.TrainTag = tag;

            (campfire.TrainConfirm, campfire.TrainBack) = Buttons(
                window,
                AdventureTexts.TrainChoice,
                -330f
            );
        }

        // --- Study ---------------------------------------------------------------------------

        private static void BuildStudy(RectTransform root, ExplorationCampfire campfire)
        {
            var (panel, window) = Panel(
                root,
                "Study",
                new Vector2(1720, 920),
                AdventureTexts.StudyChoice
            );
            campfire.Study = panel;
            var sprites = new List<RawImage>();
            var names = new List<TMP_Text>();
            var cards = new List<Button>();
            var faces = new List<BattleInspectCardView>();
            var rims = new List<Graphic>();
            var shown = CardSize * CardScale;
            for (int slot = 0; slot < StudyColumns.Length; slot++)
            {
                float x = StudyColumns[slot];
                var art = ArtAssets.LoadTexture(
                    $"{CharacterArtFolder}/Battle{SampleParty[slot]}.aseprite"
                );
                sprites.Add(
                    PixelActor(
                        window,
                        $"Member{slot + 1}",
                        art,
                        new Vector2(x, StudySpriteFeet),
                        StudyDot
                    )
                );
                var name = Label(
                    window,
                    $"Name{slot + 1}",
                    SampleParty[slot],
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                );
                Place(
                    (RectTransform)name.transform,
                    new Vector2(x, StudySpriteFeet - 26),
                    new Vector2(300, 48)
                );
                names.Add(name);
                for (int k = 0; k < ExplorationCampfire.CardsPerMember; k++)
                {
                    float offset =
                        (k - (ExplorationCampfire.CardsPerMember - 1) / 2f) * (shown.x + CardGap);
                    var body = Rect($"Card{slot + 1}_{k + 1}", window);
                    Place(body, new Vector2(x + offset, CardY), shown);
                    var rim = Rect("Rim", body);
                    Place(rim, Vector2.zero, shown + new Vector2(20, 20));
                    var glow = SpriteImage(
                        rim,
                        UiArt.SoftSpotPath,
                        new Color(1f, 0.82f, 0.42f, 0.95f)
                    );
                    glow.raycastTarget = false;
                    rim.gameObject.SetActive(false);
                    rims.Add(glow);
                    var hit = AddImage(body, Color.clear, true);
                    cards.Add(AddButton(body, hit));
                    var instance = InstantiatePrefab(
                        BattleInspectAssets.CardPrefabPath,
                        "Face",
                        body
                    );
                    var face = (RectTransform)instance.transform;
                    face.anchorMin = face.anchorMax = face.pivot = new Vector2(0.5f, 0.5f);
                    face.anchoredPosition = Vector2.zero;
                    face.localScale = Vector3.one * CardScale;
                    foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                        graphic.raycastTarget = false;
                    faces.Add(instance.GetComponent<BattleInspectCardView>());
                }
            }
            campfire.StudySprites = sprites.ToArray();
            campfire.StudyNames = names.ToArray();
            campfire.StudyCards = cards.ToArray();
            campfire.StudyFaces = faces.ToArray();
            campfire.StudyRims = rims.ToArray();

            var line = Rect("Line", window);
            Place(line, new Vector2(0, -250), new Vector2(1480, 68));
            AddImage(line, Plate, false);
            campfire.StudyLine = Label(
                line,
                "Label",
                "",
                38,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)campfire.StudyLine.transform);

            (campfire.StudyConfirm, campfire.StudyBack) = Buttons(
                window,
                AdventureTexts.StudyChoice,
                -360f
            );
        }

        // 決めるボタン（中央）と、もどるボタン（その右）。決めるボタンは、選ぶまで押せない。
        private static (Button Confirm, Button Back) Buttons(
            RectTransform window,
            string confirmText,
            float y
        )
        {
            var confirm = Rect("Confirm", window);
            Place(confirm, new Vector2(-150, y), new Vector2(480, 100));
            var (confirmButton, _, confirmContent) = GlassCardAssets.Choice(
                confirm,
                pattern: false
            );
            var confirmLabel = Label(
                confirmContent,
                "Label",
                confirmText,
                46,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)confirmLabel.transform);
            confirmButton.interactable = false;
            var back = Rect("Back", window);
            Place(back, new Vector2(270, y), new Vector2(280, 100));
            var (backButton, _, backContent) = GlassCardAssets.Choice(back, pattern: false);
            var backLabel = Label(
                backContent,
                "Label",
                AdventureDialogs.BackChoice,
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)backLabel.transform);
            return (confirmButton, backButton);
        }
    }
}
