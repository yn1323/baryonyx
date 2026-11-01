// 停止中の表示の切り替え（IEditModePreview）の状態を、ラボ以外のシーンごとに一覧にし、work/inventory.txt に書く。
// 1行は「シーン|部品の型|階層のパス|状態の番号|状態の名前」。シーンそのものは「-|-|-1|(scene)」。

var dir = System.IO.Path.GetFullPath("../output/ui-audit/work/"); System.IO.Directory.CreateDirectory(dir);
var scenes = new[] { "Top", "Home", "Guide/Pub", "Guide/Shop", "Guide/Temple", "Guide/TravelOffice", "Guide/Library", "Battle", "Exploration", "WorldMap", "WorldAtlas", "World" };
var before = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
Baryonyx.Editor.EditorScenes.SaveNamed();
var lines = new System.Collections.Generic.List<string>();
foreach (var name in scenes)
{
  var path = "Assets/Baryonyx/App/Scenes/" + name + ".unity";
  var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
  lines.Add(path + "|-|-|-1|(scene)");
  foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
  {
    if (mb is not Baryonyx.UI.EditModePreview.IEditModePreview p) continue;
    var t = mb.transform; var hp = t.name; while (t.parent != null) { t = t.parent; hp = t.name + "/" + hp; }
    System.Collections.Generic.IReadOnlyList<string> states;
    try { states = p.PreviewStates; } catch (System.Exception e) { lines.Add(path + "|" + mb.GetType().FullName + "|" + hp + "|ERR|" + e.Message); continue; }
    for (int i = 0; i < states.Count; i++) lines.Add(path + "|" + mb.GetType().FullName + "|" + hp + "|" + i + "|" + states[i]);
  }
}
System.IO.File.WriteAllLines(dir + "inventory.txt", lines);
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(before);
return lines.Count.ToString();
