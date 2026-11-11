// 舞台を組み立てるコードの値を変えたあと、3Dの舞台のPrefab・マテリアル・Profileを作り直し、
// シーンが持つ舞台の空気（環境光・霧）をコードの値に合わせて保存する。
// 5秒で応答だけが時間切れになるが、処理は続く。client/Logs/Editor.log の「[stages] done」まで待つ
// （「[stages] failed」なら例外の内容が続く）。Editorの札を取り、再コンパイルを終えてから eval_file で呼ぶ。
//
// シーンの作り直し（HomeSceneSetup・BattleSceneSetup など）は呼ばない。画面のPrefabまで作り直すと、
// 関係のない差分が出るためである。空気の値は次の所にあり、ここで書き換える。
// - Top.unity と、夕暮れの高原を使うデバッグルーム：RenderSettings（環境光・霧）
// - Battle.unity：BattleStageSelector の選択肢（背景ごとの環境光・霧）
// シーンに直接置いた部品の値（Homeの焚き火の点光源 CampfireLight など）は、ここでは変えない。
if (UnityEditor.EditorApplication.isPlaying) return "playing";
Baryonyx.Editor.EditorScenes.SaveNamed();
if (Baryonyx.Editor.EditorScenes.HasUntitledChanges()) return "untitled changes";
const string scenes = "Assets/Baryonyx/App/Scenes/";
// 作り直す舞台を置いたシーンを開いたままにしない。
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes + "Showcase.unity");
UnityEngine.Debug.Log("[stages] start");
try
{
    Baryonyx.Stages.Editor.StageSetAssets.CreateAssets();
    UnityEngine.Debug.Log("[stages] ok stages");
    Baryonyx.Stages.Editor.BattleStageSets.CreateAssets();
    UnityEngine.Debug.Log("[stages] ok battle stages");

    var top = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes + "Top.unity");
    var starlit = Baryonyx.Stages.Editor.StageSetAssets.StarlitEnvironment;
    Baryonyx.Stages.Editor.StageSetAssets.ApplyEnvironment(starlit);
    foreach (var stageCamera in UnityEngine.Object.FindObjectsByType<Baryonyx.Vfx.Hd2d.Hd2dStageCamera>(UnityEngine.FindObjectsSortMode.None))
    {
        stageCamera.Camera.backgroundColor = starlit.Fog;
        UnityEditor.EditorUtility.SetDirty(stageCamera.Camera);
    }
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(top);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(top);
    UnityEngine.Debug.Log("[stages] ok air Top");

    // 夕暮れの高原の空気を持つシーンだけを書き換える（別の舞台を置いたシーンは飛ばす）。
    var dusk = Baryonyx.Stages.Editor.StageSetAssets.DuskEnvironment;
    foreach (var path in System.IO.Directory.GetFiles(scenes + "Debug", "*.unity"))
    {
        var lab = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
        bool onDusk = false;
        foreach (var stageCamera in UnityEngine.Object.FindObjectsByType<Baryonyx.Vfx.Hd2d.Hd2dStageCamera>(UnityEngine.FindObjectsSortMode.None))
            onDusk |= stageCamera.Camera.backgroundColor == dusk.Fog;
        if (!onDusk)
            continue;
        Baryonyx.Stages.Editor.StageSetAssets.ApplyEnvironment(dusk);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lab);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(lab);
        UnityEngine.Debug.Log("[stages] ok air " + lab.name);
    }

    var battle = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes + "Battle.unity");
    var selector = UnityEngine.Object.FindFirstObjectByType<Baryonyx.Combat.Presentation.BattleStageSelector>();
    foreach (var option in selector.Options)
    {
        var air = Baryonyx.App.Editor.BattleSceneSetup.Source(option.Stage).air;
        option.AmbientSky = air.Ambient;
        option.AmbientEquator = air.Equator ?? air.Ambient;
        option.AmbientGround = air.Ground ?? air.Ambient;
        option.Fog = air.Fog;
        option.FogStart = air.FogStart;
        option.FogEnd = air.FogEnd;
    }
    UnityEditor.EditorUtility.SetDirty(selector);
    selector.Apply();
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(battle);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(battle);
    UnityEngine.Debug.Log("[stages] ok air Battle " + selector.Options.Length);

    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes + "Showcase.unity");
    UnityEngine.Debug.Log("[stages] done");
}
catch (System.Exception e)
{
    UnityEngine.Debug.Log("[stages] failed " + e);
}
return "sent";
