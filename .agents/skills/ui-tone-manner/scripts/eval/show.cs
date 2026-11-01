// work/cur.txt の1行（make_specs.py が作る specs.txt の行）のシーンを開き、その部品の状態を停止中の表示で出す。
// 表示した複製は次のフレームで描かれるため、撮影（shot.cs）は別の評価で呼ぶ。

var dir = System.IO.Path.GetFullPath("../output/ui-audit/work/");
var spec = System.IO.File.ReadAllText(dir + "cur.txt").Split('\t')[0].Trim().Split('|');
// シーンを替えるときは保存しない。停止中の表示は複製にPrefabの上書きを残すことがあり、保存するとシーンに古い上書きが入る。
// 名前のない新しいシーンで保存を呼ぶと「名前を付けて保存」の窓が開き、Editorが止まる。作業中のシーンは prepare.cs が先に保存する。
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != spec[0])
  UnityEditor.SceneManagement.EditorSceneManager.OpenScene(spec[0], UnityEditor.SceneManagement.OpenSceneMode.Single);
if (spec[1] == "-") return "scene";
foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
  if (mb.GetType().FullName != spec[1]) continue;
  var t = mb.transform; var hp = t.name; while (t.parent != null) { t = t.parent; hp = t.name + "/" + hp; }
  if (hp != spec[2]) continue;
  Baryonyx.UI.EditModePreview.Editor.EditModePreviewer.Show(mb, int.Parse(spec[3]));
  return "shown";
}
return "notfound";
