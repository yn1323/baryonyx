using TMPro;
using UnityEngine;

namespace Baryonyx.Wireframe.Editor
{
    public static partial class WireframeScreenAssets
    {
        private static void BuildFinishedPage(
            WireScreen screen,
            RectTransform page,
            WireframeLayout layout
        )
        {
            switch (screen)
            {
                case WireScreen.Home:
                    Label("HomeRunes", page, "", 15, 24).alignment = TextAlignmentOptions.Right;
                    Label("HomeGoogle", page, "", 12, 22).alignment = TextAlignmentOptions.Right;
                    PartyLandscape(page, "HomeLandscape", 200);
                    page.Find("HomeLandscape")
                        .GetComponent<UnityEngine.UI.LayoutElement>()
                        .flexibleHeight = 1;
                    Label("HomeParty", page, "", 11, 0).gameObject.SetActive(false);
                    Label("HomeStepsTitle", page, "今日の歩数", 13, 22).alignment =
                        TextAlignmentOptions.Center;
                    var steps = Button("HomeSteps", page, "未取得", 52, 32);
                    steps.image.color = Color.clear;
                    page.GetComponentInParent<WireframeHealthView>().HomeSteps =
                        steps.GetComponentInChildren<TMP_Text>();
                    Label("HomeProgress", page, "", 12, 20).alignment = TextAlignmentOptions.Center;
                    Button("HomeAdventure", page, "冒険へ出かける  →", 54, 18, true);
                    var home = Row(page, "HomeActions");
                    Button("HomeEquipment", home, "装備", 48, 14);
                    Button("HomeSettings", home, "歩数を連携", 48, 14);
                    break;
                case WireScreen.Destination:
                    Label("DestinationIntro", page, "どこへ行く？", 25, 56);
                    DestinationCard(page, 0, forest, "木漏れ日の森", "獣の気配 / 武器を探す");
                    DestinationCard(page, 1, mine, "古い坑道", "硬い敵 / 宝箱を探す");
                    Label("DestinationHint", page, "遠くの古塔\nこの先の冒険で解放", 13, 60).color =
                        Muted;
                    break;
                case WireScreen.Explore:
                    BuildExploration(page);
                    break;
                case WireScreen.Party:
                    Label("PartyHint", page, "出撃中の4人", 18, 40);
                    PortraitSlots(page, "PartySlot");
                    CharacterFeature(page, "PartyPortrait", "PartyDetails", 138);
                    Eyebrow("RosterEyebrow", page, "待機中の仲間");
                    Label("PartyEmpty", page, "探索で、新しい仲間に出会おう。", 14, 44).color =
                        Muted;
                    for (int i = 0; i < 5; i++)
                        PortraitButton("Character" + i, page, i, 62);
                    Button("PartyConfirm", page, "この仲間を編成", primary: true);
                    Button("PartyEquipment", page, "選択した仲間の装備へ");
                    break;
                case WireScreen.Equipment:
                    Label("EquipmentTitle", page, "旅のしたく", 22, 42);
                    PortraitSlots(page, "EquipmentSlot");
                    CharacterFeature(page, "EquipmentPortrait", "EquipmentCompare", 144, true);
                    Eyebrow("WeaponEyebrow", page, "所持している武器");
                    for (int i = 0; i < 3; i++)
                    {
                        var b = Button(
                            "Equipment" + i,
                            page,
                            "武器",
                            64,
                            16,
                            frameOverride: equipmentFrame
                        );
                        PixelEmblem(b.transform as RectTransform, "WeaponIcon" + i, i);
                        b.GetComponentInChildren<TMP_Text>().margin = new Vector4(56, 6, 12, 6);
                        b.GetComponentInChildren<TMP_Text>().alignment =
                            TextAlignmentOptions.MidlineLeft;
                    }
                    Button(
                        "EquipmentConfirm",
                        page,
                        "この武器に変更",
                        primary: true,
                        frameOverride: equipmentFrame
                    );
                    Label(
                        "EquipmentHint",
                        page,
                        "装備の強さは次の戦闘に反映されます。",
                        12,
                        36
                    ).color = Muted;
                    break;
                case WireScreen.Battle:
                    BuildBattle(page, layout);
                    DecorateBattle(page);
                    break;
                case WireScreen.Defeat:
                    var rest = Illustration(
                        "RestLandscape",
                        page,
                        forest,
                        138,
                        new Rect(0, .1f, 1, .55f)
                    );
                    rest.GetComponentInChildren<UnityEngine.UI.RawImage>().color = new Color(
                        .48f,
                        .56f,
                        .61f
                    );
                    Actor("RestActor", rest, 0, new Vector2(.5f, .08f), new Vector2(75, 112));
                    Label("DefeatTitle", page, "ひと休みしよう", 26, 52);
                    Card("DefeatSummary", page, "", 106, 14);
                    Button("DefeatRetry", page, "無料で再戦する", primary: true);
                    Button("DefeatParty", page, "仲間の編成を見直す");
                    Button("DefeatPath", page, "別の道を探す");
                    Button("DefeatEnd", page, "ホームへ帰る");
                    Button("DefeatRevive", page, "ルーンで復活", fontSize: 14);
                    break;
                case WireScreen.Result:
                    PartyLandscape(page, "ResultLandscape", 146);
                    var cleared = Label("ResultTitle", page, "冒険のひと区切り", 25, 48);
                    cleared.color = Gold;
                    Card("ResultSummary", page, "", 224, 16);
                    Button("ResultHome", page, "ホームへ帰る", primary: true);
                    break;
                case WireScreen.Goals:
                    Button("GoalsHealth", page, "1週間の歩数を見る  →", 64, 17, true);
                    Label("GoalsTitle", page, "運動目標", 20, 48);
                    GoalCard("GoalsDaily", page, "今日", false);
                    GoalCard("GoalsWeekly", page, "今週", true);
                    Button("GoalsEdit", page, "目標を設定・変更する", primary: true);
                    Button("GoalsHistory", page, "これまでの歩み");
                    Button("GoalsCancel", page, "変更予約を取り消す");
                    Button("GoalsClear", page, "目標を外す", fontSize: 14);
                    break;
                case WireScreen.Settings:
                    Label("SettingsGuide", page, "サウンド", 20, 48);
                    Button("SettingsVolume", page, "音量 50%", 70);
                    VolumeTrack(page);
                    Button("SettingsData", page, "Health Connect・歩数   →", 70);
                    Label(
                        "SettingsNote",
                        page,
                        "戦闘・装備・目標は試作です。冒険の進行はアプリ終了時にリセットされます。\n健康データは端末から読み取り、サーバーへ送信しません。",
                        13,
                        110
                    ).color = Muted;
                    break;
                case WireScreen.Health:
                    BuildHealth(page);
                    break;
            }
        }
    }
}
