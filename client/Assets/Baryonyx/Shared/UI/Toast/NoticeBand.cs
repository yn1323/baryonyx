using System.Collections.Generic;
using Baryonyx.UI.EditModePreview;
using TMPro;
using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// The shared notice for messages that ask nothing of the player ("〇〇をセットしました",
    /// "設定（準備中）"): white text on the dark translucent band of the battle's skill names,
    /// at the top middle of every screen. It shows for a moment, fades and never takes taps.
    /// NoticeBandAssets builds it; see doc/rules/ui-design.md.
    /// </summary>
    public sealed class NoticeBand : MonoBehaviour, IEditModePreview
    {
        public CanvasGroup Group;
        public TMP_Text Label;

        // 帯の幅を文の長さに合わせるための、技名と同じ半透明の文字パネル。
        public TranslucentTextPanel Panel;

        // 文の左右に空ける幅と、帯の最小の幅（設計座標）。
        public float Padding = 200f;
        public float MinWidth = 520f;

        [Min(0.1f)]
        public float HoldSeconds = 1.4f;

        [Min(0f)]
        public float FadeSeconds = 0.3f;

        private FadingMessage fade;

        // 表示中の文。消えていれば空。
        public string Message =>
            Group != null && Group.alpha > 0f && Label != null ? Label.text : "";

        // 最後に出した文。消えたあとも残る。
        public string LastMessage => Label != null ? Label.text : "";

        public void Show(string message)
        {
            (fade ??= new FadingMessage(this)).Show(
                Group,
                Label,
                message,
                HoldSeconds,
                FadeSeconds
            );
            Fit();
        }

        // 短い文で帯が画面の端の部品まで広がらないよう、帯の幅を文に合わせる。
        private void Fit()
        {
            if (Panel == null || Label == null)
                return;
            float width = Label.GetPreferredValues(Label.text).x + Padding;
            Panel.SetBackdropSize(new Vector2(Mathf.Max(MinWidth, width), Panel.BackdropSize.y));
        }

        public void Hide() => (fade ??= new FadingMessage(this)).Hide(Group);

        // 停止中の表示の切り替え：消えずに止まった帯。
        public const string ShortSample = "ボーナスをセットしました";
        public const string LongSample = "ルーンが足りないか、冒険の状態が変わりました";

        public IReadOnlyList<string> PreviewStates => new[] { "短い文", "長い文" };

        public void ShowPreviewState(int index)
        {
            if (Group == null || Label == null)
                return;
            Label.text = index == 0 ? ShortSample : LongSample;
            Group.alpha = 1f;
            Fit();
        }
    }
}
