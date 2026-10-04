using System;
using System.Linq;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattleSkillVfxTests
    {
        [TestCase(
            BattleInspectElement.Slash,
            BattleInspectCardEffect.DamageOne,
            BattleSkillVfxKind.Slash
        )]
        [TestCase(
            BattleInspectElement.Fire,
            BattleInspectCardEffect.DamageOne,
            BattleSkillVfxKind.Fire
        )]
        [TestCase(
            BattleInspectElement.Ice,
            BattleInspectCardEffect.DamageOne,
            BattleSkillVfxKind.Ice
        )]
        [TestCase(
            BattleInspectElement.Thunder,
            BattleInspectCardEffect.DamageAll,
            BattleSkillVfxKind.Thunder
        )]
        [TestCase(BattleInspectElement.None, BattleInspectCardEffect.Heal, BattleSkillVfxKind.Heal)]
        [TestCase(
            BattleInspectElement.None,
            BattleInspectCardEffect.Guard,
            BattleSkillVfxKind.Guard
        )]
        [TestCase(
            BattleInspectElement.None,
            BattleInspectCardEffect.DamageOne,
            BattleSkillVfxKind.Slash
        )]
        public void EachCardShowsTheEffectOfItsElementOrItsEffect(
            BattleInspectElement element,
            BattleInspectCardEffect effect,
            BattleSkillVfxKind expected
        )
        {
            Assert.That(BattleSkillVfx.KindFor(element, effect), Is.EqualTo(expected));
        }

        [Test]
        public void FullScreenFlashesStayUnderThreeASecond()
        {
            Assert.That(BattleSkillVfx.FlashAllowed(float.NegativeInfinity, 0f), Is.True);
            Assert.That(BattleSkillVfx.FlashAllowed(1f, 1f + BattleSkillVfx.FlashGap), Is.True);
            Assert.That(BattleSkillVfx.FlashAllowed(1f, 1.2f), Is.False);
            Assert.That(1f / BattleSkillVfx.FlashGap, Is.LessThan(3f), "WCAG 2.3.1");
        }

        [Test]
        public void TheStageShakesInWholeDotsAndRestsWithoutTrauma()
        {
            var still = BattleSkillVfx.ShakeOffset(0f, new Vector2(0.7f, -0.4f), Vector2.zero, 28f);
            Assert.That(still, Is.EqualTo(Vector2.zero));

            var shaken = BattleSkillVfx.ShakeOffset(
                0.9f,
                new Vector2(0.73f, -0.41f),
                new Vector2(6.5f, 0f),
                28f
            );
            Assert.That(shaken, Is.Not.EqualTo(Vector2.zero));
            Assert.That(shaken.x % 4f, Is.EqualTo(0f), "One dot is 4 px.");
            Assert.That(shaken.y % 4f, Is.EqualTo(0f));
        }

        [Test]
        public void SmallKnocksShakeMuchLessThanBigOnes()
        {
            var noise = new Vector2(1f, 0f);
            float small = BattleSkillVfx.ShakeOffset(0.3f, noise, Vector2.zero, 28f).x;
            float big = BattleSkillVfx.ShakeOffset(0.9f, noise, Vector2.zero, 28f).x;
            // The shake grows with the square of the trauma.
            Assert.That(big, Is.GreaterThanOrEqualTo(small * 6f));
        }

        [Test]
        public void TheSideFacingALightIsLitMostAndNothingPastItsReach()
        {
            var center = Vector2.zero;
            var light = new Vector2(200f, 0f);
            float near = BattleSkillVfx.LightOnCorner(new Vector2(40f, 40f), center, light, 400f);
            float far = BattleSkillVfx.LightOnCorner(new Vector2(-40f, 40f), center, light, 400f);
            Assert.That(near, Is.GreaterThan(far), "The corner toward the light gets more.");
            Assert.That(far, Is.GreaterThan(0f), "The far side still catches a little.");
            Assert.That(
                BattleSkillVfx.LightOnCorner(new Vector2(40f, 40f), center, light, 150f),
                Is.EqualTo(0f),
                "A light out of reach adds nothing."
            );
            float below = BattleSkillVfx.LightOnCorner(
                new Vector2(30f, -60f),
                center,
                new Vector2(0f, -150f),
                300f
            );
            float above = BattleSkillVfx.LightOnCorner(
                new Vector2(30f, 60f),
                center,
                new Vector2(0f, -150f),
                300f
            );
            Assert.That(below, Is.GreaterThan(above), "A light from below lights the feet most.");
        }

        [Test]
        public void AnErodingShapeStaysWholeThenIsEatenAway()
        {
            Assert.That(BattleSkillVfx.Remaining(0f, 0.3f), Is.EqualTo(1f));
            Assert.That(BattleSkillVfx.Remaining(0.3f, 0.3f), Is.EqualTo(1f));
            float mid = BattleSkillVfx.Remaining(0.65f, 0.3f);
            Assert.That(mid, Is.LessThan(1f).And.GreaterThan(0f));
            Assert.That(BattleSkillVfx.Remaining(0.9f, 0.3f), Is.LessThan(mid));
            Assert.That(BattleSkillVfx.Remaining(1f, 0.3f), Is.LessThan(0.01f));
        }

        [Test]
        public void ANumberPopsOutLargeAndSettlesToItsSize()
        {
            Assert.That(BattleInspectView.PopScale(0f), Is.EqualTo(1.8f).Within(0.001f));
            Assert.That(
                BattleInspectView.PopScale(0.6f),
                Is.LessThan(1f),
                "It snaps past its size."
            );
            Assert.That(BattleInspectView.PopScale(1f), Is.EqualTo(1f));
            Assert.That(BattleInspectView.PopScale(3f), Is.EqualTo(1f));
        }

        [Test]
        public void AShockRingSpringsOutFastAndLiesFlatOnTheFloor()
        {
            Assert.That(BattleSkillVfx.RingScale(0f, false).x, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(BattleSkillVfx.RingScale(1f, false).x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                BattleSkillVfx.RingScale(0.5f, false).x,
                Is.GreaterThan(0.15f + 0.85f * 0.5f),
                "It covers most of its spread early."
            );
            var floor = BattleSkillVfx.RingScale(1f, true);
            Assert.That(floor.y, Is.EqualTo(floor.x * BattleSkillVfx.FloorTilt).Within(0.001f));
        }

        [Test]
        public void AShapeIsWholeUntilItsHoldThenEatenAwayByTheEnd()
        {
            Assert.That(BattleSkillVfx.Eaten(0.2f, 0.4f), Is.EqualTo(0f));
            float mid = BattleSkillVfx.Eaten(0.7f, 0.4f);
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(BattleSkillVfx.Eaten(1f, 0.4f), Is.GreaterThan(0.99f));
        }

        [Test]
        public void EveryShapeIsWorkedOutByItsOwnMaterialWithNoPicture()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                BattleSkillVfxAssets.ShapeShaderPath
            );
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "The shape shader compiles.");

            var shapes = (BattleVfxShape[])Enum.GetValues(typeof(BattleVfxShape));
            foreach (var shape in shapes)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    $"{BattleSkillVfxAssets.MaterialFolder}/VfxShape{shape}.mat"
                );
                Assert.That(material, Is.Not.Null, shape.ToString());
                Assert.That(material.shader, Is.SameAs(shader), shape.ToString());
                Assert.That(
                    shapes.Where(other =>
                        material.IsKeywordEnabled(BattleSkillVfxAssets.KeywordOf(other))
                    ),
                    Is.EqualTo(new[] { shape }),
                    "Only its own shape is switched on."
                );
                var blend = BattleSkillVfxAssets.Solid(shape)
                    ? BlendMode.OneMinusSrcAlpha
                    : BlendMode.One;
                Assert.That(
                    material.GetFloat("_DstBlend"),
                    Is.EqualTo((float)blend),
                    shape.ToString()
                );
                Assert.That(material.GetTexture("_MainTex"), Is.Null, "No picture.");
            }
        }

        [Test]
        public void ABeatenEnemyCrumblesFromTheSideTheBlowCameFromAtASteadyRate()
        {
            // An 8x8 picture: a solid 6x4 block with an empty border, and a stray clear dot.
            const int size = 8;
            var pixels = new Color32[size * size];
            for (int y = 2; y < 6; y++)
            for (int x = 1; x < 7; x++)
                pixels[y * size + x] = new Color32(40, 160, 120, 255);
            pixels[3 * size + 4] = new Color32(255, 255, 255, 0);

            var art = BattleSkillVfx.CrumbleArt.Make(
                pixels,
                size,
                size,
                new Rect(0f, 0f, 1f, 1f),
                Vector2.right,
                3f
            );
            try
            {
                Assert.That(
                    art.Dots.Length,
                    Is.EqualTo(6 * 4 - 1),
                    "Every solid dot, and only those."
                );
                Assert.That(art.Dots[0].Order, Is.EqualTo(0f));
                Assert.That(art.Dots[art.Dots.Length - 1].Order, Is.EqualTo(1f));
                Assert.That(
                    art.Dots.Select(dot => dot.Order),
                    Is.Ordered,
                    "Kept in the order they go."
                );
                float left = art.Dots.Where(dot => dot.Local.x < 0.5f).Average(dot => dot.Order);
                float right = art.Dots.Where(dot => dot.Local.x > 0.5f).Average(dot => dot.Order);
                Assert.That(left, Is.LessThan(right), "The side the blow came from goes first.");
                Assert.That(art.Dots[0].Color, Is.EqualTo((Color)new Color32(40, 160, 120, 255)));

                Assert.That(art.Order.width, Is.EqualTo(size), "The map lies over the picture.");
                Assert.That(art.Order.filterMode, Is.EqualTo(FilterMode.Point));
            }
            finally
            {
                art.Release();
            }
        }

        [Test]
        public void AMoteFliesOffJustAsTheCrumbleReachesItsDot()
        {
            const float band = 0.12f;
            foreach (float order in new[] { 0f, 0.25f, 0.5f, 0.9f, 1f })
            {
                float moment = BattleSkillVfx.CrumbleMoment(order);
                Assert.That(moment, Is.InRange(0f, 1f));
                // The shader's front is the share eaten, run a band past the last dot.
                float front = BattleSkillVfx.Crumbled(moment) * (1f + band);
                Assert.That(front, Is.EqualTo(order).Within(1e-4f), order.ToString());
            }
            Assert.That(BattleSkillVfx.Crumbled(0.5f), Is.LessThan(0.5f), "It starts slow.");
            Assert.That(BattleSkillVfx.Crumbled(1f), Is.EqualTo(1f));
        }

        [Test]
        public void TheDefeatMaterialEatsTheEnemysPictureAway()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                BattleSkillVfxAssets.DefeatShaderPath
            );
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "The defeat shader compiles.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                $"{BattleSkillVfxAssets.MaterialFolder}/VfxDefeat.mat"
            );
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.SameAs(shader));
            Assert.That(material.HasProperty("_OrderTex"), Is.True);
        }
    }
}
