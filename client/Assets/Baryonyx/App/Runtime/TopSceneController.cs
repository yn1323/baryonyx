using System;
using System.Collections;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.App
{
    [RequireComponent(typeof(Button))]
    public sealed class TopSceneController : MonoBehaviour
    {
        [SerializeField]
        private string nextSceneName = "Main";

        [SerializeField]
        private SceneTransitionController transition;

        [SerializeField]
        private GameObject tapToStartPrompt;

        private Button continueButton;
        private bool transitionStarted;
        private bool inputReady;

        public string NextSceneName => nextSceneName;
        public Button ContinueButton => continueButton;
        public SceneTransitionController Transition => transition;
        public GameObject TapToStartPrompt => tapToStartPrompt;
        public bool IsInputReady => inputReady;

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

            // 起動直後のタップ（スプラッシュ中の押下を含む）を開始操作として扱わない。
            // 開く演出が終わり、開始の案内を出してから受け付ける。
            SetInputReady(false);
        }

        private IEnumerator Start()
        {
            // 遷移演出のStartで覆った状態と開く演出が始まるまで1フレーム待つ。
            yield return null;
            while (transition != null && (transition.IsPlaying || transition.IsCovered))
                yield return null;
            SetInputReady(true);
        }

        private void SetInputReady(bool ready)
        {
            inputReady = ready;
            if (continueButton != null)
                continueButton.interactable = ready;
            if (tapToStartPrompt != null)
                tapToStartPrompt.SetActive(ready);
        }

        private void OnEnable()
        {
            if (continueButton == null)
                continueButton = GetComponent<Button>();
            if (continueButton != null)
                continueButton.onClick.AddListener(LoadNextScene);
        }

        private void OnDisable()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(LoadNextScene);
        }

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
    }
}
