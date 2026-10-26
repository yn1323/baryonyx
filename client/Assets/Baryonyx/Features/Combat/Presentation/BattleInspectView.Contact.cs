using System;
using System.Collections;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The moment a blow meets its target, and what is read there
    /// (doc/features/screens/battle-effects.md, 当たりの瞬間と読みやすさ): the hit (its number, its
    /// words and the white blink) shows as the effect's body reaches the target, not as the blow's
    /// damage is worked out; an enemy draws back and dashes in to strike; the words and numbers
    /// keep off the HP bars of the others and are outlined dark, and the HP bars and weakness
    /// icons are drawn over the effects in front.
    /// </summary>
    public sealed partial class BattleInspectView
    {
        // How long (real time) the hit being landed now waits to show: its popups, its blink and
        // its weakness turning. Set only while a blow lands (AtContact, Strike).
        private float showDelay;

        // An enemy's dash at an ally: it draws back, rushes in part of the way, holds a beat at
        // the blow (through the hit stop) and goes back to where it stood.
        private const float LungeWindUp = 0.1f;
        private const float LungeDash = 0.07f;
        private const float LungeHold = 0.06f;
        private const float LungeReturn = 0.18f;
        private const float LungePull = 28f;
        private const float LungeReach = 320f;

        // A spell is cast with a lean, not a dash.
        private const float LungeSpell = 0.3f;

        // The wait after an enemy's blow before it steps back (the party's is ActionHold).
        private const float EnemyActionHold = 0.15f;

        // Room left between a popup and the HP bar or weakness icons of another character above it.
        private const float PopupBarMargin = 6f;

        // The dark outline and the shadow under the words that pop up (状態・ブロック・会心), in the
        // units of the font's distance field, as the numbers have (DamageNumberAssets).
        private const float PopupOutline = 0.3f;
        private static readonly Color PopupEdge = new(0.08f, 0.06f, 0.07f, 1f);

        // An outlined copy of the popups' font material, made on start.
        private Material popupMaterial;

        // Where the enemy dashing now stood before it drew back.
        private Vector2 lungeHome;

        /// <summary>
        /// A card's land, with its hit shown <see cref="BattleSkillVfx.ContactLag"/> later, as
        /// the effect's body (spawned as it lands) rises to its shape.
        /// </summary>
        private Func<int, BattleHitWeight> AtContact(Func<int, BattleHitWeight> land) =>
            index =>
            {
                showDelay = BattleSkillVfx.ContactLag;
                try
                {
                    return land(index);
                }
                finally
                {
                    showDelay = 0f;
                }
            };

        /// <summary>Runs <paramref name="routine"/> after <paramref name="delay"/> s of real time.</summary>
        private static IEnumerator Later(float delay, IEnumerator routine)
        {
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime)
                yield return null;
            yield return routine;
        }

        private static IEnumerator Then(Action action)
        {
            action();
            yield break;
        }

        /// <summary>
        /// Draws an enemy back and rushes it part of the way at an ally (less for a
        /// <paramref name="spell"/>, cast with a lean), leaving it there for the blow;
        /// <see cref="LungeBack"/> takes it home. Its time stops in a hit stop, as the characters do.
        /// </summary>
        private IEnumerator Lunge(int enemy, int ally, bool spell)
        {
            var body = Enemies[enemy].Body;
            lungeHome = body.anchoredPosition;
            var parent = body.parent as RectTransform;
            var area = Allies[ally].TargetArea;
            var self = Enemies[enemy].TargetArea;
            if (parent == null || area == null || self == null)
                yield break;
            Vector2 way =
                parent.InverseTransformPoint(area.TransformPoint(area.rect.center))
                - parent.InverseTransformPoint(self.TransformPoint(self.rect.center));
            float distance = way.magnitude;
            if (distance < 1f)
                yield break;
            var direction = way / distance;
            float scale = spell ? LungeSpell : 1f;
            var pulled = -direction * (LungePull * scale);
            var reached = direction * (Mathf.Min(LungeReach, distance * 0.4f) * scale);
            for (float t = 0f; t < LungeWindUp; t += Time.deltaTime)
            {
                float k = t / LungeWindUp;
                body.anchoredPosition = lungeHome + ToDots(pulled * (1f - (1f - k) * (1f - k)));
                yield return null;
            }
            if (Vfx != null && !spell)
                Vfx.Rush(self, direction);
            for (float t = 0f; t < LungeDash; t += Time.deltaTime)
            {
                float k = t / LungeDash;
                body.anchoredPosition = lungeHome + ToDots(Vector2.Lerp(pulled, reached, k * k));
                yield return null;
            }
            body.anchoredPosition = lungeHome + ToDots(reached);
        }

        /// <summary>The enemy holds a beat after its blow, then goes back to where it stood.</summary>
        private IEnumerator LungeBack(int enemy)
        {
            var body = Enemies[enemy].Body;
            var from = body.anchoredPosition;
            for (float t = 0f; t < LungeHold; t += Time.deltaTime)
                yield return null;
            for (float t = 0f; t < LungeReturn; t += Time.deltaTime)
            {
                float k = t / LungeReturn;
                body.anchoredPosition = Vector2.Lerp(from, lungeHome, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            body.anchoredPosition = lungeHome;
        }

        /// <summary>
        /// Moves a popup at <paramref name="start"/> (its middle, in <paramref name="parent"/>'s
        /// space) over a character's <paramref name="anchor"/> so that, risen by
        /// <paramref name="rise"/>, it stays under the HP bar and weakness icons of any other
        /// character above it: it rises less, or starts lower. Returns how far it moves down.
        /// </summary>
        private float KeepUnderBars(
            RectTransform anchor,
            RectTransform parent,
            Vector2 start,
            float halfWidth,
            float height,
            ref float rise
        )
        {
            if (!IsCharacter(anchor))
                return 0f;
            float ceiling = float.MaxValue;
            void Check(RectTransform bar)
            {
                if (bar == null || !bar.gameObject.activeInHierarchy)
                    return;
                var rect = bar.rect;
                var camera = CameraOf(parent);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    ScreenPointOf(bar, rect.min),
                    camera,
                    out var low
                );
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    ScreenPointOf(bar, rect.max),
                    camera,
                    out var high
                );
                bool across = low.x < start.x + halfWidth && high.x > start.x - halfWidth;
                bool above = low.y > start.y - height * 0.5f;
                if (across && above)
                    ceiling = Mathf.Min(ceiling, low.y);
            }
            foreach (var enemy in Enemies)
                if (enemy.Alive && enemy.TargetArea != anchor && enemy.Gauge != null)
                    Check(InfoOf(enemy));
            foreach (var ally in Allies)
                if (!ally.Absent && ally.TargetArea != anchor && ally.Gauge != null)
                    Check(ally.HpBar);
            if (ceiling == float.MaxValue)
                return 0f;
            float top = ceiling - PopupBarMargin - height * 0.5f;
            if (start.y + rise <= top)
                return 0f;
            float room = top - start.y;
            if (room >= 0f)
            {
                rise = room;
                return 0f;
            }
            rise = 0f;
            return -room;
        }

        private bool IsCharacter(RectTransform anchor)
        {
            if (anchor == null)
                return false;
            foreach (var enemy in Enemies)
                if (enemy.TargetArea == anchor)
                    return true;
            foreach (var ally in Allies)
                if (ally.TargetArea == anchor)
                    return true;
            return false;
        }

        // An enemy's HP bar with the row of weakness icons over it (both under "Info").
        private static RectTransform InfoOf(BattleInspectEnemy enemy)
        {
            var gauge = (RectTransform)enemy.Gauge.transform;
            return gauge.parent is RectTransform info && info.name == "Info" ? info : gauge;
        }

        /// <summary>
        /// Makes what the battle must keep readable over the effects: the popups' words get a dark
        /// outline and shadow, and the HP bars and weakness icons are drawn over the effects in
        /// front of the characters (a canvas of their own above it).
        /// </summary>
        private void SetUpReadability()
        {
            if (PopupTemplate != null && PopupTemplate.fontSharedMaterial != null)
            {
                popupMaterial = new Material(PopupTemplate.fontSharedMaterial)
                {
                    name = "BattlePopup (outlined)",
                };
                popupMaterial.EnableKeyword("OUTLINE_ON");
                popupMaterial.EnableKeyword("UNDERLAY_ON");
                // The face grows by the outline's width, so the whole outline lies outside it.
                popupMaterial.SetFloat("_FaceDilate", PopupOutline);
                popupMaterial.SetColor("_OutlineColor", PopupEdge);
                popupMaterial.SetFloat("_OutlineWidth", PopupOutline);
                popupMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.7f));
                popupMaterial.SetFloat("_UnderlayOffsetX", 0f);
                popupMaterial.SetFloat("_UnderlayOffsetY", -0.3f);
                popupMaterial.SetFloat("_UnderlayDilate", PopupOutline);
                popupMaterial.SetFloat("_UnderlaySoftness", 0.3f);
                PopupTemplate.fontSharedMaterial = popupMaterial;
            }

            var front =
                Vfx != null && Vfx.FrontLayer != null
                    ? Vfx.FrontLayer.GetComponent<Canvas>()
                    : null;
            if (front == null || !front.overrideSorting)
                return;
            void Lift(RectTransform part)
            {
                if (part == null)
                    return;
                var canvas = part.GetComponent<Canvas>();
                if (canvas == null)
                    canvas = part.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingLayerID = front.sortingLayerID;
                canvas.sortingOrder = front.sortingOrder + 1;
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            }
            foreach (var enemy in Enemies)
                if (enemy.Gauge != null)
                    Lift(InfoOf(enemy));
            foreach (var ally in Allies)
                if (ally.Gauge != null)
                    Lift(ally.HpBar);
        }
    }
}
