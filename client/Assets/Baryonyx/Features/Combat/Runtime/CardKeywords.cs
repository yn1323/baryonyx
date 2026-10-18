using System;
using System.Collections.Generic;
using System.Text;

namespace Baryonyx.Combat
{
    /// <summary>A word a card's text names in brackets ("[凍え]"), and what it means.</summary>
    public sealed class CardKeyword
    {
        public CardKeyword(string name, string text)
        {
            Name = name;
            Text = text;
        }

        public string Name { get; }

        /// <summary>The explanation shown beside a raised card.</summary>
        public string Text { get; }
    }

    /// <summary>
    /// The words a card's text may name in brackets (provisional: doc/features/combat.md, card
    /// keywords). The card shows the word alone and the battle shows its meaning beside the
    /// raised card, so a card with many effects keeps its text short.
    /// </summary>
    public static class CardKeywords
    {
        public static readonly IReadOnlyList<CardKeyword> All = new[]
        {
            new CardKeyword(
                "ブロック",
                "受けるダメージをブロックから先に引く。次の味方ターンの開始時に消える。"
            ),
            new CardKeyword(
                "弱点",
                "敵ごとの苦手な属性。突くとダメージ1.5倍で、その弱点が開示される。"
            ),
            new CardKeyword(
                "状態異常",
                "出血・やけど・毒・凍え・凍結・麻痺・防御ダウンなど、付いた者に悪い状態。"
            ),
            new CardKeyword("出血", "敵が行動するたびにダメージを受ける。"),
            new CardKeyword("やけど", "味方ターンの開始時にダメージを受ける。"),
            new CardKeyword(
                "毒",
                "味方ターンの開始時に、重なった数に応じたダメージを受け、重なりが1つ減る。"
            ),
            new CardKeyword("凍え", "速度-30%。"),
            new CardKeyword("凍結", "次の行動を1回休む。"),
            new CardKeyword("麻痺", "与えるダメージ-25%。"),
            new CardKeyword("防御ダウン", "受けるダメージ+25%。"),
            new CardKeyword("刻印", "次の味方ターンの開始時に爆発し、敵全体に炎属性のダメージ。"),
            new CardKeyword("強化", "攻撃力アップ・速度アップ・リジェネなど、付いた者に良い状態。"),
        };

        /// <summary>The keyword named <paramref name="name"/>, or null.</summary>
        public static CardKeyword Find(string name)
        {
            foreach (var keyword in All)
                if (keyword.Name == name)
                    return keyword;
            return null;
        }

        /// <summary>The names in brackets in <paramref name="text"/>, each once, in order.</summary>
        public static IReadOnlyList<string> Names(string text)
        {
            var names = new List<string>();
            Paint(
                text,
                name =>
                {
                    if (!names.Contains(name))
                        names.Add(name);
                    return name;
                }
            );
            return names;
        }

        /// <summary>The known keywords in brackets in <paramref name="text"/>, each once, in order.</summary>
        public static IReadOnlyList<CardKeyword> In(string text)
        {
            var keywords = new List<CardKeyword>();
            foreach (var name in Names(text))
            {
                var keyword = Find(name);
                if (keyword != null)
                    keywords.Add(keyword);
            }
            return keywords;
        }

        /// <summary>
        /// The text with each "[name]" put in by <paramref name="paint"/> (given the name), e.g.
        /// to colour it. A bracket left open stays as it is.
        /// </summary>
        public static string Paint(string text, Func<string, string> paint)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0)
                return text;
            var result = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                int close = open < 0 ? -1 : text.IndexOf(']', open + 1);
                if (close < 0)
                {
                    result.Append(text, i, text.Length - i);
                    break;
                }
                result.Append(text, i, open - i);
                result.Append(paint(text.Substring(open + 1, close - open - 1)));
                i = close + 1;
            }
            return result.ToString();
        }

        /// <summary>The text with the brackets taken off its keywords.</summary>
        public static string Plain(string text) => Paint(text, name => name);
    }
}
