using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>One choice of a <see cref="GameDialog"/>: its label, what it does, and whether it can be pressed.</summary>
    public readonly struct DialogChoice
    {
        public DialogChoice(string label, Action action, bool enabled = true)
        {
            Label = label ?? "";
            Action = action;
            Enabled = enabled;
        }

        public string Label { get; }
        public Action Action { get; }
        public bool Enabled { get; }
    }

    /// <summary>
    /// The shared dialog for a choice or a confirmation that the notice band cannot carry
    /// (doc/rules/ui-design.md): a dark veil over the whole screen that stops the taps behind
    /// it, and a silver-framed window with a title, a body, an optional picture and up to five
    /// choices stacked in equal rows. A pressed choice takes no more taps until the dialog is
    /// shown again, so a slow server answer is not sent twice. The device's back key does what
    /// <c>cancel</c> says (nothing when null). GameDialogAssets builds it.
    /// </summary>
    public sealed class GameDialog : MonoBehaviour
    {
        public CanvasGroup Group;
        public TMP_Text Title;
        public TMP_Text Body;

        // 本文の上に出す絵（報酬のアイコンなど）。使わないときは隠す。
        public Image Picture;
        public Button[] Buttons = Array.Empty<Button>();
        public TMP_Text[] Labels = Array.Empty<TMP_Text>();

        private DialogChoice[] choices = Array.Empty<DialogChoice>();
        private Action cancel;
        private bool wired;

        public bool IsShown => Group != null && Group.blocksRaycasts;

        // 選択肢を押したあと、次に表示するまで操作を受け付けない間。
        public bool Busy => IsShown && Group != null && !Group.interactable;

        public string TitleText => Title != null ? Title.text : "";
        public string BodyText => Body != null ? Body.text : "";

        public string[] ChoiceLabels
        {
            get
            {
                var labels = new string[choices.Length];
                for (int i = 0; i < choices.Length; i++)
                    labels[i] = choices[i].Label;
                return labels;
            }
        }

        public event Action<string> Chosen;

        private void Awake()
        {
            Wire();
            if (!IsShown)
                Hide();
        }

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
            for (int i = 0; i < Buttons.Length; i++)
            {
                int index = i;
                if (Buttons[i] != null)
                    Buttons[i].onClick.AddListener(() => Press(index));
            }
        }

        public void Show(
            string title,
            string body,
            Sprite picture,
            Action cancel,
            params DialogChoice[] choices
        )
        {
            Wire();
            this.choices = choices ?? Array.Empty<DialogChoice>();
            this.cancel = cancel;
            if (Title != null)
                Title.text = title ?? "";
            if (Body != null)
            {
                Body.text = body ?? "";
                Body.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }
            if (Picture != null)
            {
                Picture.sprite = picture;
                Picture.gameObject.SetActive(picture != null);
            }
            for (int i = 0; i < Buttons.Length; i++)
            {
                bool used = i < this.choices.Length;
                Buttons[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                Buttons[i].interactable = this.choices[i].Enabled;
                if (i < Labels.Length && Labels[i] != null)
                    Labels[i].text = this.choices[i].Label;
            }
            Group.alpha = 1f;
            Group.blocksRaycasts = true;
            Group.interactable = true;
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (Group == null)
                return;
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            choices = Array.Empty<DialogChoice>();
            cancel = null;
        }

        /// <summary>Takes taps again after a choice whose work failed, keeping the dialog as it is.</summary>
        public void Resume()
        {
            if (IsShown)
                Group.interactable = true;
        }

        /// <summary>
        /// The device's back key: while shown, the dialog takes it and does what its cancel says.
        /// False when the dialog is hidden, so the screen behind can take it.
        /// </summary>
        public bool HandleBack()
        {
            if (!IsShown)
                return false;
            if (cancel != null && !Busy)
                cancel();
            return true;
        }

        /// <summary>Presses the choice with <paramref name="label"/>, as a tap on it would.</summary>
        public bool Choose(string label)
        {
            for (int i = 0; i < choices.Length; i++)
                if (choices[i].Label == label)
                    return Press(i);
            return false;
        }

        private bool Press(int index)
        {
            if (!IsShown || Busy || index >= choices.Length || !choices[index].Enabled)
                return false;
            var choice = choices[index];
            Group.interactable = false;
            Chosen?.Invoke(choice.Label);
            choice.Action?.Invoke();
            return true;
        }
    }
}
