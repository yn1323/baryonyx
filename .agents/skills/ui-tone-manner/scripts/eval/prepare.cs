// 撮影の前に1度だけ呼ぶ。開いているシーンのうち、名前があって変更のあるものだけを保存する（作業中の変更を失わないため）。
// 名前のない新しいシーンは保存しない（「名前を付けて保存」の窓が開いてEditorが止まるため）。
var saved = new System.Collections.Generic.List<string>();
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
  var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
  if (scene.isDirty && !string.IsNullOrEmpty(scene.path) && UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
    saved.Add(scene.path);
}
return "saved: " + string.Join(", ", saved);
