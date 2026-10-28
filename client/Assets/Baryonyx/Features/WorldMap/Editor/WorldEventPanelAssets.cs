using System.Collections.Generic;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.WorldMap.Editor
{
    /// <summary>
    /// Builds one panel for each kind of event inside the event window's stage
    /// (doc/features/world-atlas.md#マスの出来事の見本): a meeting that ends with someone joining,
    /// a choice with what each answer gives, a hidden chest, a pickup in a small window, a
    /// peddler's goods, a bet on three cards and a battle that leads to the battle screen. Places
    /// are given from the stage's bottom middle (the ground the scenery's meadow shows), in the
    /// design's pixels. All words, pictures and numbers are samples.
    /// </summary>
    public static class WorldEventPanelAssets
    {
        private const string Art = "Assets/Baryonyx/Shared/Art";
        private const string TomaPath = Art + "/Characters/BattleToma.aseprite";
        private const string AriaPath = Art + "/Characters/BattleAria.aseprite";
        private const string LukaPath = Art + "/Characters/BattleLuka.aseprite";
        private const string LukaPicturePath = Art + "/Characters/Illustrations/luka.png";

        // 行商人の絵はまだないため、酒場の案内人を仮に立たせる。
        private const string MerchantPath =
            "Assets/Baryonyx/Features/Tavern/UI/Art/TavernGuide.aseprite";
        private const string CartPath = Art + "/Stages/MeadowRoad/MeadowCart.aseprite";
        private const string ChestClosedPath = Art + "/Stages/Exploration/ChestClosed.aseprite";
        private const string ChestOpenPath = Art + "/Stages/Exploration/ChestOpen.aseprite";
        private const string Enemies = "Assets/Baryonyx/Features/Combat/UI/Art/Enemies";
        private const string RunePath = Art + "/GameResources/IconRune.aseprite";
        private const string HerbPath = Art + "/Items/Herb.aseprite";
        private const string AcornPath = Art + "/Items/Acorn.aseprite";
        private const string BluntPath = Art + "/Attributes/ElementBlunt.aseprite";
        private const string IronSwordPath = Art + "/Weapons/IronSword.aseprite";

        // キャラは足元を景色の草原に置く。頭の上に吹き出しのしっぽを置く。
        private const float FeetY = 110f;
        private const float CharacterDot = 6f;
        private static readonly Vector2 BubbleSize = new(760f, 180f);
        private const float BubbleShift = 220f;
        private const float TailDrop = 26f;
        private static readonly Color Paper = new(0.98f, 0.96f, 0.9f, 1f);

        public static WorldEventPanel[] Build(RectTransform stage) =>
            new WorldEventPanel[]
            {
                Meet(stage),
                Choice(stage),
                Chest(stage),
                Pickup(stage),
                Peddler(stage),
                Bet(stage),
                Battle(stage),
            };

        private static T Panel<T>(RectTransform stage, WorldEventKind kind)
            where T : WorldEventPanel
        {
            var rect = Rect(kind.ToString(), stage);
            Stretch(rect);
            var panel = rect.gameObject.AddComponent<T>();
            panel.Kind = kind;
            panel.WindowSize = WorldMapEventWindowAssets.WindowSize;
            rect.gameObject.SetActive(false);
            return panel;
        }

        // 出会い：左にアリア、右に初めて会うルカ。会話のあと、ルカの絵が入って仲間になる。
        private static WorldEventMeet Meet(RectTransform stage)
        {
            var panel = Panel<WorldEventMeet>(stage, WorldEventKind.Meet);
            var root = (RectTransform)panel.transform;
            var (aria, ariaHead) = Character(root, "Aria", AriaPath, -440f, false);
            var (luka, lukaHead) = Character(root, "Luka", LukaPath, 440f, true);
            panel.Arrivals = new[] { aria, luka };
            panel.Talk = Bubbles(
                root,
                (new Vector2(-400f, ariaHead), BubbleShift),
                (new Vector2(400f, lukaHead), -BubbleShift)
            );

            var join = Rect("Join", root);
            Stretch(join);
            AddImage(join, new Color(0.02f, 0.03f, 0.07f, 0.62f), false);
            panel.Join = join.gameObject.AddComponent<CanvasGroup>();
            panel.Join.blocksRaycasts = false;

            var burst = Rect("Burst", join);
            Bottom(burst, new Vector2(300f, 430f), Vector2.one * 980f);
            panel.Burst = burst.gameObject.AddComponent<RawImage>();
            panel.Burst.color = new Color(1f, 0.85f, 0.5f, 0f);
            panel.Burst.raycastTarget = false;

            var texture = ArtAssets.LoadTexture(LukaPicturePath);
            float height = 820f;
            var portrait = Rect("Portrait", join);
            Bottom(
                portrait,
                new Vector2(300f, 0f),
                new Vector2(height * texture.width / texture.height, height)
            );
            portrait.pivot = new Vector2(0.5f, 0f);
            portrait.anchoredPosition = new Vector2(300f, 0f);
            var picture = portrait.gameObject.AddComponent<RawImage>();
            picture.texture = texture;
            picture.raycastTarget = false;
            panel.Portrait = portrait;

            // 帯は左の端から右へ伸び、仲間になったことを告げる。
            var band = Rect("Band", join);
            Bottom(band, new Vector2(-760f, 190f), new Vector2(980f, 124f));
            band.pivot = new Vector2(0f, 0.5f);
            band.anchoredPosition = new Vector2(-760f, 190f);
            AddImage(band, new Color(0.03f, 0.04f, 0.1f, 0.88f), false);
            var line = Rect("Line", band);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = Vector2.one;
            line.offsetMin = new Vector2(0f, -3f);
            line.offsetMax = Vector2.zero;
            AddImage(line, UiPalette.Gold, false);
            panel.Band = band;
            var joined = Label(
                band,
                "Joined",
                WorldEventSamples.MeetJoined,
                64,
                UiPalette.GoldLight,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)joined.transform);
            panel.Joined = joined;
            return panel;
        }

        // 選択：トーマが倒れた荷車を見つける。答えは右に縦に並び、選ぶともらう物が出る。
        private static WorldEventChoice Choice(RectTransform stage)
        {
            var panel = Panel<WorldEventChoice>(stage, WorldEventKind.Choice);
            var root = (RectTransform)panel.transform;
            var (toma, head) = Character(root, "Toma", TomaPath, -540f, false);
            var cart = Prop(root, "Cart", CartPath, new Vector2(330f, FeetY - 10f), 4f, 360f);
            cart.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, -6f);
            panel.Arrivals = new[] { toma, cart };
            panel.Talk = Bubbles(root, (new Vector2(-500f, head), BubbleShift));

            var answers = Rect("Answers", root);
            Bottom(answers, new Vector2(440f, 600f), new Vector2(640f, 340f));
            panel.Answers = answers.gameObject.AddComponent<CanvasGroup>();
            var options = new List<Button>();
            for (int i = 0; i < WorldEventSamples.ChoiceOptions.Length; i++)
            {
                var (button, content) = Glass(
                    answers,
                    "Option" + i,
                    new Vector2(0f, 110f - i * 112f),
                    new Vector2(640f, 100f),
                    false
                );
                var text = Label(
                    content,
                    "Label",
                    WorldEventSamples.ChoiceOptions[i],
                    42,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                );
                Stretch((RectTransform)text.transform);
                // 3つ目は、打の属性のカードを持つ仲間がいるときだけ出る答え。属性の印で示す。
                if (i == 2)
                {
                    var mark = Rect("Element", content);
                    Place(mark, new Vector2(-250f, 0f), Vector2.one * 48f);
                    Pixel(mark, BluntPath);
                }
                options.Add(button);
            }
            panel.Options = options.ToArray();

            // 選ぶと、答えがあった場所にもらう物が並ぶ。
            var answersTop = new Vector2(440f, 760f);
            var herb = (HerbPath, "薬草 ×3");
            var acorn = (AcornPath, "どんぐり ×2");
            var sword = (IronSwordPath, "鉄の剣");
            panel.Gifts = new[]
            {
                GiftList(root, "Gifts0", answersTop, sword),
                GiftList(root, "Gifts1", answersTop, herb, acorn),
                GiftList(root, "Gifts2", answersTop, sword, herb, acorn),
            };
            return panel;
        }

        // 隠し宝箱：中央の宝箱を押すと開き、中身が上へ飛び出して並ぶ。
        private static WorldEventChest Chest(RectTransform stage)
        {
            var panel = Panel<WorldEventChest>(stage, WorldEventKind.HiddenChest);
            var root = (RectTransform)panel.transform;
            var burst = Rect("Burst", root);
            Bottom(burst, new Vector2(0f, 300f), Vector2.one * 760f);
            panel.Burst = burst.gameObject.AddComponent<RawImage>();
            panel.Burst.color = new Color(1f, 0.88f, 0.55f, 0f);
            panel.Burst.raycastTarget = false;

            var chest = Prop(root, "Chest", ChestClosedPath, new Vector2(0f, 150f), 8f, 360f);
            panel.Chest = chest.GetChild(0).GetComponent<RawImage>();
            panel.ClosedArt = ArtAssets.LoadTexture(ChestClosedPath);
            panel.OpenArt = ArtAssets.LoadTexture(ChestOpenPath);
            panel.Arrivals = new[] { chest };
            panel.Gifts = Gifts(
                root,
                "Gifts",
                new Vector2(0f, 640f),
                (Art + "/Accessories/MossCharm.aseprite", "苔玉のお守り"),
                (Art + "/Materials/PolishBlue.aseprite", "青い磨き石 ×2"),
                (Art + "/Materials/AwakeningShard.aseprite", "覚醒の欠片")
            );
            return panel;
        }

        // 拾い物：小さな窓に、拾った物の絵と名前だけを出す。
        private static WorldEventPickup Pickup(RectTransform stage)
        {
            var panel = Panel<WorldEventPickup>(stage, WorldEventKind.Pickup);
            panel.WindowSize = new Vector2(820f, 230f);
            panel.ShowScenery = false;
            panel.Veil = 0.35f;
            panel.Rule = WorldMapEventWindow.CloseRule.Auto;
            var root = (RectTransform)panel.transform;
            var herb = Prop(root, "Herb", HerbPath, new Vector2(-200f, 40f), 4f, 0f);
            panel.Arrivals = new[] { herb };
            var name = Label(
                root,
                "Name",
                $"{WorldEventSamples.PickupName} ×{WorldEventSamples.PickupCount}",
                64,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Place((RectTransform)name.transform, new Vector2(150f, 0f), new Vector2(440f, 90f));
            return panel;
        }

        // 行商人：左に商人、右に今日の品物のカード。右上に持っているルーン。
        private static WorldEventPeddler Peddler(RectTransform stage)
        {
            var panel = Panel<WorldEventPeddler>(stage, WorldEventKind.Peddler);
            panel.Rule = WorldMapEventWindow.CloseRule.Anytime;
            var root = (RectTransform)panel.transform;
            var merchant = Prop(root, "Merchant", MerchantPath, new Vector2(-580f, 40f), 2f, 300f);
            panel.Arrivals = new[] { merchant };
            panel.Talk = Bubbles(root, (new Vector2(-520f, 600f), BubbleShift));
            panel.Runes = Runes(root);

            string[] paths =
            {
                IronSwordPath,
                Art + "/Weapons/FlameDagger.aseprite",
                Art + "/Weapons/FrostWand.aseprite",
                Art + "/Armor/AncientPlate.aseprite",
            };
            var goods = new List<Button>();
            var faces = new List<CanvasGroup>();
            var marks = new List<RectTransform>();
            for (int i = 0; i < paths.Length; i++)
            {
                var center = new Vector2(i % 2 == 0 ? 125f : 555f, i < 2 ? 430f : 210f);
                var (button, content) = Glass(
                    root,
                    "Good" + i,
                    center,
                    new Vector2(410f, 196f),
                    true
                );
                var face = Rect("Face", content);
                Stretch(face);
                faces.Add(face.gameObject.AddComponent<CanvasGroup>());
                var icon = Rect("Icon", face);
                Place(icon, new Vector2(-130f, 0f), Vector2.one * 128f);
                Pixel(icon, paths[i]);
                var name = Label(
                    face,
                    "Name",
                    WorldEventSamples.PeddlerGoods[i],
                    40,
                    UiPalette.TextMain,
                    TextAlignmentOptions.MidlineLeft
                );
                Place((RectTransform)name.transform, new Vector2(70f, 34f), new Vector2(260f, 56f));
                var rune = Rect("Rune", face);
                Place(rune, new Vector2(-38f, -36f), Vector2.one * 48f);
                Pixel(rune, RunePath);
                var price = Label(
                    face,
                    "Price",
                    WorldEventSamples.PeddlerPrices[i].ToString("N0"),
                    40,
                    UiPalette.GoldLight,
                    TextAlignmentOptions.MidlineLeft
                );
                Place(
                    (RectTransform)price.transform,
                    new Vector2(100f, -36f),
                    new Vector2(200f, 56f)
                );

                // 買った品は暗くなり、斜めの札が押される。
                var mark = Rect("Sold", content);
                Place(mark, new Vector2(60f, 0f), new Vector2(260f, 76f));
                mark.localRotation = Quaternion.Euler(0f, 0f, 12f);
                var plate = Sliced(mark, UiArt.CapsulePath, new Color(0.62f, 0.12f, 0.1f, 0.92f));
                plate.raycastTarget = false;
                plate.pixelsPerUnitMultiplier = 2f;
                var sold = Label(
                    mark,
                    "Label",
                    "売り切れ",
                    40,
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
            panel.SoldMarks = marks.ToArray();
            return panel;
        }

        // 賭け：3枚のカードを伏せて並べ、下に遊ぶボタン。当たりはルーン、外れはどんぐり。
        private static WorldEventBet Bet(RectTransform stage)
        {
            var panel = Panel<WorldEventBet>(stage, WorldEventKind.Bet);
            panel.Rule = WorldMapEventWindow.CloseRule.Anytime;
            var root = (RectTransform)panel.transform;
            panel.Runes = Runes(root);

            // カードはまとめて1つの台として降り立ち、台の上で入れ替わる。
            var table = Rect("Table", root);
            Bottom(table, Vector2.zero, Vector2.zero);
            var cards = new List<RectTransform>();
            var backs = new List<GameObject>();
            var winners = new List<GameObject>();
            var losers = new List<GameObject>();
            var picks = new List<Button>();
            for (int i = 0; i < 3; i++)
            {
                var card = Rect("Card" + i, table);
                Bottom(card, new Vector2((i - 1) * 300f, 480f), new Vector2(240f, 340f));
                var (button, _, content) = GlassCardAssets.Choice(
                    card,
                    pattern: false,
                    fill: GlassCardAssets.SceneFillColor
                );
                var win = Rect("Win", content);
                Stretch(win);
                var crown = Rect("Rune", win);
                Place(crown, new Vector2(0f, 30f), Vector2.one * 144f);
                Pixel(crown, RunePath);
                var prize = Label(
                    win,
                    "Prize",
                    WorldEventSamples.BetPrize.ToString("N0"),
                    52,
                    UiPalette.GoldLight,
                    TextAlignmentOptions.Center
                );
                Place(
                    (RectTransform)prize.transform,
                    new Vector2(0f, -90f),
                    new Vector2(220f, 70f)
                );
                var lose = Rect("Lose", content);
                Stretch(lose);
                var acorn = Rect("Acorn", lose);
                Place(acorn, new Vector2(0f, 10f), Vector2.one * 128f);
                Pixel(acorn, AcornPath);
                var back = GlassCardAssets.AddBack(card.GetComponent<GlassCard>());
                cards.Add(card);
                backs.Add(back.Face);
                winners.Add(win.gameObject);
                losers.Add(lose.gameObject);
                picks.Add(button);
            }
            panel.Arrivals = new[] { table };
            panel.Cards = cards.ToArray();
            panel.Backs = backs.ToArray();
            panel.Winners = winners.ToArray();
            panel.Losers = losers.ToArray();
            panel.Picks = picks.ToArray();

            var (playGroup, play, playContent) = Faded(
                root,
                "Play",
                new Vector2(0f, 150f),
                new Vector2(560f, 110f)
            );
            var label = Label(
                playContent,
                "Label",
                WorldEventSamples.BetPlay,
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            panel.Play = play;
            panel.PlayGroup = playGroup;

            panel.Win = Gifts(
                root,
                "WinGifts",
                new Vector2(0f, 150f),
                (RunePath, $"{WorldEventSamples.BetPrize}ルーン")
            );
            panel.Lose = Gifts(
                root,
                "LoseGifts",
                new Vector2(0f, 150f),
                (AcornPath, WorldEventSamples.BetConsolation)
            );
            return panel;
        }

        // 戦闘：左にパーティ、右に魔物。間の下に「たたかう」。
        private static WorldEventBattle Battle(RectTransform stage)
        {
            var panel = Panel<WorldEventBattle>(stage, WorldEventKind.Battle);
            panel.Rule = WorldMapEventWindow.CloseRule.Anytime;
            var root = (RectTransform)panel.transform;
            var (toma, _) = Character(root, "Toma", TomaPath, -600f, false, 170f);
            var (aria, _) = Character(root, "Aria", AriaPath, -380f, false, 170f);
            var slime = Prop(
                root,
                "Slime",
                Enemies + "/MossSlime.aseprite",
                new Vector2(250f, 170f),
                3f,
                200f
            );
            var wolf = Prop(
                root,
                "Wolf",
                Enemies + "/MossWolf.aseprite",
                new Vector2(540f, 170f),
                3f,
                260f
            );
            panel.Arrivals = new[] { toma, aria, slime, wolf };
            panel.Enemies = new[]
            {
                (RectTransform)slime.GetChild(0),
                (RectTransform)wolf.GetChild(0),
            };

            var (fightGroup, fight, content) = Faded(
                root,
                "Fight",
                new Vector2(0f, 70f),
                new Vector2(440f, 100f)
            );
            var label = Label(
                content,
                "Label",
                WorldEventSamples.BattleFight,
                46,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            panel.Fight = fight;
            panel.FightGroup = fightGroup;
            return panel;
        }

        // --- 部品 ------------------------------------------------------------------------

        // 舞台の下の真ん中から測って置く。
        private static void Bottom(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        // 戦闘のドット絵のキャラを影の上に立たせる。降り立つ足元と、頭の上の高さ（舞台の下から）を返す。
        // 戦闘のドット絵は右を向いているので、左を向かせるときは裏返す。
        private static (RectTransform Feet, float Head) Character(
            RectTransform parent,
            string name,
            string path,
            float x,
            bool faceLeft,
            float feetY = FeetY
        )
        {
            var texture = ArtAssets.LoadTexture(path);
            var feet = Prop(parent, name, path, new Vector2(x, feetY), CharacterDot, 280f);
            if (faceLeft)
                feet.GetChild(0).localScale = new Vector3(-1f, 1f, 1f);
            // 絵の上の端には余白があるので、頭は絵の高さの9割ほどにある。
            return (feet, feetY + texture.height * CharacterDot * 0.9f + 14f);
        }

        // ドット絵の物を、足元を基準に整数倍で置く。shadow が0なら影を敷かない。降り立つ足元を返す。
        private static RectTransform Prop(
            RectTransform parent,
            string name,
            string path,
            Vector2 feetAt,
            float dot,
            float shadow
        )
        {
            var holder = Rect(name, parent);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0f);
            holder.anchoredPosition = feetAt;
            holder.sizeDelta = Vector2.zero;
            if (shadow > 0f)
                Picture(
                    holder,
                    "Shadow",
                    ArtAssets.LoadSprite(UiArt.ShadowPath),
                    new Vector2(0f, -4f),
                    new Vector2(shadow, shadow * 0.2f),
                    0.45f
                );
            var feet = Rect("Feet", holder);
            feet.anchorMin = feet.anchorMax = new Vector2(0.5f, 0f);
            feet.pivot = new Vector2(0.5f, 0f);
            feet.sizeDelta = Vector2.zero;
            var texture = ArtAssets.LoadTexture(path);
            var art = Rect("Art", feet);
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0f);
            art.pivot = new Vector2(0.5f, 0f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(texture.width, texture.height) * dot;
            var image = art.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return feet;
        }

        // ドット絵を、置いた枠いっぱいに描く。
        private static void Pixel(RectTransform rect, string path)
        {
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = ArtAssets.LoadTexture(path);
            image.raycastTarget = false;
        }

        // 話す人ごとの吹き出し。しっぽの先（tip、舞台の下から）から、本体を shift だけ横へずらして置く。
        private static EventBubbles Bubbles(
            RectTransform parent,
            params (Vector2 Tip, float Shift)[] speakers
        )
        {
            var talk = new EventBubbles();
            var bubbles = new List<RectTransform>();
            var says = new List<TMP_Text>();
            var more = new List<Graphic>();
            for (int i = 0; i < speakers.Length; i++)
            {
                var bubble = Rect("Bubble" + i, parent);
                bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0f);
                bubble.sizeDelta = Vector2.zero;
                bubble.anchoredPosition = speakers[i].Tip;

                var tail = Rect("Tail", bubble);
                Place(tail, new Vector2(0f, TailDrop + 4f), Vector2.one * 40f);
                tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
                AddImage(tail, Paper, false);

                var body = Rect("Body", bubble);
                Place(
                    body,
                    new Vector2(speakers[i].Shift, TailDrop + BubbleSize.y * 0.5f),
                    BubbleSize
                );
                var plate = Sliced(body, UiArt.CapsulePath, Paper);
                plate.raycastTarget = false;
                plate.pixelsPerUnitMultiplier = 1.6f;

                var text = Label(
                    body,
                    "Say",
                    "",
                    44,
                    WorldMapEventWindowAssets.Ink,
                    TextAlignmentOptions.MidlineLeft,
                    false
                );
                var textRect = (RectTransform)text.transform;
                Stretch(textRect);
                textRect.offsetMin = new Vector2(44f, 18f);
                textRect.offsetMax = new Vector2(-44f, -18f);
                text.textWrappingMode = TextWrappingModes.Normal;

                // 続きがあるときだけ、右下で小さなひし形が点る。
                var mark = Rect("More", body);
                Corner(mark, new Vector2(1f, 0f), new Vector2(-30f, 22f), Vector2.one * 16f);
                mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
                more.Add(AddImage(mark, WorldMapEventWindowAssets.Ink, false));

                bubble.gameObject.SetActive(false);
                bubbles.Add(bubble);
                says.Add(text);
            }
            talk.Bubbles = bubbles.ToArray();
            talk.Says = says.ToArray();
            talk.More = more.ToArray();
            return talk;
        }

        // もらう物の並び。1つずつ、すりガラスの札に絵（ドット絵の4倍）と名前を載せ、横に並べる。
        // 札に載せるのは、空や草原の上でも名前が読めるようにするため。
        private static RectTransform Gifts(
            RectTransform parent,
            string name,
            Vector2 center,
            params (string Path, string Label)[] gifts
        )
        {
            const float step = 300f;
            var row = Rect(name, parent);
            Bottom(row, center, new Vector2(step * gifts.Length, 230f));
            for (int i = 0; i < gifts.Length; i++)
            {
                var slot = Rect("Gift" + i, row);
                Place(
                    slot,
                    new Vector2((i - (gifts.Length - 1) * 0.5f) * step, 0f),
                    new Vector2(step - 24f, 230f)
                );
                var content = GlassCardAssets.Plaque(
                    slot,
                    pattern: false,
                    fill: GlassCardAssets.SceneFillColor
                );
                GiftIcon(content, gifts[i].Path, new Vector2(0f, 32f));
                var label = Label(
                    content,
                    "Name",
                    gifts[i].Label,
                    36,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                );
                Place(
                    (RectTransform)label.transform,
                    new Vector2(0f, -76f),
                    new Vector2(step - 40f, 52f)
                );
            }
            row.gameObject.SetActive(false);
            return row;
        }

        // もらう物を縦に並べる（選択の答えがあった場所に出す）。上の端を top に合わせる。
        private static RectTransform GiftList(
            RectTransform parent,
            string name,
            Vector2 top,
            params (string Path, string Label)[] gifts
        )
        {
            const float height = 100f;
            const float gap = 12f;
            const float width = 640f;
            var list = Rect(name, parent);
            Bottom(list, top, new Vector2(width, 0f));
            for (int i = 0; i < gifts.Length; i++)
            {
                var slot = Rect("Gift" + i, list);
                Place(
                    slot,
                    new Vector2(0f, -height * 0.5f - i * (height + gap)),
                    new Vector2(width, height)
                );
                var content = GlassCardAssets.Plaque(
                    slot,
                    pattern: false,
                    fill: GlassCardAssets.SceneFillColor
                );
                var icon = GiftIcon(content, gifts[i].Path, new Vector2(-250f, 0f));
                icon.sizeDelta *= 0.62f;
                var label = Label(
                    content,
                    "Name",
                    gifts[i].Label,
                    42,
                    UiPalette.TextMain,
                    TextAlignmentOptions.MidlineLeft
                );
                Place((RectTransform)label.transform, new Vector2(60f, 0f), new Vector2(500f, 60f));
            }
            list.gameObject.SetActive(false);
            return list;
        }

        // もらう物の絵。大きい辺が128になる整数倍で描く。
        private static RectTransform GiftIcon(RectTransform parent, string path, Vector2 at)
        {
            var icon = Rect("Icon", parent);
            var texture = ArtAssets.LoadTexture(path);
            float dot = Mathf.Floor(128f / Mathf.Max(texture.width, texture.height));
            Place(icon, at, new Vector2(texture.width, texture.height) * dot);
            Pixel(icon, path);
            return icon;
        }

        // 景色の上に置く、すりガラスの押せるカード。中身を入れる枠を返す。
        private static (Button Button, RectTransform Content) Glass(
            RectTransform parent,
            string name,
            Vector2 center,
            Vector2 size,
            bool fromBottom
        )
        {
            var rect = Rect(name, parent);
            if (fromBottom)
                Bottom(rect, center, size);
            else
                Place(rect, center, size);
            var (button, _, content) = GlassCardAssets.Choice(
                rect,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            return (button, content);
        }

        // 現れたり消えたりする押せるカード。カードは自分の CanvasGroup を持つため、外側の枠で透かす。
        private static (CanvasGroup Group, Button Button, RectTransform Content) Faded(
            RectTransform parent,
            string name,
            Vector2 center,
            Vector2 size
        )
        {
            var holder = Rect(name, parent);
            Bottom(holder, center, size);
            var group = holder.gameObject.AddComponent<CanvasGroup>();
            var (button, content) = Glass(holder, "Button", Vector2.zero, size, false);
            return (group, button, content);
        }

        // 右上（×の左）に、持っているルーンを出す。
        private static TMP_Text Runes(RectTransform parent)
        {
            var pill = Rect("Runes", parent);
            Corner(pill, Vector2.one, new Vector2(-150f, -26f), new Vector2(290f, 80f));
            var plate = Sliced(pill, UiArt.CapsulePath, new Color(0.02f, 0.03f, 0.07f, 0.7f));
            plate.raycastTarget = false;
            var icon = Rect("Icon", pill);
            Place(icon, new Vector2(-96f, 0f), Vector2.one * 48f);
            Pixel(icon, RunePath);
            var text = Label(
                pill,
                "Count",
                "",
                42,
                UiPalette.GoldLight,
                TextAlignmentOptions.MidlineRight
            );
            Place((RectTransform)text.transform, new Vector2(30f, 0f), new Vector2(200f, 60f));
            return text;
        }
    }
}
