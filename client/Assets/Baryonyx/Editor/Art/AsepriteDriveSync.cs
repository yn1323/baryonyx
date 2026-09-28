using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Editor.Art
{
    // タブレットで編集するため、Assets/Baryonyx/ の .aseprite をGoogle Driveのフォルダーと手動でコピーし合う。
    // Driveには、Assets/Baryonyx/ からの相対パスで置く。
    // 前回受け渡したときの各ファイルのSHA-256を記録し、コピー先が前回から変わっていないときだけ上書きする。
    // 両方で変わっていれば確認し、上書きする場合はコピー先の今のファイルを退避する。
    // ユーザーが手動で実行する操作であり、確認のダイアログを出すため自動処理から呼ばない。
    public static class AsepriteDriveSync
    {
        private const string Title = "Asepriteの受け渡し";
        private const string ToDriveMenu = "Baryonyx/Art/Copy Aseprite Sources to Drive";
        private const string FromDriveMenu = "Baryonyx/Art/Copy Aseprite Sources from Drive";
        private const string ChooseMenu = "Baryonyx/Art/Choose Aseprite Drive Folder...";

        public sealed class Plan
        {
            public readonly List<string> Added = new();
            public readonly List<string> Updated = new();
            public readonly List<string> Unchanged = new();
            public readonly List<string> Conflicts = new();
            public readonly List<string> DestinationOnly = new();
            public readonly Dictionary<string, string> SourceHashes = new();
        }

        // Driveの場所はPCごとに異なるため、開発者ごとの設定（Gitの対象外）に保存する。
        public const string SettingsPath = "UserSettings/BaryonyxAsepriteDrive.json";

        [Serializable]
        private sealed class Settings
        {
            public string folder;
        }

        public static string BaseFile(string driveDirectory) => driveDirectory + "-sync.tsv";

        // 設定がなければnullを返す。
        public static string ReadDriveDirectory(string settingsPath)
        {
            try
            {
                if (!File.Exists(settingsPath))
                    return null;
                var folder = JsonUtility.FromJson<Settings>(File.ReadAllText(settingsPath))?.folder;
                return string.IsNullOrWhiteSpace(folder) ? null : folder;
            }
            catch (Exception exception) when (exception is IOException or ArgumentException)
            {
                return null;
            }
        }

        public static void WriteDriveDirectory(string settingsPath, string folder)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath)));
            File.WriteAllText(
                settingsPath,
                JsonUtility.ToJson(new Settings { folder = Path.GetFullPath(folder) }, true)
            );
        }

        [MenuItem(ChooseMenu)]
        private static void ChooseFromMenu() => ChooseDriveDirectory();

        // Driveの中で .aseprite を置くフォルダーを選ばせる。なければ選択画面の中で作る。
        private static string ChooseDriveDirectory()
        {
            var folder = EditorUtility.OpenFolderPanel(
                "Asepriteを置くGoogle Driveのフォルダーを選択",
                ReadDriveDirectory(SettingsPath) ?? "",
                ""
            );
            if (string.IsNullOrEmpty(folder))
                return null;
            WriteDriveDirectory(SettingsPath, folder);
            Debug.Log(
                $"Asepriteを受け渡すDriveのフォルダーを設定しました: {Path.GetFullPath(folder)}"
            );
            return Path.GetFullPath(folder);
        }

        [MenuItem(ToDriveMenu)]
        private static void CopyToDrive() => Run(toDrive: true);

        [MenuItem(FromDriveMenu)]
        private static void CopyFromDrive() => Run(toDrive: false);

        public static Plan MakePlan(
            string source,
            string destination,
            IReadOnlyDictionary<string, string> baseHashes
        )
        {
            var plan = new Plan();
            var sourceFiles = ListAseprite(source);
            foreach (var name in sourceFiles)
            {
                var hash = Sha256(Path.Combine(source, name));
                plan.SourceHashes[name] = hash;
                var target = Path.Combine(destination, name);
                if (!File.Exists(target))
                {
                    plan.Added.Add(name);
                    continue;
                }
                var targetHash = Sha256(target);
                if (targetHash == hash)
                    plan.Unchanged.Add(name);
                else if (baseHashes.TryGetValue(name, out var baseHash) && baseHash == targetHash)
                    plan.Updated.Add(name);
                else
                    plan.Conflicts.Add(name);
            }
            plan.DestinationOnly.AddRange(
                ListAseprite(destination).Where(name => !plan.SourceHashes.ContainsKey(name))
            );
            return plan;
        }

        // コピーしたファイルの名前を返し、受け渡しの記録を更新する。
        public static List<string> Apply(
            Plan plan,
            string source,
            string destination,
            bool overwriteConflicts,
            string backupDirectory,
            Dictionary<string, string> baseHashes
        )
        {
            var copied = new List<string>();
            foreach (var name in plan.Unchanged)
                baseHashes[name] = plan.SourceHashes[name];
            foreach (var name in plan.Added.Concat(plan.Updated))
            {
                Copy(Path.Combine(source, name), Path.Combine(destination, name));
                baseHashes[name] = plan.SourceHashes[name];
                copied.Add(name);
            }
            if (!overwriteConflicts)
                return copied;
            foreach (var name in plan.Conflicts)
            {
                Copy(Path.Combine(destination, name), Path.Combine(backupDirectory, name));
                Copy(Path.Combine(source, name), Path.Combine(destination, name));
                baseHashes[name] = plan.SourceHashes[name];
                copied.Add(name);
            }
            return copied;
        }

        public static Dictionary<string, string> ReadBase(string path)
        {
            var hashes = new Dictionary<string, string>();
            if (!File.Exists(path))
                return hashes;
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('\t');
                if (parts.Length == 2)
                    hashes[parts[0]] = parts[1];
            }
            return hashes;
        }

        public static void WriteBase(string path, Dictionary<string, string> hashes) =>
            File.WriteAllLines(
                path,
                hashes
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "\t" + pair.Value),
                new UTF8Encoding(false)
            );

        private static void Run(bool toDrive)
        {
            var drive = ReadDriveDirectory(SettingsPath);
            if (drive == null)
            {
                if (
                    !EditorUtility.DisplayDialog(
                        Title,
                        "Asepriteを受け渡すGoogle Driveのフォルダーが、このPCでは未設定です。\n"
                            + "Driveの中で .aseprite を置くフォルダーを選んでください（なければ選択画面の中で作れます）。",
                        "フォルダーを選ぶ",
                        "キャンセル"
                    )
                )
                    return;
                drive = ChooseDriveDirectory();
                if (drive == null)
                    return;
            }
            if (!Directory.Exists(drive))
            {
                EditorUtility.DisplayDialog(
                    Title,
                    "設定したDriveのフォルダーが見つかりません。Google Drive for desktopの起動を確認するか、"
                        + $"メニューの「{ChooseMenu.Replace("/", " > ")}」で選び直してください。\n\n{drive}",
                    "閉じる"
                );
                return;
            }
            var pc = Path.GetFullPath(AsepriteCanvasImport.ArtRoot);
            var (source, destination) = toDrive ? (pc, drive) : (drive, pc);
            var (sourceLabel, destinationLabel) = toDrive ? ("PC", "Drive") : ("Drive", "PC");
            if (
                !EditorUtility.DisplayDialog(
                    Title,
                    $"{sourceLabel} から {destinationLabel} へ .aseprite をコピーします。\n\nPC: {pc}\nDrive: {drive}",
                    "コピーする",
                    "キャンセル"
                )
            )
                return;

            var baseFile = BaseFile(drive);
            var baseHashes = ReadBase(baseFile);
            var plan = MakePlan(source, destination, baseHashes);
            if (plan.SourceHashes.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    Title,
                    $"コピー元（{sourceLabel}）に .aseprite がありません。\n\n{source}",
                    "閉じる"
                );
                return;
            }
            var backup = Path.Combine(
                drive + "-backup",
                DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                destinationLabel
            );
            var overwrite =
                plan.Conflicts.Count > 0
                && EditorUtility.DisplayDialog(
                    Title,
                    $"{destinationLabel} 側も前回の受け渡しのあとに変わっているファイルが {plan.Conflicts.Count} 件あります。\n\n"
                        + string.Join("\n", plan.Conflicts)
                        + $"\n\n上書きする場合は、{destinationLabel} 側の今のファイルを次の場所へ退避します。\n{backup}",
                    $"{sourceLabel} の内容で上書き",
                    "上書きしない"
                );
            var copied = Apply(plan, source, destination, overwrite, backup, baseHashes);
            WriteBase(baseFile, baseHashes);
            if (!toDrive && copied.Count > 0)
                AssetDatabase.Refresh();

            var message = new StringBuilder(
                $"{sourceLabel} から {destinationLabel} へ {copied.Count} 件をコピーしました。"
            );
            foreach (var name in copied)
                message.Append("\n  ").Append(name);
            if (plan.Conflicts.Count > 0 && !overwrite)
                message.Append(
                    $"\n\n両方で変わっていた {plan.Conflicts.Count} 件はコピーしていません。どちらの内容を残すか決めてから、もう一度実行してください。\n  "
                        + string.Join("\n  ", plan.Conflicts)
                );
            if (plan.DestinationOnly.Count > 0)
                message.Append(
                    $"\n\n{destinationLabel} 側にだけあるファイル（削除していません）:\n  "
                        + string.Join("\n  ", plan.DestinationOnly)
                );
            message.Append(
                toDrive
                    ? "\n\nGoogle Driveへの同期状況はGoogle Drive for desktopで確認してください。"
                    : "\n\nUnityが読み込み直しました。git statusで差分を確認してコミットしてください。"
            );
            Debug.Log(message.ToString());
            EditorUtility.DisplayDialog(Title, message.ToString(), "閉じる");
        }

        private static List<string> ListAseprite(string root)
        {
            if (!Directory.Exists(root))
                return new List<string>();
            return Directory
                .GetFiles(root, "*.aseprite", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        private static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        private static void Copy(string from, string to)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(from, to, true);
        }
    }
}
