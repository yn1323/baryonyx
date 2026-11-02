using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.WorldMap.Editor
{
    /// <summary>
    /// Keeps the map editor's changes (<see cref="WorldEventLab.Save"/>) in the nested world
    /// map's layouts: place_events.py puts the event on the square under the layout's rules, then
    /// price_squares.py prices the map again and the server's catalog of squares is written again
    /// from the layouts (doc/features/world-atlas/square-events.md#マスの付け替え). A square's settings
    /// (<see cref="WorldEventLab.SaveSetting"/>) are written by place_events.py alone, since they
    /// change no price. The maps read their events and settings from the layouts, so nothing
    /// needs rebuilding.
    /// </summary>
    [InitializeOnLoad]
    public static class WorldEventLabSave
    {
        private const string Scripts = "client/ArtSource/WorldMap/Atlas/scripts";
        private const string Layouts = "Assets/Baryonyx/Features/WorldMap/Atlas/Layouts";
        private const int TimeoutMilliseconds = 30000;

        static WorldEventLabSave()
        {
            WorldEventLab.Save = Save;
            WorldEventLab.SaveSetting = SaveSetting;
        }

        private static WorldEventLab.Saved SaveSetting(
            string map,
            string square,
            string field,
            string value
        )
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string python = windows ? "python" : "python3";
            // 言葉の設定は空白や記号を含むため、引用符で囲む（中の引用符は除く）。
            string quoted = "\"" + (value ?? "").Replace("\"", "").Replace("'", "") + "\"";
            var (code, error) = Run(
                windows,
                $"cd \"{root}/{Scripts}\" && {python} place_events.py setting {map} {square} {field} {quoted}"
            );
            if (code != 0)
                return new WorldEventLab.Saved(
                    string.IsNullOrEmpty(error) ? "保存できませんでした" : error,
                    0
                );
            AssetDatabase.ImportAsset(
                $"{Layouts}/{map}.json",
                ImportAssetOptions.ForceSynchronousImport
            );
            return new WorldEventLab.Saved(null, 0);
        }

        private static WorldEventLab.Saved Save(string map, string square, string name, string form)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string python = windows ? "python" : "python3";
            string set = $"{python} place_events.py set {map} {square} {name} {form}".TrimEnd();
            string command =
                $"cd \"{root}/{Scripts}\" && {set} && {python} price_squares.py {map}"
                + $" && cd \"{root}/server\" && node scripts/world-atlas-catalog.mjs";
            var (code, error) = Run(windows, command);
            if (code != 0)
                return new WorldEventLab.Saved(
                    string.IsNullOrEmpty(error) ? "保存できませんでした" : error,
                    0
                );

            string layout = $"{Layouts}/{map}.json";
            AssetDatabase.ImportAsset(layout, ImportAssetOptions.ForceSynchronousImport);
            foreach (var node in WorldAtlasLayout.Parse(File.ReadAllText(layout)).nodes)
                if (node.id == square)
                    return new WorldEventLab.Saved(null, node.price);
            return new WorldEventLab.Saved(null, 0);
        }

        // macOS はログインシェルで、ユーザーの PATH（python3・node）のまま動かす。
        private static (int Code, string Error) Run(bool windows, string command)
        {
            var start = new ProcessStartInfo
            {
                FileName = windows ? "cmd.exe" : "/bin/zsh",
                Arguments = windows ? $"/c \"{command}\"" : $"-lc '{command}'",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            using var process = Process.Start(start);
            string error = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(TimeoutMilliseconds))
            {
                process.Kill();
                return (1, "保存が終わりませんでした");
            }
            return (process.ExitCode, error.Trim());
        }
    }
}
