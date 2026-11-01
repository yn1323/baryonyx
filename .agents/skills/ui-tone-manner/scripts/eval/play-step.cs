// Play中の世界地図を操作する。work/play-step.txt に「open|地図のキー」「event|種類|形」「hide」「toll」のどれかを書いて呼ぶ。

var dir = System.IO.Path.GetFullPath("../output/ui-audit/work/");
var step = System.IO.File.ReadAllText(dir + "play-step.txt").Trim().Split('|');
UnityEngine.Application.runInBackground = true;
var nav = UnityEngine.Object.FindFirstObjectByType<Baryonyx.WorldMap.WorldAtlasNavigator>();
if (nav == null) return "no navigator; playing=" + UnityEngine.Application.isPlaying;
var view = nav.Current;
switch (step[0])
{
  case "open":
    nav.Open(step[1], null);
    return "opened " + nav.CurrentKey;
  case "event":
    var w = view.EventWindow;
    if (w.IsShown) w.Hide();
    var node = view.Nodes.Length > 2 ? view.Nodes[2] : view.Nodes[0];
    w.Open(node, (Baryonyx.WorldMap.WorldEventKind)System.Enum.Parse(typeof(Baryonyx.WorldMap.WorldEventKind), step[1]), () => { }, null, int.Parse(step[2]));
    w.FinishOpening();
    return "event " + step[1];
  case "hide":
    if (view.EventWindow.IsShown) view.EventWindow.Hide();
    return "hidden";
  case "toll":
    var toll = UnityEngine.Object.FindFirstObjectByType<Baryonyx.WorldMap.WorldAtlasToll>();
    if (toll == null) return "no toll";
    toll.Ask(new[] { 200, 300 }, 500);
    return "toll";
}
return "unknown";
