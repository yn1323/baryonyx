using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static Baryonyx.Vfx.Hd2d.Editor.Hd2dInspectorFields;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    [CustomEditor(typeof(Hd2dFlickerLight))]
    [CanEditMultipleObjects]
    public sealed class Hd2dFlickerLightEditor : Hd2dPreviewEditor<Hd2dFlickerLight>
    {
        protected override void Rebuild(Hd2dFlickerLight light) => light.RebuildLights();

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
    }

    [CustomPropertyDrawer(typeof(Hd2dFlickerLightSource))]
    public sealed class Hd2dFlickerLightSourceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var name = property.FindPropertyRelative("Name");
            var foldout = new Foldout { text = LabelOf(name.stringValue, "光源"), value = false };
            foldout.TrackPropertyValue(
                name,
                changed => foldout.text = LabelOf(changed.stringValue, "光源")
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
    }
}
