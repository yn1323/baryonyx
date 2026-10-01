using System;
using System.Collections.Generic;
using System.IO;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.Vfx.Hd2d.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// Generates the mock card battle screen (BattleInspectScreen.prefab): the party on the left,
    /// enemies on the right, the hand along the bottom, and the energy and end-turn controls.
    /// Coordinates follow the 1920x1080 design; pixel art is drawn at 4 px per dot.
    /// The shapes, the text shadow and the screen-building helpers are shared with the other screens.
    /// </summary>
    public static class BattleInspectAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Combat/UI/BattleInspectScreen.prefab";

        // Made once and then left to be tuned by hand: the screen generator never overwrites them.
        /// <summary>The damage, weakness and heal numbers side by side, for the showcase.</summary>
        public const string NumberSamplesPath =
            "Assets/Baryonyx/Features/Combat/UI/BattleDamageNumbers.prefab";

        public const string CardPrefabPath =
            "Assets/Baryonyx/Features/Combat/UI/BattleInspectCard.prefab";
        public const string HandSettingsPath =
            "Assets/Baryonyx/Features/Combat/UI/BattleInspectHandSettings.asset";
        public const string ArtFolder = "Assets/Baryonyx/Features/Combat/UI/Art";

        private const float DotSize = 4f;

        // Enemies are drawn at 3 px per dot so they do not crowd the screen, and the cards on a
        // finer grid of 3 px per dot so they hold more detail; both are agreed exceptions
        // (doc/art/direction.md).
        private const float EnemyDotSize = 3f;
        private const float CardDot = 3f;
        public const string FlashMaterialPath =
            "Assets/Baryonyx/Features/Combat/UI/TargetFlash.mat";
        public const string PixelArtMaterialPath =
            "Assets/Baryonyx/Features/Combat/UI/StagePixelArt.mat";

        // The card is 70x98 dots, the 5:7 of trading cards (63x88 mm, as Pokemon and Magic), with a
        // rim in the element's colour. The art (64x58) fills the top and dithers away into the
        // body; over its foot sit the owner, the name and the kind line on a dark translucent band,
        // then the description box. Rows in dots: owner 43..50, name 49..57, kind 57..63, a divider
        // at 63, description 65..93. The cost badge and the element icon sit on the art's top corners.
        private const float CardWidth = 70 * CardDot;
        private const float CardHeight = 98 * CardDot;
        private const float BandTop = 39;
        private const float BandBottom = 65;

        private static readonly Color CardOwner = new(1f, 0.93f, 0.76f);
        private const string SeparatorHex = "9a9483";
        private const int CostDigits = BattleInspectCardView.CostDigits;

        // Party turn, then enemies by index (slime 0, wolf 1, guardian 2). The two party turns in
        // a row show the consecutive turn that a large speed gap gives.
        private static readonly int[] TurnCycle =
        {
            BattleInspectView.PartyTurn,
            1,
            0,
            BattleInspectView.PartyTurn,
            BattleInspectView.PartyTurn,
            2,
        };

        // The deck: the six cards repeated in order up to 16 (doc/features/combat.md), so the
        // first six are one of each.
        private const int DeckSize = 16;

        // The deck in the bottom-left corner and the energy just above it.
        private const float DeckBottom = 40f;
        private const float EnergyBottom = 136f;

        // The turn order: 24x24-dot icons (the UI icon size) in a 1-dot frame, from the top left.
        private const int TurnSlotCount = 6;
        private const int TurnIconDots = 24;
        private const float TurnSlotSize = (TurnIconDots + 2) * DotSize;
        private const float WeakIconSize = 48f;
        private const int AimDotCount = 16;

        private static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        private static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        private static readonly Color HpBack = new(0.08f, 0.06f, 0.06f, 0.9f);
        private static readonly Color HpEnemy = new(0.66f, 0.16f, 0.14f);
        private static readonly Color HpAlly = new(0.2f, 0.52f, 0.26f);

        private sealed class AllySpec
        {
            public string Name;
            public string Label;
            public Vector2 Feet;
            public Color Color;
            public int Hp;
            public int MaxHp;
        }

        private sealed class EnemySpec
        {
            public string Name;
            public Vector2 Feet;
            public int Size;
            public int MaxHp;

            /// <summary>The name of its attack and its damage to one ally.</summary>
            public string Skill;
            public int Power;

            /// <summary>Weaknesses in icon order; those not in <see cref="Revealed"/> show "?".</summary>
            public BattleInspectElement[] Weaknesses;
            public BattleInspectElement[] Revealed;

            /// <summary>Top-left of the 24x24-dot face in the sprite (image rows from the top).</summary>
            public Vector2Int Face;
        }

        private sealed class CardSpec
        {
            public string Name;
            public string Art;
            public int Cost;
            public int Power;
            public BattleInspectElement Element;
            public BattleInspectCardEffect Effect;
            public int Owner;

            /// <summary>The description; {0} is where the power goes, drawn in the kind's colour.</summary>
            public string Text;
        }

        // Back first so those in front draw over them. The party stands in two columns of two,
        // the left column half a step higher, so from the top they zigzag Mina, Toma, Luka, Aria
        // instead of lining up in a grid. The lowest HP bar stays above the fanned hand. The party
        // and the enemies keep to the sides, clear of a pressed card standing in the bottom middle.
        private static readonly AllySpec[] Allies =
        {
            new()
            {
                Name = "Toma",
                Label = "トーマ",
                Feet = new Vector2(-420, 40),
                Color = new Color(0.66f, 0.45f, 0.9f),
                Hp = 262,
                MaxHp = 300,
            },
            new()
            {
                Name = "Mina",
                Label = "ミナ",
                Feet = new Vector2(-690, 170),
                Color = new Color(0.33f, 0.8f, 0.76f),
                Hp = 284,
                MaxHp = 310,
            },
            new()
            {
                Name = "Aria",
                Label = "アリア",
                Feet = new Vector2(-420, -220),
                Color = new Color(0.9f, 0.32f, 0.3f),
                Hp = 418,
                MaxHp = 480,
            },
            new()
            {
                Name = "Luka",
                Label = "ルカ",
                Feet = new Vector2(-690, -100),
                Color = new Color(0.5f, 0.78f, 0.32f),
                Hp = 331,
                MaxHp = 360,
            },
        };

        // The wolf (back) and the slime (front) stand in one column, one above the other, with the
        // guardian behind them, level with the gap between the two, mirroring the party's two rows
        // and keeping clear of the middle.
        private static readonly EnemySpec[] Enemies =
        {
            new()
            {
                Name = "ForestGuardian",
                Feet = new Vector2(790, -40),
                Size = 128,
                MaxHp = 3200,
                Skill = "大地の拳",
                Power = 140,
                Weaknesses = new[] { BattleInspectElement.Fire, BattleInspectElement.Slash },
                Revealed = new[] { BattleInspectElement.Fire },
                Face = new Vector2Int(12, 28),
            },
            new()
            {
                Name = "MossWolf",
                Feet = new Vector2(400, 100),
                Size = 96,
                MaxHp = 980,
                Skill = "かみつき",
                Power = 90,
                Weaknesses = new[] { BattleInspectElement.Ice, BattleInspectElement.Fire },
                Revealed = new[] { BattleInspectElement.Ice },
                Face = new Vector2Int(0, 49),
            },
            new()
            {
                Name = "MossSlime",
                Feet = new Vector2(400, -170),
                Size = 64,
                MaxHp = 420,
                Skill = "たいあたり",
                Power = 55,
                Weaknesses = new[] { BattleInspectElement.Thunder, BattleInspectElement.Fire },
                Revealed = Array.Empty<BattleInspectElement>(),
                Face = new Vector2Int(2, 36),
            },
        };

        private static readonly CardSpec[] Cards =
        {
            new()
            {
                Name = "斬り払い",
                Art = "CardSlash",
                Cost = 1,
                Power = 243,
                Element = BattleInspectElement.Slash,
                Effect = BattleInspectCardEffect.DamageOne,
                Owner = 2,
                Text = "剣で敵1体を斬りつけ、{0}ダメージ。",
            },
            new()
            {
                Name = "ファイア",
                Art = "CardFire",
                Cost = 2,
                Power = 318,
                Element = BattleInspectElement.Fire,
                Effect = BattleInspectCardEffect.DamageOne,
                Owner = 0,
                Text = "炎の玉で敵1体を焼き、{0}ダメージ。",
            },
            new()
            {
                Name = "アイスランス",
                Art = "CardIce",
                // Out of reach of the energy (5 at most on the first turn), to check how a card
                // the energy cannot pay for looks.
                Cost = 7,
                Power = 276,
                Element = BattleInspectElement.Ice,
                Effect = BattleInspectCardEffect.DamageOne,
                Owner = 0,
                Text = "氷の槍で敵1体を貫き、{0}ダメージ。",
            },
            new()
            {
                Name = "サンダー",
                Art = "CardThunder",
                Cost = 3,
                Power = 182,
                Element = BattleInspectElement.Thunder,
                Effect = BattleInspectCardEffect.DamageAll,
                Owner = 3,
                Text = "雷を落とし、敵全体に{0}ダメージ。",
            },
            new()
            {
                Name = "ヒール",
                Art = "CardHeal",
                Cost = 1,
                Power = 180,
                Element = BattleInspectElement.None,
                Effect = BattleInspectCardEffect.Heal,
                Owner = 1,
                Text = "味方1体のHPを{0}回復する。",
            },
            new()
            {
                Name = "ガード",
                Art = "CardGuard",
                Cost = 1,
                Power = 120,
                Element = BattleInspectElement.None,
                Effect = BattleInspectCardEffect.Guard,
                Owner = 2,
                Text = "守りの光で、味方全体にブロック{0}。",
            },
        };

        [MenuItem("Baryonyx/Combat/Create Battle Inspect Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
                ArtAssets.ImportTexture(AssetDatabase.GUIDToAssetPath(path), FilterMode.Point);

            using (UiBuild.Begin(font, shadowText))
            {
                EnsureCardPrefab(rebuild: false);
                // Two canvases: the battlefield, drawn by the scene's camera so the effects' light
                // goes through Bloom, and the controls on an overlay canvas over it, kept sharp.
                var root = Rect("BattleInspectScreen", null);
                Stretch(root);
                var view = root.gameObject.AddComponent<BattleInspectView>();
                view.TargetFlash = EnsureFlashMaterial();
                view.ActorMaterial = EnsurePixelArtMaterial();
                view.Settings = EnsureHandSettings();
                var stageCanvas = CanvasRoot("StageCanvas");
                stageCanvas.SetParent(root, false);
                BattleSkillVfxAssets.MakeStageCanvas(stageCanvas);
                var screen = CanvasRoot("ScreenCanvas");
                screen.SetParent(root, false);
                // Over the battlefield also where the showcase draws both canvases with one camera.
                screen.GetComponent<Canvas>().sortingOrder = 1;

                // The stage holds what a blow shakes: the background, the actors and the
                // effects among them. The flash and the cut-in go over it, the controls on top.
                // Around it, the camera's slow circle carries the whole stage.
                var drift = Rect("StageDrift", stageCanvas);
                Stretch(drift);
                drift.gameObject.AddComponent<BattleStageDrift>();
                var stage = Rect("Stage", drift);
                Stretch(stage);
                BuildBackground(stage);
                var dim = BattleSkillVfxAssets.Dim(stage);
                ReachPastDrift(dim.rectTransform);
                var back = BattleSkillVfxAssets.Layer("VfxBack", stage);
                var idle = new List<RectTransform>();
                var idleSteps = new List<float>();
                var world = BattleSkillVfxAssets.Layer("World", stage);
                BuildParty(world, view, idle, idleSteps);
                BuildEnemies(world, view, idle, idleSteps);
                view.IdleActors = idle.ToArray();
                view.IdleSteps = idleSteps.ToArray();
                var front = BattleSkillVfxAssets.Layer("VfxFront", stage);
                view.Vfx = BattleSkillVfxAssets.Attach(screen, stage, dim, back, front);
                BuildTapArea(screen, view);

                var safe = SafeArea(screen);
                BuildTurnOrder(safe, view);
                BuildSkillBanner(safe, view);
                BuildEnergy(safe, view);
                BuildDeck(safe, view);
                BuildHand(safe, view);
                BuildEndTurn(safe, view);
                BuildAim(safe, view);
                BuildPopup(screen, view);

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                BuildNumberSamples();
                BattleSkillVfxAssets.BuildPreview(Art("BattleBackground"), Art);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Remakes BattleInspectCard.prefab from the defaults, dropping any hand-made changes, then
        /// rebuilds the screen with it.
        /// </summary>
        [MenuItem("Baryonyx/Combat/Reset Battle Card Prefab")]
        public static void ResetCardPrefab()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
                EnsureCardPrefab(rebuild: true);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The hand's sizes and motion; made with the defaults when missing, never overwritten.</summary>
        private static BattleInspectHandSettings EnsureHandSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<BattleInspectHandSettings>(
                HandSettingsPath
            );
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<BattleInspectHandSettings>();
            AssetDatabase.CreateAsset(settings, HandSettingsPath);
            return settings;
        }

        /// <summary>
        /// The card (70x98 dots at 3 px): the art fading into the body, the owner, the name and the
        /// kind line over its foot on a translucent band as wide as each line, then the description.
        /// The cost and the element icon sit on the art's top corners. Made only when missing (or on
        /// reset), so the layout can be adjusted in the Prefab editor. It shows the fire card until
        /// the screen fills each card in.
        /// </summary>
        private static void EnsureCardPrefab(bool rebuild)
        {
            if (!rebuild && AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) != null)
            {
                EnsureCardParts();
                return;
            }

            var card = Rect("BattleInspectCard", null);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.gameObject.AddComponent<CanvasGroup>();
            var cardView = card.gameObject.AddComponent<BattleInspectCardView>();
            card.gameObject.AddComponent<BattleInspectCardInput>();

            // The frame is the whole card body; the art (its foot dithered away) is drawn over it.
            var frameRect = Rect("Frame", card);
            Stretch(frameRect);
            cardView.Frame = AddRaw(frameRect, Art("CardFrameFire"));
            cardView.Frame.raycastTarget = true;
            var fireArt = Art("CardFire");
            var art = Rect("Art", card);
            Corner(
                art,
                new Vector2(0, 1),
                new Vector2(3, -3) * CardDot,
                new Vector2(fireArt.width, fireArt.height) * CardDot
            );
            cardView.Art = AddRaw(art, fireArt);

            // Top's translucent panel (navy gradient, grey backdrop over it) as a band behind the
            // three lines, cut to each line's text.
            var bandRect = Rect("Band", card);
            Corner(
                bandRect,
                new Vector2(0, 1),
                new Vector2(3, -BandTop) * CardDot,
                new Vector2(70 - 6, BandBottom - BandTop) * CardDot
            );
            cardView.Bands = new[]
            {
                Band(
                    bandRect,
                    "Panel",
                    ArtAssets.LoadTexture(TranslucentTextPanelAssets.PanelTexturePath),
                    Color.white
                ),
                Band(
                    bandRect,
                    "Backdrop",
                    ArtAssets.LoadTexture(TranslucentTextPanelAssets.BackdropTexturePath),
                    TranslucentTextPanelAssets.BackdropColor
                ),
            };

            cardView.Owner = Label(
                card,
                "Owner",
                "トーマ",
                15,
                CardOwner,
                TextAlignmentOptions.Left
            );
            CardText(cardView.Owner, 6, 43.4f, 58, 6.4f);
            cardView.Name = Label(
                card,
                "Name",
                "ファイア",
                22,
                TextMain,
                TextAlignmentOptions.Left
            );
            CardText(cardView.Name, 6, 49.4f, 58, 7.6f);
            cardView.Kind = Label(card, "Kind", "", 15, TextMain, TextAlignmentOptions.Left);
            CardText(cardView.Kind, 7, 56.8f, 56, 5.6f);
            cardView.Kind.richText = true;
            cardView.Description = Label(
                card,
                "Description",
                "",
                15,
                TextSub,
                TextAlignmentOptions.TopLeft,
                shadow: false
            );
            CardText(cardView.Description, 7.5f, 66.6f, 55, 26);
            cardView.Description.richText = true;
            cardView.Description.textWrappingMode = TextWrappingModes.Normal;
            cardView.Description.enableAutoSizing = false;

            // The cost: a quiet badge with a digit (0 to 9, side by side) centred on it. The badge
            // is odd and the digit cell is odd, so the centred digit sits on whole dots.
            var costFrame = Art("CardCostFrame");
            var costDigits = Art("CardCostDigits");
            var cost = Rect("Cost", card);
            Corner(
                cost,
                new Vector2(0, 1),
                new Vector2(5, -5) * CardDot,
                new Vector2(costFrame.width, costFrame.height) * CardDot
            );
            AddRaw(cost, costFrame);
            float cell = costDigits.width / (float)CostDigits;
            var digit = Rect("Digit", cost);
            Place(digit, Vector2.zero, new Vector2(cell, costDigits.height) * CardDot);
            cardView.CostDigit = AddRaw(digit, costDigits);

            var fireIcon = Art("ElementFire");
            var element = Rect("Element", card);
            Corner(
                element,
                new Vector2(0, 1),
                new Vector2(70 - 5 - fireIcon.width, -5) * CardDot,
                new Vector2(fireIcon.width, fireIcon.height) * CardDot
            );
            cardView.Element = AddRaw(element, fireIcon);

            cardView.Show(
                "ファイア",
                "トーマ",
                KindLine(BattleInspectCardEffect.DamageOne),
                Description(
                    "炎の玉で敵1体を焼き、{0}ダメージ。",
                    BattleInspectCardEffect.DamageOne,
                    318
                ),
                fireArt,
                Art("CardFrameFire"),
                fireIcon,
                2
            );
            AddCardParts(card, cardView);
            PrefabUtility.SaveAsPrefabAsset(card.gameObject, CardPrefabPath);
        }

        /// <summary>
        /// Adds the back and the shade to a card prefab made before cards could lie face down or
        /// be darkened, and puts them in their order, keeping the rest of its hand-tuned layout.
        /// </summary>
        private static void EnsureCardParts()
        {
            var root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try
            {
                var cardView = root.GetComponent<BattleInspectCardView>();
                AddCardParts((RectTransform)root.transform, cardView);
                PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The shade over the whole card, its cost too, shown while the energy cannot pay for the
        /// card, and the back over everything, shown while it lies face down. Both take the card's
        /// outline from the back's picture (70x98 dots, the frame's outline).
        /// </summary>
        private static void AddCardParts(RectTransform card, BattleInspectCardView cardView)
        {
            var outline = Art("CardBack");
            if (cardView.Shade == null)
            {
                var shade = Rect("Shade", card);
                Stretch(shade);
                cardView.Shade = AddRaw(shade, outline);
                // The screen sets the darkness from the hand settings in Play Mode.
                cardView.Shade.color = new Color(0f, 0f, 0f, 0.7f);
                cardView.Shade.enabled = false;
            }
            if (cardView.Back == null)
            {
                var back = Rect("Back", card);
                Stretch(back);
                cardView.Back = AddRaw(back, outline);
                back.gameObject.SetActive(false);
            }
            // Bottom to top: the face, the cost, the shade (over the cost too), the back.
            var cost = cardView.CostDigit.rectTransform.parent;
            cost.SetAsLastSibling();
            cardView.Shade.rectTransform.SetAsLastSibling();
            cardView.Back.rectTransform.SetAsLastSibling();
        }

        private static BattleInspectCardBand Band(
            RectTransform parent,
            string name,
            Texture2D texture,
            Color color
        )
        {
            var rect = Rect(name, parent);
            Stretch(rect);
            var band = rect.gameObject.AddComponent<BattleInspectCardBand>();
            band.Texture = texture;
            band.color = color;
            band.raycastTarget = false;
            band.Fade = 9 * CardDot;
            // Line bottoms, down from the band's top: owner, name, kind.
            band.LineBottoms = new[]
            {
                (49.8f - BandTop) * CardDot,
                (56.6f - BandTop) * CardDot,
                (BandBottom - BandTop) * CardDot,
            };
            return band;
        }

        /// <summary>The material that turns a target's sprite white while a card is aimed at it.</summary>
        private static Material EnsureFlashMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (material != null)
                return material;
            var shader = Shader.Find("Baryonyx/UI Flash");
            if (shader == null)
                throw new InvalidOperationException("Missing shader: Baryonyx/UI Flash");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, FlashMaterialPath);
            return material;
        }

        /// <summary>
        /// The material of the pixel art on the stage (the background, the characters and their
        /// icons): sharp, yet it glides between screen pixels as the camera circles.
        /// </summary>
        private static Material EnsurePixelArtMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PixelArtMaterialPath);
            if (material != null)
                return material;
            var shader = Shader.Find("Baryonyx/UI Pixel Art");
            if (shader == null)
                throw new InvalidOperationException("Missing shader: Baryonyx/UI Pixel Art");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PixelArtMaterialPath);
            return material;
        }

        /// <summary>
        /// Lays a layer of the stage further past the screen by the camera's widest circle, on top
        /// of the room the shake needs, so its edge never comes into view.
        /// </summary>
        private static void ReachPastDrift(RectTransform rect)
        {
            rect.offsetMin -= Vector2.one * BattleStageDrift.MaxRadius;
            rect.offsetMax += Vector2.one * BattleStageDrift.MaxRadius;
        }

        /// <summary>Finds a battle image by file name anywhere under the art folder.</summary>
        private static Texture2D Art(string name)
        {
            foreach (
                var guid in AssetDatabase.FindAssets($"{name} t:Texture2D", new[] { ArtFolder })
            )
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return ArtAssets.LoadTexture(path);
            }
            throw new InvalidOperationException($"Missing battle art: {name}");
        }

        /// <summary>
        /// The background and its floor mist, a little larger than the screen so they still cover
        /// it while the stage shakes and circles, and the shades that keep the top and bottom
        /// controls legible.
        /// </summary>
        private static void BuildBackground(RectTransform parent)
        {
            var background = Art("BattleBackground");
            float aspect = background.width / (float)background.height;
            var backdrop = BattleSkillVfxAssets.Overscan("Backdrop", parent);
            ReachPastDrift(backdrop);
            var image = Rect("Background", backdrop).gameObject.AddComponent<RawImage>();
            image.texture = background;
            image.material = EnsurePixelArtMaterial();
            image.raycastTarget = false;
            image.gameObject.AddComponent<ResponsiveBackground>().AspectRatio = aspect;

            var fog = InstantiatePrefab(Hd2dAssets.FogPrefabPath, "FloorMist", backdrop);
            Stretch((RectTransform)fog.transform);
            fog.AddComponent<ResponsiveBackground>().AspectRatio = aspect;

            Shade(parent, "ShadeTop", top: true, 260f, 0.7f);
            Shade(parent, "ShadeBottom", top: false, 360f, 0.85f);
            // Their dark ends reach past the screen's edges as far as the camera circles.
            foreach (var name in new[] { "ShadeTop", "ShadeBottom" })
            {
                var shade = (RectTransform)parent.Find(name);
                float outward = shade.pivot.y > 0.5f ? 1f : -1f;
                shade.anchoredPosition = new Vector2(0, outward * BattleStageDrift.MaxRadius);
                shade.sizeDelta += new Vector2(2f, 1f) * BattleStageDrift.MaxRadius;
            }
        }

        private static void BuildParty(
            RectTransform world,
            BattleInspectView view,
            List<RectTransform> idle,
            List<float> idleSteps
        )
        {
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var cursor = Art("TargetCursor");
            var ringArt = Art("CasterRing");
            var units = new List<RectTransform>();
            var sprites = new List<RawImage>();
            var areas = new List<RectTransform>();
            var rings = new List<GameObject>();
            foreach (var ally in Allies)
            {
                var texture = Art("Battle" + ally.Name);
                var unit = Rect("Ally" + ally.Name, world);
                units.Add(unit);
                Place(unit, ally.Feet, Vector2.zero);
                Picture(unit, "Shadow", shadow, new Vector2(0, 2), new Vector2(150, 24), 0.5f);
                // On the floor over the shadow and behind the sprite; shown while a card this ally
                // uses is up.
                var ring = PixelIcon(unit, "CasterRing", ringArt);
                ((RectTransform)ring.transform).anchoredPosition = new Vector2(0, 2);
                ring.SetActive(false);
                rings.Add(ring);
                var pose = Rect("Pose", unit);
                Place(pose, Vector2.zero, Vector2.zero);
                var sprite = PixelActor(pose, "Sprite", texture, Vector2.zero, DotSize);
                sprite.material = view.ActorMaterial;
                sprites.Add(sprite);
                idle.Add(pose);
                idleSteps.Add(DotSize);
                areas.Add(TargetArea(unit, texture, DotSize));
            }

            // Drawn after every ally so the back row's bars stay over the front row.
            var allies = new List<BattleInspectAlly>();
            for (int i = 0; i < Allies.Length; i++)
            {
                var ally = Allies[i];
                var fill = HpBar(world, ally.Feet + new Vector2(0, -24), 120, 20, HpAlly);
                fill.parent.name = "Hp" + ally.Name;
                fill.anchorMax = new Vector2(ally.Hp / (float)ally.MaxHp, 1);
                var tag = NameTag((RectTransform)fill.parent, ally.Label);
                allies.Add(
                    new BattleInspectAlly
                    {
                        Body = units[i],
                        Sprite = sprites[i],
                        TargetArea = areas[i],
                        HpFill = fill,
                        Marker = TargetMarker(areas[i], cursor),
                        CasterRing = rings[i],
                        NameTag = tag,
                        StartHp = ally.Hp,
                        MaxHp = ally.MaxHp,
                    }
                );
            }
            view.Allies = allies.ToArray();

            // Heal and guard popups rise over the middle of the party.
            var anchor = Rect("PartyAnchor", world);
            Place(anchor, new Vector2(-555, 390), new Vector2(200, 80));
            view.PartyAnchor = anchor;
        }

        private static void BuildEnemies(
            RectTransform world,
            BattleInspectView view,
            List<RectTransform> idle,
            List<float> idleSteps
        )
        {
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var cursor = Art("TargetCursor");
            var enemies = new List<BattleInspectEnemy>();
            foreach (var spec in Enemies)
            {
                var texture = Art(spec.Name);
                var body = Rect("Enemy" + spec.Name, world);
                Place(body, spec.Feet, Vector2.zero);
                var group = body.gameObject.AddComponent<CanvasGroup>();

                float width = spec.Size * EnemyDotSize;
                Picture(
                    body,
                    "Shadow",
                    shadow,
                    new Vector2(0, 4),
                    new Vector2(width * 0.8f, 32),
                    0.5f
                );
                var pose = Rect("Pose", body);
                Place(pose, Vector2.zero, Vector2.zero);
                var sprite = PixelActor(pose, "Sprite", texture, Vector2.zero, EnemyDotSize);
                sprite.material = view.ActorMaterial;
                idle.Add(pose);
                idleSteps.Add(EnemyDotSize);

                var target = TargetArea(body, texture, EnemyDotSize);

                // The weakness icons sit in a row over the HP bar.
                var info = Rect("Info", body);
                Place(info, new Vector2(0, -44), new Vector2(260, 64));
                var weaknesses = WeaknessIcons(info, spec);
                var fill = HpBar(info, new Vector2(0, -16), 260, 24, HpEnemy);

                enemies.Add(
                    new BattleInspectEnemy
                    {
                        Body = body,
                        TargetArea = target,
                        Sprite = sprite,
                        HpFill = fill,
                        Group = group,
                        Marker = TargetMarker(target, cursor),
                        MaxHp = spec.MaxHp,
                        SkillName = spec.Skill,
                        Power = spec.Power,
                        Weaknesses = weaknesses,
                        IconUv = FaceUv(texture, spec.Face, TurnIconDots),
                    }
                );
            }

            // Screen order left to right: the slime is nearest the party.
            enemies.Reverse();
            view.Enemies = enemies.ToArray();
        }

        private static BattleInspectWeakness[] WeaknessIcons(RectTransform info, EnemySpec spec)
        {
            var row = Rect("Weaknesses", info);
            Place(row, new Vector2(0, 20), new Vector2(260, WeakIconSize));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var weaknesses = new List<BattleInspectWeakness>();
            foreach (var element in spec.Weaknesses)
            {
                var slot = Rect("Weakness" + element, row);
                var size = slot.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = size.preferredHeight = WeakIconSize;
                bool revealed = Array.IndexOf(spec.Revealed, element) >= 0;
                var weakness = new BattleInspectWeakness
                {
                    Element = element,
                    StartsRevealed = revealed,
                    Known = PixelIcon(slot, "Known", Art("Element" + element)),
                    Unknown = PixelIcon(slot, "Unknown", Art("ElementUnknown")),
                };
                weakness.Show(revealed);
                weaknesses.Add(weakness);
            }
            return weaknesses.ToArray();
        }

        /// <summary>A pixel-art image on the stage at 4 px per dot, centered in its parent.</summary>
        private static GameObject PixelIcon(RectTransform parent, string name, Texture2D texture)
        {
            var icon = Rect(name, parent);
            Place(icon, Vector2.zero, new Vector2(texture.width, texture.height) * DotSize);
            AddRaw(icon, texture).material = EnsurePixelArtMaterial();
            icon.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = DotSize;
            return icon.gameObject;
        }

        private static RawImage AddRaw(RectTransform rect, Texture2D texture)
        {
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// The drawn part of a character's square canvas, where a card is dropped to target it.
        /// </summary>
        private static RectTransform TargetArea(RectTransform unit, Texture2D texture, float dot)
        {
            var bounds = OpaqueBounds(texture);
            var target = Rect("TargetArea", unit);
            Place(
                target,
                new Vector2((bounds.center.x - texture.width * 0.5f) * dot, bounds.center.y * dot),
                bounds.size * dot
            );
            return target;
        }

        /// <summary>
        /// The cursor over a character's head, beside its target area. Hidden until a card that
        /// can target the character is up; the view bobs it by one dot.
        /// </summary>
        private static RectTransform TargetMarker(RectTransform area, Texture2D cursor)
        {
            var size = new Vector2(cursor.width, cursor.height) * DotSize;
            var marker = Rect("Marker", area.parent);
            Place(
                marker,
                area.anchoredPosition
                    + new Vector2(0, (area.sizeDelta.y + size.y) * 0.5f + 2 * DotSize),
                size
            );
            PixelIcon(marker, "Cursor", cursor);
            marker.gameObject.SetActive(false);
            return marker;
        }

        /// <summary>
        /// The ally's name just under its HP bar, on the dark translucent band and in the colour of
        /// the owner line on the cards, so the name on a raised card points at the ally. It moves
        /// with the bar and is shown with the caster ring.
        /// </summary>
        private static GameObject NameTag(RectTransform bar, string label)
        {
            var instance = InstantiatePrefab(TranslucentTextPanelAssets.PrefabPath, "NameTag", bar);
            var rect = (RectTransform)instance.transform;
            Place(rect, new Vector2(0, -28), new Vector2(112, 30));
            var panel = instance.GetComponent<TranslucentTextPanel>();
            panel.Backdrop.color = new Color(0.02f, 0.025f, 0.04f);
            panel.SetBackdropSize(new Vector2(160, 44));
            panel.SetBackdropAlpha(0.8f);
            panel.SetFontSize(26);
            panel.SetText(label);
            panel.Label.color = CardOwner;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Backdrop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.BackdropCanvas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Label);
            instance.SetActive(false);
            return instance;
        }

        /// <summary>
        /// A clear catcher over the battlefield and under the controls. The view turns it on while
        /// a tapped card waits, so the next tap picks the target or puts the card back.
        /// </summary>
        private static void BuildTapArea(RectTransform root, BattleInspectView view)
        {
            var area = Rect("TapArea", root);
            Stretch(area);
            AddImage(area, Color.clear, true);
            area.gameObject.AddComponent<BattleInspectTapArea>().View = view;
            area.gameObject.SetActive(false);
            view.TapArea = area.gameObject;
        }

        /// <summary>
        /// The dots that run from the centre of a swiped card to the finger, behind the hand. The
        /// view places them each frame while a card is swiped and hides them otherwise.
        /// </summary>
        private static void BuildAim(RectTransform safe, BattleInspectView view)
        {
            var aim = Rect("Aim", safe);
            Stretch(aim);
            var art = Art("AimDot");
            var dots = new List<RawImage>();
            for (int i = 0; i < AimDotCount; i++)
            {
                var dot = Rect("Dot" + i, aim);
                Place(dot, Vector2.zero, new Vector2(art.width, art.height) * DotSize);
                dots.Add(AddRaw(dot, art));
            }
            // Behind the hand, so the dots come out from under the card.
            aim.SetSiblingIndex(view.Hand.GetSiblingIndex());
            aim.gameObject.SetActive(false);
            view.Aim = aim;
            view.AimDots = dots.ToArray();
        }

        /// <summary>
        /// The turn order along the top, like the timelines of Octopath Traveler and Romancing
        /// SaGa 2. It starts at the top left, the current turn first, so "now" stays in one place
        /// when a defeated enemy leaves the order, and it keeps clear of the skill name in the top
        /// middle. The party acts as one side (by its total speed), so a party turn is one box
        /// with the party crest; each enemy has its own box with its face. The frames are blue for
        /// the party and red for an enemy, and gold for the current turn.
        /// </summary>
        private static void BuildTurnOrder(RectTransform safe, BattleInspectView view)
        {
            var bar = Rect("TurnOrder", safe);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = new Vector2(32, -24);
            bar.sizeDelta = new Vector2(0, TurnSlotSize);
            var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 2 * DotSize;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            float iconSize = TurnIconDots * DotSize;
            var frameArt = Art("TurnSlotFrame");
            var fillArt = Art("TurnSlotFill");
            var crest = Art("TurnPartyCrest");
            var slots = new List<BattleInspectTurnSlot>();
            for (int i = 0; i < TurnSlotCount; i++)
            {
                var slot = Rect("Slot" + i, bar);
                var layout = slot.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = layout.preferredHeight = TurnSlotSize;
                var fill = Rect("Fill", slot);
                Stretch(fill);
                var fillImage = AddRaw(fill, fillArt);

                var party = Rect("Party", slot);
                Place(party, Vector2.zero, Vector2.one * iconSize);
                AddRaw(party, crest);

                var enemyRect = Rect("Enemy", slot);
                Place(enemyRect, Vector2.zero, Vector2.one * iconSize);
                var enemyImage = enemyRect.gameObject.AddComponent<RawImage>();
                enemyImage.raycastTarget = false;

                // Over the icons, whose corners it notches.
                var frame = Rect("Frame", slot);
                Stretch(frame);
                var frameImage = AddRaw(frame, frameArt);

                // Bake the opening order so the prefab preview matches the first frame.
                int entry = TurnCycle[i % TurnCycle.Length];
                bool isParty = entry == BattleInspectView.PartyTurn;
                party.gameObject.SetActive(isParty);
                enemyRect.gameObject.SetActive(!isParty);
                frameImage.color =
                    i == 0 ? view.CurrentTurnFrame
                    : isParty ? view.PartyTurnFrame
                    : view.EnemyTurnFrame;
                fillImage.color = isParty ? view.PartyTurnFill : view.EnemyTurnFill;
                if (!isParty)
                {
                    var enemy = view.Enemies[entry];
                    enemyImage.texture = enemy.Sprite.texture;
                    enemyImage.uvRect = enemy.IconUv;
                }

                slots.Add(
                    new BattleInspectTurnSlot
                    {
                        Layout = layout,
                        Party = party.gameObject,
                        Enemy = enemyImage,
                        Frame = frameImage,
                        Fill = fillImage,
                    }
                );
            }
            view.TurnCycle = TurnCycle;
            view.TurnSlots = slots.ToArray();
            view.PartySlotWidth = view.EnemySlotWidth = TurnSlotSize;
        }

        /// <summary>UV of a square face crop given by its top-left dot (rows counted from the top).</summary>
        private static UnityEngine.Rect FaceUv(Texture2D texture, Vector2Int topLeft, int size) =>
            new(
                topLeft.x / (float)texture.width,
                (texture.height - topLeft.y - size) / (float)texture.height,
                size / (float)texture.width,
                size / (float)texture.height
            );

        /// <summary>
        /// The name of the skill being used, at the top middle on the dark translucent band that
        /// Top shows behind "LOADING...". Hidden until a skill is used. The band (112 high) starts
        /// 12 px below the turn order, which reaches past its left end on a 16:9 screen.
        /// </summary>
        private static void BuildSkillBanner(RectTransform safe, BattleInspectView view)
        {
            var instance = InstantiatePrefab(
                TranslucentTextPanelAssets.PrefabPath,
                "SkillName",
                safe
            );
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, -196);
            rect.sizeDelta = new Vector2(560, 80);
            var panel = instance.GetComponent<TranslucentTextPanel>();
            // Darker than Top's gray band so it reads over the lit battlefield, as the corners do.
            panel.Backdrop.color = new Color(0.02f, 0.025f, 0.04f);
            panel.SetBackdropSize(new Vector2(720, 112));
            panel.SetBackdropAlpha(0.8f);
            panel.SetFontSize(48);
            panel.SetText(Cards[0].Name);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Backdrop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.BackdropCanvas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Label);
            view.SkillBanner = panel;
            view.SkillBannerGroup = instance.AddComponent<CanvasGroup>();
            view.SkillBannerGroup.blocksRaycasts = false;
            instance.SetActive(false);
        }

        /// <summary>
        /// The energy: a small gem and the number on the soft dark band that Top shows behind
        /// "LOADING...", in the bottom-left corner beside the hand, above the deck.
        /// </summary>
        private static void BuildEnergy(RectTransform safe, BattleInspectView view)
        {
            // Baked with the opening turn so the screen reads the same outside Play Mode.
            int maxEnergy = BattleInspectView.MaxEnergyAt(view.StartTurn);
            var (label, _) = CornerCounter(
                safe,
                "Energy",
                EnergyBottom,
                BattleInspectView.EnergyText(maxEnergy, maxEnergy),
                "Gem",
                Art("EnergyGem")
            );
            view.EnergyLabel = label;
        }

        /// <summary>
        /// The deck under the energy: a small stack of card backs and the number of cards left.
        /// Dealt cards come out of the right edge of this counter.
        /// </summary>
        private static void BuildDeck(RectTransform safe, BattleInspectView view)
        {
            // Baked with the opening hand dealt, as the prefab shows it.
            int left = DeckSize - Mathf.Min(view.Settings.OpeningDraw, view.Settings.HandLimit);
            var (label, counter) = CornerCounter(
                safe,
                "Deck",
                DeckBottom,
                Mathf.Max(0, left).ToString(),
                "Stack",
                Art("DeckIcon")
            );
            view.DeckLabel = label;
            view.DeckAnchor = counter;
        }

        /// <summary>A picture and a number on the dark translucent band, in the bottom-left corner.</summary>
        private static (TMP_Text label, RectTransform counter) CornerCounter(
            RectTransform safe,
            string name,
            float bottom,
            string text,
            string iconName,
            Texture2D art
        )
        {
            var instance = InstantiatePrefab(TranslucentTextPanelAssets.PrefabPath, name, safe);
            var rect = (RectTransform)instance.transform;
            Corner(rect, Vector2.zero, new Vector2(32, bottom), new Vector2(232, 80));
            var panel = instance.GetComponent<TranslucentTextPanel>();
            // Darker than Top's gray band so it reads over the lit floor of the battlefield.
            panel.Backdrop.color = new Color(0.02f, 0.025f, 0.04f);
            panel.SetBackdropSize(new Vector2(320, 104));
            panel.SetBackdropAlpha(0.8f);
            panel.SetFontSize(52);
            panel.SetText(text);
            panel.Label.rectTransform.offsetMin = new Vector2(56, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Backdrop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.BackdropCanvas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Label.rectTransform);

            var icon = Rect(iconName, rect);
            Place(icon, new Vector2(-64, 0), new Vector2(art.width, art.height) * DotSize);
            AddRaw(icon, art);
            return (panel.Label, rect);
        }

        /// <summary>
        /// The hand, fanned below the screen so only each card's art and the owner, name and kind
        /// lines over its foot show; the description is below them. Cards may overlap. Pressing a
        /// card springs it up to show the details, and it is
        /// played by swiping it up and dropping it on a target (see BattleInspectView). Tilting
        /// the pixel-art cards is an agreed exception to the pixel art standard
        /// (doc/art/direction.md). The frame's colour shows the card's element.
        /// </summary>
        private static void BuildHand(RectTransform safe, BattleInspectView view)
        {
            var hand = Rect("Hand", safe);
            hand.anchorMin = hand.anchorMax = hand.pivot = new Vector2(0.5f, 0f);
            hand.anchoredPosition = Vector2.zero;
            hand.sizeDelta = Vector2.zero;
            view.Hand = hand;
            var settings = view.Settings;

            // Baked with the opening hand fanned out, so the prefab preview shows it; the rest of
            // the deck is hidden until dealt.
            int opening = Mathf.Min(settings.OpeningDraw, settings.HandLimit, DeckSize);
            // Cards the opening energy cannot pay for are greyed out as in Play Mode.
            int energy = BattleInspectView.MaxEnergyAt(view.StartTurn);
            float step = BattleInspectView.FanStepFor(
                opening,
                settings.FanStep,
                settings.FanMaxSpread
            );
            // The deck is data; the cards on screen show whichever of it is drawn. There are as
            // many cards on screen as in the deck (more than the hand holds), each baked with one
            // card of the deck for the preview.
            var deck = new List<BattleInspectCardData>();
            var cards = new List<BattleInspectCard>();
            for (int i = 0; i < DeckSize; i++)
            {
                var spec = Cards[i % Cards.Length];
                int copy = i / Cards.Length;
                var data = new BattleInspectCardData
                {
                    Name = spec.Name,
                    Owner = Allies[spec.Owner].Label,
                    Kind = KindLine(spec.Effect),
                    Description = Description(spec.Text, spec.Effect, spec.Power),
                    Art = Art(spec.Art),
                    Frame = Art("CardFrame" + spec.Element),
                    ElementIcon =
                        spec.Element == BattleInspectElement.None
                            ? null
                            : Art("Element" + spec.Element),
                    Cost = spec.Cost,
                    Power = spec.Power,
                    Element = spec.Element,
                    Effect = spec.Effect,
                    Caster = spec.Owner,
                };
                deck.Add(data);
                var instance = InstantiatePrefab(
                    CardPrefabPath,
                    "Card" + spec.Art.Replace("Card", "") + (copy > 0 ? (copy + 1).ToString() : ""),
                    hand
                );
                var card = (RectTransform)instance.transform;
                var (position, angle) = BattleInspectView.FanPose(
                    Mathf.Min(i, opening - 1),
                    opening,
                    step,
                    settings.FanRadius,
                    settings.FanDrop,
                    settings.RestBottom
                );
                card.anchoredPosition = position;
                card.localRotation = Quaternion.Euler(0, 0, angle);
                card.localScale = Vector3.one * settings.CardScale;
                bool playable = spec.Cost <= energy;
                var group = instance.GetComponent<CanvasGroup>();
                group.alpha =
                    i >= opening ? 0f
                    : playable ? 1f
                    : BattleInspectView.UnplayableAlpha(settings, raised: false);
                group.blocksRaycasts = i < opening;

                var cardView = instance.GetComponent<BattleInspectCardView>();
                cardView.Show(
                    data.Name,
                    data.Owner,
                    data.Kind,
                    data.Description,
                    data.Art,
                    data.Frame,
                    data.ElementIcon,
                    data.Cost
                );
                cardView.SetPlayable(
                    playable,
                    BattleInspectView.VeilDarkness(settings, group.alpha)
                );
                var input = instance.GetComponent<BattleInspectCardInput>();
                input.View = view;
                input.Index = i;
                // Keep only the per-card data as overrides; the layout stays the prefab's.
                foreach (
                    var component in new Component[]
                    {
                        card,
                        group,
                        input,
                        cardView.Owner,
                        cardView.Name,
                        cardView.Kind,
                        cardView.Description,
                        cardView.Art,
                        cardView.Frame,
                        cardView.Element,
                        cardView.CostDigit,
                        cardView.Shade,
                        cardView.Bands[0],
                        cardView.Bands[1],
                    }
                )
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);

                cards.Add(
                    new BattleInspectCard
                    {
                        Body = card,
                        Group = group,
                        Face = cardView,
                        Back = cardView.Back.gameObject,
                        Cost = spec.Cost,
                        Power = spec.Power,
                        Element = spec.Element,
                        Effect = spec.Effect,
                        Caster = spec.Owner,
                        DeckIndex = i,
                    }
                );
            }
            view.Deck = deck.ToArray();
            view.Cards = cards.ToArray();
        }

        /// <summary>
        /// Places a card label in a box given in dots from the card's top-left. The text shrinks to
        /// fit a long name or effect instead of running out of the box.
        /// </summary>
        private static void CardText(
            TMP_Text label,
            float left,
            float top,
            float width,
            float height
        )
        {
            Corner(
                label.rectTransform,
                new Vector2(0, 1),
                new Vector2(left, -top) * CardDot,
                new Vector2(width, height) * CardDot
            );
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = 12;
            label.enableAutoSizing = true;
        }

        /// <summary>A quiet end-turn button: a thin muted-gold frame around a dark inside.</summary>
        private static void BuildEndTurn(RectTransform safe, BattleInspectView view)
        {
            var art = Art("EndTurnFrame");
            var button = Rect("EndTurn", safe);
            Corner(
                button,
                new Vector2(1, 0),
                new Vector2(-40, 40),
                new Vector2(art.width, art.height) * DotSize
            );
            var face = AddRaw(button, art);
            face.raycastTarget = true;
            var label = Label(
                button,
                "Label",
                "ターン終了",
                34,
                new Color(0.84f, 0.86f, 0.91f),
                TextAlignmentOptions.Center
            );
            Stretch(label.rectTransform);
            view.EndTurnButton = AddButton(button, face);
        }

        private static void BuildPopup(RectTransform root, BattleInspectView view)
        {
            var popup = Label(root, "Popup", "0", 60, TextMain, TextAlignmentOptions.Center);
            Place(popup.rectTransform, Vector2.zero, new Vector2(480, 90));
            popup.outlineWidth = 0f;
            view.PopupTemplate = popup;
            popup.gameObject.SetActive(false);

            view.DamageNumber = DamageNumberAssets.Label(
                root,
                "DamageNumber",
                "0",
                DamageNumberAssets.Normal
            );
            view.WeakNumber = DamageNumberAssets.Label(
                root,
                "WeakNumber",
                "0",
                DamageNumberAssets.Weak
            );
            view.HealNumber = DamageNumberAssets.Label(
                root,
                "HealNumber",
                "0",
                DamageNumberAssets.Heal
            );
            view.DamageNumber.gameObject.SetActive(false);
            view.WeakNumber.gameObject.SetActive(false);
            view.HealNumber.gameObject.SetActive(false);
        }

        // The rising numbers only exist while playing, so the showcase gets them laid out on the
        // battle background.
        private static void BuildNumberSamples()
        {
            var root = CanvasRoot("BattleDamageNumbers");
            BuildBackground(root);
            var samples = new[]
            {
                ("通常", "128", DamageNumberAssets.Normal),
                ("弱点", "192", DamageNumberAssets.Weak),
                ("回復", "40", DamageNumberAssets.Heal),
            };
            for (int i = 0; i < samples.Length; i++)
            {
                var (caption, text, style) = samples[i];
                float x = (i - 1) * 440f;
                var number = DamageNumberAssets.Label(root, caption, text, style);
                number.rectTransform.anchoredPosition = new Vector2(x, 0f);
                var label = Label(
                    root,
                    caption + "Caption",
                    caption,
                    32,
                    TextMain,
                    TextAlignmentOptions.Center
                );
                Place(label.rectTransform, new Vector2(x, -120f), new Vector2(320, 48));
            }
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, NumberSamplesPath);
        }

        private static RectTransform HpBar(
            RectTransform parent,
            Vector2 center,
            float width,
            float height,
            Color color
        )
        {
            var bar = Rect("HpBar", parent);
            Place(bar, center, new Vector2(width, height));
            AddImage(bar, HpBack, false);
            var fill = Rect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(2, 2);
            fill.offsetMax = new Vector2(-2, -2);
            AddImage(fill, color, false);
            return fill;
        }

        /// <summary>"攻撃・敵単体": the kind in its colour, then who it reaches (rich text).</summary>
        private static string KindLine(BattleInspectCardEffect effect)
        {
            var (kind, scope) = effect switch
            {
                BattleInspectCardEffect.DamageOne => ("攻撃", "敵単体"),
                BattleInspectCardEffect.DamageAll => ("攻撃", "敵全体"),
                BattleInspectCardEffect.Heal => ("回復", "味方単体"),
                _ => ("防御", "味方全体"),
            };
            return $"<color=#{KindHex(effect)}>{kind}</color><color=#{SeparatorHex}>・</color>{scope}";
        }

        /// <summary>The description with the power in the kind's colour (rich text).</summary>
        private static string Description(string text, BattleInspectCardEffect effect, int power) =>
            string.Format(text, $"<color=#{PowerHex(effect)}>{power}</color>");

        private static string KindHex(BattleInspectCardEffect effect) =>
            effect switch
            {
                BattleInspectCardEffect.Heal => "96e678",
                BattleInspectCardEffect.Guard => "96c8ff",
                _ => "ff966e",
            };

        private static string PowerHex(BattleInspectCardEffect effect) =>
            effect switch
            {
                BattleInspectCardEffect.Heal => "8ce878",
                BattleInspectCardEffect.Guard => "96c8ff",
                _ => "ffce60",
            };

        /// <summary>The drawn part of a sprite in dots, with y measured up from the bottom.</summary>
        private static UnityEngine.Rect OpaqueBounds(Texture2D texture)
        {
            var pixels = AssetDatabase.GetAssetPath(texture) is { } path
                ? AsepriteCanvasImport.IsAseprite(path)
                    ? AsepriteCanvasImport.ReadFramePixels(path)
                    : LoadReadable(path)
                : null;
            if (pixels == null)
                return new UnityEngine.Rect(0, 0, texture.width, texture.height);
            int minX = texture.width,
                minY = texture.height,
                maxX = -1,
                maxY = -1;
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a < 128)
                    continue;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            return maxX < 0
                ? new UnityEngine.Rect(0, 0, texture.width, texture.height)
                : UnityEngine.Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
        }

        private static Color32[] LoadReadable(string path)
        {
            var copy = new Texture2D(2, 2);
            try
            {
                return copy.LoadImage(File.ReadAllBytes(path)) ? copy.GetPixels32() : null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }
    }
}
