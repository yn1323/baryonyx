// Unity CLIの eval_file で、Play中に1回実行する録画の台本。
// 作業用の場所へ写し、「ここを書き換える」の3か所だけを録る場面に合わせて直してから使う。
// 書き出しの値の正本は doc/rules/client-testing.md の「動画の録画」。正本を変えたら同じ変更でここも直す。
//
// 流れ：隠すCanvasを消す → 録画を始める → leadSeconds 後に trigger を呼ぶ → さらに recordSeconds 後に止める
// → 消したCanvasを戻し、Temp/Recordings/<outputName>.done を書く（終わりの印）。
// 進行は EditorApplication.update でゲームの時間を見て行う。シーンの物に頼らないので、隠した物や
// 場面の切り替えで止まらない。

// ===== ここを書き換える 1：名前と長さ =====
var outputName = "recording";
float leadSeconds = 0.5f;
float recordSeconds = 3.0f;

// ===== ここを書き換える 2：録画に映さないCanvas（デバッグルームの操作盤など）のGameObjectの名前 =====
// GameObject ごと無効にすると、そこに付いた部品の処理まで止まるため、Canvas だけを消す。
var hiddenCanvases = new string[] { };

// ===== ここを書き換える 3：見せたい動きを始める処理 =====
System.Action trigger = () =>
{
    // 例：探索のマスへ進む
    // UnityEngine.Object.FindFirstObjectByType<Baryonyx.Adventure.ExplorationMomentLab>()
    //     .Run(Baryonyx.Adventure.ExplorationMoment.Walk);
};

// ===== ここから下は書き換えない =====
int outputWidth = 960;
int outputHeight = 540;
float frameRate = 30f;

if (!UnityEditor.EditorApplication.isPlaying)
    return "not playing";
System.IO.Directory.CreateDirectory("Temp/Recordings");
var donePath = "Temp/Recordings/" + outputName + ".done";
System.IO.File.Delete(donePath);
UnityEngine.Application.runInBackground = true;

var hidden = new System.Collections.Generic.List<UnityEngine.Canvas>();
foreach (var name in hiddenCanvases)
{
    var found = UnityEngine.GameObject.Find(name);
    var canvas = found != null ? found.GetComponent<UnityEngine.Canvas>() : null;
    if (canvas == null)
        return "canvas not found: " + name;
    canvas.enabled = false;
    hidden.Add(canvas);
}

var settings = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.Recorder.RecorderControllerSettings>();
var movie = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.Recorder.MovieRecorderSettings>();
movie.name = outputName;
movie.Enabled = true;
movie.EncoderSettings = new UnityEditor.Recorder.Encoder.CoreEncoderSettings
{
    Codec = UnityEditor.Recorder.Encoder.CoreEncoderSettings.OutputCodec.MP4,
    EncodingQuality = UnityEditor.Recorder.Encoder.CoreEncoderSettings.VideoEncodingQuality.High,
};
movie.CaptureAudio = false;
movie.ImageInputSettings = new UnityEditor.Recorder.Input.GameViewInputSettings
{
    OutputWidth = outputWidth,
    OutputHeight = outputHeight,
};
movie.OutputFile = System.IO.Path.GetFullPath("Temp/Recordings/" + outputName);
settings.AddRecorderSettings(movie);
settings.SetRecordModeToManual();
settings.FrameRate = frameRate;
settings.CapFrameRate = true;

var controller = new UnityEditor.Recorder.RecorderController(settings);
controller.PrepareRecording();
if (!controller.StartRecording())
    return "recorder did not start";

float startedAt = UnityEngine.Time.time;
bool triggered = false;
UnityEditor.EditorApplication.CallbackFunction step = null;
step = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying)
    {
        // 途中でPlayが止まった。Recorderはここで書き終える。
        UnityEditor.EditorApplication.update -= step;
        controller.StopRecording();
        System.IO.File.WriteAllText(donePath, "stopped early");
        return;
    }
    float elapsed = UnityEngine.Time.time - startedAt;
    if (!triggered && elapsed >= leadSeconds)
    {
        triggered = true;
        trigger();
    }
    if (elapsed >= leadSeconds + recordSeconds)
    {
        UnityEditor.EditorApplication.update -= step;
        controller.StopRecording();
        foreach (var canvas in hidden)
            if (canvas != null)
                canvas.enabled = true;
        UnityEngine.Application.runInBackground = false;
        System.IO.File.WriteAllText(donePath, UnityEngine.Time.frameCount.ToString());
    }
};
UnityEditor.EditorApplication.update += step;
return "recording " + outputName + " " + outputWidth + "x" + outputHeight;
