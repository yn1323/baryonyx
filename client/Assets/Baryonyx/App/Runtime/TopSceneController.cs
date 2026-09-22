using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.App
{
    [RequireComponent(typeof(Button))]
    public sealed class TopSceneController : MonoBehaviour
    {
        [SerializeField] private string nextSceneName = "Home";

        private Button continueButton;
        private bool transitionStarted;

        public string NextSceneName => nextSceneName;
        public Button ContinueButton => continueButton;

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
            if (transitionStarted)
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
            SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
        }
    }
}
