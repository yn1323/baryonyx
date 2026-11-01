using System.IO;
using Baryonyx.Combat.Editor;
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

namespace Baryonyx.WorldMap.Editor
{
    /// <summary>
    /// The map editor's controls (WorldEventLabPanel.prefab), laid over the nested world map
    /// (<see cref="WorldEventLab"/>): two rows along the bottom, right of the corner the "ホームへ"
    /// button takes, with the event buttons and "なし" above and, below, the map, the list of
    /// squares, the party member good at it and the trial of the chosen square's event; the list
    /// (of maps or of squares) down the left side under the back button; and the chosen square's
    /// settings down the right side under the map's name and runes.
    /// </summary>
    public static class WorldEventLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/WorldMap/UI/WorldEventLabPanel.prefab";

        // 左下の隅（「ホームへ」と、歩いている間の止めるボタン）の右から並べる。
        private const float Left = 176f;
        private const float Bottom = 24f;
        private const float ControlWidth = 380f;

        // 試す・コピーのボタンの幅。下の下の段が右端（設定の枠の下）からはみ出さない幅にする。
        private const float ActionWidth = 240f;
        private const float Margin = 24f;
        private const float ListWidth = 480f;
        private const float InspectorWidth = 640f;

        // 左の一覧は、左上のもどる（24＋128）の下から。右の設定は、地図の名前と持っているルーン（上から約220）の下から。
        private const float ListTop = 176f;
        private const float InspectorTop = 236f;

        private static readonly Color Field = new(0.98f, 0.96f, 0.9f, 0.95f);
        private static readonly Color Hint = new(0.45f, 0.42f, 0.38f, 1f);

