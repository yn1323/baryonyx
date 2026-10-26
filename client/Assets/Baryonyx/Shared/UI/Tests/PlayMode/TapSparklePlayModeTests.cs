using System.Collections;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 起動時に全画面の手前へ置いた層が、仮想のマウスで押した位置にキラキラを出すことを確かめる。
    public sealed class TapSparklePlayModeTests : ScenarioInputFixture
    {
        [UnityTest]
        public IEnumerator TheLayerInFrontOfEveryScreenBurstsWhereTheMouseClicked()
        {
            TapSparkleLayer.Install();
            var layer = TapSparkleLayer.Installed;
            Assert.That(layer, Is.Not.Null);
            Assert.That(layer.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            var canvas = layer.GetComponent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(layer.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Null);
            layer.Gestures.Clear();

            var screenPoint = new Vector2(Screen.width * 0.25f, Screen.height * 0.5f);
            Set(Mouse.position, screenPoint);
            Press(Mouse.leftButton);
            yield return null;
            Assert.That(layer.Gestures.Sparks.Count, Is.EqualTo(1));
            Assert.That(layer.Gestures.Sparks[0].Phase, Is.EqualTo(TapSparkleGestures.Phase.Sign));

            Release(Mouse.leftButton);
            yield return null;
            yield return null;

            var spark = layer.Gestures.Sparks[0];
            Assert.That(spark.Phase, Is.EqualTo(TapSparkleGestures.Phase.Burst));
            Assert.That(spark.At, Is.EqualTo(layer.Marks.DesignPoint(screenPoint)));
            Assert.That(layer.Marks.Count, Is.GreaterThan(0));
        }
    }
}
