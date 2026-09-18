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
            }
            var s = view.Session;
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
                    s.Slot == i
                        ? new Color(.86f, .82f, .55f, .62f)
                        : new Color(.6f, .75f, .68f, .12f);
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
                if (s.CombatState == WireCombatState.Weak)
                    brightness = .60f;
                if (s.CombatState == WireCombatState.Down)
                    brightness = .40f;
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
            GoalProgress("GoalsDailyProgress", false);
            GoalProgress("GoalsWeeklyProgress", true);
            rects["VolumeProgressFill"].anchorMax = new Vector2(s.Volume / 100f, 1);
        }

        private void Selected(string name, bool selected)
        {
            var button = view.Button(name);
            button.image.color = selected ? new Color(1.48f, 1.35f, .90f) : Color.white;
            var text = button.GetComponentInChildren<TMP_Text>();
            text.color =
                !button.interactable ? new Color(.50f, .57f, .55f)
                : selected ? new Color(1, .88f, .59f)
                : new Color(.94f, .90f, .78f);
        }

        private void GoalProgress(string name, bool weekly)
        {
            var state = view.Session.Goal(weekly);
            bool visible = state is WireGoalState.Progress or WireGoalState.Achieved;
            rects[name].gameObject.SetActive(visible);
            float amount =
                state == WireGoalState.Achieved ? 1
                : view.Session.GoalUsesDistance(weekly) ? (weekly ? 8.2f / 15 : .6f)
                : weekly ? .728f
                : .764f;
            rects[name + "Fill"].anchorMax = new Vector2(amount, 1);
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
            const float duration = .28f;
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
