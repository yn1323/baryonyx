using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baryonyx.Showcase;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.Showcase.Editor
{
    public static class ShowcaseCatalogBuilder
    {
        public const string RootPath = "Assets/Baryonyx/Features/Showcase";
        public const string GeneratedEntriesPath = RootPath + "/Entries/Generated";
        public const string CatalogPath = RootPath + "/Data/ShowcaseCatalog.asset";
        public const string ScenePath = "Assets/Baryonyx/App/Scenes/Showcase.unity";

        [InitializeOnLoadMethod]
        private static void RefreshOnEditorLoad()
        {
            if (SessionState.GetBool("Baryonyx.Showcase.RefreshOnLoad", false))
                return;

            SessionState.SetBool("Baryonyx.Showcase.RefreshOnLoad", true);
            EditorApplication.delayCall += RefreshCatalog;
        }

        [MenuItem("Baryonyx/Showcase/Refresh Catalog")]
        public static void RefreshCatalog()
        {
            EnsureFolders();
            var catalog = AssetDatabase.LoadAssetAtPath<ShowcaseCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ShowcaseCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var generated = new List<ShowcaseEntry>();
            var seenAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddAssets(generated, seenAssetPaths, "t:Sprite", ShowcaseCategory.Image);
            AddAssets(generated, seenAssetPaths, "t:Texture2D", ShowcaseCategory.Image);
            AddAssets(generated, seenAssetPaths, "t:Prefab", ShowcaseCategory.Other);
            AddAssets(generated, seenAssetPaths, "t:AudioClip", ShowcaseCategory.Audio);
            AddAssets(generated, seenAssetPaths, "t:AnimationClip", ShowcaseCategory.Animation);
            AddAssets(generated, seenAssetPaths, "t:Material", ShowcaseCategory.Material);
            AddAssets(generated, seenAssetPaths, "t:Shader", ShowcaseCategory.Material);
            AddAssets(generated, seenAssetPaths, "t:ScriptableObject", ShowcaseCategory.Data);
            AddSceneAssets(generated, seenAssetPaths);

            var manual = AssetDatabase
                .FindAssets("t:ShowcaseEntry", new[] { RootPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    !path.StartsWith(GeneratedEntriesPath + "/", StringComparison.OrdinalIgnoreCase)
                )
                .Select(AssetDatabase.LoadAssetAtPath<ShowcaseEntry>)
                .Where(entry => entry != null);
            generated.AddRange(manual);
            EnsureScenesInBuildSettings(generated);
            catalog.SetEntries(generated.OrderBy(entry => entry.Label));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureScenesInBuildSettings(IEnumerable<ShowcaseEntry> entries)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var showcasePath = entries
                .Where(entry => entry.Category == ShowcaseCategory.Scene)
                .Select(entry => entry.ScenePath)
                .FirstOrDefault(path =>
                    string.Equals(path, ScenePath, StringComparison.OrdinalIgnoreCase)
                );
            if (
                !string.IsNullOrWhiteSpace(showcasePath)
                && !scenes.Any(scene =>
                    string.Equals(scene.path, showcasePath, StringComparison.OrdinalIgnoreCase)
                )
            )
                scenes.Add(new EditorBuildSettingsScene(showcasePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddAssets(
            List<ShowcaseEntry> entries,
            HashSet<string> seenAssetPaths,
            string filter,
            ShowcaseCategory defaultCategory
        )
        {
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { "Assets/Baryonyx" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!ShouldInclude(path) || !seenAssetPaths.Add(path))
                    continue;

                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                    continue;
                var sprites = AssetDatabase
                    .LoadAllAssetRepresentationsAtPath(path)
                    .OfType<Sprite>()
                    .ToList();
                if (sprites.Count > 0)
                {
                    foreach (var sprite in sprites)
                    {
                        var entry = GetOrCreateGeneratedEntry($"{guid}_{StableKey(sprite.name)}");
                        ConfigureEntry(
                            entry,
                            $"{guid}_{StableKey(sprite.name)}",
                            sprite.name,
                            $"{path}#{sprite.name}",
                            InferCategory(path, defaultCategory),
                            sprite
                        );
                        entries.Add(entry);
                    }
                }
                else
                {
                    var entry = GetOrCreateGeneratedEntry(guid);
                    ConfigureEntry(
                        entry,
                        guid,
                        Path.GetFileNameWithoutExtension(path),
                        path,
                        InferCategory(path, defaultCategory),
                        asset
                    );
                    entries.Add(entry);
                }
            }
        }

        private static void ConfigureEntry(
            ShowcaseEntry entry,
            string id,
            string displayName,
            string description,
            ShowcaseCategory category,
            UnityEngine.Object asset
        )
        {
            entry.Id = id;
            entry.DisplayName = displayName;
            entry.Description = description;
            entry.Category = category;
            entry.Asset = asset;
            entry.PreviewPrefab = asset as GameObject;
            entry.PreviewAnimation = asset as AnimationClip;
            entry.AnimationStateName = string.Empty;
            if (entry.PreviewPrefab != null)
            {
                var animator = entry.PreviewPrefab.GetComponentInChildren<Animator>();
                entry.PreviewAnimation =
                    animator != null && animator.runtimeAnimatorController != null
                        ? animator.runtimeAnimatorController.animationClips.FirstOrDefault()
                        : null;
                if (
                    animator != null
                    && animator.runtimeAnimatorController is AnimatorController controller
                )
                {
                    entry.AnimationStateName =
                        controller
                            .layers.SelectMany(layer => layer.stateMachine.states)
                            .Select(state => state.state.name)
                            .FirstOrDefault()
                        ?? string.Empty;
                }

                if (entry.PreviewAnimation == null)
                {
                    var legacyAnimation = entry.PreviewPrefab.GetComponentInChildren<Animation>();
                    if (legacyAnimation != null)
                    {
                        foreach (AnimationState state in legacyAnimation)
                        {
                            entry.PreviewAnimation = state.clip;
                            break;
                        }
                    }
                }
            }
            entry.ScenePath = string.Empty;
            entry.Enabled = true;
            EditorUtility.SetDirty(entry);
        }

        private static string StableKey(string value)
        {
            return new string(
                value
                    .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                    .ToArray()
            );
        }

        private static void AddSceneAssets(
            List<ShowcaseEntry> entries,
            HashSet<string> seenAssetPaths
        )
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Baryonyx" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!ShouldInclude(path) || !seenAssetPaths.Add(path))
                    continue;

                var entry = GetOrCreateGeneratedEntry(guid);
                entry.Id = guid;
                entry.DisplayName = Path.GetFileNameWithoutExtension(path);
                entry.Description = path;
                entry.Category = ShowcaseCategory.Scene;
                entry.Asset = null;
                entry.PreviewPrefab = null;
                entry.PreviewAnimation = null;
                entry.ScenePath = path;
                entry.Enabled = true;
                EditorUtility.SetDirty(entry);
                entries.Add(entry);
            }
        }

        private static ShowcaseEntry GetOrCreateGeneratedEntry(string guid)
        {
            var path = $"{GeneratedEntriesPath}/{guid}.asset";
            var entry = AssetDatabase.LoadAssetAtPath<ShowcaseEntry>(path);
            if (entry != null)
                return entry;

            entry = ScriptableObject.CreateInstance<ShowcaseEntry>();
            AssetDatabase.CreateAsset(entry, path);
            return entry;
        }

        private static bool ShouldInclude(string path)
        {
            if (
                string.IsNullOrWhiteSpace(path)
                || !path.StartsWith("Assets/Baryonyx/", StringComparison.OrdinalIgnoreCase)
            )
                return false;
            return !path.Contains("/Editor/", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("/Showcase/", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("/DevCaptures/", StringComparison.OrdinalIgnoreCase);
        }

        private static ShowcaseCategory InferCategory(string path, ShowcaseCategory fallback)
        {
            var value = path.ToLowerInvariant();
            if (
                value.Contains("character")
                || value.Contains("adventurer")
                || value.Contains("player")
            )
                return ShowcaseCategory.Character;
            if (
                value.Contains("background")
                || value.Contains("environment")
                || value.Contains("forest")
                || value.Contains("mine")
            )
                return ShowcaseCategory.Environment;
            if (value.Contains("weapon") || value.Contains("sword") || value.Contains("bow"))
                return ShowcaseCategory.Weapon;
            if (value.Contains("item") || value.Contains("potion") || value.Contains("icon"))
                return ShowcaseCategory.Item;
            if (
                value.Contains("/ui/")
                || value.Contains("button")
                || value.Contains("panel")
                || value.Contains("screen")
            )
                return ShowcaseCategory.Ui;
            if (value.Contains("vfx") || value.Contains("effect") || value.Contains("particle"))
                return ShowcaseCategory.Vfx;
            return fallback;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Baryonyx/Features");
            EnsureFolder(RootPath);
            EnsureFolder(RootPath + "/Entries");
            EnsureFolder(GeneratedEntriesPath);
            EnsureFolder(RootPath + "/Data");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            var name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    public sealed class ShowcaseCatalogPostprocessor : AssetPostprocessor
    {
        private static bool refreshQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (refreshQueued || !TouchesClientAssets(importedAssets, movedAssets, deletedAssets))
                return;

            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                ShowcaseCatalogBuilder.RefreshCatalog();
            };
        }

        private static bool TouchesClientAssets(params string[][] paths)
        {
            return paths
                .SelectMany(value => value)
                .Any(path =>
                    path.StartsWith("Assets/Baryonyx/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/Showcase/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/Editor/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
                );
        }
    }
}
