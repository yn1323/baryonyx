using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Baryonyx.Editor
{
    /// <summary>
    /// Saves the open scenes without ever asking. Unity shows the "Save As" window for a scene
    /// that has never been saved (Untitled), even when a script calls
    /// <see cref="EditorSceneManager.SaveOpenScenes"/>, and the window stops the Editor until a
    /// person closes it, so commands from the AI and the CLI time out. Use this instead of
    /// <c>SaveOpenScenes</c> in editor code and in evaluations.
    /// </summary>
    public static class EditorScenes
    {
        /// <summary>
        /// Saves every open scene that has changes and a file; an Untitled scene is left as it is.
        /// Returns false when a scene with a file could not be saved.
        /// </summary>
        public static bool SaveNamed()
        {
            bool saved = true;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path))
                    saved &= EditorSceneManager.SaveScene(scene);
            }
            return saved;
        }

        /// <summary>True when an open scene that was never saved has changes.</summary>
        public static bool HasUntitledChanges()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && string.IsNullOrEmpty(scene.path))
                    return true;
            }
            return false;
        }
    }
}
