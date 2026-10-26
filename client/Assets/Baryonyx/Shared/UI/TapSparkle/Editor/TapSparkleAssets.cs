using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Sets up the tap sparkle (<see cref="TapSparkleLayer"/>): puts its shader in the shaders every
    /// build includes, since the layer finds it by name when the game starts, and builds the
    /// showcase's sample, a canvas that taps its own centre over and over.
    /// </summary>
    public static class TapSparkleAssets
    {
        public const string Folder = "Assets/Baryonyx/Shared/UI/TapSparkle";
        public const string ShaderPath = Folder + "/TapSparkle.shader";
        public const string PreviewPath = Folder + "/TapSparklePreview.prefab";

        // 展示室の見本で、自動で押す間隔（秒）。金の粉が消えてから少し間を置く。
        private const float PreviewEvery = 1.2f;

        [MenuItem("Baryonyx/UI/Create Tap Sparkle Assets")]
        public static void CreateAssets()
        {
            IncludeShader();
            CreatePreview();
        }

        /// <summary>Puts the shader in Graphics Settings' Always Included Shaders, once.</summary>
        public static void IncludeShader()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new System.InvalidOperationException("Missing shader: " + ShaderPath);
            var settings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]
            );
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Whether every build carries the shader.</summary>
        public static bool ShaderIncluded()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var settings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]
            );
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            return shader != null
                && Enumerable
                    .Range(0, list.arraySize)
                    .Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == shader);
        }

        // 展示室の見本：入力は読まず、画面の中央を決まった間隔で押す。
        private static void CreatePreview()
        {
            // 開いているシーンを汚さないよう、プレビュー用のシーンで組み立てる。
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("TapSparklePreview", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1;
                var marks = TapSparkleMarks.AddTo(
                    root.transform,
                    AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath)
                );
                var layer = root.AddComponent<TapSparkleLayer>();
                layer.Marks = marks;
                layer.ReadsInput = false;
                layer.PreviewEvery = PreviewEvery;
                PrefabUtility.SaveAsPrefabAsset(root, PreviewPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
