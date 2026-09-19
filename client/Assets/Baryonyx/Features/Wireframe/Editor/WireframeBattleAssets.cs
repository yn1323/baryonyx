using TMPro;
using UnityEngine;

namespace Baryonyx.Wireframe.Editor
{
    public static partial class WireframeScreenAssets
    {
        private static void BuildBattle(RectTransform page, WireframeLayout layout)
        {
            Card("BattleWarning", page, "敵の予告", 42, 12);
            var field = Rect("Battlefield", page);
            var fieldSize = field.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            fieldSize.minHeight = 290;
            var view = layout.GetComponent<WireframeView>();
            fieldSize.flexibleHeight = 1;
            Image(field, new Color(.84f, .86f, .87f), false);
            for (int i = 0; i < 4; i++)
            {
                var ally = Button("Ally" + i, field, "味方", 54, 13);
                var rect = (RectTransform)ally.transform;
                float x = i % 2 == 0 ? .04f : .23f;
                float y = i < 2 ? .47f : .09f;
                rect.anchorMin = new Vector2(x, y);
                rect.anchorMax = new Vector2(x + .25f, y + .30f);
                rect.offsetMin = new Vector2(4, 2);
                rect.offsetMax = new Vector2(-4, -2);
            }
            for (int i = 0; i < 3; i++)
            {
                var enemy = Button("Enemy" + i, field, "敵", 54, 14, true);
                var rect = (RectTransform)enemy.transform;
                rect.anchorMin = new Vector2(.56f, .69f - i * .32f);
                rect.anchorMax = new Vector2(.98f, .98f - i * .32f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                view.HpFills[i + 4] = HealthBar(enemy);
            }
            var hp = Rect("HpGrid", page);
            layout.HpGrid = hp.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            layout.HpGrid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            layout.HpGrid.constraintCount = 2;
            layout.HpGrid.spacing = new Vector2(8, 8);
            layout.HpGrid.cellSize = new Vector2(80, 52);
            layout.HpSize = hp.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.HpSize.minHeight = layout.HpSize.preferredHeight = 52;
            for (int i = 0; i < 4; i++)
                view.HpFills[i] = HealthBar(Button("Hp" + i, hp, "HP", 52, 11));
            var selection = Row(page, "BattleSelectionRow");
            var selectionText = Label("BattleSelection", selection, "キャラを選択", 12, 50);
            selectionText.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
            Button("UseSkill", selection, "使う", 50, 15, true, 62);
            var skills = Row(page, "Skills");
            for (int i = 0; i < 3; i++)
                Button("Skill" + i, skills, "スキル", 54, 13, true);
        }

        private static RectTransform HealthBar(UnityEngine.UI.Button button)
        {
            var track = Rect(button.name + "Track", button.transform);
            track.anchorMin = Vector2.zero;
            track.anchorMax = new Vector2(1, 0);
            track.pivot = new Vector2(.5f, 0);
            track.sizeDelta = new Vector2(-16, 5);
            track.anchoredPosition = new Vector2(0, 6);
            Image(track, new Color(.035f, .065f, .07f), false);
            var fill = Rect(button.name + "Fill", track);
            Stretch(fill);
            Image(fill, new Color(.42f, .68f, .49f), false);
            ((RectTransform)button.GetComponentInChildren<TMP_Text>().transform).offsetMin +=
                new Vector2(0, 9);
            return fill;
        }
    }
}
