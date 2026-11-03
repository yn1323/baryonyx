using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Health.Editor
{
    // Health Connect連携モーダルのPrefabを生成する。ほかの窓と同じ作り（doc/rules/ui-design.md の共通の部品）で、
    // 親Canvasの全面を共通の暗い幕で覆い、Safe Area内の中央に、濃い紺に金の細い縁の窓を置く。
    // 窓には明るい金の題、本文、下に「あとで」と、決めるボタン（青い板）を並べる。
    public static class HealthLinkModalAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Health/UI/HealthLinkModal.prefab";

        private const float ButtonHeight = 112f;
        private const float LaterWidth = 320f;
        private const float ActionWidth = 680f;

        [MenuItem("Baryonyx/Health/Create Link Modal Prefab")]
        public static GameObject CreatePrefab()
        {
            EditorGuard.RequireEditMode();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var font = GameFontAssets.GetOrCreate();
            using (Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = Rect("HealthLinkModal", null);
                Stretch(root);
                // 暗幕は全面に置き、背面のタップとスクロールを止める。
                AddImage(root, UiPalette.Veil, true);
                var view = root.gameObject.AddComponent<HealthLinkModalView>();

                var safe = SafeArea(root);
                // 題は共通のダイアログと同じ、明るい金の中央寄せ。
                RectTransform window;
                (window, view.TitleLabel) = GuideFrameAssets.MessageWindow(
                    safe,
                    GuideFrameAssets.WindowSize.Medium,
                    32f
                );
                view.BodyLabel = Label(
                    window,
                    "Message",
                    "",
                    UiType.Lead,
                    UiPalette.TextMain,
                    TextAlignmentOptions.TopLeft
                );
                view.BodyLabel.textWrappingMode = TextWrappingModes.Normal;
                view.BodyLabel.lineSpacing = 12f;

                var buttons = GuideFrameAssets.ButtonRow(window);

                // Androidのタップ領域の目安（48dp）を十分に上回る高さにする。
                // 左に見送る「あとで」、右に決めるボタン（doc/rules/ui-design.md#決めるボタン）。
                (view.LaterButton, view.LaterLabel, _) = ActionButtonAssets.InLayout(
                    buttons,
                    "LaterButton",
                    "",
                    ButtonRole.Secondary,
                    LaterWidth,
                    ButtonHeight,
                    UiType.Lead
                );
                (view.ActionButton, view.ActionLabel, _) = ActionButtonAssets.InLayout(
                    buttons,
                    "ActionButton",
                    "",
                    ButtonRole.Primary,
                    ActionWidth,
                    ButtonHeight,
                    UiType.Lead
                );

                view.Show(HealthLinkStatus.PermissionRequired);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
        }
    }
}
