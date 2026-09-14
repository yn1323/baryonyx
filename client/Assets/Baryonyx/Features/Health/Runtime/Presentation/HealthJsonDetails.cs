using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Baryonyx.Health
{
    internal sealed class HealthJsonDetails : IDisposable
    {
        private readonly GameObject overlay;
        private readonly UnityEngine.UI.ScrollRect mainScroll;
        private readonly UnityEngine.UI.ScrollRect jsonScroll;
        private readonly TMP_Text title;
        private readonly TMP_Text json;
        private readonly UnityEngine.UI.Button copy;
        private readonly UnityEngine.UI.Button close;
        private readonly TMP_Text copyLabel;
        private readonly Action closeRequested;
        private HealthDaySnapshot renderedDay;
        private GameObject previousSelection;

        public HealthJsonDetails(
            GameObject overlay,
            UnityEngine.UI.ScrollRect mainScroll,
            UnityEngine.UI.ScrollRect jsonScroll,
            TMP_Text title,
            TMP_Text json,
            UnityEngine.UI.Button copy,
            UnityEngine.UI.Button close,
            Action closeRequested
        )
        {
            this.overlay = overlay;
            this.mainScroll = mainScroll;
            this.jsonScroll = jsonScroll;
            this.title = title;
            this.json = json;
            this.copy = copy;
            this.close = close;
            this.closeRequested = closeRequested;
            copyLabel = copy.GetComponentInChildren<TMP_Text>(true);
            copy.onClick.AddListener(Copy);
            close.onClick.AddListener(Close);
        }

        public void Reset() => renderedDay = null;

        public void HandleInput()
        {
            if (overlay.activeSelf && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                Close();
        }

        public void Render(HealthDaySnapshot day, bool preview)
        {
            bool open = day != null;
            if (open && !overlay.activeSelf)
            {
                var events = EventSystem.current;
                previousSelection = events != null ? events.currentSelectedGameObject : null;
                mainScroll.StopMovement();
                overlay.SetActive(true);
                close.Select();
            }
            else if (!open && overlay.activeSelf)
            {
                overlay.SetActive(false);
                json.text = "";
                var events = EventSystem.current;
                if (
                    previousSelection != null
                    && previousSelection.activeInHierarchy
                    && events != null
                )
                    events.SetSelectedGameObject(previousSelection);
                previousSelection = null;
            }
            mainScroll.enabled = !open;
            if (open)
            {
                title.text = day.Day + (preview ? " / サンプルJSON" : "  /  JSON");
                if (!ReferenceEquals(renderedDay, day))
                {
                    json.text = day.Json;
                    copyLabel.text = "JSONをコピー";
                    Canvas.ForceUpdateCanvases();
                    jsonScroll.normalizedPosition = new Vector2(0, 1);
                }
            }
            renderedDay = day;
        }

        private void Copy()
        {
            if (!overlay.activeSelf || renderedDay == null)
                return;
            GUIUtility.systemCopyBuffer = renderedDay.Json;
            copyLabel.text = "コピーしました";
        }

        private void Close() => closeRequested();

        public void Dispose()
        {
            if (copy != null)
                copy.onClick.RemoveListener(Copy);
            if (close != null)
                close.onClick.RemoveListener(Close);
            if (json != null)
                json.text = "";
            renderedDay = null;
            previousSelection = null;
        }
    }
}
