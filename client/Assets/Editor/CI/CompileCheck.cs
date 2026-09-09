using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Baryonyx.Editor.CI
{
    public static class CompileCheck
    {
        private const string AnalyzerDirectory = "Assets/Analyzers/Microsoft.Unity.Analyzers/";
        private const string AnalyzerPath = AnalyzerDirectory + "Microsoft.Unity.Analyzers.dll";
        private const string RulesetPath = "Assets/Default.ruleset";
        private const string ReportPath = "Logs/client-compile.txt";
        private static bool failed;
        private static int compiledAssemblies;

        // Called with -batchmode, without -quit. The compilation callback exits Unity.
        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("CompileCheck must run in batch mode.");

            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, $"Unity {Application.unityVersion}\n");
            try
            {
                ValidateConfiguration();
                failed = false;
                compiledAssemblies = 0;
                CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
                CompilationPipeline.assemblyCompilationNotRequired += OnAssemblyUnchanged;
                CompilationPipeline.compilationFinished += OnCompilationFinished;
                // Run analyzers even when Library was restored from a CI cache.
                CompilationPipeline.RequestScriptCompilation(
                    RequestScriptCompilationOptions.CleanBuildCache
                );
            }
            catch (Exception exception)
            {
                File.AppendAllText(ReportPath, exception + "\n");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateConfiguration()
        {
            var provenance = JsonUtility.FromJson<Provenance>(
                File.ReadAllText(AnalyzerDirectory + "provenance.json")
            );
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(AnalyzerPath);
            var hash = BitConverter
                .ToString(sha.ComputeHash(stream))
                .Replace("-", "")
                .ToLowerInvariant();
            Require(hash == provenance.dllSha256, "Analyzer DLL SHA-256 mismatch.");

            var importer = AssetImporter.GetAtPath(AnalyzerPath) as PluginImporter;
            Require(importer != null, "Analyzer PluginImporter is missing.");
            Require(
                !importer.GetCompatibleWithAnyPlatform(),
                "Disable Any Platform for the analyzer."
            );
            Require(
                !importer.GetCompatibleWithEditor(),
                "Disable Editor runtime loading for the analyzer."
            );
            Require(
                AssetDatabase.GetLabels(importer).Contains("RoslynAnalyzer"),
                "Analyzer must have the RoslynAnalyzer label."
            );

            var rules = XDocument
                .Load(RulesetPath)
                .Descendants("Rules")
                .Single(element =>
                    (string)element.Attribute("AnalyzerId") == "Microsoft.Unity.Analyzers"
                );
            foreach (var id in new[] { "UNT0006", "UNT0007", "UNT0008", "UNT0010", "UNT0011" })
                Require(
                    rules
                        .Elements("Rule")
                        .Any(element =>
                            (string)element.Attribute("Id") == id
                            && (string)element.Attribute("Action") == "Error"
                        ),
                    $"{id} must be Error in {RulesetPath}."
                );

            var assemblies = CompilationPipeline
                .GetAssemblies(AssembliesType.Editor)
                .Where(assembly =>
                    assembly.sourceFiles.Any(source =>
                        source.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal)
                    )
                )
                .ToArray();
            Require(assemblies.Length > 0, "No project C# assemblies were found.");
            foreach (var assembly in assemblies)
            {
                var options = assembly.compilerOptions;
                Require(
                    options.RoslynAnalyzerDllPaths.Any(path => SamePath(path, AnalyzerPath)),
                    $"Analyzer is not enabled for {assembly.name}."
                );
                Require(
                    SamePath(options.RoslynAnalyzerRulesetPath, RulesetPath),
                    $"{assembly.name} must use {RulesetPath}."
                );
                File.AppendAllText(ReportPath, $"Configured: {assembly.name}\n");
            }
        }

        private static bool SamePath(string first, string second) =>
            !string.IsNullOrEmpty(first)
            && string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase
            );

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void OnAssemblyFinished(string assemblyPath, CompilerMessage[] messages)
        {
            compiledAssemblies++;
            foreach (var message in messages)
            {
                File.AppendAllText(ReportPath, $"{assemblyPath}: {message.message}\n");
                // Roslyn reports analyzer load/execution failures as warnings.
                if (
                    message.type == CompilerMessageType.Error
                    || Regex.IsMatch(
                        message.message,
                        @"\b(CS803[234]|CS9057|AD000[12]|CS878[45])\b"
                    )
                )
                    failed = true;
            }
        }

        // Unity also uses this event after a clean compile whose DLL bytes did not change.
        private static void OnAssemblyUnchanged(string assemblyPath) => compiledAssemblies++;

        private static void OnCompilationFinished(object context)
        {
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyFinished;
            CompilationPipeline.assemblyCompilationNotRequired -= OnAssemblyUnchanged;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            var success = !failed && compiledAssemblies > 0;
            var result = success ? "BARYONYX_COMPILE_OK" : "BARYONYX_COMPILE_FAILED";
            File.AppendAllText(ReportPath, $"{result}: {compiledAssemblies} assemblies\n");
            Debug.Log(result);
            EditorApplication.Exit(success ? 0 : 1);
        }

        [Serializable]
        private sealed class Provenance
        {
            public string dllSha256;
        }
    }
}
