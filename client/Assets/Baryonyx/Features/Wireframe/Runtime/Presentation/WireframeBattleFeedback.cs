using System.Collections.Generic;
using Baryonyx.Combat;
using TMPro;
using UnityEngine;

namespace Baryonyx.Wireframe
{
    public sealed class WireframeBattleFeedback : MonoBehaviour
    {
        private readonly RectTransform[] actors = new RectTransform[7];
        private readonly Vector2[] origins = new Vector2[7];
        private readonly TMP_Text[] numbers = new TMP_Text[7];
        private readonly float[] remaining = new float[7];
        private WireframeView view;
        private CombatEncounter battle;

        public void Initialize(
            Dictionary<string, UnityEngine.UI.RawImage> images,
            WireframeView screen
        )
        {
            view = screen;
            for (int i = 0; i < 7; i++)
            {
                actors[i] = images[
                    i < 4 ? "BattleAllyArt" + i : "BattleEnemyArt" + (i - 4)
                ].rectTransform;
                origins[i] = actors[i].anchoredPosition;
                var parent = view.Button(i < 4 ? "Ally" + i : "Enemy" + (i - 4)).transform;
                var go = new GameObject(
                    "DamageFeedback" + i,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI)
                );
                go.transform.SetParent(parent, false);
                var label = go.GetComponent<TextMeshProUGUI>();
                label.font = screen.Text("Title").font;
                label.fontSize = 20;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0, .45f);
                rect.anchorMax = new Vector2(1, .85f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                go.AddComponent<UnityEngine.UI.Shadow>().effectColor = new Color(0, 0, 0, .75f);
                numbers[i] = label;
                go.SetActive(false);
            }
        }

        public void Bind(CombatEncounter value)
        {
            if (battle == value)
                return;
            if (battle != null)
                battle.Hit -= OnHit;
            battle = value;
            if (battle != null)
                battle.Hit += OnHit;
            for (int i = 0; i < 7; i++)
                remaining[i] = 0;
        }

        private void OnHit(CombatHit hit)
        {
            int index = hit.Enemy ? hit.Index + 4 : hit.Index;
            remaining[index] = hit.Downed ? 1.2f : .65f;
            numbers[index].text = hit.Downed ? "ダウン！" : (hit.Healing ? "+" : "") + hit.Amount;
            numbers[index].color =
                hit.Downed ? new Color(1, .87f, .45f)
                : hit.Healing ? new Color(.64f, 1, .67f)
                : Color.white;
        }

        private void LateUpdate()
        {
            if (view == null || view.Session.Screen != WireScreen.Battle)
                return;
            for (int i = 0; i < 7; i++)
            {
                remaining[i] = Mathf.Max(0, remaining[i] - Time.unscaledDeltaTime);
                numbers[i].gameObject.SetActive(remaining[i] > 0);
                float sway = Mathf.Sin(Time.unscaledTime * 2 + i) * 1.2f;
                float impact = remaining[i] > .45f ? Mathf.Sin(remaining[i] * 80) * 3 : 0;
                actors[i].anchoredPosition = origins[i] + new Vector2(impact, sway);
                bool down = i >= 4 && i - 4 < battle.Enemies.Length && battle.Enemies[i - 4].IsDown;
                actors[i].localRotation = Quaternion.Euler(0, 0, down ? -12 : 0);
            }
        }

        private void OnDestroy()
        {
            if (battle != null)
                battle.Hit -= OnHit;
        }
    }
}
