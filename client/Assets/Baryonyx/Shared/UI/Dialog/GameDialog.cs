using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// One choice of a <see cref="GameDialog"/>: its label, what it does, whether it can be
    /// pressed, and its role, which gives its look in a row (doc/rules/ui-design.md#決めるボタン).
    /// </summary>
    public readonly struct DialogChoice
    {
        public DialogChoice(
            string label,
            Action action,
            bool enabled = true,
            ButtonRole role = ButtonRole.Secondary
        )
        {
            Label = label ?? "";
            Action = action;
            Enabled = enabled;
            Role = role;
        }

        public string Label { get; }
        public Action Action { get; }
        public bool Enabled { get; }
        public ButtonRole Role { get; }
    }

    /// <summary>How a <see cref="GameDialog"/> lays out its choices and how it closes.</summary>
    [Flags]
    public enum DialogStyle
    {
        // 選択肢を窓の幅いっぱいに縦に並べる。
        Column = 0,

        // 選択肢を窓の下に横に並べる（左に「やめる・つづける」、右に決める方）。
        Row = 1,

        // 右上に×を出し、押すと cancel を行う（閉じる・つづけるの選択肢の代わり）。
        Cross = 2,

        // ボタンを出さず、画面のどこを押しても最初の選択肢を行う（勝利）。
        TapAnywhere = 4,
    }

    /// <summary>
    /// The shared dialog for a choice or a confirmation that the notice band cannot carry
    /// (doc/rules/ui-design.md): a dark veil over the whole screen that stops the taps behind
    /// it, and a window in the guide frame (a gold line on the dark pane) with a title, a body,
    /// an optional picture and its choices as frosted glass cards: stacked in equal rows, side
    /// by side along the bottom (<see cref="DialogStyle.Row"/>), or none with a tap anywhere
    /// going on (<see cref="DialogStyle.TapAnywhere"/>). A window that can be closed may show the
    /// × at its top right (<see cref="DialogStyle.Cross"/>).
    /// A pressed choice takes no more taps until the dialog is shown again, so a slow server
    /// answer is not sent twice. The device's back key does what <c>cancel</c> says (nothing
    /// when null). GameDialogAssets builds it.
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

        // 横に並べるときの選択肢と、その並び。
        public GameObject Row;
        public Button[] RowButtons = Array.Empty<Button>();
        public TMP_Text[] RowLabels = Array.Empty<TMP_Text>();

        // 横の並びのボタンの見た目（選択肢の役割で、決める・取り消し・危険を替える）。
        public ButtonFaces[] RowFaces = Array.Empty<ButtonFaces>();

        // 右上の×。
        public Button Cross;

        // 画面全体を覆う、どこを押しても進む板。
        public Button TapAnywhere;

        private DialogChoice[] choices = Array.Empty<DialogChoice>();
        private Action cancel;
        private bool wired;

        public DialogStyle Style { get; private set; }

        public bool CrossShown => Cross != null && Cross.gameObject.activeSelf;

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
            for (int i = 0; i < RowButtons.Length; i++)
            {
                int index = i;
                if (RowButtons[i] != null)
                    RowButtons[i].onClick.AddListener(() => Press(index));
            }
            if (Cross != null)
                Cross.onClick.AddListener(() => HandleBack());
            if (TapAnywhere != null)
                TapAnywhere.onClick.AddListener(() => Press(0));
        }

        public void Show(
            string title,
            string body,
            Sprite picture,
            Action cancel,
            params DialogChoice[] choices
        ) => Show(title, body, picture, cancel, DialogStyle.Column, choices);

        public void Show(
            string title,
            string body,
            Sprite picture,
            Action cancel,
            DialogStyle style,
            params DialogChoice[] choices
        )
        {
            Wire();
            this.choices = choices ?? Array.Empty<DialogChoice>();
            this.cancel = cancel;
            // 作り直す前のPrefab（横の並びや×がない）では、縦の並びで出す。
            if ((style & DialogStyle.Row) != 0 && RowButtons.Length < this.choices.Length)
                style &= ~DialogStyle.Row;
            if ((style & DialogStyle.TapAnywhere) != 0 && TapAnywhere == null)
                style &= ~DialogStyle.TapAnywhere;
            Style = style;
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
            bool tap = (style & DialogStyle.TapAnywhere) != 0;
            bool row = !tap && (style & DialogStyle.Row) != 0;
            Fill(Buttons, Labels, tap || row ? 0 : this.choices.Length);
            if (Row != null)
                Row.SetActive(row);
            Fill(RowButtons, RowLabels, row ? this.choices.Length : 0);
            for (int i = 0; row && i < RowFaces.Length && i < this.choices.Length; i++)
                if (RowFaces[i] != null)
                    RowFaces[i].Show(this.choices[i].Role);
            if (Cross != null)
                Cross.gameObject.SetActive((style & DialogStyle.Cross) != 0 && cancel != null);
            if (TapAnywhere != null)
            {
                TapAnywhere.gameObject.SetActive(tap);
                TapAnywhere.transform.SetAsLastSibling();
            }
            Group.alpha = 1f;
            Group.blocksRaycasts = true;
            Group.interactable = true;
            transform.SetAsLastSibling();
        }

        // 先頭の count 個のボタンに選択肢を入れて出し、残りは隠す。
        private void Fill(Button[] buttons, TMP_Text[] labels, int count)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                bool used = i < count;
                buttons[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                buttons[i].interactable = choices[i].Enabled;
                if (i < labels.Length && labels[i] != null)
                    labels[i].text = choices[i].Label;
            }
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
