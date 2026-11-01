// 撮影の最後に呼ぶ。停止中の表示で変わった今のシーンを、保存せずにファイルから開き直して捨てる。
// 次の撮影の prepare.cs が、停止中の表示の上書きを作業中の変更と取り違えて保存しないようにする。
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (string.IsNullOrEmpty(scene.path)) return "untitled";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
return "reopened " + scene.path;
