using System;
using System.Collections.Generic;
using Baryonyx.Inventory;
using Baryonyx.UI;
using Baryonyx.UI.EditModePreview;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure's controls laid over the battle screen: the menu at the top left, the
    /// plate that shows each drop of a won battle, the dialog of the win, the revive and the
    /// result, and the notice band. The battle screen itself stays the mock's
    /// (BattleInspectView). AdventureAssets builds it.
    /// </summary>
    public sealed class AdventureOverlay : MonoBehaviour, IEditModePreview
    {
        public Button MenuButton;
        public GameDialog Dialog;
        public NoticeBand Notice;

        // 勝った戦闘のドロップを1つずつ見せる札。
        public RewardReveal Reveal;

        // ドロップのアイテム・装備の絵（種類のIDで引く）。
        public ItemArt ItemArt;

        // ドロップ・結果の札を押すと開く詳しい窓の中身を引く図鑑と、その窓。
        public Library.LibraryData DropLibrary;
        public ItemDetailWindow DetailWindow;

        // クリア報酬のルーンの札の絵。
        public Sprite RuneIcon;

        // 勝った戦闘のあとに選ぶ、戦闘後の強化（仲間ごとの4つの列）。
        public AdventureBoonPicker Boons;

        // 冒険の結果（最奥の主を倒した・全滅して帰った・やめた）。
        public AdventureResultPanel Result;

        private bool wired;

        public event Action MenuPressed;

        // 端末の戻るキー。開いているダイアログが先に受け取り、なければメニューを開く。
        public event Action BackPressed;

        // 戦闘の画面の重ね（山札の一覧など）を戻るキーで閉じる処理。閉じたら true。戦闘の流れが渡す。
        public Func<bool> BattleBack { get; set; }

        private void Awake() => Wire();

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            if (MenuButton != null)
                MenuButton.onClick.AddListener(() => MenuPressed?.Invoke());
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                PressBack();
        }

        public void PressBack()
        {
            if (Dialog != null && Dialog.HandleBack())
                return;
            if (DetailWindow != null && DetailWindow.HandleBack())
                return;
            if (Result != null && Result.HandleBack())
                return;
            if (Boons != null && Boons.HandleBack())
                return;
            if (BattleBack != null && BattleBack())
                return;
            BackPressed?.Invoke();
        }

        public void ShowNotice(string message)
        {
            if (Notice != null)
                Notice.Show(message);
        }

        /// <summary>Shows or hides the menu button (hidden while the battle has no adventure).</summary>
        public void ShowMenu(bool shown)
        {
            if (MenuButton != null)
                MenuButton.gameObject.SetActive(shown);
        }

        // 停止中の表示の切り替え：戦闘の後のダイアログを、BattleAdventureFlow と同じ AdventureDialogs で出す。
        public IReadOnlyList<string> PreviewStates =>
            new[]
            {
                "勝利",
                "全滅（復活できる）",
                "全滅（ルーン不足）",
                "メニュー",
                "やめる確認",
                "結果（クリア）",
                "結果（冒険の終わり）",
                "最奥のドロップ（クリア報酬）",
                "保存できない",
                "戦闘後の強化",
                "戦闘後の強化 › 決めたとき",
            };

        public void ShowPreviewState(int index)
        {
            if (Boons != null)
                Boons.Close();
            if (Result != null)
                Result.Close();
            if (Reveal != null)
                Reveal.HideNow();
            int boons = PreviewStates.Count - 2;
            if (index >= boons)
            {
                if (Dialog != null)
                    Dialog.Hide();
                if (Boons != null)
                {
                    Boons.ShowSample();
                    // 決めたとき：光の柱がいちばん明るい瞬間で止める。
                    if (index > boons)
                        Boons.ShowTake(Boons.Picked, AdventureBoonPicker.TakePeak);
                }
                return;
            }
            if (Dialog == null)
                return;
            Action none = () => { };
            int cost = AdventureDialogSamples.ReviveCost;
            switch (index)
            {
                case 0:
                    AdventureDialogs.Victory(Dialog, none);
                    break;
                case 1:
                    AdventureDialogs.Revive(
                        Dialog,
                        cost,
                        AdventureDialogSamples.Runes,
                        true,
                        none,
                        none
                    );
                    break;
                case 2:
                    AdventureDialogs.Revive(Dialog, cost, cost / 3, false, none, none);
                    break;
                case 3:
                    AdventureDialogs.Menu(
                        Dialog,
                        AdventureDialogSamples.Run,
                        AdventureTexts.SuspendBody,
                        none,
                        none,
                        none
                    );
                    break;
                case 4:
                    AdventureDialogs.ConfirmQuit(Dialog, none, none, none);
                    break;
                case 5:
                case 6:
                    Dialog.Hide();
                    if (Result != null)
                        Result.ShowSample(
                            index == 5 ? AdventureEndStatus.Cleared : AdventureEndStatus.Defeated,
                            ItemArt
                        );
                    break;
                case 7:
                    // 最奥の主を倒したときに、主のドロップと並ぶクリア回数の報酬の札（開き終えた形）。
                    Dialog.Hide();
                    if (Reveal != null)
                        Reveal.ShowStill(
                            AdventureDropItems.FromClearReward(
                                AdventureClearRewards.At(AdventureCatalog.ForestRuins, 2),
                                ItemArt,
                                RuneIcon,
                                DropLibrary
                            )[0]
                        );
                    break;
                default:
                    AdventureDialogs.SaveFailed(Dialog, none);
                    break;
            }
        }
    }
}
