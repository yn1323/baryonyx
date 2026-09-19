using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Baryonyx.Wireframe
{
    public sealed partial class WireframeView : MonoBehaviour
    {
        public GameObject[] Pages;
        public GameObject PopupOverlay;
        public GameObject DebugOverlay;
        public GameObject Navigation;
        public UnityEngine.UI.ScrollRect PageScroll;
        public RectTransform[] HpFills = new RectTransform[7];
        private readonly Dictionary<string, UnityEngine.UI.Button> buttons = new();
        private readonly Dictionary<string, TMP_Text> labels = new();
        private readonly Dictionary<WireScreen, float> positions = new();
        private WireScreen previous;
        private bool rendered;
        private WireframeArt art;
        private WireframeHealthView health;
        private float battleRenderClock;
        private bool foreground = true;
        public WireframeSession Session { get; private set; }

        public UnityEngine.UI.Button Button(string name) => buttons[name];

        public TMP_Text Text(string name) => labels[name];

        public void Bind(WireframeSession session)
        {
            if (Session != null)
                Session.Changed -= Render;
            Session = session;
            art = GetComponent<WireframeArt>();
            health = GetComponent<WireframeHealthView>();
            buttons.Clear();
            labels.Clear();
            foreach (var button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                buttons.Add(button.name, button);
                button.onClick.RemoveAllListeners();
            }
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                labels.Add(label.name, label);
            WireActions();
            Session.Changed += Render;
            rendered = false;
            Render();
        }

        private void OnDestroy()
        {
            if (Session != null)
                Session.Changed -= Render;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (health != null && health.DetailsOpen)
                    health.Close();
                else
                    Session?.Back();
            }
            if (foreground && Session?.Screen == WireScreen.Battle)
            {
                battleRenderClock += Time.unscaledDeltaTime;
                if (battleRenderClock >= .05f)
                {
                    Session.AdvanceBattle(battleRenderClock);
                    battleRenderClock = 0;
                }
            }
            else
                battleRenderClock = 0;
        }

        private void OnApplicationPause(bool paused)
        {
            foreground = !paused;
            battleRenderClock = 0;
        }

        private void On(string name, Action action) =>
            Button(name).onClick.AddListener(() => action());

        private void Set(string name, string value) => Text(name).text = value;

        private void Caption(string name, string value) =>
            Button(name).GetComponentInChildren<TMP_Text>().text = value;

        private void Enable(string name, bool value) => Button(name).interactable = value;

        private void Visible(string name, bool value) => Button(name).gameObject.SetActive(value);

        private void WireActions()
        {
            On(
                "Back",
                () =>
                {
                    if (health != null && health.DetailsOpen)
                        health.Close();
                    else
                        Session.Back();
                }
            );
            On("HeaderSettings", () => Session.Open(WireScreen.Settings));
            On("HomeSteps", () => Session.Open(WireScreen.Health));
            On("GoalsHealth", () => Session.Open(WireScreen.Health));
            On("Preview", Session.ToggleDebug);
            On("HomeAdventure", () => Session.Open(WireScreen.Destination));
            On("HomeEquipment", () => Session.Open(WireScreen.Equipment));
            On("HomeSettings", () => Session.Open(WireScreen.Health));
            On("NavHome", () => Session.HomeTab(WireScreen.Home));
            On("NavParty", () => Session.HomeTab(WireScreen.Party));
            On("NavGoals", () => Session.HomeTab(WireScreen.Goals));
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                On("Destination" + i, () => Session.BeginAdventure(index));
            }
            On("ExploreDoor", () => Travel(false, Session.EnterDoor));
            On("ExploreChest", () => Travel(true, Session.OpenChest));
            On("ExploreParty", () => Session.Open(WireScreen.Party));
            On("ExploreEquipment", () => Session.Open(WireScreen.Equipment));
            On("ExploreSettings", () => Session.Open(WireScreen.Settings));
            On("ExploreEnd", Session.EndAdventure);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                foreach (string prefix in new[] { "PartySlot", "EquipmentSlot", "Ally", "Hp" })
                    On(prefix + i, () => Session.SelectSlot(index));
            }
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                On("Character" + i, () => Session.ChooseCharacter(index));
            }
            On("PartyConfirm", Session.ConfirmCharacter);
            On("PartyEquipment", () => Session.Open(WireScreen.Equipment));
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                On("Equipment" + i, () => Session.ChooseEquipment(index));
                On("Skill" + i, () => Session.ChooseSkill(index));
                On("Enemy" + i, () => Session.ChooseTarget(index));
            }
            On("EquipmentConfirm", Session.ConfirmEquipment);
            On("UseSkill", Session.UseSkill);
            On("DefeatRetry", Session.Retry);
            On("DefeatRevive", Session.ShowRevive);
            On("DefeatParty", () => Session.Open(WireScreen.Party));
            On("DefeatPath", Session.OtherPath);
            On("DefeatEnd", Session.EndAdventure);
            On("ResultHome", Session.EndAdventure);
            On("GoalsEdit", () => Session.ShowGoals(false));
            On("GoalsHistory", () => Session.ShowGoals(true));
            On("GoalsClear", Session.ClearGoal);
            On("GoalsCancel", Session.CancelSchedule);
            On("SettingsVolume", Session.ChangeVolume);
            On("SettingsData", () => Session.Open(WireScreen.Health));
            On("PopupClose", Session.Back);
            On("PopupPrimary", PopupPrimary);
            On(
                "PopupSecondary",
                () =>
                {
                    if (Session.Popup == WirePopup.Intro)
                        Session.StartGoals();
                    else
                        Session.Back();
                }
            );
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                On("GoalOption" + i, () => Session.ToggleGoal(index));
            }
            On("DebugClose", Session.ToggleDebug);
            On("DebugWin", () => Session.PreviewFinish(true));
            On("DebugLose", () => Session.PreviewFinish(false));
            On("DebugReset", Session.Reset);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                On("DebugCombat" + i, () => Session.PreviewCombat((WireCombatState)index));
                On("DebugGoal" + i, () => Session.PreviewGoal((WireGoalState)index));
            }
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                On("DebugOption" + i, () => Session.ToggleSample(index));
            }
        }

        private void PopupPrimary()
        {
            switch (Session.Popup)
            {
                case WirePopup.NewEquipment:
                case WirePopup.NewCompanion:
                    Session.ManageAcquisition();
                    break;
                case WirePopup.Revive:
                    Session.ConfirmRevive();
                    break;
                case WirePopup.EndAdventure:
                    Session.EndAdventure();
                    break;
                case WirePopup.GoalEdit:
                    Session.SaveGoal();
                    break;
                case WirePopup.Data:
                    Session.ConfirmData();
                    break;
                default:
                    Session.Back();
                    break;
            }
        }

        private void Travel(bool sidePath, Action arrived)
        {
            if (art != null)
                art.Travel(sidePath, arrived);
            else
                arrived();
        }

        private void Render()
        {
            var s = Session;
            bool switched = !rendered || previous != s.Screen;
            if (rendered && switched)
                positions[previous] = PageScroll.verticalNormalizedPosition;
            if (s.Popup == WirePopup.Intro)
                positions.Clear();
            for (int i = 0; i < Pages.Length; i++)
                Pages[i].SetActive(i == (int)s.Screen);
            string[] titles =
            {
                "てくてくダンジョン",
                "冒険先",
                "探索",
                "仲間・編成",
                "装備",
                "戦闘",
                "全滅",
                "冒険の成果",
                "運動目標",
                "設定",
                "歩数",
            };
            Set("Title", titles[(int)s.Screen]);
            Visible("HeaderSettings", s.Screen == WireScreen.Home);
            Visible("Preview", false);
            Visible("Back", s.Screen != WireScreen.Home);
            Enable(
                "Back",
                s.Screen != WireScreen.Home
                    && s.Screen != WireScreen.Defeat
                    && s.Screen != WireScreen.Result
            );
            Navigation.SetActive(!s.InAdventure && s.Screen != WireScreen.Destination);
            PopupOverlay.SetActive(s.Popup != WirePopup.None);
            DebugOverlay.SetActive(s.DebugOpen);
            RenderPages();
            RenderPopup();
            if (art != null)
                art.Render(this);
            Enable("DebugWin", s.Screen == WireScreen.Battle);
            Enable("DebugLose", s.Screen == WireScreen.Battle);
            for (int i = 0; i < 5; i++)
                Enable("DebugCombat" + i, s.Screen == WireScreen.Battle);
            Caption("DebugOption0", "スキル枠：" + (s.ThreeSkills ? "3つ" : "2つ"));
            Caption("DebugOption1", "敵予告：" + (s.MultipleWarnings ? "複数" : "1件"));
            Caption("DebugOption2", "敵編成：" + (s.IsBoss ? "ボス" : "通常敵"));
            Enable("DebugOption2", s.Screen == WireScreen.Battle);
            if (switched)
            {
                Canvas.ForceUpdateCanvases();
                PageScroll.verticalNormalizedPosition = positions.TryGetValue(
                    s.Screen,
                    out float position
                )
                    ? position
                    : 1;
            }
            previous = s.Screen;
            rendered = true;
        }
    }
}
