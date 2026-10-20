using System;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>How one kind of enemy is drawn on the battle screen.</summary>
    [Serializable]
    public sealed class BattleEnemyLook
    {
        public string Id = "";
        public Texture2D Texture;

        /// <summary>
        /// The drawn part of the picture, where a card is dropped to target it, in dots: its
        /// middle from the feet (across from the picture's middle, up from its bottom) and its size.
        /// </summary>
        public Vector2 TargetCenter;
        public Vector2 TargetSize;

        /// <summary>The face crop shown in the turn order.</summary>
        public Rect IconUv;
    }

    /// <summary>
    /// Every kind of enemy's picture, made by the battle screen's generator from the enemy
    /// pictures (Combat/UI/Art/Enemies), so a battle of the adventure can put any enemy in a
    /// place on the field (<see cref="BattleInspectView.UseEncounter(System.Collections.Generic.IReadOnlyList{string}, int, float)"/>).
    /// </summary>
    public sealed class BattleEnemyLooks : ScriptableObject
    {
        public BattleEnemyLook[] Looks = Array.Empty<BattleEnemyLook>();

        public BattleEnemyLook Find(string id) =>
            string.IsNullOrEmpty(id) ? null : Array.Find(Looks, look => look.Id == id);
    }
}
