namespace Baryonyx.Wireframe
{
    public sealed partial class WireframeView
    {
        private void RenderPages()
        {
            var s = Session;
            var data = s.Data;
            var names = new string[4];
            for (int i = 0; i < 4; i++)
                names[i] = data.Characters[s.PartyMember(i)].Name;
            Set("HomeParty", string.Join(" ・ ", names));
            Set("HomeRunes", $"ルーン  {s.Runes:N0}");
            Set("HomeProgress", Session.Goal(false) == WireGoalState.Unset ? "" : "目標 5,000歩");
            Set("ExplorePlace", $"現在地：{s.Place}  /  区間 {s.Step + 1}");
            Set("ExplorePartyText", string.Join(" ・ ", names));
            Set("ExploreNotice", string.IsNullOrEmpty(s.Notice) ? "" : s.Notice);
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
            bool hasStandby = false;
            for (int i = 0; i < 5; i++)
            {
                bool active = false;
                for (int slot = 0; slot < 4; slot++)
                    active |= s.PartyMember(slot) == i;
                bool standby = s.OwnsCharacter(i) && !active;
                hasStandby |= standby;
                Visible("Character" + i, standby);
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
            Text("PartyEmpty").gameObject.SetActive(!hasStandby);
            Visible("PartyConfirm", hasStandby);
            Enable("PartyConfirm", s.Candidate != s.SelectedCharacter);
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
                $"{names[s.Slot]}\n{chosen.Name}\n\n威力  {current.Power} → {chosen.Power}  ({chosen.Power - current.Power:+0;-0;±0})\nダウン  {current.Down} → {chosen.Down}  ({chosen.Down - current.Down:+0;-0;±0})"
            );
            for (int i = 0; i < 3; i++)
            {
                Caption(
                    "Equipment" + i,
                    (s.SelectedEquipment == i ? "● " : "")
                        + (
                            s.OwnsEquipment(i)
                                ? data.Equipment[i].Name
                                    + $"\n威力 {data.Equipment[i].Power}  /  ダウン {data.Equipment[i].Down}"
                                : "未獲得の武器"
                        )
                );
                Enable("Equipment" + i, s.OwnsEquipment(i));
            }
            Set(
                "DefeatSummary",
                $"獲得済みのルーン・装備・仲間は残ります。\n所持 {s.Runes:N0}ルーン / {s.Place}\n無料再戦は全快から。復活は敵HPを引き継ぎます。"
            );
            Set(
                "ResultSummary",
                $"{s.Place}の区間を踏破！\n\n今回のボス報酬：300ルーン\n所持ルーン：{s.Runes:N0}\n\n新しい道が見えてきた。\n装備：{(s.HasNewEquipment ? "宝箱の武器を獲得済み" : "追加なし")}\n仲間：{(s.HasNewCompanion ? "ノエルが加入済み" : "追加なし")}"
            );
            Set("GoalsDaily", GoalText(false));
            Set("GoalsWeekly", GoalText(true));
            Visible("GoalsCancel", s.HasGoalSchedule);
            Caption("SettingsVolume", $"音量  {s.Volume}%   /   タップで変更");
            if (s.Screen == WireScreen.Battle)
                RenderBattle(names);
        }

        private string GoalText(bool weekly)
        {
            string period = weekly ? "今週" : "今日";
            if (Session.Goal(weekly) == WireGoalState.Unset)
                return period + "の目標は未設定";
            if (Session.Goal(weekly) == WireGoalState.Scheduled)
                return period + "：変更予約あり\n" + Session.SavedGoal(weekly, true);
            string note =
                Session.GoalUsesDistance(weekly) ? "距離の評価は準備中です。"
                : weekly ? "歩数の実績は「1週間の歩数」で確認できます。"
                : health != null && health.TodaySteps.HasValue
                    ? $"{health.TodaySteps.Value:N0} / 5,000歩"
                : "歩数は未取得です。";
            return Session.SavedGoal(weekly) + "\n" + note + "\n目標の評価・保存は試作段階です。";
        }
    }
}
