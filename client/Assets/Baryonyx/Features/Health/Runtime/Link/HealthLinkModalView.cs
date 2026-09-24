using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Health
{
    // Health Connect連携モーダルの描画とボタン通知だけを担う。表示する条件は呼び出し側が決める。
    public sealed class HealthLinkModalView : MonoBehaviour
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

        private void OnAction() => ActionPressed?.Invoke();

        private void OnLater() => LaterPressed?.Invoke();

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text;
        }
    }
}
