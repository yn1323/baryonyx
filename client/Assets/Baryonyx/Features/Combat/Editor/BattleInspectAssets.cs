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
        public const string CardPrefabPath =
            "Assets/Baryonyx/Features/Combat/UI/BattleInspectCard.prefab";
        public const string HandSettingsPath =
            "Assets/Baryonyx/Features/Combat/UI/BattleInspectHandSettings.asset";
        public const string ArtFolder = "Assets/Baryonyx/Features/Combat/UI/Art";

        private const float DotSize = 4f;

        // Enemies are drawn at 3 px per dot so they do not crowd the screen, and the cards at 6 px
        // per dot (1.25x) so the hand reads well; both are agreed exceptions (doc/art/direction.md).
        private const float EnemyDotSize = 3f;
        private const float CardDot = 5f;
        public const string FlashMaterialPath =
            "Assets/Baryonyx/Features/Combat/UI/TargetFlash.mat";

        // The card frame is 42x59 dots, the 5:7 of trading cards (63x88 mm, as Pokemon and
        // Magic), with a thin rim in the element's colour. Like a Pokemon card, the art is a
        // landscape window (32x20 art drawn for it, at 1x) with a tall details panel below. Rows in dots: name 3..9, art 12..31, details 34..55 (effect, a line at 40,
        // description, target). The cost badge sits over the top-left corner.
        private const int ArtHeight = 20;
        private const float CardWidth = 42 * CardDot;
        private const float CardHeight = 59 * CardDot;
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
        private const int TurnSlotCount = 6;
        private const float TurnSlotHeight = 72f;
        private const float WeakIconSize = 48f;

        private static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        private static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        private static readonly Color TextFaint = new(0.604f, 0.580f, 0.514f);
        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);
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

            /// <summary>Top-left of the 12x12-dot face in the sprite (image rows from the top).</summary>
            public Vector2Int Face;
        }

        private sealed class EnemySpec
        {
            public string Name;
            public Vector2 Feet;
            public int Size;
            public int MaxHp;

            /// <summary>Weaknesses in icon order; those not in <see cref="Revealed"/> show "?".</summary>
            public BattleInspectElement[] Weaknesses;
            public BattleInspectElement[] Revealed;

            /// <summary>Top-left of the 16x16-dot face in the sprite (image rows from the top).</summary>
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

            /// <summary>The description under the effect, as on a Pokemon card's attack.</summary>
            public string Text;
        }

        // Back row first so the front row draws over it. The battlefield sits higher than the
        // screen's middle so the fanned hand below does not cover the HP bars.
        private static readonly AllySpec[] Allies =
        {
            new()
            {
                Name = "Toma",
                Label = "トーマ",
                Feet = new Vector2(-530, 20),
                Color = new Color(0.66f, 0.45f, 0.9f),
                Hp = 262,
                MaxHp = 300,
                Face = new Vector2Int(22, 23),
            },
            new()
            {
                Name = "Mina",
                Label = "ミナ",
                Feet = new Vector2(-770, 20),
                Color = new Color(0.33f, 0.8f, 0.76f),
                Hp = 284,
                MaxHp = 310,
                Face = new Vector2Int(26, 22),
            },
            new()
            {
                Name = "Aria",
                Label = "アリア",
                Feet = new Vector2(-410, -96),
                Color = new Color(0.9f, 0.32f, 0.3f),
                Hp = 418,
                MaxHp = 480,
                Face = new Vector2Int(27, 22),
            },
            new()
            {
                Name = "Luka",
                Label = "ルカ",
                Feet = new Vector2(-650, -96),
                Color = new Color(0.5f, 0.78f, 0.32f),
                Hp = 331,
                MaxHp = 360,
                Face = new Vector2Int(30, 24),
            },
        };

        // Aria leads the party and stands for it in the turn order.
        private const int Leader = 2;

        private static readonly EnemySpec[] Enemies =
        {
            new()
            {
                Name = "ForestGuardian",
                Feet = new Vector2(690, 8),
                Size = 128,
                MaxHp = 3200,
                Weaknesses = new[] { BattleInspectElement.Fire, BattleInspectElement.Slash },
                Revealed = new[] { BattleInspectElement.Fire },
                Face = new Vector2Int(16, 32),
            },
            new()
            {
                Name = "MossWolf",
                Feet = new Vector2(370, -68),
                Size = 96,
                MaxHp = 980,
                Weaknesses = new[] { BattleInspectElement.Ice, BattleInspectElement.Fire },
                Revealed = new[] { BattleInspectElement.Ice },
                Face = new Vector2Int(1, 53),
            },
            new()
            {
                Name = "MossSlime",
                Feet = new Vector2(90, -84),
                Size = 64,
                MaxHp = 420,
                Weaknesses = new[] { BattleInspectElement.Thunder, BattleInspectElement.Fire },
                Revealed = Array.Empty<BattleInspectElement>(),
                Face = new Vector2Int(6, 40),
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
                Text = "剣で敵1体を斬りつける。",
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
                Text = "炎の玉で敵1体を焼く。",
            },
            new()
            {
                Name = "アイスランス",
                Art = "CardIce",
                Cost = 2,
                Power = 276,
                Element = BattleInspectElement.Ice,
                Effect = BattleInspectCardEffect.DamageOne,
                Owner = 0,
                Text = "氷の槍で敵1体を貫く。",
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
                Text = "雷を落とし、敵全体を撃つ。",
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
                Text = "味方1体の傷を癒やす。",
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
                Text = "味方全体を守りの光で包む。",
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
                var root = CanvasRoot("BattleInspectScreen");
                var view = root.gameObject.AddComponent<BattleInspectView>();
                view.TargetFlash = EnsureFlashMaterial();
                view.Settings = EnsureHandSettings();

                BuildBackground(root);
                var idle = new List<RectTransform>();
                var idleSteps = new List<float>();
                var world = Rect("World", root);
                world.anchorMin = world.anchorMax = world.pivot = Vector2.one * 0.5f;
                world.sizeDelta = new Vector2(1920, 1080);
                world.gameObject.AddComponent<WorldLayerFit>();
                BuildParty(world, view, idle, idleSteps);
                BuildEnemies(world, view, idle, idleSteps);
                view.IdleActors = idle.ToArray();
                view.IdleSteps = idleSteps.ToArray();

                var safe = SafeArea(root);
                BuildTurnOrder(safe, view);
                BuildEnergy(safe, view);
                BuildHand(safe, view);
                BuildEndTurn(safe, view);
                BuildPopup(root, view);

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
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
        /// The card (42x59 dots at 5 px): name, a landscape art window (32x20 art at 1x), then the effect, a line in the element colour, the description and
        /// the target, with the cost over the top-left corner. Made only when missing (or on
        /// reset), so the layout can be adjusted in the Prefab editor. It shows the fire card until
        /// the screen fills each card in.
        /// </summary>
        private static void EnsureCardPrefab(bool rebuild)
        {
            if (!rebuild && AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) != null)
                return;

            var card = Rect("BattleInspectCard", null);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.gameObject.AddComponent<CanvasGroup>();
            var cardView = card.gameObject.AddComponent<BattleInspectCardView>();
            card.gameObject.AddComponent<BattleInspectCardInput>();

            // The art shows through the window of the frame drawn over it.
            var art = Rect("Art", card);
            Corner(
                art,
                new Vector2(0, 1),
                new Vector2(5, -12) * CardDot,
                new Vector2(32, ArtHeight) * CardDot
            );
            cardView.Art = AddRaw(art, Art("CardFire"));
            var frameRect = Rect("Frame", card);
            Stretch(frameRect);
            cardView.Frame = AddRaw(frameRect, Art("CardFrameFire"));
            cardView.Frame.raycastTarget = true;

            cardView.Name = Label(
                card,
                "Name",
                "ファイア",
                30,
                TextMain,
                TextAlignmentOptions.Center
            );
            CardText(cardView.Name, 4, 3, 34, 7);
            cardView.Effect = Label(
                card,
                "Effect",
                "318 ダメージ",
                26,
                Gold,
                TextAlignmentOptions.Center
            );
            CardText(cardView.Effect, 4, 35, 34, 5);
            cardView.Description = Label(
                card,
                "Description",
                "炎の玉で敵1体を焼く。",
                18,
                TextSub,
                TextAlignmentOptions.TopLeft
            );
            CardText(cardView.Description, 4, 41, 34, 9);
            cardView.Description.textWrappingMode = TextWrappingModes.Normal;
            cardView.Target = Label(
                card,
                "Target",
                "敵1体 ・ トーマ",
                18,
                TextFaint,
                TextAlignmentOptions.Center
            );
            CardText(cardView.Target, 4, 50, 34, 5);

            // The cost is a diamond frame with a digit (0 to 9, side by side) centred on it. Both
            // are even sizes, so the centred digit sits on whole dots.
            var costFrame = Art("CardCostFrame");
            var costDigits = Art("CardCostDigits");
            var cost = Rect("Cost", card);
            Corner(
                cost,
                new Vector2(0, 1),
                new Vector2(-4, 4) * CardDot,
                new Vector2(costFrame.width, costFrame.height) * CardDot
            );
            AddRaw(cost, costFrame);
            float cell = costDigits.width / (float)CostDigits;
            var digit = Rect("Digit", cost);
            Place(digit, Vector2.zero, new Vector2(cell, costDigits.height) * CardDot);
            cardView.CostDigit = AddRaw(digit, costDigits);
            cardView.CostDigit.uvRect = new UnityEngine.Rect(
                2f / CostDigits,
                0,
                1f / CostDigits,
                1
            );

            PrefabUtility.SaveAsPrefabAsset(card.gameObject, CardPrefabPath);
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

        private static void BuildBackground(RectTransform root)
        {
            var background = Art("BattleBackground");
            float aspect = background.width / (float)background.height;
            var image = Rect("Background", root).gameObject.AddComponent<RawImage>();
            image.texture = background;
            image.raycastTarget = false;
            image.gameObject.AddComponent<ResponsiveBackground>().AspectRatio = aspect;

            var fog = InstantiatePrefab(Hd2dAssets.FogPrefabPath, "FloorMist", root);
            Stretch((RectTransform)fog.transform);
            fog.AddComponent<ResponsiveBackground>().AspectRatio = aspect;

            Shade(root, "ShadeTop", top: true, 260f, 0.7f);
            Shade(root, "ShadeBottom", top: false, 360f, 0.85f);
        }

        private static void BuildParty(
            RectTransform world,
            BattleInspectView view,
            List<RectTransform> idle,
            List<float> idleSteps
        )
        {
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var sprites = new List<RawImage>();
            var areas = new List<RectTransform>();
            foreach (var ally in Allies)
            {
                var texture = Art("Battle" + ally.Name);
                var unit = Rect("Ally" + ally.Name, world);
                Place(unit, ally.Feet, Vector2.zero);
                Picture(unit, "Shadow", shadow, new Vector2(0, 2), new Vector2(150, 24), 0.5f);
                var pose = Rect("Pose", unit);
                Place(pose, Vector2.zero, Vector2.zero);
                sprites.Add(PixelActor(pose, "Sprite", texture, Vector2.zero, DotSize));
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
                allies.Add(
                    new BattleInspectAlly
                    {
                        Sprite = sprites[i],
                        TargetArea = areas[i],
                        HpFill = fill,
                        StartHp = ally.Hp,
                        MaxHp = ally.MaxHp,
                    }
                );
            }
            view.Allies = allies.ToArray();

            // Heal and guard popups rise over the middle of the party.
            var anchor = Rect("PartyAnchor", world);
            Place(anchor, new Vector2(-590, 200), new Vector2(200, 80));
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
                        MaxHp = spec.MaxHp,
                        Weaknesses = weaknesses,
                        IconUv = FaceUv(texture, spec.Face, 16),
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

        /// <summary>A pixel-art image at 4 px per dot, centered in its parent.</summary>
        private static GameObject PixelIcon(RectTransform parent, string name, Texture2D texture)
        {
            var icon = Rect(name, parent);
            Place(icon, Vector2.zero, new Vector2(texture.width, texture.height) * DotSize);
            AddRaw(icon, texture);
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
        /// The turn order along the top, like the timelines of Octopath Traveler and Romancing
        /// SaGa 2. The party acts as one side (by its total speed), so a party turn is one box
        /// with the leader's face; each enemy has its own box.
        /// </summary>
        private static void BuildTurnOrder(RectTransform safe, BattleInspectView view)
        {
            var bar = Rect("TurnOrder", safe);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(20, -16);
            bar.sizeDelta = new Vector2(0, TurnSlotHeight);
            var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            float faceSize = 12 * DotSize;
            float slotWidth = 8 + 16 * DotSize;
            var leader = Allies[Leader];
            var leaderTexture = Art("Battle" + leader.Name);
            var slots = new List<BattleInspectTurnSlot>();
            for (int i = 0; i < TurnSlotCount; i++)
            {
                var slot = Rect("Slot" + i, bar);
                var layout = slot.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = TurnSlotHeight;
                Sliced(
                    slot,
                    UiArt.RoundedRectPath,
                    new Color(0.03f, 0.04f, 0.07f, 0.88f)
                ).raycastTarget = false;
                var ring = Rect("Ring", slot);
                Stretch(ring);
                Sliced(
                    ring,
                    UiArt.RoundedRingPath,
                    i == 0 ? Gold : new Color(0.55f, 0.55f, 0.6f, 0.7f)
                ).raycastTarget = false;

                // Only the leader stands for the party, so every box has the same width.
                var party = Rect("Party", slot);
                Place(party, Vector2.zero, Vector2.one * faceSize);
                var partyImage = party.gameObject.AddComponent<RawImage>();
                partyImage.texture = leaderTexture;
                partyImage.uvRect = FaceUv(leaderTexture, leader.Face, 12);
                partyImage.raycastTarget = false;

                var enemyRect = Rect("Enemy", slot);
                Place(enemyRect, Vector2.zero, Vector2.one * 16 * DotSize);
                var enemyImage = enemyRect.gameObject.AddComponent<RawImage>();
                enemyImage.raycastTarget = false;

                // Bake the opening order so the prefab preview matches the first frame.
                int entry = TurnCycle[i % TurnCycle.Length];
                bool isParty = entry == BattleInspectView.PartyTurn;
                party.gameObject.SetActive(isParty);
                enemyRect.gameObject.SetActive(!isParty);
                layout.preferredWidth = slotWidth;
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
                    }
                );
            }
            view.TurnCycle = TurnCycle;
            view.TurnSlots = slots.ToArray();
            view.PartySlotWidth = view.EnemySlotWidth = slotWidth;
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
        /// The energy: a small gem and the number on the soft dark band that Top shows behind
        /// "LOADING...", in the bottom-left corner beside the hand.
        /// </summary>
        private static void BuildEnergy(RectTransform safe, BattleInspectView view)
        {
            var instance = InstantiatePrefab(TranslucentTextPanelAssets.PrefabPath, "Energy", safe);
            var rect = (RectTransform)instance.transform;
            Corner(rect, Vector2.zero, new Vector2(32, 40), new Vector2(232, 80));
            var panel = instance.GetComponent<TranslucentTextPanel>();
            // Darker than Top's gray band so it reads over the lit floor of the battlefield.
            panel.Backdrop.color = new Color(0.02f, 0.025f, 0.04f);
            panel.SetBackdropSize(new Vector2(320, 120));
            panel.SetBackdropAlpha(0.8f);
            panel.SetFontSize(52);
            // Baked with the opening turn so the screen reads the same outside Play Mode.
            int maxEnergy = BattleInspectView.MaxEnergyAt(view.StartTurn);
            panel.SetText(BattleInspectView.EnergyText(maxEnergy, maxEnergy));
            panel.Label.rectTransform.offsetMin = new Vector2(56, 0);
            view.EnergyLabel = panel.Label;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Backdrop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.BackdropCanvas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Label.rectTransform);

            var gem = Art("EnergyGem");
            var icon = Rect("Gem", rect);
            Place(icon, new Vector2(-64, 0), new Vector2(gem.width, gem.height) * DotSize);
            AddRaw(icon, gem);
        }

        /// <summary>
        /// The hand, fanned below the screen so only each card's name and art show: name on top,
        /// the art in the middle and the effect and target below, like Slay the Spire or Pokemon
        /// cards. Cards may overlap. Pressing a card springs it up to show the details, and it is
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

            var cards = new List<BattleInspectCard>();
            for (int i = 0; i < Cards.Length; i++)
            {
                var spec = Cards[i];
                var owner = Allies[spec.Owner];
                var instance = InstantiatePrefab(
                    CardPrefabPath,
                    "Card" + spec.Art.Replace("Card", ""),
                    hand
                );
                var card = (RectTransform)instance.transform;
                // Baked with the settings so the prefab preview shows the fan.
                var (position, angle) = BattleInspectView.FanPose(
                    i,
                    Cards.Length,
                    settings.FanStep,
                    settings.FanRadius,
                    settings.FanDrop,
                    settings.RestBottom
                );
                card.anchoredPosition = position;
                card.localRotation = Quaternion.Euler(0, 0, angle);
                card.localScale = Vector3.one * settings.CardScale;

                var cardView = instance.GetComponent<BattleInspectCardView>();
                cardView.Show(
                    spec.Name,
                    EffectValue(spec),
                    spec.Text,
                    EffectScope(spec.Effect) + " ・ " + owner.Label,
                    Art(spec.Art),
                    Art("CardFrame" + spec.Element),
                    spec.Cost
                );
                var input = instance.GetComponent<BattleInspectCardInput>();
                input.View = view;
                input.Index = i;
                // Keep only the per-card data as overrides; the layout stays the prefab's.
                foreach (
                    var component in new Component[]
                    {
                        card,
                        input,
                        cardView.Name,
                        cardView.Effect,
                        cardView.Description,
                        cardView.Target,
                        cardView.Art,
                        cardView.Frame,
                        cardView.CostDigit,
                    }
                )
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);

                cards.Add(
                    new BattleInspectCard
                    {
                        Body = card,
                        Group = instance.GetComponent<CanvasGroup>(),
                        Cost = spec.Cost,
                        Power = spec.Power,
                        Element = spec.Element,
                        Effect = spec.Effect,
                    }
                );
            }
            view.Cards = cards.ToArray();
        }

        /// <summary>
        /// Places a card label in a box given in dots from the card's top-left. The text shrinks to
        /// fit a long name or effect instead of running out of the box.
        /// </summary>
        private static void CardText(TMP_Text label, int left, int top, int width, int height)
        {
            Corner(
                label.rectTransform,
                new Vector2(0, 1),
                new Vector2(left, -top) * CardDot,
                new Vector2(width, height) * CardDot
            );
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = 16;
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

        private static string EffectValue(CardSpec spec) =>
            spec.Effect switch
            {
                BattleInspectCardEffect.Heal => $"{spec.Power} 回復",
                BattleInspectCardEffect.Guard => $"ブロック {spec.Power}",
                _ => $"{spec.Power} ダメージ",
            };

        private static string EffectScope(BattleInspectCardEffect effect) =>
            effect switch
            {
                BattleInspectCardEffect.DamageOne => "敵1体",
                BattleInspectCardEffect.DamageAll => "敵全体",
                BattleInspectCardEffect.Heal => "味方1体",
                _ => "味方全体",
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
