using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static Baryonyx.Vfx.Hd2d.Editor.Hd2dInspectorFields;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    [CustomEditor(typeof(Hd2dEmberEmitter))]
    [CanEditMultipleObjects]
    public sealed class Hd2dEmberEmitterEditor : Hd2dPreviewEditor<Hd2dEmberEmitter>
    {
        protected override void Rebuild(Hd2dEmberEmitter emitter) => emitter.RebuildParticles();

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
            AddField(assets, "CrossSprite", "十字の画像（未指定で点）");
            AddField(assets, "StreakSprite", "尾を引く粒の画像（未指定で点）");
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
    }

    [CustomPropertyDrawer(typeof(Hd2dEmberSource))]
    public sealed class Hd2dEmberSourceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var name = property.FindPropertyRelative("Name");
            var foldout = new Foldout { text = LabelOf(name.stringValue, "発生源"), value = false };
            foldout.TrackPropertyValue(
                name,
                changed => foldout.text = LabelOf(changed.stringValue, "発生源")
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
            AddField(foldout, property, "Curl", "カールノイズの強さ（px/秒、0で無効）");
            AddField(foldout, property, "CurlScale", "渦の大きさ（px）");
            AddField(foldout, property, "CurlSpeed", "流れが変わる速さ");
            AddField(foldout, property, "DotSize", "1ドットの大きさ（px）");
            AddField(foldout, property, "CrossShare", "十字の割合");
            AddField(foldout, property, "StreakShare", "尾を引く粒の割合（残りは点）");
            AddField(foldout, property, "StartColor", "出たときの色");
            AddField(foldout, property, "EndColor", "消える直前の色");
            AddField(foldout, property, "Twinkle", "ちらつき");
            return foldout;
        }
    }
}
