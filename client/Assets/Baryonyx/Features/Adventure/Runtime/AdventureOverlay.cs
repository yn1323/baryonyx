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

        // 勝った戦闘のあとに選ぶ、戦闘後の強化（仲間ごとの4つの列）。
        public AdventureBoonPicker Boons;

        private bool wired;

        public event Action MenuPressed;

        // 端末の戻るキー。開いているダイアログが先に受け取り、なければメニューを開く。
        public event Action BackPressed;

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
            if (Boons != null && Boons.HandleBack())
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
                "勝利（最奥）",
                "全滅（復活できる）",
                "全滅（ルーン不足）",
                "メニュー",
                "やめる確認",
                "結果（冒険の終わり）",
                "保存できない",
                "戦闘後の強化",
                "戦闘後の強化 › 決めたとき",
            };

        public void ShowPreviewState(int index)
        {
            if (Boons != null)
                Boons.Close();
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
                case 1:
                    AdventureDialogs.Victory(Dialog, index == 1, none);
                    break;
                case 2:
                    AdventureDialogs.Revive(
                        Dialog,
                        cost,
                        AdventureDialogSamples.Runes,
                        true,
                        none,
                        none
                    );
                    break;
                case 3:
                    AdventureDialogs.Revive(Dialog, cost, cost / 3, false, none, none);
                    break;
                case 4:
                    AdventureDialogs.Menu(
                        Dialog,
                        AdventureDialogSamples.Run,
                        AdventureTexts.SuspendBody,
                        AdventureTexts.GoOnChoice,
                        none,
                        none,
                        none
                    );
                    break;
                case 5:
                    AdventureDialogs.ConfirmQuit(Dialog, none, none, none);
                    break;
                case 6:
                    AdventureDialogs.Result(
                        Dialog,
                        AdventureDialogSamples.Result(AdventureEndStatus.Defeated),
                        none
                    );
                    break;
                default:
                    AdventureDialogs.SaveFailed(Dialog, none);
                    break;
            }
        }
    }
}
