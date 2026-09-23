using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    [CustomEditor(typeof(Hd2dLightShaft))]
    [CanEditMultipleObjects]
    public sealed class Hd2dLightShaftEditor : UnityEditor.Editor
    {
        private bool previewUpdateQueued;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(
                new HelpBox(
                    "TopCanvas > TopHd2dLightShaft で調整できます。再生を停止した状態で変更し、シーンを保存すると値が残ります。範囲のXは最小、Yは最大です。",
                    HelpBoxMessageType.Info
                )
            );

            AddField(root, "PreviewInEditor", "Editorで表示");
            var opacity = new Slider("光の濃さ（0で透明）", 0f, 1f)
            {
                name = "shaftOpacity",
                bindingPath = "ShaftColor.a",
                showInputField = true,
                tooltip = "小さいほど背景になじみます。現在の目安は0.34です。",
            };
            opacity.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            opacity.RegisterValueChangedCallback(_ => QueuePreviewUpdate());
            root.Add(opacity);
            AddField(root, "ShaftColor", "光の色");
            AddField(
                root,
                "SourceAnchor",
                "開始位置（X: 左右 / Y: 上下）",
                "Xを小さくすると角度を変えずに左へ移動します。Yを1より大きくすると画面の上側から入ります。"
            );
            AddField(
                root,
                "SourceSpread",
                "開始位置の広がり",
                "複数本を左右に並べる幅です。0にすると同じ位置から出ます。"
            );
            AddField(root, "WidthRange", "太さの範囲");
            AddField(root, "LengthRange", "長さの範囲");
            AddField(root, "RotationRange", "角度の範囲（度）");
            AddField(root, "ShaftCount", "光芒の本数");
            AddField(root, "WidthScaleRange", "細い方 / 太い方の倍率");
            AddField(root, "OpacityRange", "1本ごとの濃さの倍率");

            var floorPool = new Foldout { text = "床の光だまり", value = false };
            AddField(floorPool, "FloorPoolAlpha", "濃さ（0で非表示）");
            AddField(floorPool, "FloorPoolAnchor", "中心位置（X: 左右 / Y: 上下）");
            AddField(floorPool, "FloorPoolSize", "大きさ");
            AddField(floorPool, "FloorPoolSprite", "画像");
            root.Add(floorPool);

            var motes = new Foldout { text = "光の中の埃", value = false };
            AddField(motes, "MotesPerShaft", "1本あたりの数（0で非表示）");
            AddField(motes, "MoteColor", "色");
            AddField(motes, "MoteSizeRange", "大きさの範囲（px）");
            AddField(motes, "MoteSpeedRange", "流れる速さの範囲（px/秒）");
            AddField(motes, "MoteSway", "横ゆれの幅");
            AddField(motes, "MoteSprite", "画像（未指定で四角い点）");
            root.Add(motes);

            var variation = new Foldout { text = "ばらつき・ゆらぎ", value = false };
            AddField(variation, "SourceJitter", "開始位置のばらつき");
            AddField(variation, "RandomSeed", "配置パターン");
            AddField(variation, "Animate", "再生中にゆらぐ");
            AddField(variation, "FlickerAmount", "明るさのゆらぎ");
            AddField(variation, "FlickerSpeed", "明るさの変化速度");
            AddField(variation, "MotionAmplitude", "移動の幅");
            AddField(variation, "MotionSpeed", "移動の速度");
            root.Add(variation);

            var assets = new Foldout { text = "素材・再生設定", value = false };
            AddField(assets, "ShaftLayer", "描画レイヤー");
            AddField(assets, "ShaftSprite", "光芒の画像");
            AddField(assets, "PlayOnEnable", "有効時に自動表示");
            AddField(assets, "UseUnscaledTime", "ゲーム速度に影響されない");
            root.Add(assets);
            root.Add(
                new HelpBox(
                    "Editorでは静止表示します。ゆらぎの動きはPlay Modeで確認してください。Play Mode中の変更は停止時に戻ります。",
                    HelpBoxMessageType.None
                )
            );
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ => QueuePreviewUpdate());
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
                    editedTarget is Hd2dLightShaft shaft
                    && !UnityEngine.Application.IsPlaying(shaft.gameObject)
                )
                    shaft.RebuildShafts();
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
}
