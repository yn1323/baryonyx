using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    // HD-2Dの演出のInspectorで、日本語のラベルをつけた項目を並べる。
    internal static class Hd2dInspectorFields
    {
        public static void AddField(
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

        public static void AddField(
            VisualElement parent,
            SerializedProperty property,
            string relativePath,
            string label
        )
        {
            parent.Add(new PropertyField(property.FindPropertyRelative(relativePath), label));
        }

        // 名前のない要素は、種類の名前で表示する。
        public static string LabelOf(string name, string fallback) =>
            string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
