using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Baryonyx.UI
{
    // シーンを遷移演出で覆ってから読み込む。演出がなければすぐに読み込む。
    // 読み込めなかったときは覆いを開き、onFailed を呼んで、押した画面の操作を戻せるようにする。
    public static class SceneLoader
    {
        public static bool Load(
            string sceneName,
            SceneTransitionController transition,
            Object context,
            Action onFailed = null
        )
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("No destination scene is set.", context);
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"The scene '{sceneName}' is not enabled in Build Settings.",
                    context
                );
                return false;
            }
            if (transition == null)
                return LoadNow(sceneName, null, context, onFailed);
            return transition.PlayOut(() => LoadNow(sceneName, transition, context, onFailed));
        }

        private static bool LoadNow(
            string sceneName,
            SceneTransitionController transition,
            Object context,
            Action onFailed
        )
        {
            AsyncOperation operation = null;
            Exception error = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            if (operation != null)
                return true;

            if (transition != null)
                transition.PlayIn();
            var message = $"The scene '{sceneName}' could not be loaded.";
            Debug.LogError(error == null ? message : message + " " + error.Message, context);
            onFailed?.Invoke();
            return false;
        }
    }
}
