using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static Baryonyx.Vfx.Hd2d.Editor.Hd2dInspectorFields;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    [CustomEditor(typeof(Hd2dFog))]
    [CanEditMultipleObjects]
    public sealed class Hd2dFogEditor : Hd2dPreviewEditor<Hd2dFog>
    {
        protected override void Rebuild(Hd2dFog fog) => fog.RebuildLayers();

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
    }

    [CustomPropertyDrawer(typeof(Hd2dFogLayer))]
    public sealed class Hd2dFogLayerDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var name = property.FindPropertyRelative("Name");
            var foldout = new Foldout { text = LabelOf(name.stringValue, "霧"), value = false };
            foldout.TrackPropertyValue(
                name,
                changed => foldout.text = LabelOf(changed.stringValue, "霧")
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
    }
}
