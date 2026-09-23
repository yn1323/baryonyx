using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    [CustomEditor(typeof(Hd2dEmberEmitter))]
    [CanEditMultipleObjects]
    public sealed class Hd2dEmberEmitterEditor : UnityEditor.Editor
    {
        private bool previewUpdateQueued;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(
                new HelpBox(
                    "発生源ごとに位置・数・向き・速さ・色を調整できます。位置は親の正規化座標で、背景と同じ範囲に揃えた親に置くと絵の光源から出ます。再生を停止した状態で変更し、シーンを保存すると値が残ります。",
                    HelpBoxMessageType.Info
                )
            );

            AddField(root, "PreviewInEditor", "Editorで表示");
            AddField(root, "Intensity", "全体の濃さ", "全粒子の濃さに掛ける倍率です。");
            AddField(root, "Sources", "発生源");

            var variation = new Foldout { text = "ばらつき・再生設定", value = false };
            AddField(variation, "RandomSeed", "配置パターン");
            AddField(variation, "Animate", "再生中に動かす");
            AddField(variation, "PlayOnEnable", "有効時に自動表示");
            AddField(variation, "UseUnscaledTime", "ゲーム速度に影響されない");
            root.Add(variation);

            var assets = new Foldout { text = "素材", value = false };
            AddField(assets, "ParticleLayer", "描画レイヤー");
            AddField(assets, "ParticleSprite", "粒子の画像（未指定で四角い点）");
            AddField(assets, "AdditiveMaterial", "加算合成マテリアル");
            root.Add(assets);

            root.Add(
                new HelpBox(
                    "Editorでは再生開始時と同じ配置を静止表示します。動きはPlay Modeで確認してください。Play Mode中の変更は停止時に戻ります。",
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
                    editedTarget is Hd2dEmberEmitter emitter
                    && !UnityEngine.Application.IsPlaying(emitter.gameObject)
                )
                    emitter.RebuildParticles();
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

    [CustomPropertyDrawer(typeof(Hd2dEmberSource))]
    public sealed class Hd2dEmberSourceDrawer : PropertyDrawer
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
            AddField(foldout, property, "Anchor", "発生位置（X: 左右 / Y: 上下）");
            AddField(foldout, property, "SpawnArea", "発生位置の広がり（px）");
            AddField(foldout, property, "Count", "同時に出す数");
            AddField(foldout, property, "Loop", "出し続ける");
            AddField(foldout, property, "LifetimeRange", "寿命の範囲（秒）");
            AddField(foldout, property, "Direction", "向き（度、90で上）");
            AddField(foldout, property, "Spread", "向きのばらつき（度）");
            AddField(foldout, property, "SpeedRange", "初速の範囲（px/秒）");
            AddField(foldout, property, "Buoyancy", "上向きの加速（負で落下）");
            AddField(foldout, property, "Sway", "横ゆれの幅（px）");
            AddField(foldout, property, "SwaySpeed", "横ゆれの速さ");
            AddField(foldout, property, "SizeRange", "大きさの範囲（px）");
            AddField(foldout, property, "StartColor", "出たときの色");
            AddField(foldout, property, "EndColor", "消える直前の色");
            AddField(foldout, property, "Twinkle", "ちらつき");
            return foldout;
        }

        private static string LabelOf(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "発生源" : name;
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
