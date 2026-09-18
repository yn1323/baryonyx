using System;
using System.Collections.Generic;

namespace Baryonyx.Wireframe
{
    public enum WireScreen
    {
        Home,
        Destination,
        Explore,
        Party,
        Equipment,
        Battle,
        Defeat,
        Result,
        Goals,
        Settings,
    }

    public enum WirePopup
    {
        None,
        Intro,
        NewEquipment,
        NewCompanion,
        Revive,
        EndAdventure,
        GoalEdit,
        GoalHistory,
        Data,
    }

    public enum WireCombatState
    {
        Normal,
        Casting,
        Cooldown,
        Weak,
        Down,
    }

    public enum WireGoalState
    {
        Unset,
        Progress,
        Achieved,
        Unknown,
        Scheduled,
    }

    public sealed class WireframeSession
    {
        private readonly Stack<Route> history = new();
        private readonly int[] party = { 0, 1, 2, 3 };
        private readonly int[] equipment = new int[5];
        private readonly int[] progress = new int[2];
        private readonly WireGoalState[] goals = new WireGoalState[2];
        private readonly bool[] goalDistances = new bool[2];
        private readonly bool[] goalTimes = new bool[2];
        private readonly bool[] pendingDistances = new bool[2];
        private readonly bool[] pendingTimes = new bool[2];
        private bool acquiredRoute;
        private bool initialGoals;
        private WirePopup dataReturn;

        private readonly struct Route
        {
            public readonly WireScreen Screen;
            public readonly int Slot;
            public readonly int Candidate;
            public readonly int Equipment;
            public readonly bool Acquired;

            public Route(WireScreen screen, int slot, int candidate, int equipment, bool acquired)
            {
                Screen = screen;
                Slot = slot;
                Candidate = candidate;
                Equipment = equipment;
                Acquired = acquired;
            }
        }

        public event Action Changed;
        public WireframeData Data { get; }
        public WireScreen Screen { get; private set; } = WireScreen.Home;
        public WirePopup Popup { get; private set; } = WirePopup.Intro;
        public WireCombatState CombatState { get; private set; }
        public WireGoalState GoalState => goals[GoalWeekly ? 1 : 0];
        public bool HasGoalSchedule =>
            goals[0] == WireGoalState.Scheduled || goals[1] == WireGoalState.Scheduled;
        public int Slot { get; private set; }
        public int SelectedCharacter => party[Slot];
        public int Candidate { get; private set; }
        public int SelectedEquipment { get; private set; }
        public int Area { get; private set; }
        public int Step => progress[Area];
        public int Runes { get; private set; } = 860;
        public bool HasNewEquipment { get; private set; }
        public bool HasNewCompanion { get; private set; }
        public bool IsBoss { get; private set; }
        public bool SkillSelection { get; private set; }
        public int Skill { get; private set; } = -1;
        public int Target { get; private set; }
        public bool ThreeSkills { get; private set; }
        public bool MultipleWarnings { get; private set; }
        public bool DebugOpen { get; private set; }
        public bool GoalWeekly { get; private set; }
        public bool GoalDistance { get; private set; }
        public bool GoalTimed { get; private set; }
        public int Volume { get; private set; } = 50;
        public string Notice { get; private set; } = "";
        public int EnemyHp { get; private set; } = 100;
        public bool AlliesRecovered { get; private set; }
        public int CastingSlot { get; private set; }
        public bool InAdventure
        {
            get
            {
                if (
                    Screen == WireScreen.Explore
                    || Screen == WireScreen.Battle
                    || Screen == WireScreen.Defeat
                    || Screen == WireScreen.Result
                )
                    return true;
                foreach (var route in history)
                    if (route.Screen == WireScreen.Explore || route.Screen == WireScreen.Defeat)
                        return true;
                return false;
            }
        }
        public string Place => Data.Places[Area];
        public string GoalSummary => DescribeGoal(GoalWeekly, GoalDistance, GoalTimed);

