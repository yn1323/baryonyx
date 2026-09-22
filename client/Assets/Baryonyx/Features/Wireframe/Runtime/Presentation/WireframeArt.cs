using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.UI;
using TMPro;
using UnityEngine;

namespace Baryonyx.Wireframe
{
    // Presentation only: all sample game state remains in WireframeSession.
    public sealed class WireframeArt : MonoBehaviour
    {
        public Texture2D Actors;
        public Texture2D Forest;
        public Texture2D Mine;
        public RectTransform ExplorationParty;
        private readonly Dictionary<string, UnityEngine.UI.RawImage> images = new();
        private readonly Dictionary<string, RectTransform> rects = new();
        private WireframeView view;
        private Coroutine travel;
        private CanvasGroup input;
        private Vector2 partyOrigin;
        private bool prepared;
        private WireframeBattleFeedback feedback;
        private Sprite equipmentFrame;
        private readonly List<UnityEngine.UI.Image> explorationNodes = new();
        private readonly List<UnityEngine.UI.Image> explorationPaths = new();

        public static Rect ActorUv(int index) =>
            new((index % 4) * .25f, index < 4 ? .5f : 0, .25f, .5f);

        public void Render(WireframeView currentView)
        {
            view = currentView;
            if (!prepared)
            {
                foreach (var image in GetComponentsInChildren<UnityEngine.UI.RawImage>(true))
                    images[image.name] = image;
                foreach (var rect in GetComponentsInChildren<RectTransform>(true))
                    rects[rect.name] = rect;
                input = gameObject.AddComponent<CanvasGroup>();
                partyOrigin = ExplorationParty.anchoredPosition;
                prepared = true;
                equipmentFrame = CreateEquipmentFrame();
                CreateExplorationRoute();
                feedback = gameObject.AddComponent<WireframeBattleFeedback>();
                feedback.Initialize(images, view);
            }
            var s = view.Session;
            ApplyEquipmentFrame();
            RenderExplorationRoute(s.Step);
            feedback.Bind(s.Battle);
            if (
                travel != null
                && (s.Screen != WireScreen.Explore || s.Popup != WirePopup.None || s.DebugOpen)
            )
                CancelTravel();
            for (int i = 0; i < 4; i++)
            {
                int actor = s.PartyMember(i);
                foreach (
                    string prefix in new[]
                    {
                        "HomeLandscapeActor",
                        "ResultLandscapeActor",
                        "ExploreActor",
                        "PartySlot",
                        "EquipmentSlot",
                        "BattleAllyArt",
                    }
                )
                {
                    string key =
                        prefix + i + (prefix is "PartySlot" or "EquipmentSlot" ? "Art" : "");
                    images[key].uvRect = ActorUv(actor);
                }
                Selected("PartySlot" + i, s.Slot == i);
                Selected("EquipmentSlot" + i, s.Slot == i);
                Selected("Hp" + i, s.Slot == i);
                view.Button("Ally" + i).image.color =
                    s.SkillSelection && s.Slot == i
                        ? new Color(.86f, .82f, .55f, .20f)
                        : Color.clear;
            }
            for (int i = 0; i < 5; i++)
            {
                Selected("Character" + i, s.Candidate == i);
                images["Character" + i + "Art"].color = s.OwnsCharacter(i)
                    ? Color.white
                    : new Color(.18f, .23f, .23f, .7f);
            }
            for (int i = 0; i < 3; i++)
            {
                Selected("Equipment" + i, s.SelectedEquipment == i);
                Selected("Skill" + i, s.SkillSelection && s.Skill == i);
                var enemy = images["BattleEnemyArt" + i];
                enemy.uvRect = ActorUv(s.IsBoss ? 7 : (i == 1 ? 6 : 5));
                float brightness = 1;
                if (s.Battle != null && i < s.Battle.Enemies.Length)
                {
                    var state = s.Battle.Enemies[i];
                    brightness =
                        state.IsDown ? .42f
                        : state.DownRatio < .35f ? .65f
                        : 1;
                }
                enemy.color = new Color(brightness, brightness, brightness, 1);
                view.Button("Enemy" + i).image.color =
                    s.SkillSelection && s.Target == i
                        ? new Color(.88f, .73f, .42f, .60f)
                        : new Color(.13f, .19f, .16f, .08f);
            }
            images["PartyPortrait"].uvRect = ActorUv(s.Candidate);
            images["EquipmentPortrait"].uvRect = ActorUv(s.SelectedCharacter);
            images["RestActor"].uvRect = ActorUv(s.PartyMember(0));
            var environment = s.Area == 1 ? Mine : Forest;
            images["ExploreMapArt"].texture = environment;
            images["BattleBackdrop"].texture = environment;
            var ambient = images["BattlefieldAmbient"];
            ambient.texture = environment;
            var responsiveBackground = ambient.GetComponent<ResponsiveBackground>();
            if (responsiveBackground != null && environment != null && environment.height > 0)
                responsiveBackground.AspectRatio = environment.width / (float)environment.height;
            images["RestLandscapeArt"].texture = environment;
            images["ResultLandscapeArt"].texture = environment;
            images["PopupIllustrationArt"].texture =
                s.Popup == WirePopup.Intro ? Forest : environment;
            images["PopupActor"].uvRect = ActorUv(
                s.Popup == WirePopup.NewCompanion ? 4 : s.SelectedCharacter
            );
            rects["PopupIllustration"]
                .gameObject.SetActive(
                    s.Popup
                        is WirePopup.Intro
                            or WirePopup.NewCompanion
                            or WirePopup.NewEquipment
                            or WirePopup.Revive
                );
            Selected("NavHome", s.Screen == WireScreen.Home);
            Selected("NavParty", s.Screen == WireScreen.Party);
            Selected("NavGoals", s.Screen == WireScreen.Goals);
            // Only the Health chart displays measured progress in this prototype.
            rects["GoalsDailyProgress"].gameObject.SetActive(false);
            rects["GoalsWeeklyProgress"].gameObject.SetActive(false);
            rects["VolumeProgressFill"].anchorMax = new Vector2(s.Volume / 100f, 1);
        }

