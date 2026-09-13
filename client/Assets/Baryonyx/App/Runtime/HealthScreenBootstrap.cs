using Baryonyx.Health;
using UnityEngine;

namespace Baryonyx.App
{
    public sealed class HealthScreenBootstrap : MonoBehaviour
    {
        public HealthConnectionSettings Settings;
        public HealthScreenView Screen;
        private HealthScreenPresenter presenter;
        private bool paused;
        private bool focused = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        private UmothGoogleSignInProvider authentication;
#endif

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            authentication = new UmothGoogleSignInProvider(
                Settings != null ? Settings.GoogleWebClientId : ""
            );
            presenter = new HealthScreenPresenter(authentication, new HealthConnectProvider());
            Screen.Bind(presenter);
#else
            var preview = new HealthScreenPreviewProvider();
            presenter = new HealthScreenPresenter(preview, preview);
            Screen.Bind(presenter);
            presenter.Changed += RenderPreview;
            Screen.SignInButton.GetComponentInChildren<TMPro.TMP_Text>(true).text =
                "Google接続を試す（サンプル）";
            Screen.SignOutButton.GetComponentInChildren<TMPro.TMP_Text>(true).text =
                "Google接続を解除（サンプル）";
            Screen.ConnectButton.GetComponentInChildren<TMPro.TMP_Text>(true).text =
                "サンプルデータを表示";
            RenderPreview();
            _ = StartPreviewAsync();
#endif
            ApplyForeground();
        }

#if UNITY_EDITOR || !UNITY_ANDROID
        private async System.Threading.Tasks.Task StartPreviewAsync()
        {
            await presenter.ConnectAsync();
        }

        // Subscribe after the view so its normal rendering cannot hide the preview label.
        private void RenderPreview()
        {
            if (Screen == null)
                return;
            Screen.Progress.text = "サンプルデータ / プレビュー";
            Screen.GoogleStatus.text = presenter.SignedIn
                ? "Google接続済みのサンプルです。実際の認証は行いません。"
                : "未接続のサンプルです。歩数の表示とは別に操作できます。";
            Screen.Footnote.text =
                "架空の歩数データです。Google認証・Health Connectには接続しません。";
            Screen.Status.text = presenter.Phase switch
            {
                HealthScreenPhase.ReadyToConnect =>
                    "サンプルデータを表示して、日別の歩数を確認できます。",
                HealthScreenPhase.Ready => "日付を選ぶとサンプルJSONを確認できます。",
                HealthScreenPhase.Failed =>
                    "プレビューを表示できませんでした。もう一度お試しください。",
                _ => "サンプルデータを準備しています…",
            };
            if (presenter.SelectedDay != null)
                Screen.DetailsTitle.text = presenter.SelectedDay.Day + " / サンプルJSON";
        }
#endif

        private void OnApplicationPause(bool value)
        {
            paused = value;
            ApplyForeground();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            ApplyForeground();
        }

        private void ApplyForeground() => presenter?.SetForeground(!paused && focused);

        private void OnDestroy()
        {
            presenter?.Dispose();
#if UNITY_ANDROID && !UNITY_EDITOR
            authentication?.Dispose();
#endif
        }
    }
}
