using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Inventory;
using Baryonyx.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The effects of the battle's turning points (doc/features/screens/debug-rooms.md, the battle moment
    /// lab), drawn with the same shapes as the skills: an enemy landing on the field as the
    /// battle opens, the party's light when it wins, and a drop, a ball of light that falls from
    /// the beaten enemy, bounces on the floor, gathers itself and bursts into a pillar of light
    /// whose size and colour tell the quality of what it holds.
    /// </summary>
    public sealed partial class BattleSkillVfx
    {
        // The dust an enemy raises as it lands, a dry grey-brown that reads on grass and stone.
        private static readonly Color ArrivalDust = new(0.5f, 0.46f, 0.4f, 0.5f);

        // The warm light of a win.
        private static readonly Color CheerGold = new(1f, 0.86f, 0.46f);

        // How a drop falls: the arc from the enemy to the floor and the two bounces after.
        public const float DropFlight = 0.5f;
        private const float DropBounce = 0.22f;
        private const float DropHop = 0.13f;

        // How high a drop lifts off the floor as it gathers itself.
        private const float DropLift = 36f;

        /// <summary>
        /// How long a drop of <paramref name="quality"/> takes from the enemy to its burst: the
        /// fall and bounces, then a gathering that grows with the quality, so a better drop keeps
        /// the player waiting a little longer.
        /// </summary>
        public static float DropReveal(ItemQuality quality) =>
            DropFlight + DropBounce + DropHop + DropCharge(quality);

        private static float DropCharge(ItemQuality quality) =>
            quality switch
            {
                ItemQuality.S => 0.75f,
                ItemQuality.A => 0.45f,
                ItemQuality.B => 0.2f,
                _ => 0.08f,
            };

        /// <summary>
        /// An enemy landing on the field: dust kicked up at its feet behind it and a little in
        /// front, dust driven out along the floor with a little grit, and a small knock of the
        /// stage. A <paramref name="boss"/> lands heavily: more dust driven further, a red glow
        /// rising behind it and a harder knock.
        /// </summary>
        public void Arrive(RectTransform target, bool boss)
        {
            if (FrontLayer == null || target == null)
                return;
            var feet = Feet(FrontLayer, target);
            float width = Mathf.Max(80f, target.rect.width);
            Puffs(backSmoke, feet, ArrivalDust, boss ? 10 : 5, width * 0.45f, boss ? 1.1f : 0.7f);
            Puffs(
                frontSmoke,
                feet + Vector2.down * 8f,
                ArrivalDust,
                boss ? 5 : 2,
                width * 0.35f,
                boss ? 0.9f : 0.6f,
                90f
            );
            // The weight landing drives dust out along the floor and flicks up grit, as a real
            // landing does; no ring of light, which a landing does not make.
            Shockwave(feet, boss ? width * 2.6f : width * 1.6f, boss ? 0.9f : 0.3f);
            if (!boss)
            {
                Shake(0.12f, Vector2.down);
                return;
            }
            var red = new Color(1f, 0.3f, 0.22f);
            Glow(backLight, Center(FrontLayer, target), red, width * 2.4f, 1.1f, 0.45f);
            Pool(feet, red, width * 2f, 0.2f, 0.9f);
            Shake(0.5f, Vector2.down);
            HitStop(0.06f);
        }

        /// <summary>
        /// The light of a win on the party: a warm pool under each ally and a light on them, and
        /// sparkles that rise from among them and drift away. No shake nor flash: the fight is over.
        /// </summary>
        public void Cheer(IReadOnlyList<RectTransform> allies)
        {
            if (FrontLayer == null || allies == null || allies.Count == 0)
                return;
            var middle = Vector2.zero;
            foreach (var ally in allies)
            {
                var feet = Feet(FrontLayer, ally);
                middle += Center(FrontLayer, ally) / allies.Count;
                Pool(feet, CheerGold, 300f, 0.5f, 0.9f);
                Illuminate(Center(FrontLayer, ally), CheerGold, 0.55f, 300f, 0.5f, 0.9f);
            }
            Ring(backLight, Feet(FrontLayer, allies[0]), CheerGold, 640f, 0.7f, floor: true);
            Spray(
                frontLight,
                middle,
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(CheerGold.r, CheerGold.g, CheerGold.b, 0f),
                    Speed = new Vector2(60f, 220f),
                    Direction = 90f,
                    Spread = 120f,
                    Life = new Vector2(0.8f, 1.5f),
                    Size = new Vector2(14f, 34f),
                    Shrink = 0.2f,
                    Gravity = -60f,
                    Drag = 1.2f,
                    Spin = 140f,
                    Area = 160f,
                    Delay = new Vector2(0f, 0.5f),
                },
                28
            );
        }

        /// <summary>
        /// The fallen party standing up again on a revive paid with runes: under each ally a
        /// pillar of the runes' pale blue rises and is eaten away from its top, with a ring and a
        /// pool along the floor, a light on the ally and motes drifting up. Gentle, like a heal.
        /// </summary>
        public void Rise(IReadOnlyList<RectTransform> allies)
        {
            if (FrontLayer == null || allies == null)
                return;
            var rune = new Color(0.55f, 0.86f, 1f);
            foreach (var ally in allies)
            {
                var feet = Feet(FrontLayer, ally);
                var at = Center(FrontLayer, ally);
                DropPillar(feet, rune, 220f, 460f, 1.1f);
                Ring(backLight, feet, rune, 340f, 0.7f, floor: true);
                Pool(feet, rune, 340f, 0.5f, 0.7f);
                Illuminate(at, rune, 0.6f, 280f, 0.5f, 0.7f);
                Spray(
                    frontLight,
                    feet + Vector2.up * 30f,
                    new Burst
                    {
                        Shape = BattleVfxShape.Sparkle,
                        Bright = CoreBright,
                        From = Color.white,
                        To = new Color(rune.r, rune.g, rune.b, 0f),
                        Speed = new Vector2(40f, 160f),
                        Direction = 90f,
                        Spread = 40f,
                        Life = new Vector2(0.6f, 1.1f),
                        Size = new Vector2(12f, 28f),
                        Shrink = 0.2f,
                        Gravity = -120f,
                        Spin = 160f,
                        Area = 50f,
                    },
                    10
                );
            }
        }

        /// <summary>
        /// A drop from the enemy at <paramref name="from"/>, in three beats. The wind-up: a ball of
        /// light leaps out of the enemy, arcs down to the floor in front of it trailing sparks,
        /// bounces twice, and gathers itself (longer for a better drop; at A and S the stage
        /// darkens and motes of light are drawn into it). The burst: it flashes white and opens
        /// into the quality's colour, a pillar of light (from B), rays, rings along the floor and
        /// a light on everyone near; S also flashes the screen softly and knocks the stage.
        /// <paramref name="burst"/> is called at that moment, to show what was found. The fall-off:
        /// the pillar is eaten away from the top, sparkles fall, and the dark lifts. Ends when the
        /// last light is gone.
        /// </summary>
        public IEnumerator Drop(RectTransform from, ItemQuality quality, Action burst)
        {
            if (FrontLayer == null || from == null)
            {
                burst?.Invoke();
                yield break;
            }
            int grade = Quality.Grade(quality);
            var color = QualityPalette.Of(quality);
            var start = Center(FrontLayer, from);
            // In front of the enemy, toward the party, so the light is not behind its body, and
            // in from the edge, so the pillar is not cut off by it.
            var land = Inside(Feet(FrontLayer, from) + new Vector2(-70f, -10f), 640f);
            float charge = DropCharge(quality);
            float reveal = DropReveal(quality);
            const float fade = 0.35f;
            float size = 56f + 10f * grade;

            var orb = Spawn(frontLight, BattleVfxShape.Glow, start, Vector2.one * size, color);
            var core = Spawn(
                frontLight,
                BattleVfxShape.Spark,
                start,
                Vector2.one * size * 0.4f,
                Color.white
            );
            Animate(
                orb,
                reveal + fade,
                (s, k) =>
                {
                    float t = k * (reveal + fade);
                    // Gathering, it lifts off the floor, beats faster and swells as the burst comes.
                    float c = Mathf.Clamp01((t - (reveal - charge)) / Mathf.Max(0.01f, charge));
                    s.Holder.anchoredPosition =
                        DropPath(start, land, t) + Vector2.up * (DropLift * EaseOut(c));
                    float swell = 1f;
                    float alpha = 0.9f;
                    if (t > reveal - charge && t < reveal)
                        swell = 1f + 0.35f * c + 0.12f * Mathf.Sin(c * c * 40f);
                    else if (t >= reveal)
                    {
                        float f = (t - reveal) / fade;
                        swell = 1.6f - 0.6f * f;
                        alpha = 0.9f * (1f - f);
                    }
                    s.Holder.localScale = Vector3.one * swell;
                    var tint = Color.Lerp(Color.white, color, 0.55f);
                    tint.a = alpha;
                    Show(s, tint, k, 0f, CoreBright);
                }
            );
            // The white-hot middle rides with the ball until it bursts.
            Animate(
                core,
                reveal,
                (s, k) =>
                {
                    s.Holder.anchoredPosition = orb.Holder.anchoredPosition;
                    s.Holder.localScale = orb.Holder.localScale;
                    Show(s, Color.white, k, 0f, CoreBright);
                }
            );

            // The leap and the fall, trailing sparks of light.
            float flown = 0f;
            float lastSpark = 0f;
            while (flown < DropFlight + DropBounce + DropHop)
            {
                flown += Time.deltaTime;
                if (flown - lastSpark >= 0.035f && flown < DropFlight)
                {
                    lastSpark = flown;
                    Spray(
                        frontLight,
                        orb.Holder.anchoredPosition,
                        new Burst
                        {
                            Shape = BattleVfxShape.Sparkle,
                            Bright = CoreBright,
                            From = Color.white,
                            To = new Color(color.r, color.g, color.b, 0f),
                            Speed = new Vector2(10f, 40f),
                            Direction = 90f,
                            Spread = 360f,
                            Life = new Vector2(0.25f, 0.45f),
                            Size = new Vector2(10f, 18f) + Vector2.one * (3f * grade),
                            Shrink = 0.2f,
                            Gravity = 30f,
                            Spin = 200f,
                            Area = 6f,
                        },
                        1
                    );
                }
                if (flown >= DropFlight && flown - Time.deltaTime < DropFlight)
                    Land(land, color, size * 3f, 0.4f);
                else if (
                    flown >= DropFlight + DropBounce
                    && flown - Time.deltaTime < DropFlight + DropBounce
                )
                    Land(land, color, size * 2f, 0.25f);
                yield return null;
            }

            // The gathering.
            if (grade >= 1)
                Gather(
                    land + Vector2.up * DropLift,
                    color,
                    4 + 5 * grade,
                    160f + 50f * grade,
                    charge
                );
            if (grade >= 2)
            {
                // A magic circle opens on the floor under it and the stage darkens round it.
                DimTo(grade == 3 ? 0.55f : 0.35f, charge * 0.6f);
                Circle(land, color, charge + 0.45f);
            }
            yield return Wait(charge);

            // The burst.
            burst?.Invoke();
            Open(land, color, grade);
            yield return Wait(fade + 0.2f);
            DimTo(0f, 0.6f);
            yield return Wait(grade >= 2 ? 1.2f : 0.7f);
        }

        // How many of the enemies' dots one dot of the battle's chest is.
        private const float ChestDots = 1.6f;

        // The battle's chest, made the first time one is opened and used again after.
        private RawImage chest;

        /// <summary>
        /// How long a chest of <paramref name="quality"/> trembles before it opens: longer for a
        /// better one, as the exploration's chest.
        /// </summary>
        public static float ChestWait(ItemQuality quality) =>
            quality switch
            {
                ItemQuality.S => 1.1f,
                ItemQuality.A => 0.75f,
                ItemQuality.B => 0.5f,
                _ => 0.35f,
            };

        /// <summary>
        /// What the battle left, as a chest on the floor where <paramref name="enemy"/> (the last
        /// enemy beaten) stood, drawn in its dots (<paramref name="closed"/>, then
        /// <paramref name="open"/>). Nothing falls: the chest wells up out of a soft light, then
        /// trembles harder and harder as the quality's light leaks out of it (longer for a better
        /// one; at A and S a circle opens under it), and flies open in the drop's burst
        /// (<see cref="Open"/>), when <paramref name="burst"/> is called to show everything that
        /// was in it together. It stays open while the light falls off, then fades. The chest
        /// stands among the characters (beside the enemy's picture), not on the effects' layers,
        /// so the stage's depth and darkening treat it as one of them. Ends when the last light
        /// is gone.
        /// </summary>
        public IEnumerator Chest(
            RawImage enemy,
            ItemQuality quality,
            Texture closed,
            Texture open,
            Action burst
        )
        {
            // The layer the characters stand on: the enemy's picture is in its pose, in its body.
            var world = enemy != null ? enemy.rectTransform.parent : null;
            for (int up = 0; up < 2 && world != null; up++)
                world = world.parent;
            if (FrontLayer == null || world == null || closed == null)
            {
                burst?.Invoke();
                yield break;
            }
            int grade = Quality.Grade(quality);
            var color = QualityPalette.Of(quality);
            var area = enemy.rectTransform;
            var land = Inside(Feet(FrontLayer, area) + new Vector2(-70f, -10f), 640f);
            // One dot of the chest is a little more than one of the enemies', so it reads on the floor.
            var corners = new Vector3[4];
            area.GetWorldCorners(corners);
            float enemyWidth = Mathf.Abs(corners[2].x - corners[0].x) / Mathf.Abs(world.lossyScale.x);
            float dots = enemy.texture != null ? enemy.texture.width * enemy.uvRect.width : 0f;
            float dot = (dots > 0f ? enemyWidth / dots : 3f) * ChestDots;
            var size = new Vector2(closed.width, closed.height) * dot;
            // Where it stands, among the characters.
            Vector2 feet = world.InverseTransformPoint(FrontLayer.TransformPoint(land));
            var layer = (RectTransform)world;
            // Its size on the effects' layer, for the light round it.
            float across = size.x * Mathf.Abs(world.lossyScale.x / FrontLayer.lossyScale.x);

            if (chest == null)
            {
                chest = new GameObject("BattleChest", typeof(RectTransform)).AddComponent<RawImage>();
                chest.raycastTarget = false;
                chest.rectTransform.anchorMin = chest.rectTransform.anchorMax = Vector2.one * 0.5f;
                chest.rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            var rect = chest.rectTransform;
            rect.SetParent(layer, false);
            // Lit and drawn as the characters are.
            chest.material = enemy.material;
            rect.SetAsLastSibling();
            rect.sizeDelta = size;
            chest.texture = closed;
            chest.color = new Color(1f, 1f, 1f, 0f);
            chest.gameObject.SetActive(true);

            const float appear = 0.3f;
            float wait = ChestWait(quality);
            float after = 0.55f + (grade >= 2 ? 1.2f : 0.7f);
            float total = appear + wait + after;
            bool woundUp = false;
            bool opened = false;
            // A soft light on the floor it comes out of.
            Pool(land, color, across * 2f, 0.25f, appear + 0.3f);
            Ring(backLight, land, Color.Lerp(color, Color.white, 0.5f), across * 2.4f, 0.45f, floor: true);
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (!woundUp && t >= appear)
                {
                    // The wind-up: its light leaks out round it as it shakes. The stage is not
                    // darkened round it, which would darken the chest with the characters.
                    woundUp = true;
                    var middle = land + Vector2.up * (across * 0.4f);
                    Glow(backLight, middle, color, across * (1.6f + 0.3f * Mathf.Max(0, grade)), wait + 0.15f, 0.35f + 0.1f * grade);
                    if (grade >= 1)
                        Gather(middle, color, 4 + 5 * grade, 160f + 50f * grade, wait);
                    if (grade >= 2)
                        Circle(land, color, wait + 0.45f);
                }
                if (!opened && t >= appear + wait)
                {
                    // The burst: it flies open, and what was in it comes out together.
                    opened = true;
                    if (open != null)
                        chest.texture = open;
                    burst?.Invoke();
                    Open(land + Vector2.up * (across * 0.4f - DropLift), color, grade);
                }
                // It wells up from the floor, springing a little past its size, then trembles
                // harder and harder until it opens, by whole dots.
                float rise = Mathf.Clamp01(t / appear);
                float scale = rise < 1f ? Mathf.Max(0f, RewardReveal.SpringOut(rise)) : 1f;
                float shaking = Mathf.Clamp01((t - appear) / wait);
                float tremble =
                    woundUp && !opened
                        ? (Mathf.Repeat(t, 0.08f) < 0.04f ? 1f : -1f) * dot * (shaking < 0.5f ? 1f : 2f)
                        : 0f;
                rect.anchoredPosition = feet + new Vector2(tremble, 0f);
                rect.localScale = new Vector3(scale, scale, 1f);
                chest.color = new Color(1f, 1f, 1f, Mathf.Clamp01(rise * 3f) * Mathf.Clamp01((total - t) / 0.4f));
                yield return null;
            }
            chest.gameObject.SetActive(false);
        }

        /// <summary>
        /// Where a drop is <paramref name="t"/> seconds after it leaps: an arc from the enemy up
        /// and down to the floor, then two bounces, each lower and shorter, then it rests.
        /// </summary>
        public static Vector2 DropPath(Vector2 start, Vector2 land, float t)
        {
            if (t < DropFlight)
            {
                float u = t / DropFlight;
                float apex = 150f + Mathf.Max(0f, land.y - start.y);
                return Vector2.Lerp(start, land, u) + Vector2.up * (4f * apex * u * (1f - u));
            }
            t -= DropFlight;
            if (t < DropBounce)
            {
                float u = t / DropBounce;
                return land + Vector2.up * (4f * 46f * u * (1f - u));
            }
            t -= DropBounce;
            if (t < DropHop)
            {
                float u = t / DropHop;
                return land + Vector2.up * (4f * 14f * u * (1f - u));
            }
            return land;
        }

        /// <summary>A drop touching the floor: a small ring and a pool of light under it.</summary>
        private void Land(Vector2 at, Color color, float size, float seconds)
        {
            Ring(backLight, at, Color.Lerp(color, Color.white, 0.4f), size, seconds, floor: true);
            Pool(at, color, size * 0.8f, 0.04f, seconds);
            Puffs(backSmoke, at, ArrivalDust, 2, 20f, 0.5f, 60f);
        }

        /// <summary>
        /// The drop opening: a white-hot flash, then in the quality's colour rays, a pillar of
        /// light (from B), rings along the floor, a pool and a light on everyone near, and
        /// sparkles thrown up that fall back. The better the quality, the bigger and longer each.
        /// </summary>
        private void Open(Vector2 at, Color color, int grade)
        {
            var middle = at + Vector2.up * DropLift;
            HotFlash(middle, color, 200f + 80f * grade);
            if (grade >= 1)
            {
                Star(middle, color, 180f + 120f * grade, 0.3f + 0.08f * grade);
                DropPillar(
                    at,
                    color,
                    150f + 70f * grade,
                    280f + 160f * grade,
                    0.9f + 0.35f * grade
                );
            }
            Ring(
                backLight,
                at,
                Color.Lerp(color, Color.white, 0.4f),
                300f + 140f * grade,
                0.5f,
                floor: true
            );
            if (grade >= 2)
                Ring(backLight, at, color, 560f + 160f * grade, 0.7f, floor: true);
            Pool(at, color, 300f + 100f * grade, 0.3f + 0.15f * grade, 0.8f);
            Illuminate(middle, color, 0.5f + 0.15f * grade, 300f + 110f * grade, 0.3f, 0.8f);
            Spray(
                frontLight,
                middle,
                new Burst
                {
                    Shape = BattleVfxShape.Sparkle,
                    Bright = CoreBright,
                    From = Color.white,
                    To = new Color(color.r, color.g, color.b, 0f),
                    Speed = new Vector2(150f, 320f + 90f * grade),
                    Direction = 90f,
                    Spread = 110f,
                    Life = new Vector2(0.6f, 1.1f + 0.2f * grade),
                    Size = new Vector2(14f, 30f + 6f * grade),
                    Shrink = 0.2f,
                    Gravity = 420f,
                    Drag = 1.6f,
                    Spin = 220f,
                    Area = 20f,
                },
                6 + 8 * grade
            );
            if (grade == 3)
            {
                ScreenFlash(Color.Lerp(Color.white, color, 0.5f), 0.22f);
                Shake(0.22f, Vector2.down);
            }
            else if (grade == 2)
                Shake(0.1f, Vector2.down);
        }

        /// <summary>A pillar of light rising from the floor, eaten away from its faint top.</summary>
        private void DropPillar(Vector2 feet, Color color, float width, float height, float seconds)
        {
            var shot = Spawn(
                backLight,
                BattleVfxShape.Pillar,
                feet,
                new Vector2(width, height),
                color,
                new Vector2(0.5f, 0.1f)
            );
            Animate(
                shot,
                seconds,
                (s, k) =>
                {
                    float rise = EaseOut(Mathf.Min(1f, k * 5f));
                    s.Holder.localScale = new Vector3(0.5f + 0.5f * rise, 0.15f + 0.85f * rise, 1f);
                    Show(s, color, k, Eaten(k, 0.5f), ErodeBright);
                }
            );
        }
    }
}
