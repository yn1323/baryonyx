using System.Collections;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class PixelPerfectRawImageTests
    {
        private GameObject root;

        [UnityTest]
        public IEnumerator CameraCanvasKeepsTheDesignSize()
        {
            // The showcase previews UI prefabs on a camera canvas, whose world units are not
            // screen pixels; the sprite must keep 4 units per texel there.
            root = new GameObject("PixelPerfectCameraCanvas", typeof(Camera), typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = root.GetComponent<Camera>();
            canvas.planeDistance = 10f;

            var sprite = new GameObject("Sprite", typeof(RectTransform), typeof(RawImage));
            sprite.transform.SetParent(root.transform, false);
            var texture = new Texture2D(64, 64);
            sprite.GetComponent<RawImage>().texture = texture;
            sprite.GetComponent<RawImage>().uvRect = new Rect(0f, 0f, 0.25f, 0.5f);
            sprite.AddComponent<PixelPerfectRawImage>().DotSize = 4f;

            yield return null;
            yield return null;

            Assert.That(
                ((RectTransform)sprite.transform).sizeDelta,
                Is.EqualTo(new Vector2(16f * 4f, 32f * 4f))
            );
            Object.Destroy(texture);
        }

        [TearDown]
        public void DestroyCanvas()
        {
            if (root != null)
                Object.Destroy(root);
        }
    }
}
