using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    [CustomEditor(typeof(Hd2dFog))]
    [CanEditMultipleObjects]
    public sealed class Hd2dFogEditor : UnityEditor.Editor
    {
        private bool previewUpdateQueued;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(
                new HelpBox(
                    "霧のレイヤーごとに範囲・色・流れを調整できます。再生を停止した状態で変更し、シーンを保存すると値が残ります。",
                    HelpBoxMessageType.Info
                )
            );

            AddField(root, "PreviewInEditor", "Editorで表示");
            AddField(root, "Intensity", "全体の濃さ", "全レイヤーの濃さに掛ける倍率です。");
            AddField(root, "Layers", "霧のレイヤー");

            var variation = new Foldout { text = "ばらつき・再生設定", value = false };
            AddField(variation, "RandomSeed", "模様の開始位置");
            AddField(variation, "Animate", "再生中に流す");
            AddField(variation, "PlayOnEnable", "有効時に自動表示");
            AddField(variation, "UseUnscaledTime", "ゲーム速度に影響されない");
            root.Add(variation);

            var assets = new Foldout { text = "素材", value = false };
            AddField(assets, "FogLayer", "描画レイヤー");
            AddField(assets, "NoiseTexture", "ノイズ画像");
            root.Add(assets);

            root.Add(
                new HelpBox(
                    "Editorでは静止表示します。霧の流れと濃さの変化はPlay Modeで確認してください。Play Mode中の変更は停止時に戻ります。",
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
                    editedTarget is Hd2dFog fog
                    && !UnityEngine.Application.IsPlaying(fog.gameObject)
                )
                    fog.RebuildLayers();
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

    [CustomPropertyDrawer(typeof(Hd2dFogLayer))]
    public sealed class Hd2dFogLayerDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var name = property.FindPropertyRelative("Name");
            var foldout = new Foldout
            {
                text = string.IsNullOrWhiteSpace(name.stringValue) ? "霧" : name.stringValue,
                value = false,
            };
            foldout.TrackPropertyValue(
                name,
                changed =>
                    foldout.text = string.IsNullOrWhiteSpace(changed.stringValue)
                        ? "霧"
                        : changed.stringValue
            );

            AddField(foldout, property, "Name", "名前");
            AddField(foldout, property, "Color", "色（Aが濃さ）");
            AddField(foldout, property, "AnchorMin", "範囲の左下（X: 左右 / Y: 上下）");
            AddField(foldout, property, "AnchorMax", "範囲の右上（X: 左右 / Y: 上下）");
            AddField(foldout, property, "Softness", "縁のぼかし（px）");
            AddField(foldout, property, "TileSize", "模様の大きさ（px）");
            AddField(foldout, property, "ScrollSpeed", "流れる速さ（px/秒）");
            AddField(foldout, property, "DetailOpacity", "逆向きの細かい模様");
            AddField(foldout, property, "BreathAmount", "濃さの増減");
            AddField(foldout, property, "BreathSpeed", "増減の速さ");
            AddField(foldout, property, "Texture", "画像（未指定で共通ノイズ）");
            return foldout;
        }

        private static void AddField(
            VisualElement parent,
            SerializedProperty property,
            string relativePath,
            string label
        )
        {
            var child = property.FindPropertyRelative(relativePath);
            parent.Add(new PropertyField(child, label));
        }
    }
}
