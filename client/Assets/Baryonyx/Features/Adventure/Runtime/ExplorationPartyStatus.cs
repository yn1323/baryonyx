using System;
using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The party's HP on the exploration map and at the campfire (doc/features/screens/adventure.md,
    /// 探索): each slot's sprite, a bar of the HP it carries into the next battle and its number. A
    /// fallen member (HP 0, doc/features/stage-progression.md, HPの持ち越し) is drawn grey with
    /// 「戦闘不能」 in place of the number, so the next room is chosen knowing who is down.
    /// </summary>
    public sealed class ExplorationPartyStatus : MonoBehaviour
    {
        public static readonly Color HealthyFill = new(0.36f, 0.86f, 0.46f);
        public static readonly Color HurtFill = new(0.96f, 0.78f, 0.3f);
        public static readonly Color CriticalFill = new(0.95f, 0.36f, 0.32f);
        public static readonly Color DownText = new(1f, 0.46f, 0.42f);

        // 倒れた人の絵は、色を抜いて暗くする。
        public static readonly Color DownTint = new(0.5f, 0.5f, 0.54f, 0.8f);

        // この割合を下回ると、バーの色を変える。
        public const float HurtShare = 0.5f;
        public const float CriticalShare = 0.25f;

        [Serializable]
        public sealed class Slot
        {
            public GameObject Root;
            public RawImage Sprite;
            public RectTransform Fill;
            public Image FillImage;
            public TMP_Text Number;
        }

        public Slot[] Slots = Array.Empty<Slot>();

        /// <summary>
        /// Shows <paramref name="party"/> (in slot order, null for an empty slot) with the share of
        /// maximum HP each slot carries (<see cref="AdventureVitals.Of"/>).
        /// </summary>
        public void Show(IReadOnlyList<BattlePartyMember> party, IReadOnlyList<float> shares)
        {
            for (int slot = 0; slot < Slots.Length; slot++)
            {
                var view = Slots[slot];
                if (view == null)
                    continue;
                var member = party != null && slot < party.Count ? party[slot] : null;
                if (view.Root != null)
                    view.Root.SetActive(member != null);
                if (member == null)
                    continue;
                float share =
                    shares != null && slot < shares.Count ? Mathf.Clamp01(shares[slot]) : 1f;
                int max = Mathf.Max(1, member.Stats.Hp);
                int hp = BattleInspectView.StartHpOf(max, share);
                bool down = hp <= 0;
                if (view.Sprite != null)
                {
                    view.Sprite.texture = member.Art;
                    view.Sprite.color = down ? DownTint : member.Tint;
                }
                if (view.Fill != null)
                {
                    view.Fill.gameObject.SetActive(!down);
                    view.Fill.anchorMax = new Vector2(hp / (float)max, 1f);
                }
                if (view.FillImage != null)
                    view.FillImage.color = FillOf(hp / (float)max);
                if (view.Number != null)
                {
                    view.Number.text = down ? "戦闘不能" : $"{hp}/{max}";
                    view.Number.color = down ? DownText : Color.white;
                }
            }
        }

        public static Color FillOf(float share) =>
            share < CriticalShare ? CriticalFill
            : share < HurtShare ? HurtFill
            : HealthyFill;
    }
}
