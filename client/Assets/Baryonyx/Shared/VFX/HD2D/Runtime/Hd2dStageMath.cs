using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// The arithmetic of the 3D stage, kept apart from the components so it can be tested:
    /// turning a point of the UI into a point on the ground seen by the camera, and the size of
    /// one UI unit at a given depth. The UI is measured in canvas units (1920×1080 by height).
    /// </summary>
    public static class Hd2dStageMath
    {
        /// <summary>
        /// The direction of the ray through a point of the screen, in the camera's own axes
        /// (z forward), for a camera with the given vertical field of view and aspect.
        /// <paramref name="normalized"/> runs from (0, 0) at the bottom left to (1, 1) at the top right.
        /// </summary>
        public static Vector3 ViewDirection(Vector2 normalized, float verticalFov, float aspect)
        {
            float tanY = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad);
            return new Vector3(
                (normalized.x * 2f - 1f) * tanY * aspect,
                (normalized.y * 2f - 1f) * tanY,
                1f
            );
        }

        /// <summary>
        /// Where a ray from <paramref name="origin"/> meets the level ground at
        /// <paramref name="groundHeight"/>; false if the ray never comes down to it.
        /// </summary>
        public static bool GroundHit(
            Vector3 origin,
            Vector3 direction,
            float groundHeight,
            out Vector3 hit
        )
        {
            hit = default;
            if (direction.y >= -1e-5f)
                return false;
            float distance = (groundHeight - origin.y) / direction.y;
            if (distance <= 0f)
                return false;
            hit = origin + direction * distance;
            return true;
        }

        /// <summary>
        /// World units covered by one canvas unit on a plane facing the camera at
        /// <paramref name="depth"/>, when the canvas is <paramref name="canvasHeight"/> units tall.
        /// </summary>
        public static float WorldPerCanvasUnit(
            float depth,
            float verticalFov,
            float canvasHeight
        ) =>
            2f
            * depth
            * Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad)
            / Mathf.Max(canvasHeight, 1f);

        /// <summary>
        /// The way to move the camera (in its right and up axes, world units) so that what lies at
        /// <paramref name="focusDepth"/> moves on screen by <paramref name="uiOffset"/> canvas units,
        /// as the UI of the stage does. Nearer things then move more and farther things less.
        /// </summary>
        public static Vector2 CameraShift(
            Vector2 uiOffset,
            float focusDepth,
            float verticalFov,
            float canvasHeight
        ) => -uiOffset * WorldPerCanvasUnit(focusDepth, verticalFov, canvasHeight);

        /// <summary>
        /// The horizontal direction a vertical shadow board should run across so that it shows
        /// its full outline to a light: square to the light's direction on the ground, and turned
        /// to the camera's right so that it does not flip from frame to frame.
        /// </summary>
        public static Vector3 ShadowBoardRight(Vector3 towardsLight, Vector3 cameraRight)
        {
            var flat = new Vector3(towardsLight.x, 0f, towardsLight.z);
            var right = new Vector3(cameraRight.x, 0f, cameraRight.z);
            if (right.sqrMagnitude < 1e-6f)
                right = Vector3.right;
            right.Normalize();
            if (flat.sqrMagnitude < 1e-6f)
                return right;
            var across = Vector3.Cross(Vector3.up, flat.normalized);
            return Vector3.Dot(across, right) < 0f ? -across : across;
        }

        /// <summary>
        /// How much of an establishing camera move is left after <paramref name="elapsed"/>
        /// seconds of <paramref name="duration"/>: 1 at the start, easing out to 0, so the camera
        /// glides in and settles without a jolt.
        /// </summary>
        public static float IntroRemaining(float elapsed, float duration)
        {
            if (duration <= 0f)
                return 0f;
            float left = 1f - Mathf.Clamp01(elapsed / duration);
            return left * left * left;
        }
    }
}
