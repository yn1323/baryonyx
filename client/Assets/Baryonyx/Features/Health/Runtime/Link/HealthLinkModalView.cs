using System;
using System.Collections.Generic;
using Baryonyx.UI.EditModePreview;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Health
{
    // Health Connect連携モーダルの描画とボタン通知だけを担う。表示する条件は呼び出し側が決める。
    public sealed class HealthLinkModalView : MonoBehaviour, IEditModePreview
    {
        public TMP_Text TitleLabel;
        public TMP_Text BodyLabel;
        public Button ActionButton;
        public TMP_Text ActionLabel;
        public Button LaterButton;
        public TMP_Text LaterLabel;

        public event Action ActionPressed;
        public event Action LaterPressed;

        public bool IsShown => gameObject.activeSelf;

        private void OnEnable()
        {
            if (ActionButton != null)
                ActionButton.onClick.AddListener(OnAction);
            if (LaterButton != null)
                LaterButton.onClick.AddListener(OnLater);
        }

        private void OnDisable()
        {
            if (ActionButton != null)
                ActionButton.onClick.RemoveListener(OnAction);
            if (LaterButton != null)
                LaterButton.onClick.RemoveListener(OnLater);
        }

        public void Show(HealthLinkStatus status, bool interactable = true)
        {
            var message = HealthLinkMessage.For(status);
            Set(TitleLabel, HealthLinkMessage.Title);
            Set(BodyLabel, message.Body);
            Set(ActionLabel, message.Action);
            Set(LaterLabel, HealthLinkMessage.LaterLabel);
            if (ActionButton != null)
                ActionButton.interactable = interactable;
            if (LaterButton != null)
                LaterButton.interactable = interactable;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        // 停止中の表示の切り替え：連携の状態ごとの本文と主ボタン。最後はボタンを押せない間（確認中）。
        private static readonly HealthLinkStatus[] PreviewStatuses =
        {
            HealthLinkStatus.PermissionRequired,
            HealthLinkStatus.SettingsRequired,
            HealthLinkStatus.InstallRequired,
            HealthLinkStatus.UpdateRequired,
            HealthLinkStatus.PermissionRequired,
        };

        public IReadOnlyList<string> PreviewStates =>
            new[] { "連携の許可", "設定を開く", "入手が必要", "更新が必要", "確認中（押せない）" };

        public void ShowPreviewState(int index) =>
            Show(PreviewStatuses[index], index != PreviewStatuses.Length - 1);

        private void OnAction() => ActionPressed?.Invoke();

        private void OnLater() => LaterPressed?.Invoke();

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text;
        }
    }
}