        public WireGoalState Goal(bool weekly) => goals[weekly ? 1 : 0];

        public bool GoalUsesDistance(bool weekly) => goalDistances[weekly ? 1 : 0];

        public string SavedGoal(bool weekly, bool pending = false) =>
            DescribeGoal(
                weekly,
                (pending ? pendingDistances : goalDistances)[weekly ? 1 : 0],
                (pending ? pendingTimes : goalTimes)[weekly ? 1 : 0]
            );

        private static string DescribeGoal(bool weekly, bool distance, bool timed) =>
            $"{(weekly ? "1週間" : "1日")}の目標：{(timed ? "19〜20時" : "時間帯指定なし")}・{(distance ? (weekly ? "ウォーキング 15km" : "ウォーキング 3km") : (weekly ? "25,000歩" : "5,000歩"))}";

        public WireframeSession(WireframeData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            Data = data;
        }

        public int PartyMember(int slot) => party[slot];

        public int Equipped(int character) => equipment[character];

        public bool OwnsCharacter(int index) => index >= 0 && index < (HasNewCompanion ? 5 : 4);

        public bool OwnsEquipment(int index) => index >= 0 && index < (HasNewEquipment ? 3 : 2);

        private void Notify() => Changed?.Invoke();

        private bool Ready(WireScreen screen) =>
            Screen == screen && Popup == WirePopup.None && !DebugOpen;

        public void Open(WireScreen screen)
        {
            if (Popup != WirePopup.None || DebugOpen)
                return;
            bool allowed = Screen switch
            {
                WireScreen.Home => screen == WireScreen.Destination
                    || screen == WireScreen.Party
                    || screen == WireScreen.Equipment
                    || screen == WireScreen.Goals
                    || screen == WireScreen.Settings,
                WireScreen.Explore => screen == WireScreen.Party
                    || screen == WireScreen.Equipment
                    || screen == WireScreen.Settings,
                WireScreen.Defeat => screen == WireScreen.Party,
                WireScreen.Party => screen == WireScreen.Equipment,
                _ => false,
            };
            if (!allowed)
                return;
            Push(screen, false);
            Notify();
        }

        private void Push(WireScreen screen, bool fromAcquisition)
        {
            history.Push(new Route(Screen, Slot, Candidate, SelectedEquipment, acquiredRoute));
            acquiredRoute = fromAcquisition;
            Screen = screen;
            Candidate = SelectedCharacter;
            SelectedEquipment = fromAcquisition ? 2 : equipment[SelectedCharacter];
        }

        public void Back()
        {
            if (DebugOpen)
            {
                DebugOpen = false;
                Notify();
                return;
            }
            if (Popup != WirePopup.None)
            {
                if (Popup == WirePopup.GoalEdit && initialGoals)
                {
                    initialGoals = false;
                    Root(WireScreen.Home);
                    Notify();
                    return;
                }
                Popup = Popup == WirePopup.Data ? dataReturn : WirePopup.None;
                Notify();
                return;
            }
            if (Screen == WireScreen.Battle)
            {
                SkillSelection = false;
                Skill = -1;
                Notify();
                return;
            }
            if (Screen == WireScreen.Explore)
            {
                Popup = WirePopup.EndAdventure;
                Notify();
                return;
            }
            if (Screen == WireScreen.Defeat || Screen == WireScreen.Result)
                return;
            if (history.Count > 0)
            {
                var route = history.Pop();
                Screen = route.Screen;
                Slot = route.Slot;
                acquiredRoute = route.Acquired;
                Candidate = route.Candidate;
                SelectedEquipment = route.Equipment;
            }
            if (Screen == WireScreen.Home)
                initialGoals = false;
            Notify();
        }

