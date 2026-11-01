// 世界地図のシーンを開いてPlayを始める。撮り終えたら play-exit.cs で必ず止める（runInBackground も戻す）。

Baryonyx.Editor.EditorScenes.SaveNamed();
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Baryonyx/App/Scenes/World.unity");
UnityEngine.Application.runInBackground = true;
UnityEditor.EditorApplication.EnterPlaymode();
return "entering";
