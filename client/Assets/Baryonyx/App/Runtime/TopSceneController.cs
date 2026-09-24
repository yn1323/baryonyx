using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.App
{
    [RequireComponent(typeof(Button))]
    public sealed class TopSceneController : MonoBehaviour
    {
        public const string LoadingText = "LOADING...";
        public const string StartText = "TAP TO START";
        public const string RetryText = "TAP TO RETRY";

        [SerializeField]
        private string nextSceneName = "Main";

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private GameObject tapToStartPrompt;

        [SerializeField]
        private HealthConnectionSettings settings;

        [SerializeField]
        private HealthLinkModalView linkModal;

        [SerializeField]
        private Button settingsButton;

        private readonly CancellationTokenSource lifetime = new();
        private Button continueButton;
        private HealthStartupFlow flow;
        private bool transitionStarted;
        private bool inputReady;
        private bool confirming;
        private bool startRequested;

        public string NextSceneName => nextSceneName;
        public Button ContinueButton => continueButton;
        public SceneTransitionController Transition => transition;
        public GameObject TapToStartPrompt => tapToStartPrompt;
        public HealthLinkModalView LinkModal => linkModal;
        public Button SettingsButton => settingsButton;
        public HealthStartupFlow Flow => flow;
        public bool IsInputReady => inputReady;

        public string PromptText
        {
            get
            {
                var panel =
                    tapToStartPrompt != null
                        ? tapToStartPrompt.GetComponent<TranslucentTextPanel>()
                        : null;
                return panel != null && panel.Label != null ? panel.Label.text : "";
            }
        }

        private void Awake()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Application.targetFrameRate = 60;

            continueButton = GetComponent<Button>();
            if (continueButton == null)
                Debug.LogError("TopSceneController requires a Button component.", this);

            flow = new HealthStartupFlow(GameServices.GetOrCreate(settings).Health);
            flow.Changed += Render;
            if (linkModal != null)
            {
                linkModal.ActionPressed += OnLinkPressed;
                linkModal.LaterPressed += OnLaterPressed;
                linkModal.Hide();
            }

            // 起動直後のタップ（スプラッシュ中の押下を含む）を開始操作として扱わない。
            // 開く演出が終わり、開始の案内を出してから受け付ける。
            SetInputReady(false);
        }

        private IEnumerator Start()
        {
            // 開く演出と並行してサーバー接続と連携の確認を始める。
            Forget(flow.StartAsync(lifetime.Token));
            // 遷移演出のStartで覆った状態と開く演出が始まるまで1フレーム待つ。
            yield return null;
            while (transition != null && (transition.IsPlaying || transition.IsCovered))
                yield return null;
            SetInputReady(true);
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
            if (flow != null)
                flow.Changed -= Render;
            if (linkModal != null)
            {
                linkModal.ActionPressed -= OnLinkPressed;
                linkModal.LaterPressed -= OnLaterPressed;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // アプリから開いたHealth Connectの設定から戻ったときに、連携を確認し直す。
            if (hasFocus && flow != null && !lifetime.IsCancellationRequested)
                Forget(flow.ResumeAsync(lifetime.Token));
        }

        private void SetInputReady(bool ready)
        {
            inputReady = ready;
            Render();
        }

        private void Render()
        {
            if (flow == null)
                return;
            var phase = flow.Phase;
            if (continueButton != null)
                continueButton.interactable = inputReady && !transitionStarted;
            if (tapToStartPrompt != null)
            {
                tapToStartPrompt.SetActive(inputReady && phase != HealthStartupPhase.LinkRequired);
                var panel = tapToStartPrompt.GetComponent<TranslucentTextPanel>();
                if (panel != null)
                    panel.SetText(
                        phase switch
                        {
                            HealthStartupPhase.Ready => StartText,
                            HealthStartupPhase.Failed => $"{flow.FailureMessage}  {RetryText}",
                            _ => LoadingText,
                        }
                    );
            }
            if (linkModal != null)
            {
                if (phase == HealthStartupPhase.LinkRequired)
                    linkModal.Show(flow.LinkStatus);
                else
                    linkModal.Hide();
            }
            if (phase == HealthStartupPhase.Ready && startRequested)
            {
                startRequested = false;
                LoadNextScene();
            }
        }

        private void OnEnable()
        {
            if (continueButton == null)
                continueButton = GetComponent<Button>();
            if (continueButton != null)
                continueButton.onClick.AddListener(OnScreenPressed);
        }

        private void OnDisable()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(OnScreenPressed);
        }

        private void OnScreenPressed()
        {
            if (!inputReady || transitionStarted || confirming)
                return;
            switch (flow.Phase)
            {
                case HealthStartupPhase.Failed:
                    Forget(flow.RetryAsync(lifetime.Token));
                    break;
                case HealthStartupPhase.Ready:
                    Forget(ConfirmAndStartAsync());
                    break;
            }
        }

        private async Task ConfirmAndStartAsync()
        {
            confirming = true;
            bool allowed;
            try
            {
                allowed = await flow.ConfirmStartAsync(lifetime.Token);
            }
            finally
            {
                confirming = false;
            }
            if (allowed)
                LoadNextScene();
            else if (flow.Phase == HealthStartupPhase.LinkRequired)
                // 連携するか「あとで」を選んだら、そのまま開始する。
                startRequested = true;
        }

        private void OnLinkPressed() => Forget(flow.LinkAsync(lifetime.Token));

        private void OnLaterPressed() => flow.Dismiss();

        private void LoadNextScene()
        {
            if (!inputReady || transitionStarted)
                return;
            if (string.IsNullOrWhiteSpace(nextSceneName))
            {
                Debug.LogError("TopSceneController has no destination scene.", this);
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                Debug.LogError(
                    $"The destination scene '{nextSceneName}' is not enabled in Build Settings.",
                    this
                );
                return;
            }

            transitionStarted = true;
            continueButton.interactable = false;
            if (transition != null)
            {
                if (!transition.PlayOut(LoadNextSceneAfterCovered))
                {
                    transitionStarted = false;
                    continueButton.interactable = true;
                }
                return;
            }

            LoadNextSceneAfterCovered();
        }

        private void LoadNextSceneAfterCovered()
        {
            AsyncOperation operation = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                HandleSceneLoadFailure(exception);
                return;
            }

            if (operation != null)
                return;

            HandleSceneLoadFailure(null);
        }

        private void HandleSceneLoadFailure(Exception exception)
        {
            transitionStarted = false;
            if (continueButton != null)
                continueButton.interactable = true;
            if (transition != null)
                transition.PlayIn();

            var message = $"The destination scene '{nextSceneName}' could not be loaded.";
            if (exception == null)
                Debug.LogError(message, this);
            else
                Debug.LogError(message + " " + exception.Message, this);
        }

        private async void Forget(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
