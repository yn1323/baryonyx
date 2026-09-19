using System;
using System.Collections;
using System.Collections.Generic;
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
                feedback = gameObject.AddComponent<WireframeBattleFeedback>();
                feedback.Initialize(images, view);
            }
            var s = view.Session;
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

        private void Selected(string name, bool selected)
        {
            var button = view.Button(name);
            button.image.color = selected ? new Color(.77f, .88f, .76f) : Color.white;
            var text = button.GetComponentInChildren<TMP_Text>();
            text.color =
                !button.interactable ? new Color(.50f, .57f, .55f)
                : name.StartsWith("Skill", StringComparison.Ordinal) ? new Color(.98f, .97f, .91f)
                : new Color(.16f, .23f, .20f);
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