        public void StartGoals()
        {
            if (Popup != WirePopup.Intro)
                return;
            Popup = WirePopup.None;
            initialGoals = true;
            Push(WireScreen.Goals, false);
            Popup = WirePopup.GoalEdit;
            Notify();
        }

        public void HomeTab(WireScreen screen)
        {
            if (Popup != WirePopup.None || DebugOpen || InAdventure)
                return;
            if (
                screen != WireScreen.Home
                && screen != WireScreen.Party
                && screen != WireScreen.Goals
            )
                return;
            Root(WireScreen.Home);
            if (screen != WireScreen.Home)
                Push(screen, false);
            Notify();
        }

        public void BeginAdventure(int area)
        {
            if (!Ready(WireScreen.Destination) || area < 0 || area >= progress.Length)
                return;
            Area = area;
            Root(WireScreen.Explore);
            Notice = "入口を選んで先へ進もう。";
            Notify();
        }

        public void EnterDoor()
        {
            if (!Ready(WireScreen.Explore))
                return;
            IsBoss = Step >= 2;
            Root(WireScreen.Battle);
            ResetBattle();
            Notify();
        }

        public void OpenChest()
        {
            if (!Ready(WireScreen.Explore))
                return;
            if (!HasNewCompanion)
            {
                HasNewCompanion = true;
                Popup = WirePopup.NewCompanion;
                Notice = "ノエルが仲間になった。";
            }
            else if (!HasNewEquipment)
            {
                HasNewEquipment = true;
                Popup = WirePopup.NewEquipment;
                Notice = "宝箱の武器を見つけた。";
            }
            else
                Notice = "宝箱は空だった。別の入口を選ぼう。";
            Notify();
        }

        public void ManageAcquisition()
        {
            if (Popup != WirePopup.NewEquipment && Popup != WirePopup.NewCompanion)
                return;
            bool companion = Popup == WirePopup.NewCompanion;
            Popup = WirePopup.None;
            Push(companion ? WireScreen.Party : WireScreen.Equipment, true);
            if (companion)
                Candidate = 4;
            Notify();
        }

        public void SelectSlot(int slot)
        {
            if (Popup != WirePopup.None || DebugOpen || slot < 0 || slot >= 4)
                return;
            if (
                Screen != WireScreen.Party
                && Screen != WireScreen.Equipment
                && Screen != WireScreen.Battle
            )
                return;
            Slot = slot;
            Skill = -1;
            if (Screen == WireScreen.Battle)
                SkillSelection = true;
            if (Screen == WireScreen.Party && !acquiredRoute)
                Candidate = SelectedCharacter;
            Notify();
        }

        public void ChooseCharacter(int index)
        {
            if (!Ready(WireScreen.Party) || !OwnsCharacter(index))
                return;
            Candidate = index;
            Notify();
        }

        public void ConfirmCharacter()
        {
            if (!Ready(WireScreen.Party) || !OwnsCharacter(Candidate))
                return;
            int previous = party[Slot];
            int existingSlot = Array.IndexOf(party, Candidate);
            party[Slot] = Candidate;
            if (existingSlot >= 0 && existingSlot != Slot)
                party[existingSlot] = previous;
            Notice = $"{Data.Characters[Candidate].Name}を編成した。";
            if (acquiredRoute)
                ReturnToExplore();
            Notify();
        }

        public void ChooseEquipment(int index)
        {
            if (!Ready(WireScreen.Equipment) || !OwnsEquipment(index))
                return;
            SelectedEquipment = index;
            Notify();
        }

        public void ConfirmEquipment()
        {
            if (!Ready(WireScreen.Equipment) || !OwnsEquipment(SelectedEquipment))
                return;
            equipment[SelectedCharacter] = SelectedEquipment;
            Notice = $"{Data.Characters[SelectedCharacter].Name}の装備を変更した。";
            if (acquiredRoute)
                ReturnToExplore();
            Notify();
        }

