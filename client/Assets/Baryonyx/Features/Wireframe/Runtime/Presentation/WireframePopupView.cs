namespace Baryonyx.Wireframe
{
    public sealed partial class WireframeView
    {
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
                    body = "4人の仲間と、冒険へ。\n\n毎日の歩数は、あとから連携できます。";
                    primary = "冒険をはじめる";
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
                        "100ルーンを使って味方全員を全快します。\n敵のHPとダウン状態は引き継ぎます。\n無料再戦も選べます。";
                    primary = "100ルーンで復活";
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
                    title = "これまでの歩み";
                    body =
                        "目標の履歴はまだありません。\n\n日々の歩数は「1週間の歩数」で確認できます。";
                    break;
                case WirePopup.Data:
                    title = "運動データの連携案内";
                    body =
                        "距離や運動時間には、対応する運動アプリ・機器の記録が必要です。\n\n記録元アプリのHealth Connectへの書き込み許可と、このアプリの読み取り許可は別です。\n\n歩数画面から接続・更新・権限の確認ができます。";
                    primary = "確認して戻る";
                    break;
            }
            Set("PopupTitle", title);
            Set("PopupText", body);
            Caption("PopupPrimary", primary);
            Enable("PopupPrimary", s.Popup != WirePopup.Revive || s.Runes >= 100);
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
