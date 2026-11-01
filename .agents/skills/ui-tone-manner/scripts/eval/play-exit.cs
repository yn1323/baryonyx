// Playを止め、runInBackground を戻す。止めたあと、ProjectSettings.asset に runInBackground: 1 が残っていないかを git で確かめる。

UnityEngine.Application.runInBackground = false;
UnityEditor.PlayerSettings.runInBackground = false;
UnityEditor.EditorApplication.ExitPlaymode();
return "exiting";
