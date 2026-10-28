using UnityEngine;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// A pickup: a small window with what was found, closing by itself after a moment or at a tap.
    /// Nothing is chosen, since roads are full of these.
    /// </summary>
    public sealed class WorldEventPickup : WorldEventPanel
    {
        public const float StaySeconds = 1.8f;

        protected override void Restart() { }

        public override void Advance()
        {
            Done = true;
            Host.Hide();
        }

        protected override void Draw(float dt)
        {
            Done = true;
            if (Clock >= StaySeconds)
                Host.Hide();
        }
    }
}