        public void ChooseSkill(int index)
        {
            if (
                !Ready(WireScreen.Battle)
                || index < 0
                || index >= (ThreeSkills ? 3 : 2)
                || CombatState == WireCombatState.Cooldown
            )
                return;
            SkillSelection = true;
            Skill = index;
            Notify();
        }

        public void ChooseTarget(int index)
        {
            if (!Ready(WireScreen.Battle) || index < 0 || index >= (IsBoss ? 1 : 3))
                return;
            Target = index;
            Notify();
        }

        public void UseSkill()
        {
            if (!Ready(WireScreen.Battle) || !SkillSelection || Skill < 0)
                return;
            CombatState = WireCombatState.Casting;
            CastingSlot = Slot;
            SkillSelection = false;
            Notify();
        }

        public void FinishBattle(bool victory)
        {
            // Change screen before notifying: repeated input cannot grant a reward twice.
            if (!Ready(WireScreen.Battle))
                return;
            SkillSelection = false;
            if (!victory)
            {
                EnemyHp = 38;
                CombatState = WireCombatState.Weak;
                Root(WireScreen.Defeat);
            }
            else
            {
                Runes += IsBoss ? 300 : 120;
                progress[Area]++;
                Root(IsBoss ? WireScreen.Result : WireScreen.Explore);
                Notice = IsBoss ? "この区間を踏破した！" : "勝利！ 120ルーンを獲得した。";
                if (!IsBoss && !HasNewEquipment)
                {
                    HasNewEquipment = true;
                    Popup = WirePopup.NewEquipment;
                }
            }
            Notify();
        }

        public void Retry()
        {
            if (!Ready(WireScreen.Defeat))
                return;
            Root(WireScreen.Battle);
            ResetBattle();
            AlliesRecovered = true;
            Notify();
        }

        public void ShowRevive()
        {
            if (!Ready(WireScreen.Defeat))
                return;
            Popup = WirePopup.Revive;
            Notify();
        }

        public void ConfirmRevive()
        {
            if (Popup != WirePopup.Revive || Screen != WireScreen.Defeat)
                return;
            Root(WireScreen.Battle);
            AlliesRecovered = true;
            Notice = "復活の表示サンプル。ルーンは減りません。";
            Notify();
        }

        public void OtherPath()
        {
            if (!Ready(WireScreen.Defeat))
                return;
            ReturnToExplore();
            Notify();
        }

        public void EndAdventure()
        {
            if (Ready(WireScreen.Explore))
                Popup = WirePopup.EndAdventure;
            else if (
                Ready(WireScreen.Defeat)
                || Ready(WireScreen.Result)
                || Popup == WirePopup.EndAdventure
            )
                Root(WireScreen.Home);
            else
                return;
            Notify();
        }

        public void ShowGoals(bool historyView)
        {
            if (!Ready(WireScreen.Goals))
                return;
            Popup = historyView ? WirePopup.GoalHistory : WirePopup.GoalEdit;
            Notify();
        }

        public void ToggleGoal(int option)
        {
            if (Popup != WirePopup.GoalEdit)
                return;
            if (option == 0)
                GoalWeekly = !GoalWeekly;
            if (option == 1)
                GoalDistance = !GoalDistance;
            if (option == 2)
                GoalTimed = !GoalTimed;
            Notify();
        }

        public void SaveGoal()
        {
            if (Popup != WirePopup.GoalEdit)
                return;
            if (GoalDistance)
            {
                dataReturn = WirePopup.GoalEdit;
                Popup = WirePopup.Data;
                Notify();
                return;
            }
            CompleteGoal();
        }

        public void ConfirmData()
        {
            if (Popup != WirePopup.Data)
                return;
            if (dataReturn == WirePopup.GoalEdit)
                CompleteGoal();
            else
                Back();
        }

