using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    [CustomEditor(typeof(Hd2dFlickerLight))]
    [CanEditMultipleObjects]
    public sealed class Hd2dFlickerLightEditor : UnityEditor.Editor
    {
        private bool previewUpdateQueued;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(
                new HelpBox(
                    "光源ごとに位置・色・明るさ・揺らぎを調整できます。位置は親の正規化座標で、背景と同じ範囲に揃えた親に置くと絵の光源に重なります。再生を停止した状態で変更し、シーンを保存すると値が残ります。",
                    HelpBoxMessageType.Info
                )
            );

            AddField(root, "PreviewInEditor", "Editorで表示");
            AddField(root, "Intensity", "全体の明るさ", "全光源の明るさに掛ける倍率です。");
            AddField(root, "Sources", "光源");

            var variation = new Foldout { text = "ばらつき・再生設定", value = false };
            AddField(variation, "RandomSeed", "揺らぎのパターン");
            AddField(variation, "Animate", "再生中に揺らす");
            AddField(variation, "PlayOnEnable", "有効時に自動表示");
            AddField(variation, "UseUnscaledTime", "ゲーム速度に影響されない");
            root.Add(variation);

            var assets = new Foldout { text = "素材", value = false };
            AddField(assets, "LightLayer", "描画レイヤー");
            AddField(assets, "GlowSprite", "光の画像");
            AddField(assets, "AdditiveMaterial", "加算合成マテリアル");
            root.Add(assets);

            root.Add(
                new HelpBox(
                    "Editorでは静止表示します。揺らぎはPlay Modeで確認してください。Play Mode中の変更は停止時に戻ります。",
                    HelpBoxMessageType.None
                )
            );
            // Tracks list edits (add, remove, reorder) as well as nested field changes.
            root.TrackSerializedObjectValue(serializedObject, _ => QueuePreviewUpdate());
            return root;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += QueuePreviewUpdate;
            QueuePreviewUpdate();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= QueuePreviewUpdate;
            EditorApplication.delayCall -= RefreshPreview;
        }

        private void QueuePreviewUpdate()
        {
            if (previewUpdateQueued)
                return;

            previewUpdateQueued = true;
            EditorApplication.delayCall += RefreshPreview;
        }

        private void RefreshPreview()
        {
            previewUpdateQueued = false;
            if (this == null)
                return;

            foreach (var editedTarget in targets)
            {
                if (
                    editedTarget is Hd2dFlickerLight light
                    && !UnityEngine.Application.IsPlaying(light.gameObject)
                )
                    light.RebuildLights();
            }
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private static void AddField(
            VisualElement parent,
            string path,
            string label,
            string tooltip = null
        )
        {
            parent.Add(
                new PropertyField
                {
                    bindingPath = path,
                    label = label,
                    tooltip = tooltip,
                }
            );
        }
    }

    [CustomPropertyDrawer(typeof(Hd2dFlickerLightSource))]
    public sealed class Hd2dFlickerLightSourceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var name = property.FindPropertyRelative("Name");
            var foldout = new Foldout { text = LabelOf(name.stringValue), value = false };
            foldout.TrackPropertyValue(
                name,
                changed => foldout.text = LabelOf(changed.stringValue)
            );

            AddField(foldout, property, "Name", "名前");
            AddField(foldout, property, "Anchor", "光源の位置（X: 左右 / Y: 上下）");
            AddField(foldout, property, "Color", "色");
            AddField(foldout, property, "CoreAlpha", "芯の明るさ");
            AddField(foldout, property, "CoreSize", "芯の大きさ（px）");
            AddField(foldout, property, "HaloAlpha", "周りを照らす明るさ");
            AddField(foldout, property, "HaloSize", "周りを照らす大きさ（px）");
            AddField(foldout, property, "ReflectionAlpha", "照り返しの明るさ（0で非表示）");
            AddField(foldout, property, "ReflectionAnchor", "照り返しの位置（X: 左右 / Y: 上下）");
            AddField(foldout, property, "ReflectionSize", "照り返しの大きさ（px）");
            AddField(foldout, property, "FlickerAmount", "揺らぎの量");
            AddField(foldout, property, "FlickerSpeed", "揺らぎの速さ");
            AddField(foldout, property, "SizeJitter", "大きさの揺らぎ");
            return foldout;
        }

        private static string LabelOf(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "光源" : name;
        }

        private static void AddField(
            VisualElement parent,
            SerializedProperty property,
            string relativePath,
            string label
        )
        {
            parent.Add(new PropertyField(property.FindPropertyRelative(relativePath), label));
        }
    }
}
