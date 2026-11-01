// Play中の画面を撮り、work/shots/<play-name.txt の名前>.png に書く。

var dir = System.IO.Path.GetFullPath("../output/ui-audit/work/"); System.IO.Directory.CreateDirectory(dir + "shots");
var name = System.IO.File.ReadAllText(dir + "play-name.txt").Trim();
UnityEngine.RenderTexture rt = null;
var changed = new System.Collections.Generic.List<(UnityEngine.Canvas c, UnityEngine.RenderMode m, UnityEngine.Camera cam, float d, int layer, int order)>();
try {
  var cams = UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(UnityEngine.FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled && c.targetTexture == null).OrderBy(c => c.depth).ToArray();
  var top = cams.Last();
  var layers = UnityEngine.SortingLayer.layers; var lastLayer = layers[layers.Length - 1].id;
  foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None))
    if (c.isRootCanvas && c.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay && c.name != "SceneTransition")
    { changed.Add((c, c.renderMode, c.worldCamera, c.planeDistance, c.sortingLayerID, c.sortingOrder)); c.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; c.worldCamera = top; c.planeDistance = top.nearClipPlane + 0.05f; c.sortingLayerID = lastLayer; c.sortingOrder += 30000; }
  rt = new UnityEngine.RenderTexture(1920, 1080, 24);
  UnityEngine.Canvas.ForceUpdateCanvases();
  foreach (var c in cams) { c.targetTexture = rt; c.Render(); c.targetTexture = null; }
  UnityEngine.RenderTexture.active = rt;
  var tex = new UnityEngine.Texture2D(1920, 1080, UnityEngine.TextureFormat.RGB24, false);
  tex.ReadPixels(new UnityEngine.Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
  UnityEngine.RenderTexture.active = null;
  System.IO.File.WriteAllBytes(dir + "shots/" + name + ".png", UnityEngine.ImageConversion.EncodeToPNG(tex));
  UnityEngine.Object.Destroy(tex);
} finally {
  if (rt != null) rt.Release();
  foreach (var (c, m, cam, d, layer, order) in changed) if (c != null) { c.renderMode = m; c.worldCamera = cam; c.planeDistance = d; c.sortingLayerID = layer; c.sortingOrder = order; }
}
return "shot " + name;
