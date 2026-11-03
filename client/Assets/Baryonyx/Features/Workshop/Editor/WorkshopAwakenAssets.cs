using System;
using System.Linq;
using Baryonyx.CardLoadout.Editor;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor.UI;
using Baryonyx.Enhancement;
using Baryonyx.Inventory;
using Baryonyx.Inventory.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.Workshop.Editor.WorkshopPanelAssets;
using ElementIconRowAssets = Baryonyx.UI.Editor.ElementIconRowAssets;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using GlassCardAssets = Baryonyx.UI.Editor.GlassCardAssets;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// Builds the skill awakening panel (<see cref="WorkshopAwakenView"/>): one scrolling list
    /// across the screen, each row a glass card with the person who has the skill at its left
    /// (their battle sprite at 2x, behind a darker band and a thin gold line), the card's art
    /// with its element (no cost), the name, kind and effect as in the adventurer's skills tab,
    /// and the quality's letter and multiplier at the right end. Over it, hidden until a row is
    /// pressed, the awakening scene: a dark veil, the card hovering at the top and the battle's
    /// choice of the next stage (<see cref="BattleEvolutionChoiceAssets"/>), and the shard and
    /// runes it takes at the bottom left. The mock party's rows are baked so the prefab reads in
    /// the editor.
    /// </summary>
    public static class WorkshopAwakenAssets
    {
        // 行の高さ（冒険者のスキルの行と同じ）と、キャラが替わる行の上に空ける間。
        private const float RowHeight = 140f;

        // 一覧の上の見出し。
        private const string ListTitle = "覚醒できる固有スキル";
        private const float GroupGap = 36f;

        // 行の左のキャラの所：戦闘のドット絵（64×64）を2倍で、少し暗い地の上に置き、金の細い線で区切る。
        private const float PersonWidth = 160f;
        private const float PersonDot = 2f;
        private static readonly Color PersonBack = new(0f, 0f, 0f, 0.22f);
        private static readonly Color PersonLine = new(0.86f, 0.71f, 0.38f, 0.8f);

        // カードの挿絵（2倍）、名前と効果の文の左端、行の右端の余白。品質と倍率は名前のすぐ右に出す
        // （横長の行。doc/rules/ui-design.md の装備の見せ方）。
        private const float ThumbLeft = PersonWidth + 14f;
        private const float TextLeft = ThumbLeft + 64f * 2f + 20f;
        private const float QualityWidth = 24f;

        // 覚醒の場面：一覧を覆う幕、上の中央に浮かぶ今のカード（戦闘のカードを縮めて）。
        private static readonly Color Veil = UiPalette.VeilDeep;
        private const float OriginTop = 22f;
        private const float OriginScale = 0.55f;

        /// <summary>The panel for the trading house's skill awakening item.</summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var (view, panel) = GuidePanelTemplate.FullList<WorkshopAwakenView>(
                safe,
                "WorkshopAwaken",
                ListTitle
            );
            view.Guide = guide;
            view.Party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            view.Training = AssetDatabase.LoadAssetAtPath<TrainingMockData>(
                TrainingAssets.DataPath
            );
            view.RowHeight = RowHeight;
            view.GroupGap = GroupGap;
            view.ElementIcons = ElementIcons();
            var elements = (CardElement[])Enum.GetValues(typeof(CardElement));
            view.CardFrames = elements
                .Select(element => (Texture)BattleInspectAssets.Art("CardFrame" + element))
                .ToArray();
            view.ElementArts = elements
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : (Texture)BattleInspectAssets.Art("Element" + element)
                )
                .ToArray();

            var content = panel.Content;
            view.Content = content;
            view.List = panel.Scroll;
            view.Template = BuildRow(content, "Template");
            view.Template.gameObject.SetActive(false);
            view.Status = panel.Status;

            // 仮の編成の行を焼き込み、Prefabを開くと停止中でも一覧が読めるようにする。
            view.Use(PartyFormation.From(view.Party), null);
            for (int i = 0; i < view.Entries.Count; i++)
            {
                var row = BuildRow(content, "Row" + i);
                view.Paint(
                    row,
                    view.Entries[i],
                    i > 0 && view.Entries[i].Character != view.Entries[i - 1].Character
                );
                view.Rows.Add(row);
            }

            BuildScene((RectTransform)view.transform, view);
            return view.gameObject;
        }

        private static WorkshopAwakenRow BuildRow(RectTransform content, string name)
        {
            var root = Layout.ListItem(content, name, RowHeight);
            var row = root.gameObject.AddComponent<WorkshopAwakenRow>();
            row.Size = root.GetComponent<LayoutElement>();

            // カードは行の下端に寄せ、キャラが替わる行だけ上に間が空くようにする。
            var body = Rect("Card", root);
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 0f);
            body.pivot = new Vector2(0.5f, 0f);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = new Vector2(0f, RowHeight);
            var (button, _, inside) = GlassCardAssets.Choice(body, pattern: false);
            row.Button = button;
            row.Card = body.gameObject;
            // 読み込む間は、この行を伏せて一覧を埋める（GlassCardCover）。
            GlassCardAssets.AddBack(body.GetComponent<GlassCard>(), vertical: true);

            var band = Rect("Person", inside);
            band.anchorMin = new Vector2(0f, 0f);
            band.anchorMax = new Vector2(0f, 1f);
            band.pivot = new Vector2(0f, 0.5f);
            band.anchoredPosition = Vector2.zero;
            band.sizeDelta = new Vector2(PersonWidth, 0f);
            AddImage(band, PersonBack, false);
            var line = Rect("Line", band);
            line.anchorMin = new Vector2(1f, 0f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(1f, 0.5f);
            line.anchoredPosition = Vector2.zero;
            line.sizeDelta = new Vector2(2f, -16f);
            AddImage(line, PersonLine, false);
            var sprite = Rect("Sprite", band);
            Place(sprite, Vector2.zero, Vector2.one * 64f * PersonDot);
            row.Character = sprite.gameObject.AddComponent<RawImage>();
            row.Character.raycastTarget = false;
            sprite.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = PersonDot;

            var art = CardLoadoutAssets.BuildThumb(inside, ThumbLeft);
            row.Art = art;
            row.Foil = CardFoilAssets.Add(art);
            // 属性は、編成のスキルの行と同じく名前の行の右端に出す（2026-11-03）。
            // 一覧ではコストを出さない（左上の数字が覚醒の段階と紛らわしい。2026-11-01、ユーザーの指示）。
            // コストは覚醒の場面のカードで出す。
            (_, _, row.Element, _) = CardLoadoutAssets.BuildCorner(
                inside,
                12,
                QualityWidth,
                cost: false
            );
            (row.Name, row.Kind) = CardLoadoutAssets.BuildNameLine(
                inside,
                12,
                TextLeft,
                QualityWidth + ElementIconRowAssets.Size + 12f
            );
            // 名前のすぐ右の品質と倍率は、リッチテキストで色を付ける。
            row.Name.richText = true;
            row.Description = CardLoadoutAssets.BuildDescription(
                inside,
                64,
                64,
                TextLeft,
                QualityWidth
            );

            return row;
        }

        // 覚醒の欠片の絵（32×32のドット絵）を出す大きさ。
        private const float ShardArt = 32f * 2f;

        private static RawImage ShardImage(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = ItemArtAssets.Load().TextureOf(EnhancementMaterials.Shard);
            image.raycastTarget = false;
            rect.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = 2f;
            return image;
        }

        // 覚醒の場面：ヘッダーごと画面を暗く覆い、戦闘と同じ覚醒の演出の層、浮かぶカード、覚醒先の選択、左下に払う物の札、
        // いちばん上にヘッダーと同じ所の「もどる」を置く。
        private static void BuildScene(RectTransform panel, WorkshopAwakenView view)
        {
            var scene = Rect("AwakenScene", panel);
            Stretch(scene);
            // 後ろの一覧とヘッダーを押せないよう、幕でタップを受ける。
            AddImage(scene, Veil, true);
            view.Scene = scene.gameObject;

            // 戦闘の覚醒の演出の舞台（暗転・後ろと前の光の層）。カードは実行中に前の層へ移る。
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
            view.Origin = instance.GetComponent<BattleInspectCardView>();

            var beam = AssetDatabase.LoadAssetAtPath<Material>(
                $"{BattleSkillVfxAssets.MaterialFolder}/VfxShapeBeam.mat"
            );
            if (beam == null)
                throw new InvalidOperationException("Missing material: VfxShapeBeam.mat");
            view.Choice = BattleEvolutionChoiceAssets.Build(scene, beam);

            view.Vfx = BattleSkillVfxAssets.Attach(scene, stage, dim, back, front);

            BuildCost(SafeArea(scene), view);

            BuildBack(scene, view);
            scene.gameObject.SetActive(false);
        }

        // 幕の上の「もどる」：ヘッダーの「もどる」と同じ所に、同じ紺の板と白い矢印を置き（案内人の画面の←と同じ形）、
        // 欠片が足りなくても場面を閉じられるようにする。
        private static void BuildBack(RectTransform scene, WorkshopAwakenView view)
        {
            var header = (RectTransform)view.Guide.Back.transform;
            var back = Rect("Back", scene);
            back.anchorMin = header.anchorMin;
            back.anchorMax = header.anchorMax;
            back.pivot = header.pivot;
            back.anchoredPosition = header.anchoredPosition;
            back.sizeDelta = header.sizeDelta;
            var headerTab = (RectTransform)header.Find("Tab");
            var tab = Rect("Tab", back);
            Stretch(tab);
            tab.offsetMin = headerTab.offsetMin;
            tab.offsetMax = headerTab.offsetMax;
            var plate = AddImage(tab, GuideFrameAssets.HeaderColor, true);
            var icon = Rect("Icon", back);
            Place(icon, Vector2.zero, Vector2.one * Guide.IconSize);
            SpriteImage(icon, Guide.IconBackPath, Color.white);
            view.SceneBack = UiBuild.AddTintButton(back, plate);
        }

        // 左下の払う物の札：覚醒の欠片の絵と「使う数/持っている数」、ルーンのアイコンと払う額。
        private const float CostWidth = 440f;
        private const float CostHeight = 104f;

        private static void BuildCost(RectTransform safe, WorkshopAwakenView view)
        {
            var plaque = Rect("Cost", safe);
            Corner(
                plaque,
                new Vector2(0f, 0f),
                new Vector2(Edge, 44f),
                new Vector2(CostWidth, CostHeight)
            );
            view.CostCard = plaque.gameObject;
            var inside = GlassCardAssets.Plaque(plaque, pattern: false);
            var shard = Rect("Shard", inside);
            Corner(shard, new Vector2(0f, 0.5f), new Vector2(20f, 0f), Vector2.one * ShardArt);
            view.CostShard = ShardImage(shard);
            view.ShardCount = Label(
                inside,
                "ShardCount",
                "1/0",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Corner(
                view.ShardCount.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(20f + ShardArt + 10f, 0f),
                new Vector2(120f, 60f)
            );
            view.RuneCost = Frame.RuneAmount(
                inside,
                "RuneCost",
                0f,
                0f,
                220f,
                60f,
                UiType.Lead,
                TextAnchor.MiddleLeft,
                48f,
                UiPalette.TextMain
            );
            view.RuneCost.text = "0";
            Corner(
                (RectTransform)view.RuneCost.transform.parent,
                new Vector2(0f, 0.5f),
                new Vector2(230f, 0f),
                new Vector2(220f, 60f)
            );
        }
    }
}