        private void CompleteGoal()
        {
            int index = GoalWeekly ? 1 : 0;
            bool first = goals[index] == WireGoalState.Unset;
            (first ? goalDistances : pendingDistances)[index] = GoalDistance;
            (first ? goalTimes : pendingTimes)[index] = GoalTimed;
            goals[index] = first ? WireGoalState.Progress : WireGoalState.Scheduled;
            Popup = WirePopup.None;
            if (initialGoals)
            {
                initialGoals = false;
                Root(WireScreen.Home);
            }
            Notify();
        }

        public void ClearGoal()
        {
            if (!Ready(WireScreen.Goals))
                return;
            Array.Clear(goals, 0, goals.Length);
            Notify();
        }

        public void CancelSchedule()
        {
            if (!Ready(WireScreen.Goals) || !HasGoalSchedule)
                return;
            for (int i = 0; i < goals.Length; i++)
                if (goals[i] == WireGoalState.Scheduled)
                    goals[i] = WireGoalState.Progress;
            Notify();
        }

        public void ShowData()
        {
            if (!Ready(WireScreen.Settings))
                return;
            dataReturn = WirePopup.None;
            Popup = WirePopup.Data;
            Notify();
        }

        public void ChangeVolume()
        {
            if (!Ready(WireScreen.Settings))
                return;
            Volume = (Volume + 25) % 125;
            Notify();
        }

        public void ToggleDebug()
        {
            if (Popup != WirePopup.None)
                return;
            DebugOpen = !DebugOpen;
            Notify();
        }

        public void PreviewCombat(WireCombatState state)
        {
            if (!DebugOpen || Screen != WireScreen.Battle)
                return;
            CombatState = state;
            CastingSlot = Slot;
            DebugOpen = false;
            Notify();
        }

        public void PreviewGoal(WireGoalState state)
        {
            if (!DebugOpen)
                return;
            for (int i = 0; i < goals.Length; i++)
                goals[i] = state;
            DebugOpen = false;
            Notify();
        }

        public void ToggleSample(int option)
        {
            if (!DebugOpen)
                return;
            if (option == 0)
                ThreeSkills = !ThreeSkills;
            if (option == 1)
                MultipleWarnings = !MultipleWarnings;
            if (option == 2 && Screen == WireScreen.Battle)
            {
                IsBoss = !IsBoss;
                Target = 0;
            }
            Notify();
        }

        public void PreviewFinish(bool victory)
        {
            if (!DebugOpen)
                return;
            DebugOpen = false;
            FinishBattle(victory);
        }

        public void Reset()
        {
            if (!DebugOpen)
                return;
            for (int i = 0; i < 4; i++)
                party[i] = i;
            Array.Clear(equipment, 0, equipment.Length);
            Array.Clear(progress, 0, progress.Length);
            Slot = Candidate = SelectedEquipment = Area = 0;
            Runes = 860;
            HasNewCompanion =
                HasNewEquipment =
                ThreeSkills =
                MultipleWarnings =
                initialGoals =
                    false;
            GoalWeekly = GoalDistance = GoalTimed = IsBoss = false;
            Array.Clear(goals, 0, goals.Length);
            Array.Clear(goalDistances, 0, goalDistances.Length);
            Array.Clear(goalTimes, 0, goalTimes.Length);
            Array.Clear(pendingDistances, 0, pendingDistances.Length);
            Array.Clear(pendingTimes, 0, pendingTimes.Length);
            Volume = 50;
            Notice = "";
            Root(WireScreen.Home);
            ResetBattle();
            Popup = WirePopup.Intro;
            Notify();
        }

        private void ResetBattle()
        {
            CombatState = WireCombatState.Normal;
            EnemyHp = 100;
            Target = 0;
            Skill = -1;
            SkillSelection = AlliesRecovered = false;
        }

        private void Root(WireScreen screen)
        {
            history.Clear();
            Screen = screen;
            Popup = WirePopup.None;
            acquiredRoute = DebugOpen = false;
        }

        private void ReturnToExplore() => Root(WireScreen.Explore);
    }
}
