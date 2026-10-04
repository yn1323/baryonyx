using System;
using Baryonyx.StepBonus;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The adventure's controls laid over the battle screen: the menu at the top left, the
    /// dialog of the reward, the revive and the result, and the notice band. The battle screen
    /// itself stays the mock's (BattleInspectView). AdventureAssets builds it.
    /// </summary>
    public sealed class AdventureOverlay : MonoBehaviour
    {
        public Button MenuButton;
        public GameDialog Dialog;
        public NoticeBand Notice;

        // 報酬のボーナスの名前とアイコン。
        public StepBonusMockData Bonuses;

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
    }
}
