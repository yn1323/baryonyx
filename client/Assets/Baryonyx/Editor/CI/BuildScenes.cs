using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Baryonyx.Editor.CI
{
    public static class BuildScenes
    {
        public static string[] GetEnabledScenePaths(EditorBuildSettingsScene[] settings)
        {
            var scenes = settings
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("Enable at least one scene in Build Profiles.");
            if (scenes.Any(path => !File.Exists(path)))
                throw new BuildFailedException("An enabled build scene does not exist.");
            if (scenes.Distinct().Count() != scenes.Length)
                throw new BuildFailedException("An enabled build scene is listed more than once.");
            return scenes;
        }
    }
}
