using System;
using Baryonyx.Combat;
using UnityEngine;

namespace Baryonyx.Party
{
    /// <summary>
    /// The mock characters and the four party slots until characters have data
    /// (doc/features/party.md). The tavern's formation reads it.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Party Mock Data")]
    public sealed class PartyMockData : ScriptableObject
    {
        // 持っているキャラ。右の「仲間」はこの順に並べる。
        public PartyMember[] Members = Array.Empty<PartyMember>();

        // 4つの枠に入れておくキャラのID。空なら空き。
        public string[] Formation = Array.Empty<string>();

        // 属性ごとのアイコン（CardElementの値の順。Noneはnull）。カードは属性から絵を引く。
        public Sprite[] ElementIcons = Array.Empty<Sprite>();

        public PartyMember Find(string id) =>
            string.IsNullOrEmpty(id) ? null : Array.Find(Members, member => member.Id == id);

        /// <summary>
        /// The attribute icon of a card skill, or null for a card without an attribute (shown as
        /// "無") or an unknown skill.
        /// </summary>
        public Sprite IconOf(string skill)
        {
            var card = CardSkills.Find(skill);
            int index = card != null ? (int)card.Element : -1;
            return card != null && card.Element != CardElement.None && index < ElementIcons.Length
                ? ElementIcons[index]
                : null;
        }
    }

    [Serializable]
    public sealed class PartyMember
    {
        public string Id = "";
        public string Name = "";

        [Min(1)]
        public int Level = 1;

        // 64×64の戦闘のドット絵。4倍で表示する。
        public Texture2D Art;

        // 絵のないキャラは、既存の絵に色を掛け、左右を反転して仮に見分ける。
        public Color Tint = Color.white;
        public bool Flip;

        // 装備しているスキル（4枚）。
        public PartyCard[] Cards = Array.Empty<PartyCard>();
    }

    [Serializable]
    public sealed class PartyCard
    {
        // スキルのID（CardSkills）。属性のアイコンは PartyMockData.IconOf で引く。
        public string Skill = "";
    }
}
