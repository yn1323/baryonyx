// 撮影の前に、Editorのメモリの中だけでずれたKiwi Maruの字の表とアトラスを読み直す（ファイルには差分を出さない）。
// APKのビルドや全アセットの読み込み直しのあと、撮った画面の文字が四角や別の字に化けたときに使う。
// 影付きの材質も読み直し、TMPの材質の控えを消して、開いているシーンを開き直す。
var path = "Assets/Baryonyx/Shared/UI/Fonts/KiwiMaru.asset";
foreach (var part in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
  if (part is UnityEngine.Material || part is UnityEngine.Texture2D || part is TMPro.TMP_FontAsset)
    UnityEngine.Resources.UnloadAsset(part);
var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
font.ReadFontAssetDefinition();
foreach (var p in new[] { "Assets/Baryonyx/Shared/UI/GuideMenu/GuideTextShadow.mat", "Assets/Baryonyx/Shared/UI/Fonts/TextShadow.mat" })
{
  var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(p);
  if (m != null) UnityEngine.Resources.UnloadAsset(m);
  UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(p);
}
typeof(TMPro.TMP_MaterialManager).GetMethod("ClearMaterials", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
if (!string.IsNullOrEmpty(scene)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);
foreach (var text in UnityEngine.Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()) text.SetAllDirty();
var found = font.characterLookupTable.TryGetValue('覚', out var ch);
return font.glyphTable.Count + " glyphs; 覚=" + (found ? ch.glyph.glyphRect.x + "," + ch.glyph.glyphRect.y : "none") + " (ファイルの値と比べる)";
