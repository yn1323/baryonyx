using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace Baryonyx.Editor
{
    public sealed class AnalyzerProjectSettings : AssetPostprocessor
    {
        private const string AnalyzerPath =
            "Assets/Analyzers/Microsoft.Unity.Analyzers/Microsoft.Unity.Analyzers.dll";

        private static string OnGeneratedCSProject(string path, string content)
        {
            var project = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
            var root = project.Root;
            var ns = root.Name.Namespace;
            var assemblyName = root.Descendants(ns + "AssemblyName").FirstOrDefault()?.Value;
            var assembly = CompilationPipeline
                .GetAssemblies(AssembliesType.Editor)
                .FirstOrDefault(candidate => candidate.name == assemblyName);
            if (
                assembly == null
                || !assembly.compilerOptions.RoslynAnalyzerDllPaths.Any(IsProjectAnalyzer)
            )
                return content;

            // Keep the project's pinned analyzer; remove only copies supplied by an IDE.
            var analyzers = root.Descendants(ns + "Analyzer").ToArray();
            var kept = false;
            foreach (var analyzer in analyzers)
            {
                var include = ((string)analyzer.Attribute("Include") ?? "").Replace('\\', '/');
                if (
                    !include.EndsWith(
                        "/Microsoft.Unity.Analyzers.dll",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && include != "Microsoft.Unity.Analyzers.dll"
                )
                    continue;
                if (IsProjectAnalyzer(include) && !kept)
                    kept = true;
                else
                    analyzer.Remove();
            }

            // Zed's generator may omit analyzer references when no VS installation is selected.
            if (!kept)
                root.Add(
                    new XElement(
                        ns + "ItemGroup",
                        new XElement(ns + "Analyzer", new XAttribute("Include", AnalyzerPath))
                    )
                );

            var ruleset = assembly.compilerOptions.RoslynAnalyzerRulesetPath;
            var property = root.Descendants(ns + "CodeAnalysisRuleSet").FirstOrDefault();
            if (property == null)
                root.Add(
                    new XElement(
                        ns + "PropertyGroup",
                        new XElement(ns + "CodeAnalysisRuleSet", ruleset)
                    )
                );
            else
                property.Value = ruleset;

            return project.ToString(SaveOptions.DisableFormatting);
        }

        private static bool IsProjectAnalyzer(string path) =>
            string.Equals(
                Path.GetFullPath(path.Replace('\\', '/')),
                Path.GetFullPath(AnalyzerPath),
                StringComparison.OrdinalIgnoreCase
            );
    }
}
