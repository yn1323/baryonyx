// 生成スクリプトが作ったPrefabとシーンを、比べられる形のテキストへ書き出す。
// Unity CLIで接続中のEditorに `unity command eval_file --file <このファイル>` で実行する。
// 書き出すのは階層、コンポーネント、全てのシリアライズ値。内部の参照は階層のパス、
// アセットの参照はGUIDとlocal idで表すため、生成し直しで振り直されるfileIDの違いを無視できる。
// 使う前に Output と Paths を書き換える。シーンは直下の構成だけを書き出す（RootsOnly）。
var Output = "/tmp/dump.txt";
var RootsOnly = true;
var Paths = new[]
{
    "Assets/Baryonyx/Features/Home/UI/HomeScreen.prefab",
    // "Assets/Baryonyx/App/Scenes/Home.unity",
};

var sb = new System.Text.StringBuilder();
foreach (var path in Paths)
{
    sb.AppendLine("### " + path);
    bool isScene = path.EndsWith(".unity");
    UnityEngine.GameObject prefabRoot = null;
    var scene = default(UnityEngine.SceneManagement.Scene);
    UnityEngine.GameObject[] roots;
    if (isScene)
    {
        scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            path,
            UnityEditor.SceneManagement.OpenSceneMode.Additive
        );
        roots = scene.GetRootGameObjects();
    }
    else
    {
        prefabRoot = UnityEditor.PrefabUtility.LoadPrefabContents(path);
        roots = new[] { prefabRoot };
    }
    try
    {
        var names = new System.Collections.Generic.Dictionary<UnityEngine.Object, string>();
        void Name(UnityEngine.Transform t, string prefix)
        {
            string p = prefix + "/" + t.name + "[" + t.GetSiblingIndex() + "]";
            names[t.gameObject] = p;
            foreach (var c in t.GetComponents<UnityEngine.Component>())
                if (c != null)
                    names[c] = p + ":" + c.GetType().Name;
            for (int i = 0; i < t.childCount; i++)
                Name(t.GetChild(i), p);
        }
        foreach (var root in roots)
            Name(root.transform, "");

        string Ref(UnityEngine.Object o)
        {
            if (o == null)
                return "null";
            if (names.TryGetValue(o, out var n))
                return "#" + n;
            if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id))
                return "asset:" + guid + ":" + id;
            return "other:" + o.GetType().Name + ":" + o.name;
        }

        void Dump(UnityEngine.Transform t, bool deep)
        {
            var go = t.gameObject;
            sb.AppendLine("GO " + names[go] + " active=" + go.activeSelf + " layer=" + go.layer + " tag=" + go.tag);
            foreach (var c in t.GetComponents<UnityEngine.Component>())
            {
                if (c == null)
                {
                    sb.AppendLine("  missing component");
                    continue;
                }
                // 名前空間やクラス名を変えても比べられるよう、クラス名だけを書く。
                sb.AppendLine("  C " + c.GetType().Name);
                var it = new UnityEditor.SerializedObject(c).GetIterator();
                bool enter = true;
                while (it.Next(enter))
                {
                    enter = it.propertyType != UnityEditor.SerializedPropertyType.ObjectReference;
                    string p = it.propertyPath;
                    if (p == "m_ObjectHideFlags" || p == "m_CorrespondingSourceObject" || p == "m_PrefabInstance"
                        || p == "m_PrefabAsset" || p == "m_EditorClassIdentifier" || p == "m_GameObject")
                    {
                        enter = false;
                        continue;
                    }
                    string v;
                    switch (it.propertyType)
                    {
                        case UnityEditor.SerializedPropertyType.Integer: v = it.longValue.ToString(); break;
                        case UnityEditor.SerializedPropertyType.Boolean: v = it.boolValue.ToString(); break;
                        case UnityEditor.SerializedPropertyType.Float: v = it.doubleValue.ToString("R"); break;
                        case UnityEditor.SerializedPropertyType.String: v = it.stringValue; break;
                        case UnityEditor.SerializedPropertyType.Color: v = it.colorValue.ToString("F5"); break;
                        case UnityEditor.SerializedPropertyType.ObjectReference: v = Ref(it.objectReferenceValue); break;
                        case UnityEditor.SerializedPropertyType.Enum:
                        case UnityEditor.SerializedPropertyType.ArraySize:
                        case UnityEditor.SerializedPropertyType.Character:
                        case UnityEditor.SerializedPropertyType.LayerMask: v = it.intValue.ToString(); break;
                        case UnityEditor.SerializedPropertyType.Vector2: v = it.vector2Value.ToString("F5"); break;
                        case UnityEditor.SerializedPropertyType.Vector3: v = it.vector3Value.ToString("F5"); break;
                        case UnityEditor.SerializedPropertyType.Vector4: v = it.vector4Value.ToString("F5"); break;
                        case UnityEditor.SerializedPropertyType.Quaternion: v = it.quaternionValue.ToString("F5"); break;
                        case UnityEditor.SerializedPropertyType.Rect: v = it.rectValue.ToString("F5"); break;
                        default: v = "(" + it.propertyType + ")"; break;
                    }
                    sb.AppendLine("    " + p + " = " + v);
                }
            }
            if (!deep)
                return;
            for (int i = 0; i < t.childCount; i++)
                Dump(t.GetChild(i), true);
        }
        foreach (var root in roots)
            Dump(root.transform, !(isScene && RootsOnly));
    }
    finally
    {
        if (isScene)
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        else
            UnityEditor.PrefabUtility.UnloadPrefabContents(prefabRoot);
    }
}
System.IO.File.WriteAllText(Output, sb.ToString());
return Output + " " + sb.Length;