        [MenuItem("Baryonyx/World Map/Create Event Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();
            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            using (UiBuild.Begin(font, UiArt.EnsureTextShadow(font)))
            {
                var root = CanvasRoot("WorldEventLabPanel");
                // 地図の上に重ねる。出来事の窓が開いている間は、自分で隠れる。
                root.GetComponent<Canvas>().sortingOrder = 10;
                var lab = root.gameObject.AddComponent<WorldEventLab>();
                var safe = SafeArea(root);
                BuildRows(safe, lab);
                BuildList(safe, lab);
                BuildInspector(safe, lab);
                FillMaps(lab);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        private static float PanelBottom =>
            Bottom + EnemyLabAssets.RowHeight * 2f + EnemyLabAssets.RowGap + 16f;

        private static void BuildRows(RectTransform safe, WorldEventLab lab)
        {
            float rowHeight = EnemyLabAssets.RowHeight;
            float gap = EnemyLabAssets.RowGap;
            var rows = Rect("Rows", safe);
            rows.anchorMin = Vector2.zero;
            rows.anchorMax = new Vector2(1f, 0f);
            rows.pivot = new Vector2(0.5f, 0f);
            rows.offsetMin = new Vector2(Left, Bottom);
            rows.offsetMax = new Vector2(-Margin, Bottom + rowHeight * 2f + gap);
            lab.Group = rows.gameObject.AddComponent<CanvasGroup>();

            var events = EnemyLabAssets.Row(rows, "Events", rowHeight + gap);
            events.offsetMin = new Vector2(0f, events.offsetMin.y);
            events.offsetMax = new Vector2(0f, events.offsetMax.y);
            var buttons = new Button[WorldEventLab.Entries.Length];
            for (int i = 0; i < buttons.Length; i++)
                buttons[i] = EnemyLabAssets.SharedWidthButton(
                    events,
                    "Event" + i,
                    WorldEventLab.Entries[i].Name
                );
            lab.Buttons = buttons;
            lab.NoneButton = EnemyLabAssets.SharedWidthButton(events, "None", "なし");

            var controls = EnemyLabAssets.Row(rows, "Controls", 0f);
            controls.offsetMin = new Vector2(0f, controls.offsetMin.y);
            controls.offsetMax = new Vector2(0f, controls.offsetMax.y);
            controls.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            (lab.MapButton, lab.MapLabel) = EnemyLabAssets.ControlButton(
                controls,
                "Map",
                WorldEventLab.MapText("世界地図"),
                ControlWidth
            );
            lab.ListButton = EnemyLabAssets
                .ControlButton(controls, "List", "マスの一覧", ControlWidth)
                .button;
            (lab.HelpButton, lab.HelpLabel) = EnemyLabAssets.ControlButton(
                controls,
                "Help",
                WorldEventLab.HelpText(true),
                ControlWidth
            );
            lab.TryButton = EnemyLabAssets.ControlButton(controls, "Try", "試す", ActionWidth).button;
            // 端末で変えたマスをJSONにしてクリップボードへ写す（AIに貼って配置へ反映する）。
            lab.CopyButton = EnemyLabAssets
                .ControlButton(controls, "Copy", "コピー", ActionWidth)
                .button;
        }

        // 左の縦に長い枠に、地図かマスの行を並べ、上下にスクロールする。行は Play 中に見本から作る。
        private static void BuildList(RectTransform safe, WorldEventLab lab)
        {
            var panel = Rect("List", safe);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.offsetMin = new Vector2(Margin, PanelBottom);
            panel.offsetMax = new Vector2(Margin + ListWidth, -ListTop);
            lab.ListGroup = HiddenGroup(panel);
            GlassCardAssets.Plaque(panel, pattern: false, fill: EnemyLabAssets.StripColor);

            var viewport = GuidePanelLayout.Viewport(panel, Vector2.one * 12f, -Vector2.one * 12f);
            var content = GuidePanelLayout.Column(viewport, "Rows", EnemyLabAssets.RowGap);
            GuidePanelLayout.Scroll(panel, viewport, content);
            lab.ListContent = content;

            var (row, label) = EnemyLabAssets.ControlButton(content, "Row", "", 0f);
            row.GetComponentInParent<LayoutElement>().preferredHeight = EnemyLabAssets.RowHeight;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
            lab.RowTemplate = row;
        }

        // 右の枠に、選んだマスの題と、戦闘の設定（敵の言葉・Lv・強さと、そこから決まる敵）を縦に置く。
        // 戦闘でないマスでは、戦闘の設定の代わりに、これから足す設定の一覧を出す。
        private static void BuildInspector(RectTransform safe, WorldEventLab lab)
        {
            var panel = Rect("Inspector", safe);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(-Margin - InspectorWidth, PanelBottom);
            panel.offsetMax = new Vector2(-Margin, -InspectorTop);
            lab.InspectorGroup = HiddenGroup(panel);
            GlassCardAssets.Plaque(panel, pattern: false, fill: EnemyLabAssets.StripColor);

            lab.InspectorTitle = Label(
                panel,
                "Title",
                "",
                38,
                EnemyLabAssets.Gold,
                TextAlignmentOptions.MidlineLeft
            );
            Top(lab.InspectorTitle.rectTransform, 20f, 56f);

            var battle = Rect("Battle", panel);
            Top(battle, 84f, 300f);
            lab.BattleGroup = HiddenGroup(battle);
            Caption(battle, "敵", 0f);
            lab.EnemiesInput = Input(battle, "Enemies", 0f, 110f, "ランダム／苔スライム、ランダム");
            Caption(battle, "Lv", 92f);
            lab.LevelInput = Input(battle, "Level", 92f, 110f, "", 150f);
            lab.LevelInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            var strength = Rect("Strength", battle);
            strength.anchorMin = strength.anchorMax = strength.pivot = new Vector2(0f, 1f);
            strength.anchoredPosition = new Vector2(290f, -92f);
            strength.sizeDelta = new Vector2(300f, 76f);
            (lab.StrengthButton, lab.StrengthLabel) = EnemyLabAssets.ControlButton(
                strength,
                "Button",
                WorldEventLab.StrengthText(WorldBattleStrength.Pack),
                0f
            );
            Stretch((RectTransform)lab.StrengthButton.transform);
            lab.Preview = Label(
                battle,
                "Preview",
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft,
                false
            );
            lab.Preview.textWrappingMode = TextWrappingModes.Normal;
            Top(lab.Preview.rectTransform, 184f, 110f, 24f);

            lab.Notes = Label(
                panel,
                "Notes",
                "",
                22,
                new Color(0.8f, 0.8f, 0.82f, 1f),
                TextAlignmentOptions.BottomLeft,
                false
            );
            lab.Notes.textWrappingMode = TextWrappingModes.Normal;
            var notes = lab.Notes.rectTransform;
            notes.anchorMin = Vector2.zero;
            notes.anchorMax = new Vector2(1f, 0f);
            notes.pivot = new Vector2(0.5f, 0f);
            notes.offsetMin = new Vector2(24f, 16f);
            notes.offsetMax = new Vector2(-24f, 16f + 380f);
        }

        // 枠の上の端から top だけ下に、左右いっぱい（inset の余白）に置く。
        private static void Top(RectTransform rect, float top, float height, float inset = 24f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(inset, -top - height);
            rect.offsetMax = new Vector2(-inset, -top);
        }

        private static void Caption(RectTransform parent, string text, float top)
        {
            var label = Label(
                parent,
                "Caption",
                text,
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -top);
            rect.sizeDelta = new Vector2(80f, 76f);
        }

        // 1行の文字を書く欄（生成りの地に濃い文字、空のときは薄い見本の文字）。
        private static TMP_InputField Input(
            RectTransform parent,
            string name,
            float top,
            float left,
            string hint,
            float width = 0f
        )
        {
            // width が0なら、右の端まで伸ばす。
            var rect = Rect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = width > 0f ? new Vector2(0f, 1f) : Vector2.one;
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, -top - 76f);
            rect.offsetMax = width > 0f ? new Vector2(left + width, -top) : new Vector2(-24f, -top);
            var plate = Sliced(rect, UiArt.CapsulePath, Field);
            plate.pixelsPerUnitMultiplier = 2.4f;

            var area = Rect("Text Area", rect);
            Stretch(area);
            area.offsetMin = new Vector2(20f, 6f);
            area.offsetMax = new Vector2(-20f, -6f);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Label(
                area,
                "Placeholder",
                hint,
                30,
                Hint,
                TextAlignmentOptions.MidlineLeft,
                false
            );
            Stretch(placeholder.rectTransform);
            var text = Label(
                area,
                "Text",
                "",
                30,
                WorldMapEventWindowAssets.Ink,
                TextAlignmentOptions.MidlineLeft,
                false
            );
            Stretch(text.rectTransform);

            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = plate;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = text.font;
            input.pointSize = 30;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = WorldMapEventWindowAssets.Ink;
            input.customCaretColor = true;
            return input;
        }

        // 地図の一覧：世界地図と、地域のエリアマップ（配置があるもの）。
        private static void FillMaps(WorldEventLab lab)
        {
            var keys = new System.Collections.Generic.List<string>();
            var names = new System.Collections.Generic.List<string>();
            foreach (string key in WorldAtlasAssets.Keys)
            {
                string path = $"{WorldAtlasAssets.LayoutFolder}/{key}.json";
                if (!File.Exists(path))
                    continue;
                keys.Add(key);
                names.Add(WorldAtlasLayout.Parse(File.ReadAllText(path)).name);
            }
            lab.MapKeys = keys.ToArray();
            lab.MapNames = names.ToArray();
        }
    }
}
