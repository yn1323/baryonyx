using System;
using System.Collections.Generic;
using System.IO;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
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
        public const string ArtFolder = "Assets/Baryonyx/Features/Combat/UI/Art";

        private const float DotSize = 4f;
        private const float CardWidth = 180f;
        private const float CardHeight = 252f;
        private const float CardSpacing = 12f;

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
        private const float PartyFaceStep = 40f;

        private static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        private static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        private static readonly Color TextFaint = new(0.604f, 0.580f, 0.514f);
        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);
        private static readonly Color Plate = new(0.012f, 0.02f, 0.04f, 0.88f);
        private static readonly Color CardFace = new(0.075f, 0.082f, 0.118f, 1f);
        private static readonly Color HpBack = new(0.08f, 0.06f, 0.06f, 0.9f);
        private static readonly Color HpEnemy = new(0.66f, 0.16f, 0.14f);
        private static readonly Color HpAlly = new(0.2f, 0.52f, 0.26f);
        private static readonly Color IntentAttack = new(0.62f, 0.14f, 0.12f, 0.92f);
        private static readonly Color IntentGuard = new(0.16f, 0.3f, 0.6f, 0.92f);
        private static readonly Color IntentWarn = new(0.85f, 0.45f, 0.1f, 0.95f);

        private sealed class AllySpec
        {
            public string Name;
            public string Label;
            public Vector2 Feet;
            public Color Color;
            public int Hp;
            public int MaxHp;
            public int Cooldown;

            /// <summary>Top-left of the 12x12-dot face in the sprite (image rows from the top).</summary>
            public Vector2Int Face;
        }

        private sealed class EnemySpec
        {
            public string Name;
            public string Label;
            public Vector2 Feet;
            public int Size;
            public int MaxHp;
            public BattleInspectElement Weakness;
            public string Intent;
            public Color IntentColor;
            public string Warning;

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
        }

        // Back row first so the front row draws over it.
        private static readonly AllySpec[] Allies =
        {
            new()
            {
                Name = "Toma",
                Label = "トーマ",
                Feet = new Vector2(-450, -60),
                Color = new Color(0.66f, 0.45f, 0.9f),
                Hp = 262,
                MaxHp = 300,
                Cooldown = 2,
                Face = new Vector2Int(22, 23),
            },
            new()
            {
                Name = "Mina",
                Label = "ミナ",
                Feet = new Vector2(-690, -60),
                Color = new Color(0.33f, 0.8f, 0.76f),
                Hp = 284,
                MaxHp = 310,
                Cooldown = 1,
                Face = new Vector2Int(26, 22),
            },
            new()
            {
                Name = "Aria",
                Label = "アリア",
                Feet = new Vector2(-330, -176),
                Color = new Color(0.9f, 0.32f, 0.3f),
                Hp = 418,
                MaxHp = 480,
                Cooldown = 0,
                Face = new Vector2Int(27, 22),
            },
            new()
            {
                Name = "Luka",
                Label = "ルカ",
                Feet = new Vector2(-570, -176),
                Color = new Color(0.5f, 0.78f, 0.32f),
                Hp = 331,
                MaxHp = 360,
                Cooldown = 0,
                Face = new Vector2Int(30, 24),
            },
        };

        // Party panel order (not draw order).
        private static readonly int[] PanelOrder = { 2, 0, 3, 1 };

        private static readonly EnemySpec[] Enemies =
        {
            new()
            {
                Name = "ForestGuardian",
                Label = "森の守り手",
                Feet = new Vector2(690, -72),
                Size = 128,
                MaxHp = 3200,
                Weakness = BattleInspectElement.Fire,
                Intent = "防御 +300",
                IntentColor = IntentGuard,
                Warning = "大技まで 2ターン",
                Face = new Vector2Int(16, 32),
            },
            new()
            {
                Name = "MossWolf",
                Label = "苔むした狼",
                Feet = new Vector2(330, -148),
                Size = 96,
                MaxHp = 980,
                Weakness = BattleInspectElement.Ice,
                Intent = "攻撃 45×2",
                Face = new Vector2Int(1, 53),
                IntentColor = IntentAttack,
            },
            new()
            {
                Name = "MossSlime",
                Label = "苔スライム",
                Feet = new Vector2(40, -164),
                Size = 64,
                MaxHp = 420,
                Weakness = BattleInspectElement.Thunder,
                Intent = "攻撃 60",
                Face = new Vector2Int(6, 40),
                IntentColor = IntentAttack,
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
                var root = CanvasRoot("BattleInspectScreen");
                var view = root.gameObject.AddComponent<BattleInspectView>();

                BuildBackground(root);
                var idle = new List<RectTransform>();
                var world = Rect("World", root);
                world.anchorMin = world.anchorMax = world.pivot = Vector2.one * 0.5f;
                world.sizeDelta = new Vector2(1920, 1080);
                world.gameObject.AddComponent<WorldLayerFit>();
                BuildParty(world, view, idle);
                BuildEnemies(world, view, idle);
                view.IdleActors = idle.ToArray();

                var safe = SafeArea(root);
                BuildPartyPanel(safe, view);
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
            List<RectTransform> idle
        )
        {
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            foreach (var ally in Allies)
            {
                var unit = Rect("Ally" + ally.Name, world);
                Place(unit, ally.Feet, Vector2.zero);
                Picture(unit, "Shadow", shadow, new Vector2(0, 2), new Vector2(150, 24), 0.5f);
                var pose = Rect("Pose", unit);
                Place(pose, Vector2.zero, Vector2.zero);
                PixelActor(pose, "Sprite", Art("Battle" + ally.Name), Vector2.zero, DotSize);
                idle.Add(pose);
            }

            // Heal and guard popups rise over the middle of the party.
            var anchor = Rect("PartyAnchor", world);
            Place(anchor, new Vector2(-510, 120), new Vector2(200, 80));
            view.PartyAnchor = anchor;
        }

        private static void BuildEnemies(
            RectTransform world,
            BattleInspectView view,
            List<RectTransform> idle
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

                float width = spec.Size * DotSize;
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
                var sprite = PixelActor(pose, "Sprite", texture, Vector2.zero, DotSize);
                idle.Add(pose);

                // The visible body (the drawn part of the square canvas) is the tap target.
                var bounds = OpaqueBounds(texture);
                var target = Rect("TargetArea", body);
                Place(
                    target,
                    new Vector2(
                        (bounds.center.x - texture.width * 0.5f) * DotSize,
                        bounds.center.y * DotSize
                    ),
                    bounds.size * DotSize + new Vector2(16, 16)
                );
                var button = AddButton(target, AddImage(target, Color.clear, true));
                button.transition = Selectable.Transition.None;

                float top = bounds.yMax * DotSize;
                // The next action, and the turns left until a big attack, in one row over the head.
                var intents = Rect("Intents", body);
                Place(intents, new Vector2(0, top + 36), new Vector2(0, 48));
                var row = intents.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 12;
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = false;
                intents.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
                Chip(intents, "Intent", spec.Intent, 30, spec.IntentColor, fitWidth: false);
                if (spec.Warning != null)
                    Chip(intents, "Warning", spec.Warning, 26, IntentWarn, fitWidth: false);

                var info = Rect("Info", body);
                Place(info, new Vector2(0, -44), new Vector2(260, 64));
                var nameLabel = Label(
                    info,
                    "Name",
                    spec.Label,
                    24,
                    TextMain,
                    TextAlignmentOptions.Left
                );
                Place(nameLabel.rectTransform, new Vector2(0, 16), new Vector2(260, 30));
                var weak = Chip(
                    info,
                    "Weakness",
                    "弱点 " + ElementLabel(spec.Weakness),
                    20,
                    ElementColor(spec.Weakness) * new Color(0.55f, 0.55f, 0.55f, 0.95f)
                );
                weak.anchorMin = weak.anchorMax = weak.pivot = new Vector2(1, 0.5f);
                weak.anchoredPosition = new Vector2(0, 16);
                weak.sizeDelta = new Vector2(0, 30);
                var (fill, hpLabel) = HpBar(info, new Vector2(0, -16), 260, HpEnemy);
                hpLabel.text = $"{spec.MaxHp}/{spec.MaxHp}";

                enemies.Add(
                    new BattleInspectEnemy
                    {
                        Button = button,
                        Body = body,
                        TargetArea = target,
                        Sprite = sprite,
                        HpFill = fill,
                        HpLabel = hpLabel,
                        Group = group,
                        MaxHp = spec.MaxHp,
                        Weakness = spec.Weakness,
                        IconUv = FaceUv(texture, spec.Face, 16),
                    }
                );
            }

            // Screen order left to right: the slime is nearest the party.
            enemies.Reverse();
            view.Enemies = enemies.ToArray();
            view.TargetFrame = TargetFrame(world);
        }

        private static RectTransform TargetFrame(RectTransform parent)
        {
            var frame = Rect("TargetFrame", parent);
            Stretch(frame);
            const float length = 40f;
            const float thickness = 8f;
            foreach (var corner in new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one })
            {
                var sign = new Vector2(corner.x * 2 - 1, corner.y * 2 - 1);
                foreach (bool horizontal in new[] { true, false })
                {
                    var bar = Rect("Corner", frame);
                    bar.anchorMin = bar.anchorMax = bar.pivot = corner;
                    bar.sizeDelta = horizontal
                        ? new Vector2(length, thickness)
                        : new Vector2(thickness, length);
                    bar.anchoredPosition = sign * 4f;
                    AddImage(bar, Gold, false);
                }
            }
            return frame;
        }

        private static void BuildPartyPanel(RectTransform safe, BattleInspectView view)
        {
            var panel = Rect("PartyPanel", safe);
            Corner(panel, new Vector2(0, 1), new Vector2(8, -8), new Vector2(560, 400));
            var plate = Sliced(panel, UiArt.FeatherPath, Plate);
            plate.raycastTarget = false;

            var stage = Label(
                panel,
                "Stage",
                "森の遺跡 B3F",
                30,
                TextSub,
                TextAlignmentOptions.Left
            );
            Corner(
                stage.rectTransform,
                new Vector2(0, 1),
                new Vector2(56, -40),
                new Vector2(300, 40)
            );
            view.TurnLabel = Label(panel, "Turn", "", 30, Gold, TextAlignmentOptions.Right);
            Corner(
                view.TurnLabel.rectTransform,
                new Vector2(1, 1),
                new Vector2(-56, -40),
                new Vector2(200, 40)
            );

            for (int row = 0; row < PanelOrder.Length; row++)
            {
                var ally = Allies[PanelOrder[row]];
                var line = Rect("Member" + ally.Name, panel);
                Corner(
                    line,
                    new Vector2(0, 1),
                    new Vector2(40, -96 - row * 72),
                    new Vector2(480, 64)
                );

                var skill = Rect("Skill", line);
                Corner(skill, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(60, 60));
                var ring = SpriteImage(skill, UiArt.CirclePath, ally.Color);
                ring.raycastTarget = true;
                var inner = Rect("Inner", skill);
                Stretch(inner);
                inner.offsetMin = new Vector2(5, 5);
                inner.offsetMax = new Vector2(-5, -5);
                SpriteImage(inner, UiArt.CirclePath, new Color(0.06f, 0.06f, 0.09f, 1f));
                bool ready = ally.Cooldown == 0;
                var skillLabel = Label(
                    inner,
                    "Label",
                    ready ? "技" : ally.Cooldown.ToString(),
                    28,
                    ready ? Gold : TextFaint,
                    TextAlignmentOptions.Center
                );
                Stretch(skillLabel.rectTransform);
                AddButton(skill, ring);
                if (!ready)
                    ring.color = ally.Color * new Color(0.45f, 0.45f, 0.45f, 1f);

                var nameLabel = Label(
                    line,
                    "Name",
                    ally.Label,
                    26,
                    ally.Color,
                    TextAlignmentOptions.Left
                );
                Corner(
                    nameLabel.rectTransform,
                    new Vector2(0, 1),
                    new Vector2(80, 0),
                    new Vector2(160, 34)
                );
                var (fill, hpLabel) = HpBar(line, Vector2.zero, 0, HpAlly);
                var bar = (RectTransform)fill.parent;
                bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0, 0);
                bar.anchoredPosition = new Vector2(80, 2);
                bar.sizeDelta = new Vector2(400, 24);
                fill.anchorMax = new Vector2(ally.Hp / (float)ally.MaxHp, 1);
                hpLabel.text = $"{ally.Hp}/{ally.MaxHp}";
            }
        }

        /// <summary>
        /// The turn order along the top, like the timelines of Octopath Traveler and Romancing
        /// SaGa 2. The party acts as one side (by its total speed), so a party turn is one box
        /// with the four faces; each enemy has its own box.
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
            float partyWidth = 8 + faceSize + PartyFaceStep * (PanelOrder.Length - 1) + 8;
            float enemyWidth = 8 + 16 * DotSize;
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

                var party = Rect("Party", slot);
                Stretch(party);
                // Right to left, so each face is drawn over the back of the head to its right.
                for (int k = PanelOrder.Length - 1; k >= 0; k--)
                {
                    var ally = Allies[PanelOrder[k]];
                    var texture = Art("Battle" + ally.Name);
                    var face = Rect("Face" + ally.Name, party);
                    face.anchorMin = face.anchorMax = face.pivot = new Vector2(0, 0.5f);
                    face.anchoredPosition = new Vector2(8 + k * PartyFaceStep, 0);
                    face.sizeDelta = Vector2.one * faceSize;
                    var image = face.gameObject.AddComponent<RawImage>();
                    image.texture = texture;
                    image.uvRect = FaceUv(texture, ally.Face, 12);
                    image.raycastTarget = false;
                }

                var enemyRect = Rect("Enemy", slot);
                Place(enemyRect, Vector2.zero, Vector2.one * 16 * DotSize);
                var enemyImage = enemyRect.gameObject.AddComponent<RawImage>();
                enemyImage.raycastTarget = false;

                GameObject now = null;
                if (i == 0)
                {
                    var label = Label(slot, "Now", "いま", 22, Gold, TextAlignmentOptions.Center);
                    label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(
                        0.5f,
                        0f
                    );
                    label.rectTransform.pivot = new Vector2(0.5f, 1f);
                    label.rectTransform.anchoredPosition = new Vector2(0, -2);
                    label.rectTransform.sizeDelta = new Vector2(80, 28);
                    now = label.gameObject;
                }

                // Bake the opening order so the prefab preview matches the first frame.
                int entry = TurnCycle[i % TurnCycle.Length];
                bool isParty = entry == BattleInspectView.PartyTurn;
                party.gameObject.SetActive(isParty);
                enemyRect.gameObject.SetActive(!isParty);
                layout.preferredWidth = isParty ? partyWidth : enemyWidth;
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
                        Now = now,
                    }
                );
            }
            view.TurnCycle = TurnCycle;
            view.TurnSlots = slots.ToArray();
            view.PartySlotWidth = partyWidth;
            view.EnemySlotWidth = enemyWidth;
        }

        /// <summary>UV of a square face crop given by its top-left dot (rows counted from the top).</summary>
        private static UnityEngine.Rect FaceUv(Texture2D texture, Vector2Int topLeft, int size) =>
            new(
                topLeft.x / (float)texture.width,
                (texture.height - topLeft.y - size) / (float)texture.height,
                size / (float)texture.width,
                size / (float)texture.height
            );

        private static void BuildEnergy(RectTransform safe, BattleInspectView view)
        {
            var orb = Rect("Energy", safe);
            Corner(orb, Vector2.zero, new Vector2(40, 64), new Vector2(168, 168));
            SpriteImage(orb, UiArt.CirclePath, new Color(0.35f, 0.2f, 0.05f, 1f));
            var core = Rect("Core", orb);
            Stretch(core);
            core.offsetMin = new Vector2(10, 10);
            core.offsetMax = new Vector2(-10, -10);
            SpriteImage(core, UiArt.CirclePath, new Color(0.95f, 0.7f, 0.24f, 1f));
            view.EnergyLabel = Label(
                orb,
                "Value",
                "",
                56,
                new Color(0.2f, 0.1f, 0.02f),
                TextAlignmentOptions.Center,
                shadow: false
            );
            Stretch(view.EnergyLabel.rectTransform);
            var caption = Label(
                orb,
                "Caption",
                "エネルギー",
                24,
                TextSub,
                TextAlignmentOptions.Center
            );
            Place(caption.rectTransform, new Vector2(0, -104), new Vector2(200, 30));

            var deck = Label(safe, "Deck", "山札 6", 26, TextSub, TextAlignmentOptions.Left);
            Corner(deck.rectTransform, Vector2.zero, new Vector2(232, 76), new Vector2(160, 34));
        }

        private static void BuildHand(RectTransform safe, BattleInspectView view)
        {
            var hand = Rect("Hand", safe);
            hand.anchorMin = hand.anchorMax = hand.pivot = new Vector2(0.5f, 0f);
            hand.anchoredPosition = new Vector2(0, 16);
            float total = Cards.Length * CardWidth + (Cards.Length - 1) * CardSpacing;
            hand.sizeDelta = new Vector2(total, CardHeight);

            var cards = new List<BattleInspectCard>();
            for (int i = 0; i < Cards.Length; i++)
            {
                var spec = Cards[i];
                var owner = Allies[spec.Owner];
                var card = Rect("Card" + spec.Art.Replace("Card", ""), hand);
                card.anchorMin = card.anchorMax = new Vector2(0, 0);
                card.pivot = new Vector2(0.5f, 0);
                card.sizeDelta = new Vector2(CardWidth, CardHeight);
                card.anchoredPosition = new Vector2(
                    CardWidth * 0.5f + i * (CardWidth + CardSpacing),
                    0
                );
                var group = card.gameObject.AddComponent<CanvasGroup>();

                var highlight = Rect("Highlight", card);
                Stretch(highlight);
                highlight.offsetMin = new Vector2(-8, -8);
                highlight.offsetMax = new Vector2(8, 8);
                Sliced(highlight, UiArt.RoundedRectPath, Gold).raycastTarget = false;

                // The face is a child so the highlight, created first, glows behind it.
                var faceRect = Rect("Face", card);
                Stretch(faceRect);
                var face = Sliced(faceRect, UiArt.RoundedRectPath, CardFace);
                var border = Rect("Border", card);
                Stretch(border);
                Sliced(border, UiArt.RoundedRingPath, owner.Color).raycastTarget = false;

                var art = Rect("Art", card);
                art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 1);
                art.anchoredPosition = new Vector2(0, -20);
                art.sizeDelta = new Vector2(32, 32) * DotSize;
                var artImage = art.gameObject.AddComponent<RawImage>();
                artImage.texture = Art(spec.Art);
                artImage.raycastTarget = false;

                var cost = Rect("Cost", card);
                Corner(cost, new Vector2(0, 1), new Vector2(-12, 12), new Vector2(56, 56));
                SpriteImage(cost, UiArt.CirclePath, new Color(0.35f, 0.2f, 0.05f, 1f));
                var costCore = Rect("Core", cost);
                Stretch(costCore);
                costCore.offsetMin = new Vector2(4, 4);
                costCore.offsetMax = new Vector2(-4, -4);
                SpriteImage(costCore, UiArt.CirclePath, new Color(0.95f, 0.7f, 0.24f, 1f));
                var costLabel = Label(
                    cost,
                    "Value",
                    spec.Cost.ToString(),
                    34,
                    new Color(0.2f, 0.1f, 0.02f),
                    TextAlignmentOptions.Center,
                    shadow: false
                );
                Stretch(costLabel.rectTransform);

                if (spec.Element != BattleInspectElement.None)
                {
                    var element = Chip(
                        card,
                        "Element",
                        ElementLabel(spec.Element),
                        22,
                        ElementColor(spec.Element) * new Color(0.5f, 0.5f, 0.5f, 1f)
                    );
                    element.anchorMin = element.anchorMax = element.pivot = new Vector2(1, 1);
                    element.anchoredPosition = new Vector2(4, 4);
                    element.sizeDelta = new Vector2(0, 32);
                }

                var title = Label(
                    card,
                    "Name",
                    spec.Name,
                    26,
                    TextMain,
                    TextAlignmentOptions.Center
                );
                Place(title.rectTransform, new Vector2(0, -44), new Vector2(CardWidth - 12, 32));
                var value = Label(
                    card,
                    "Effect",
                    EffectValue(spec),
                    26,
                    Gold,
                    TextAlignmentOptions.Center
                );
                Place(value.rectTransform, new Vector2(0, -78), new Vector2(CardWidth - 12, 32));
                var scope = Label(
                    card,
                    "Scope",
                    EffectScope(spec.Effect) + " / " + owner.Label,
                    18,
                    TextFaint,
                    TextAlignmentOptions.Center
                );
                Place(scope.rectTransform, new Vector2(0, -106), new Vector2(CardWidth - 12, 24));

                cards.Add(
                    new BattleInspectCard
                    {
                        Button = AddButton(card, face),
                        Body = card,
                        Group = group,
                        Highlight = highlight.gameObject,
                        Cost = spec.Cost,
                        Power = spec.Power,
                        Element = spec.Element,
                        Effect = spec.Effect,
                    }
                );
                highlight.gameObject.SetActive(false);
            }
            view.Cards = cards.ToArray();
        }

        private static void BuildEndTurn(RectTransform safe, BattleInspectView view)
        {
            var button = Rect("EndTurn", safe);
            Corner(button, new Vector2(1, 0), new Vector2(-40, 72), new Vector2(260, 96));
            var face = Sliced(button, UiArt.RoundedRectPath, new Color(0.42f, 0.24f, 0.08f, 1f));
            var ring = Rect("Ring", button);
            Stretch(ring);
            Sliced(ring, UiArt.RoundedRingPath, Gold).raycastTarget = false;
            var label = Label(
                button,
                "Label",
                "ターン終了",
                36,
                TextMain,
                TextAlignmentOptions.Center
            );
            Stretch(label.rectTransform);
            view.EndTurnButton = AddButton(button, face);

            var discard = Label(
                safe,
                "Discard",
                "捨て札 4",
                26,
                TextSub,
                TextAlignmentOptions.Right
            );
            Corner(
                discard.rectTransform,
                new Vector2(1, 0),
                new Vector2(-48, 184),
                new Vector2(200, 34)
            );
        }

        private static void BuildPopup(RectTransform root, BattleInspectView view)
        {
            var popup = Label(root, "Popup", "0", 60, TextMain, TextAlignmentOptions.Center);
            Place(popup.rectTransform, Vector2.zero, new Vector2(480, 90));
            popup.outlineWidth = 0f;
            view.PopupTemplate = popup;
            popup.gameObject.SetActive(false);
        }

        private static (RectTransform fill, TMP_Text label) HpBar(
            RectTransform parent,
            Vector2 center,
            float width,
            Color color
        )
        {
            var bar = Rect("HpBar", parent);
            Place(bar, center, new Vector2(width, 24));
            AddImage(bar, HpBack, false);
            var fill = Rect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(2, 2);
            fill.offsetMax = new Vector2(-2, -2);
            AddImage(fill, color, false);
            // The numbers sit inside the bar so the bar needs no extra row.
            var label = Label(bar, "Value", "", 20, TextMain, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return (fill, label);
        }

        /// <summary>A pill of text whose width follows the text.</summary>
        private static RectTransform Chip(
            RectTransform parent,
            string name,
            string text,
            float fontSize,
            Color color,
            bool fitWidth = true
        )
        {
            var chip = Rect(name, parent);
            Sliced(chip, UiArt.CapsulePath, color).raycastTarget = false;
            var row = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 0, 0);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            // Inside a layout group the group sizes the chip instead.
            if (fitWidth)
                chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter
                    .FitMode
                    .PreferredSize;
            Label(chip, "Label", text, fontSize, TextMain, TextAlignmentOptions.Center);
            return chip;
        }

        private static string ElementLabel(BattleInspectElement element) =>
            element switch
            {
                BattleInspectElement.Slash => "斬",
                BattleInspectElement.Fire => "炎",
                BattleInspectElement.Ice => "氷",
                BattleInspectElement.Thunder => "雷",
                _ => "",
            };

        private static Color ElementColor(BattleInspectElement element) =>
            element switch
            {
                BattleInspectElement.Slash => new Color(0.85f, 0.87f, 0.92f),
                BattleInspectElement.Fire => new Color(1f, 0.45f, 0.22f),
                BattleInspectElement.Ice => new Color(0.45f, 0.78f, 1f),
                BattleInspectElement.Thunder => new Color(1f, 0.86f, 0.25f),
                _ => Color.gray,
            };

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