        private void CreateExplorationRoute()
        {
            if (!rects.TryGetValue("ExploreMap", out var map))
                return;
            var positions = new[]
            {
                new Vector2(.17f, .24f),
                new Vector2(.39f, .38f),
                new Vector2(.62f, .31f),
                new Vector2(.84f, .49f),
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var node = new GameObject(
                    "ExploreRouteNode" + i,
                    typeof(RectTransform),
                    typeof(UnityEngine.UI.Image)
                );
                node.transform.SetParent(map, false);
                var rect = (RectTransform)node.transform;
                rect.anchorMin = rect.anchorMax = positions[i];
                rect.sizeDelta = new Vector2(26, 26);
                rect.SetSiblingIndex(Mathf.Min(1, map.childCount - 1));
                var image = node.GetComponent<UnityEngine.UI.Image>();
                image.raycastTarget = false;
                explorationNodes.Add(image);
            }
            for (int i = 0; i < positions.Length - 1; i++)
            {
                var path = new GameObject(
                    "ExploreRoutePath" + i,
                    typeof(RectTransform),
                    typeof(UnityEngine.UI.Image)
                );
                path.transform.SetParent(map, false);
                var rect = (RectTransform)path.transform;
                float y = (positions[i].y + positions[i + 1].y) * .5f;
                rect.anchorMin = new Vector2(positions[i].x, y);
                rect.anchorMax = new Vector2(positions[i + 1].x, y);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 5);
                rect.SetSiblingIndex(Mathf.Min(1, map.childCount - 1));
                var image = path.GetComponent<UnityEngine.UI.Image>();
                image.raycastTarget = false;
                explorationPaths.Add(image);
            }
        }

