// 開いている画面を1920x1080で撮り、work/shots/<画像の名前>.png に書く。手前のCanvasは、最前面の層へ移してカメラで描かせ、
// 撮ったらすぐ戻す（戦闘の舞台の板がUIの上に描かれないようにするため）。最後に停止中の表示を元へ戻す。

var dir = System.IO.Path.GetFullPath("../output/ui-audit/work/"); System.IO.Directory.CreateDirectory(dir + "shots");
var line = System.IO.File.ReadAllText(dir + "cur.txt").Split('\t');
var spec = line[0].Trim().Split('|');
UnityEngine.RenderTexture rt = null; var log = "";
var changed = new System.Collections.Generic.List<(UnityEngine.Canvas c, UnityEngine.RenderMode m, UnityEngine.Camera cam, float d, int layer, int order)>();
try {
  var cams = UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(UnityEngine.FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled && c.targetTexture == null).OrderBy(c => c.depth).ToArray();
  var top = cams.Length > 0 ? cams.Last() : null;
  var layers = UnityEngine.SortingLayer.layers; var lastLayer = layers[layers.Length - 1].id;
  if (top != null)
    foreach (var c in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Canvas>().Where(k => k.gameObject.scene.isLoaded && k.gameObject.activeInHierarchy))
      if (c.isRootCanvas && c.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay && c.name != "SceneTransition")
      { changed.Add((c, c.renderMode, c.worldCamera, c.planeDistance, c.sortingLayerID, c.sortingOrder)); c.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; c.worldCamera = top; c.planeDistance = top.nearClipPlane + 0.05f; c.sortingLayerID = lastLayer; c.sortingOrder += 30000; }
  rt = new UnityEngine.RenderTexture(1920, 1080, 24);
  UnityEngine.Canvas.ForceUpdateCanvases();
  foreach (var c in cams) { c.targetTexture = rt; c.Render(); c.targetTexture = null; }
  UnityEngine.RenderTexture.active = rt;
  var tex = new UnityEngine.Texture2D(1920, 1080, UnityEngine.TextureFormat.RGB24, false);
  tex.ReadPixels(new UnityEngine.Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
  UnityEngine.RenderTexture.active = null;
  System.IO.File.WriteAllBytes(dir + "shots/" + spec[4] + ".png", UnityEngine.ImageConversion.EncodeToPNG(tex));
  UnityEngine.Object.DestroyImmediate(tex);
  log = "ok " + cams.Length;
} finally {
  if (rt != null) rt.Release();
  foreach (var (c, m, cam, d, layer, order) in changed) if (c != null) { c.renderMode = m; c.worldCamera = cam; c.planeDistance = d; c.sortingLayerID = layer; c.sortingOrder = order; }
  if (spec[1] != "-")
    foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
      if (mb is Baryonyx.UI.EditModePreview.IEditModePreview && mb.GetType().FullName == spec[1]) Baryonyx.UI.EditModePreview.Editor.EditModePreviewer.Restore(mb);
}
return log;
