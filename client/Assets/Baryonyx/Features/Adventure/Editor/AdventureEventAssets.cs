using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using Baryonyx.WorldMap;
using Baryonyx.WorldMap.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Panels = Baryonyx.WorldMap.Editor.WorldEventPanelAssets;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// Builds the rooms that open a window in the exploration screen, the same event window as
    /// the map's squares (<see cref="WorldMapEventWindowAssets"/>) with the destination's battle
    /// backdrop as its scenery: the merchant's wares (<see cref="AdventurePeddlerPanel"/>), the
    /// shady chest's two answers (<see cref="AdventureShadyChestPanel"/>) and the altar's hall
    /// with the party's cards (<see cref="AdventureAltarPanel"/>); and the awakening scene after
    /// the altar (<see cref="AdventureAwakening"/>), the same as the workshop's. Places in a panel
    /// are from the window's bottom middle, in the design's pixels.
    /// </summary>
    public static class AdventureEventAssets
    {
        public const string AltarBackdropPath = AdventureAssets.ArtFolder + "/AltarBackdrop.png";
        private const string StagesFolder = "Assets/Baryonyx/Shared/Art/Stages";
        private const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";
        private static readonly string[] SampleParty = { "Toma", "Luka", "Aria", "Mina" };

        // 行商人の品物の札：右に2列3段。左に絵、右に名前・分類と☆・値段、左上に品質。
        private static readonly Vector2 GoodSize = new(380f, 168f);
        private static readonly float[] GoodColumns = { 175f, 575f };
        private static readonly float[] GoodRows = { 560f, 375f, 190f };

        // 仲間の札（編成の冒険者の札と同じ形）を、窓の下に横一列に並べる。
        private static readonly Vector2 MemberSize = new(360f, 176f);
        private static readonly float[] MemberColumns = { -570f, -190f, 190f, 570f };
        private const float MemberY = 120f;
        private const float MemberDot = 4f;

        // 選んだ仲間の固有スキルのカードを、仲間の札の上に並べる。
        private const float SkillY = 520f;
        private const float SkillGap = 340f;
        private const float SkillScale = 1.25f;

        // 覚醒の場面の、上に浮かぶ今のカード。
        private const float OriginTop = 22f;
        private const float OriginScale = 0.55f;

        /// <summary>The battle backdrop of every destination, for the event window's scenery.</summary>
        public static ExplorationGround[] Backdrops() =>
            AdventureCatalog
                .Areas.Select(area => (area.Id, Path: BackdropPath(area.Stage)))
                .Where(entry => entry.Path != null)
                .Select(entry => new ExplorationGround
                {
                    DestinationId = entry.Id,
                    Texture = ArtAssets.LoadTexture(entry.Path),
                })
                .ToArray();

        // 戦闘の舞台の奥の景色（*Backdrop.aseprite）。
        private static string BackdropPath(BattleStage stage)
        {
            string folder = $"{StagesFolder}/{stage}";
            if (!Directory.Exists(folder))
                return null;
            return Directory
                .GetFiles(folder, "*Backdrop.aseprite")
                .Select(path => path.Replace('\\', '/'))
                .FirstOrDefault();
        }

        /// <summary>Adds the event window and the awakening scene to the exploration screen.</summary>
        public static void Build(RectTransform root, ExplorationView view)
        {
            ArtAssets.ImportTexture(AltarBackdropPath, FilterMode.Bilinear);
            view.Backdrops = Backdrops();
            string scenery =
                BackdropPath(AdventureCatalog.StageOf(AdventureCatalog.ForestRuins))
                ?? WorldMapEventWindowAssets.SceneryPath;
            var elements = (CardElement[])Enum.GetValues(typeof(CardElement));
            var frames = elements
                .Select(element => (Texture)BattleInspectAssets.Art("CardFrame" + element))
                .ToArray();
            var icons = elements
                .Select(element => AttributeIconArt.TextureOf(element))
                .ToArray();
            AdventurePeddlerPanel peddler = null;
            AdventureShadyChestPanel shady = null;
            AdventureAltarPanel altar = null;
            view.Events = WorldMapEventWindowAssets.Build(
                root,
                scenery,
                stage =>
                {
                    peddler = Peddler(stage);
                    shady = Shady(stage);
                    altar = Altar(stage, frames, icons);
                    return new WorldEventPanel[] { peddler, shady, altar };
                }
            );
            view.Peddler = peddler;
            view.ShadyChest = shady;
            view.Altar = altar;
            Hide(view.Events);
            view.Awakening = Awakening(root, frames, icons);
        }

        // 停止中（Awake の前）も窓が見えないよう、閉じた形で保存する。
        private static void Hide(WorldMapEventWindow events)
        {
            events.Group.alpha = 0f;
            events.Group.blocksRaycasts = false;
            events.Group.interactable = false;
            events.Window.gameObject.SetActive(false);
            if (events.Glow != null)
                events.Glow.color = Color.clear;
            foreach (var ray in events.Rays)
                ray.enabled = false;
        }

        // --- 行商人 --------------------------------------------------------------------------

        private static AdventurePeddlerPanel Peddler(RectTransform stage)
        {
            var panel = Panels.Panel<AdventurePeddlerPanel>(stage, WorldEventKind.Peddler);
            panel.name = "Merchant";
            panel.Rule = WorldMapEventWindow.CloseRule.Anytime;
            var root = (RectTransform)panel.transform;
            var merchant = Panels.Prop(
                root,
                "Merchant",
                Panels.MerchantPath,
                new Vector2(-580f, 40f),
                2f,
                300f
            );
            panel.Arrivals = new[] { merchant };
            panel.Talk = Panels.Bubbles(root, (new Vector2(-520f, 600f), Panels.BubbleShift));
            panel.Runes = Panels.Runes(root);

            var goods = new List<Button>();
            var faces = new List<CanvasGroup>();
            var icons = new List<Image>();
            var names = new List<TMP_Text>();
            var kinds = new List<TMP_Text>();
            var grades = new List<TMP_Text>();
            var prices = new List<TMP_Text>();
            var marks = new List<RectTransform>();
            for (int i = 0; i < GoodColumns.Length * GoodRows.Length; i++)
            {
                var center = new Vector2(GoodColumns[i % 2], GoodRows[i / 2]);
                var (button, content) = Panels.Glass(root, "Good" + i, center, GoodSize, true);
                var face = Rect("Face", content);
                Stretch(face);
                faces.Add(face.gameObject.AddComponent<CanvasGroup>());

                var icon = Rect("Icon", face);
                Place(icon, new Vector2(-112f, 0f), Vector2.one * 112f);
                var image = icon.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                icons.Add(image);

                var grade = Label(
                    face,
                    "Quality",
                    "",
                    30,
                    UiPalette.TextMain,
                    TextAlignmentOptions.TopLeft
                );
                Corner(
                    (RectTransform)grade.transform,
                    new Vector2(0f, 1f),
                    new Vector2(14f, -8f),
                    new Vector2(60f, 40f)
                );
                grades.Add(grade);

                var name = Label(
                    face,
                    "Name",
                    "",
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.MidlineLeft
                );
                Place((RectTransform)name.transform, new Vector2(72f, 46f), new Vector2(228f, 48f));
                GuideMenuAssets.Shrink(name, 22);
                names.Add(name);

                var kind = Label(
                    face,
                    "Kind",
                    "",
                    24,
                    UiPalette.TextSub,
                    TextAlignmentOptions.MidlineLeft
                );
                Place((RectTransform)kind.transform, new Vector2(72f, 4f), new Vector2(228f, 34f));
                GuideMenuAssets.Shrink(kind, 18);
                kinds.Add(kind);

                var rune = Rect("Rune", face);
                Place(rune, new Vector2(-22f, -44f), Vector2.one * 40f);
                Panels.Pixel(rune, Panels.RunePath);
                var price = Label(
                    face,
                    "Price",
                    "",
                    36,
                    UiPalette.GoldLight,
                    TextAlignmentOptions.MidlineLeft
                );
                Place(
                    (RectTransform)price.transform,
                    new Vector2(90f, -44f),
                    new Vector2(180f, 48f)
                );
                prices.Add(price);

                // 買った品は暗くなり、斜めの札が押される（世界地図の行商人と同じ）。
                var mark = Rect("Sold", content);
                Place(mark, new Vector2(40f, 0f), new Vector2(240f, 70f));
                mark.localRotation = Quaternion.Euler(0f, 0f, 12f);
                var plate = Sliced(mark, UiArt.CapsulePath, new Color(0.62f, 0.12f, 0.1f, 0.92f));
                plate.raycastTarget = false;
                plate.pixelsPerUnitMultiplier = 2f;
                var sold = Label(
                    mark,
                    "Label",
                    "売り切れ",
                    38,
                    Color.white,
                    TextAlignmentOptions.Center
                );
                Stretch((RectTransform)sold.transform);
                mark.gameObject.SetActive(false);

                goods.Add(button);
                marks.Add(mark);
            }
            panel.Goods = goods.ToArray();
            panel.Faces = faces.ToArray();
            panel.Icons = icons.ToArray();
            panel.Names = names.ToArray();
            panel.Kinds = kinds.ToArray();
            panel.Grades = grades.ToArray();
            panel.Prices = prices.ToArray();
            panel.SoldMarks = marks.ToArray();
            return panel;
        }

        // --- 怪しい宝箱 ----------------------------------------------------------------------

        private static AdventureShadyChestPanel Shady(RectTransform stage)
        {
            var panel = Panels.Panel<AdventureShadyChestPanel>(stage, WorldEventKind.HiddenChest);
            panel.name = "ShadyChest";
            panel.Rule = WorldMapEventWindow.CloseRule.Anytime;
            var root = (RectTransform)panel.transform;

            // 宝箱のまわりの、紫の淡い気配。
            var aura = Rect("Aura", root);
            Panels.Bottom(aura, new Vector2(0f, 400f), Vector2.one * 720f);
            panel.Aura = aura.gameObject.AddComponent<RawImage>();
            panel.Aura.color = new Color(0.62f, 0.36f, 0.95f, 0.5f);
            panel.Aura.raycastTarget = false;

            var chest = Panels.Prop(
                root,
                "Chest",
                Panels.ChestClosedPath,
                new Vector2(0f, 260f),
                8f,
                380f
            );
            panel.Chest = chest.GetChild(0).GetComponent<RawImage>();
            panel.Arrivals = new[] { chest };

            // 下に、通り過ぎる（左）と開ける（右）。
            var row = Rect("Answers", root);
            Panels.Bottom(row, new Vector2(0f, 120f), new Vector2(900f, 112f));
            panel.PassButton = Panels.TextButton(
                row,
                "Pass",
                new Vector2(-230f, 0f),
                new Vector2(400f, 112f),
                AdventureDialogs.PassChoice
            );
            panel.OpenButton = Panels.TextButton(
                row,
                "Open",
                new Vector2(230f, 0f),
                new Vector2(400f, 112f),
                AdventureDialogs.OpenChoice
            );
            return panel;
        }

        // --- 覚醒の祭壇 ----------------------------------------------------------------------

        private static AdventureAltarPanel Altar(
            RectTransform stage,
            Texture[] frames,
            Texture[] icons
        )
        {
            var panel = Panels.Panel<AdventureAltarPanel>(stage, WorldEventKind.Choice);
            panel.name = "Altar";
            panel.Rule = WorldMapEventWindow.CloseRule.Anytime;
            panel.ShowScenery = false;
            panel.CardFrames = frames;
            panel.ElementIcons = icons;
            var root = (RectTransform)panel.transform;

            // 祭壇の間の絵を、窓の高さいっぱいにして左右を切る。
            var backdrop = Rect("Backdrop", root);
            Stretch(backdrop);
            backdrop.SetAsFirstSibling();
            panel.Backdrop = backdrop.gameObject.AddComponent<RawImage>();
            var texture = ArtAssets.LoadTexture(AltarBackdropPath);
            panel.Backdrop.texture = texture;
            panel.Backdrop.raycastTarget = false;
            var size = panel.WindowSize - Vector2.one * (WorldMapEventWindowAssets.Inset * 2f);
            float width = size.x / size.y / ((float)texture.width / texture.height);
            panel.Backdrop.uvRect = new Rect((1f - width) * 0.5f, 0f, width, 1f);

            var members = new List<Button>();
            var figures = new List<RawImage>();
            var names = new List<TMP_Text>();
            var levels = new List<TMP_Text>();
            var picked = new List<GameObject>();
            var arrivals = new List<RectTransform>();
            for (int i = 0; i < MemberColumns.Length; i++)
            {
                var holder = Rect("Member" + i, root);
                Panels.Bottom(holder, new Vector2(MemberColumns[i], MemberY), MemberSize);
                var feet = Rect("Feet", holder);
                Stretch(feet);
                arrivals.Add(feet);
                var (button, rim, content) = GlassCardAssets.Choice(
                    feet,
                    pattern: true,
                    fill: GlassCardAssets.SceneFillColor
                );
                rim.SetActive(false);
                picked.Add(rim);
                var art = ArtAssets.LoadTexture(
                    $"{CharacterArtFolder}/Battle{SampleParty[i]}.aseprite"
                );
                figures.Add(
                    PixelActor(
                        content,
                        "Figure",
                        art,
                        new Vector2(-100f, -MemberSize.y * 0.5f + 8f),
                        MemberDot
                    )
                );
                var name = Label(
                    content,
                    "Name",
                    SampleParty[i],
                    40,
                    UiPalette.TextMain,
                    TextAlignmentOptions.BottomLeft
                );
                Place((RectTransform)name.transform, new Vector2(80f, 22f), new Vector2(180f, 56f));
                GuideMenuAssets.Shrink(name, 26);
                names.Add(name);
                var tag = Label(
                    content,
                    "LevelTag",
                    "Lv",
                    26,
                    UiPalette.Gold,
                    TextAlignmentOptions.BottomLeft
                );
                Place((RectTransform)tag.transform, new Vector2(10f, -36f), new Vector2(40f, 40f));
                var level = Label(
                    content,
                    "Level",
                    "1",
                    40,
                    UiPalette.Gold,
                    TextAlignmentOptions.BottomLeft
                );
                Place(
                    (RectTransform)level.transform,
                    new Vector2(100f, -36f),
                    new Vector2(140f, 52f)
                );
                levels.Add(level);
                members.Add(button);
            }
            panel.Members = members.ToArray();
            panel.Figures = figures.ToArray();
            panel.Names = names.ToArray();
            panel.Levels = levels.ToArray();
            panel.Picked = picked.ToArray();
            panel.Arrivals = arrivals.ToArray();

            var skills = new List<Button>();
            var faces = new List<BattleInspectCardView>();
            for (int i = 0; i < 2; i++)
            {
                var body = Rect("Skill" + i, root);
                Panels.Bottom(
                    body,
                    new Vector2((i - 0.5f) * SkillGap, SkillY),
                    new Vector2(270f, 380f)
                );
                var hit = AddImage(body, Color.clear, true);
                skills.Add(AddButton(body, hit));
                var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Face", body);
                var face = (RectTransform)instance.transform;
                face.anchorMin = face.anchorMax = face.pivot = new Vector2(0.5f, 0.5f);
                face.anchoredPosition = Vector2.zero;
                face.localScale = Vector3.one * SkillScale;
                var input = instance.GetComponent<BattleInspectCardInput>();
                if (input != null)
                    UnityEngine.Object.DestroyImmediate(input);
                foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
                faces.Add(instance.GetComponent<BattleInspectCardView>());
                body.gameObject.SetActive(false);
            }
            panel.Skills = skills.ToArray();
            panel.Faces = faces.ToArray();
            return panel;
        }

        // --- 覚醒の場面 ----------------------------------------------------------------------

        // 商会のスキル覚醒の場面と同じ：画面を暗く覆い、戦闘と同じ覚醒の演出の層、浮かぶカード、
        // 覚醒先の選択、左上に「もどる」を置く。
        private static AdventureAwakening Awakening(
            RectTransform root,
            Texture[] frames,
            Texture[] icons
        )
        {
            var scene = Rect("Awakening", root);
            Stretch(scene);
            AddImage(scene, UiPalette.VeilDeep, true);
            var awakening = scene.gameObject.AddComponent<AdventureAwakening>();
            awakening.Scene = scene.gameObject;
            awakening.CardFrames = frames;
            awakening.ElementIcons = icons;

            var stage = Rect("Stage", scene);
            Stretch(stage);
            var dim = BattleSkillVfxAssets.Dim(stage);
            var back = BattleSkillVfxAssets.Layer("VfxBack", stage);
            var front = BattleSkillVfxAssets.Layer("VfxFront", stage);

            var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Origin", scene);
            var origin = (RectTransform)instance.transform;
            origin.anchorMin = origin.anchorMax = origin.pivot = new Vector2(0.5f, 1f);
            origin.anchoredPosition = new Vector2(0f, -OriginTop);
            origin.localScale = Vector3.one * OriginScale;
            var input = instance.GetComponent<BattleInspectCardInput>();
            if (input != null)
                UnityEngine.Object.DestroyImmediate(input);
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            awakening.Origin = instance.GetComponent<BattleInspectCardView>();

            var beam = AssetDatabase.LoadAssetAtPath<Material>(
                $"{BattleSkillVfxAssets.MaterialFolder}/VfxShapeBeam.mat"
            );
            if (beam == null)
                throw new InvalidOperationException("Missing material: VfxShapeBeam.mat");
            awakening.Choice = BattleEvolutionChoiceAssets.Build(scene, beam);
            awakening.Vfx = BattleSkillVfxAssets.Attach(scene, stage, dim, back, front);

            // 左上の「もどる」：探索のメニューと同じ所に、白い矢印を置く。
            var safe = SafeArea(scene);
            var arrow = Rect("Back", safe);
            Corner(
                arrow,
                new Vector2(0f, 1f),
                new Vector2(UiSpace.Edge, -UiSpace.Edge),
                Vector2.one * UiSpace.Hit
            );
            AddImage(arrow, Color.clear, true);
            var icon = Rect("Icon", arrow);
            Place(icon, Vector2.zero, Vector2.one * GuideMenuAssets.IconSize);
            awakening.Back = AddTintButton(
                arrow,
                SpriteImage(icon, GuideMenuAssets.IconBackPath, Color.white)
            );

            scene.gameObject.SetActive(false);
            return awakening;
        }
    }
}
