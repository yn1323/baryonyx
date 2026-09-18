using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Baryonyx.Wireframe
{
    public sealed class WireframeView : MonoBehaviour
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
        public WireframeSession Session { get; private set; }

        public UnityEngine.UI.Button Button(string name) => buttons[name];

        public TMP_Text Text(string name) => labels[name];

        public void Bind(WireframeSession session)
        {
            if (Session != null)
                Session.Changed -= Render;
            Session = session;
            art = GetComponent<WireframeArt>();
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
                Session?.Back();
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
            On("Back", Session.Back);
            On("Preview", Session.ToggleDebug);
            On("HomeAdventure", () => Session.Open(WireScreen.Destination));
            On("HomeEquipment", () => Session.Open(WireScreen.Equipment));
            On("HomeSettings", () => Session.Open(WireScreen.Settings));
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
            On("SettingsData", Session.ShowData);
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
                "ホーム",
                "冒険先",
                "探索",
                "仲間・編成",
                "装備",
                "戦闘",
                "全滅",
                "冒険の成果",
                "運動目標",
                "設定",
            };
            Set("Title", titles[(int)s.Screen]);
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

        private void RenderPages()
        {
            var s = Session;
            var data = s.Data;
            var names = new string[4];
            for (int i = 0; i < 4; i++)
                names[i] = data.Characters[s.PartyMember(i)].Name;
            Set("HomeParty", string.Join(" ・ ", names));
            Set("HomeRunes", $"ルーン  {s.Runes:N0}");
            Set("HomeProgress", HomeGoalText());
            Set("ExplorePlace", $"{s.Place}  /  区間 {s.Step + 1}");
            Set("ExplorePartyText", string.Join(" ・ ", names));
            Set(
                "ExploreNotice",
                string.IsNullOrEmpty(s.Notice) ? "気になる入口をタップして、先へ進もう。" : s.Notice
            );
            Caption(
                "ExploreDoor",
                s.Step >= 2
                    ? "↑ 奥の広間\n大きな敵の気配"
                    : (
                        s.Area == 1
                            ? "↑ 坑道の奥へ\n敵と装備の気配"
                            : "↑ 木々の先へ\n敵と装備の気配"
                    )
            );
            Caption("ExploreChest", s.HasNewCompanion ? "→ 脇道\n古い宝箱" : "→ 脇道\n旅人の姿");
            for (int i = 0; i < 4; i++)
            {
                string selected = i == s.Slot ? "● " : "";
                Caption("PartySlot" + i, selected + names[i]);
                Caption("EquipmentSlot" + i, selected + names[i]);
            }
            for (int i = 0; i < 5; i++)
            {
                Caption(
                    "Character" + i,
                    (i == s.Candidate ? "● " : "")
                        + (
                            s.OwnsCharacter(i)
                                ? data.Characters[i].Name + "  /  " + data.Characters[i].Role
                                : "未加入の仲間"
                        )
                );
                Enable("Character" + i, s.OwnsCharacter(i));
            }
            var candidate = data.Characters[s.Candidate];
            Set(
                "PartyDetails",
                $"{candidate.Name}  /  {candidate.Role}\nスキル：{candidate.Skills[0]}・{candidate.Skills[1]}\n装備：{data.Equipment[s.Equipped(s.Candidate)].Name}"
            );
            Caption("PartyConfirm", $"{names[s.Slot]}の枠に編成");
            var current = data.Equipment[s.Equipped(s.SelectedCharacter)];
            var chosen = data.Equipment[s.SelectedEquipment];
            Set(
                "EquipmentCompare",
                $"{names[s.Slot]}の武器\n装備中：{current.Name}\n候補：{chosen.Name}\n威力  {current.Power} → {chosen.Power}\nダウン値  {current.Down} → {chosen.Down}"
            );
            for (int i = 0; i < 3; i++)
            {
                Caption(
                    "Equipment" + i,
                    (s.SelectedEquipment == i ? "● " : "")
                        + (s.OwnsEquipment(i) ? data.Equipment[i].Name : "未獲得の武器")
                );
                Enable("Equipment" + i, s.OwnsEquipment(i));
            }
            Set(
                "DefeatSummary",
                $"獲得済みのルーン・装備・仲間は残ります。\n所持 {s.Runes:N0}ルーン / {s.Place}\n無料再戦は全快から、復活は敵HPを引き継ぐ表示です。"
            );
            Set(
                "ResultSummary",
                $"{s.Place}の区間を踏破！\n\n今回のボス報酬：300ルーン\n所持ルーン：{s.Runes:N0}\n\n新しい道が見えてきた。\n装備：{(s.HasNewEquipment ? "宝箱の武器を獲得済み" : "追加なし")}\n仲間：{(s.HasNewCompanion ? "ノエルが加入済み" : "追加なし")}"
            );
            Set("GoalsDaily", GoalText(false));
            Set("GoalsWeekly", GoalText(true));
            Visible("GoalsCancel", s.HasGoalSchedule);
            Caption("SettingsVolume", $"音量  {s.Volume}%   /   タップで変更");
            RenderBattle(names);
        }

        private string HomeGoalText()
        {
            bool distance = Session.GoalUsesDistance(false);
            return Session.Goal(false) switch
            {
                WireGoalState.Unset => "今日の目標は未設定\n目標なしでも冒険を始められます。",
                WireGoalState.Unknown => "今日の運動データは未確認\n確認後に歩みを表示します。",
                WireGoalState.Achieved => "今日の目標を達成！\n"
                    + (distance ? "4.2 / 3km" : "6,500 / 5,000歩"),
                WireGoalState.Scheduled => "目標の変更を予約しました\n新しい目標は明日から。",
                _ => "今日の歩み\n" + (distance ? "1.8 / 3km" : "3,820 / 5,000歩"),
            };
        }

        private string GoalText(bool weekly)
        {
            string period = weekly ? "今週" : "今日";
            bool distance = Session.GoalUsesDistance(weekly);
            string progress = distance
                ? (weekly ? "8.2 / 15km" : "1.8 / 3km")
                : (weekly ? "18,200 / 25,000歩" : "3,820 / 5,000歩");
            string achieved = distance
                ? (weekly ? "18 / 15km" : "4.2 / 3km")
                : (weekly ? "32,000 / 25,000歩" : "6,500 / 5,000歩");
            return Session.Goal(weekly) switch
            {
                WireGoalState.Unset => $"{period}の目標は未設定\n設定せずに冒険を始められます。",
                WireGoalState.Unknown =>
                    $"{period}の運動データは未確認\n実績ゼロ・未達とは区別します。",
                WireGoalState.Achieved =>
                    $"{period}：達成済み\n{achieved}\n難易度 ×1.2  /  継続 ×1.4（仮）\n{(weekly ? "直近12週：8週達成" : "直近90日：42日達成")}",
                WireGoalState.Scheduled =>
                    $"{period}：変更予約あり\n現在：{Session.SavedGoal(weekly)}\n予約：{Session.SavedGoal(weekly, true)}\n{(weekly ? "翌週" : "翌日")}から適用",
                _ =>
                    $"{period}：進行中\n{Session.SavedGoal(weekly)}\n{progress}\n難易度 ×1.2  /  継続 ×1.4（仮）\n{(weekly ? "直近12週：8週達成" : "直近90日：42日達成")}",
            };
        }

        private void RenderBattle(string[] names)
        {
            var s = Session;
            string enemy = s.IsBoss ? "森の大きな守り手" : "森の獣";
            Set(
                "BattleWarning",
                $"予告  {enemy}：大きな一撃 → {names[0]}"
                    + (s.MultipleWarnings ? $"\n予告  敵2：突進 → {names[2]}" : "")
            );
            for (int i = 0; i < 4; i++)
            {
                string selected = i == s.Slot ? "● " : "";
                string casting =
                    s.CombatState == WireCombatState.Casting && s.CastingSlot == i
                        ? "\n詠唱中"
                        : "";
                Caption("Ally" + i, selected + names[i] + casting);
                int hp = s.AlliesRecovered ? 100 : 100 - i * 15;
                Caption("Hp" + i, $"{selected}{names[i]}  {hp}%");
                HpFills[i].anchorMax = new Vector2(hp / 100f, 1);
            }
            for (int i = 0; i < 3; i++)
            {
                Visible("Enemy" + i, !s.IsBoss || i == 0);
                string state =
                    s.CombatState == WireCombatState.Down ? "\nダウン中"
                    : s.CombatState == WireCombatState.Weak ? "\nよろけ"
                    : "";
                Caption(
                    "Enemy" + i,
                    (s.Target == i ? "◎ " : "") + (s.IsBoss ? "守り手" : "敵 " + (i + 1)) + state
                );
                HpFills[i + 4].anchorMax = new Vector2(s.EnemyHp / 100f, 1);
                Text("Enemy" + i + "Label").color = s.CombatState
                    is WireCombatState.Down
                        or WireCombatState.Weak
                    ? new Color(.90f, .85f, .74f)
                    : Color.white;
                if (i == 0)
                {
                    var rect = (RectTransform)Button("Enemy0").transform;
                    rect.anchorMin = s.IsBoss ? new Vector2(.49f, .12f) : new Vector2(.56f, .69f);
                    rect.anchorMax = s.IsBoss ? new Vector2(.98f, .88f) : new Vector2(.98f, .98f);
                }
                Button("Enemy" + i).image.color =
                    s.CombatState == WireCombatState.Down ? new Color(.79f, .80f, .81f)
                    : s.CombatState == WireCombatState.Weak ? new Color(.61f, .64f, .67f)
                    : new Color(.38f, .43f, .47f);
            }
            var skills = s.Data.Characters[s.SelectedCharacter].Skills;
            for (int i = 0; i < 3; i++)
            {
                Visible("Skill" + i, i < 2 || s.ThreeSkills);
                Caption(
                    "Skill" + i,
                    (s.SkillSelection && s.Skill == i ? "● " : "")
                        + skills[i]
                        + (s.CombatState == WireCombatState.Cooldown ? "\n3.2秒" : "")
                );
                Enable("Skill" + i, s.CombatState != WireCombatState.Cooldown);
            }
            Set(
                "BattleSelection",
                s.SkillSelection
                    ? $"{names[s.Slot]} / 選択中（強スローの例）\n"
                        + (
                            s.Skill >= 0
                                ? $"{skills[s.Skill]} → 敵{s.Target + 1}  威力120 / ダウン40（仮）"
                                : "スキルを選択してください"
                        )
                    : $"{names[s.Slot]}  /  キャラ・スキルをタップ"
            );
            Visible("UseSkill", s.SkillSelection);
            Enable("UseSkill", s.Skill >= 0);
        }

        private void RenderPopup()
        {
            var s = Session;
            string title = "",
                body = "",
                primary = "閉じる",
                secondary = "戻る";
            switch (s.Popup)
            {
                case WirePopup.Intro:
                    title = "てくてくダンジョン";
                    body =
                        "毎日の歩みと、小さな冒険を。\n仲間を連れて、森や坑道へ出かけましょう。\n\n運動目標は、あとから設定できます。\n\n体験版：データはサンプルです。勝敗は右上の「確認」で切り替え、終了すると初期状態に戻ります。";
                    primary = "設定せずに始める";
                    secondary = "目標を設定する";
                    break;
                case WirePopup.NewEquipment:
                    title = "新しい装備を獲得";
                    body =
                        "宝箱の武器\n威力18 / ダウン値14（仮）\n\n所持一覧へ追加しました。探索中でも装備を変更できます。";
                    primary = "今すぐ装備";
                    secondary = "あとで";
                    break;
                case WirePopup.NewCompanion:
                    title = "ノエルが仲間になった！";
                    body =
                        "旅の戦士 / 横なぎ・砕き打ち\n\n今の4人のうち、誰と入れ替えるか選べます。";
                    primary = "編成する";
                    secondary = "あとで";
                    break;
                case WirePopup.Revive:
                    title = "ルーンで復活";
                    body =
                        "消費量の例：100ルーン\n敵HP 38%と濃淡を引き継ぎ、味方は全快します。\n獲得済みの成果は残ります。\n\nこのワイヤーではルーンを消費しません。無料再戦も選べます。";
                    primary = "復活の表示を確認";
                    secondary = "敗北画面へ戻る";
                    break;
                case WirePopup.EndAdventure:
                    title = "冒険を終えますか？";
                    body =
                        "この実行中は探索場所と獲得物を保持します。\nホームから同じ冒険先の続きを試せます。\nアプリを終了すると初期状態へ戻ります。";
                    primary = "ホームへ帰る";
                    secondary = "探索を続ける";
                    break;
                case WirePopup.GoalEdit:
                    title = "目標の設定例";
                    body =
                        s.GoalSummary
                        + "\n\n難易度倍率の表示例："
                        + (s.GoalTimed ? "×1.4" : "×1.2")
                        + "\n変更は日次なら翌日、週次なら翌週からの予約例です。\n入力・倍率計算・保存は行いません。";
                    primary = "この条件を表示する";
                    secondary = "設定せずに戻る";
                    break;
                case WirePopup.GoalHistory:
                    title = "過去の目標（サンプル）";
                    body =
                        "昨日 / 1日5,000歩・時間帯指定なし\n実績6,500歩 / 達成済み\n難易度×1.2 / 継続×1.4（仮）\n\n先週 / 週25,000歩・時間帯指定なし\n実績32,000歩 / 達成済み\n\n開始前の期間：—";
                    break;
                case WirePopup.Data:
                    title = "運動データの連携案内";
                    body =
                        "距離や運動時間には、対応する運動アプリ・機器の記録が必要です。\n\n記録元アプリのHealth Connectへの書き込み許可と、このアプリの読み取り許可は別です。\n\n設定を開く・公式ヘルプ・再取得の操作は、本接続時に用意します。この画面からOS設定や通信は実行しません。";
                    primary = "確認して戻る";
                    break;
            }
            Set("PopupTitle", title);
            Set("PopupText", body);
            Caption("PopupPrimary", primary);
            Caption("PopupSecondary", secondary);
            Visible(
                "PopupSecondary",
                s.Popup != WirePopup.GoalHistory && s.Popup != WirePopup.Data
            );
            for (int i = 0; i < 3; i++)
                Visible("GoalOption" + i, s.Popup == WirePopup.GoalEdit);
            Caption("GoalOption0", s.GoalWeekly ? "期間：1週間" : "期間：1日");
            Caption(
                "GoalOption1",
                s.GoalDistance
                    ? (s.GoalWeekly ? "基準：距離 15km" : "基準：距離 3km")
                    : (s.GoalWeekly ? "基準：歩数 25,000歩" : "基準：歩数 5,000歩")
            );
            Caption("GoalOption2", s.GoalTimed ? "時間帯：19〜20時" : "時間帯：指定なし");
        }
    }
}