        private void RenderExplorationRoute(int step)
        {
            if (explorationNodes.Count == 0)
                return;
            int current = Mathf.Clamp(step, 0, explorationNodes.Count - 1);
            var explored = new Color(.70f, .55f, .28f, .92f);
            var currentColor = new Color(.98f, .88f, .48f, 1f);
            var hidden = new Color(.12f, .20f, .24f, .94f);
            for (int i = 0; i < explorationNodes.Count; i++)
                explorationNodes[i].color =
                    i < current ? explored
                    : i == current ? currentColor
                    : hidden;
            for (int i = 0; i < explorationPaths.Count; i++)
                explorationPaths[i].color = i < current ? explored : hidden;
            if (ExplorationParty != null && explorationNodes.Count > 0)
            {
                float progress = current / (float)Mathf.Max(1, explorationNodes.Count - 1);
                ExplorationParty.anchoredPosition =
                    partyOrigin + new Vector2(progress * 250f, progress * 52f);
            }
        }

        private void Selected(string name, bool selected)
        {
            var button = view.Button(name);
            button.image.color = selected ? new Color(.77f, .88f, .76f) : Color.white;
            var text = button.GetComponentInChildren<TMP_Text>();
            bool equipmentOption =
                name == "Equipment0" || name == "Equipment1" || name == "Equipment2";
            text.color =
                !button.interactable ? new Color(.50f, .57f, .55f)
                : name.StartsWith("Skill", StringComparison.Ordinal) ? new Color(.98f, .97f, .91f)
                : equipmentOption ? new Color(.96f, .94f, .86f)
                : new Color(.16f, .23f, .20f);
        }

        private void ApplyEquipmentFrame()
        {
            if (equipmentFrame == null)
                return;
            ApplyFrame(rects.TryGetValue("EquipmentCompareCard", out var card) ? card : null);
            foreach (
                var name in new[] { "Equipment0", "Equipment1", "Equipment2", "EquipmentConfirm" }
            )
            {
                if (!view.TryGetButton(name, out var button))
                    continue;
                ApplyFrame(button.transform as RectTransform);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.color = new Color(.96f, .94f, .86f);
            }
        }

        private void ApplyFrame(RectTransform rect)
        {
            if (rect == null)
                return;
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
                return;
            image.sprite = equipmentFrame;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
        }

        private static Sprite CreateEquipmentFrame()
        {
            const int size = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeEquipmentFrame",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var fill = new Color(.08f, .12f, .17f);
            var edge = new Color(.65f, .49f, .23f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(5 - x, x - 18, 0);
                float dy = Mathf.Max(5 - y, y - 18, 0);
                bool cut = dx * dx + dy * dy > 25;
                bool border = x < 2 || x > 21 || y < 2 || y > 21;
                texture.SetPixel(
                    x,
                    y,
                    cut ? Color.clear
                        : border ? edge
                        : fill
                );
            }
            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(.5f, .5f),
                100,
                0,
                SpriteMeshType.FullRect,
                new Vector4(6, 6, 6, 6)
            );
        }

        public void Travel(bool sidePath, Action arrived)
        {
            if (travel != null || view == null)
                return;
            travel = StartCoroutine(Walk(sidePath, arrived));
        }

        private IEnumerator Walk(bool sidePath, Action arrived)
        {
            input.interactable = false;
            var party = ExplorationParty;
            Vector2 end = partyOrigin + new Vector2(sidePath ? 100 : -22, sidePath ? 40 : 92);
            float start = Time.unscaledTime;
            const float duration = 1.1f;
            while (Time.unscaledTime - start < duration)
            {
                float t = (Time.unscaledTime - start) / duration;
                party.anchoredPosition =
                    Vector2.Lerp(partyOrigin, end, t)
                    + new Vector2(0, Mathf.Sin(t * Mathf.PI * 6) * 2);
                yield return null;
            }
            party.anchoredPosition = partyOrigin;
            input.interactable = true;
            travel = null;
            arrived();
        }

        private void CancelTravel()
        {
            if (travel != null)
                StopCoroutine(travel);
            travel = null;
            if (prepared)
            {
                ExplorationParty.anchoredPosition = partyOrigin;
                input.interactable = true;
            }
        }

        private void OnDisable() => CancelTravel();
    }
}
