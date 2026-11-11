// 開いているシーンの舞台の灯りを、保存せずに何倍かにして試す（stage_shots.sh の TRY に渡す）。
// 写しを作って倍率を書き換えて使う。点光源はシーンの部品の値を、光だまり・光芒・炎のまわりの光は
// MaterialPropertyBlock を変えるだけで、マテリアルとPrefabは書き換えない。シーンを開き直すと元に戻る。
// 決めた倍率は、舞台を組み立てるコード（StageSetAssets・BattleStageSets・Hd2dStageKit）の値へ写す。
float pointIntensity = 1f; // 点光源の強さ
float pointRange = 1f; // 点光源の届く範囲
float pool = 1f; // 光だまり（Baryonyx/HD2D/Light Pool）の強さ
float beam = 1f; // 光芒（Baryonyx/HD2D/Light Beam）の強さ
float flameHalo = 1f; // 炎のまわりの光（Baryonyx/HD2D/Flame の _Halo）

int lights = 0, pools = 0, beams = 0, flames = 0;
foreach (var light in UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.None))
{
    if (light.type != UnityEngine.LightType.Point)
        continue;
    light.intensity *= pointIntensity;
    light.range *= pointRange;
    lights++;
}
foreach (var renderer in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsSortMode.None))
{
    var material = renderer.sharedMaterial;
    if (material == null || material.shader == null)
        continue;
    var block = new UnityEngine.MaterialPropertyBlock();
    switch (material.shader.name)
    {
        case "Baryonyx/HD2D/Light Pool":
            block.SetFloat("_Intensity", material.GetFloat("_Intensity") * pool);
            pools++;
            break;
        case "Baryonyx/HD2D/Light Beam":
            block.SetFloat("_Intensity", material.GetFloat("_Intensity") * beam);
            beams++;
            break;
        case "Baryonyx/HD2D/Flame":
            block.SetFloat("_Halo", material.GetFloat("_Halo") * flameHalo);
            flames++;
            break;
        default:
            continue;
    }
    renderer.SetPropertyBlock(block);
}
return "lights " + lights + ", pools " + pools + ", beams " + beams + ", flames " + flames;
