using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Baryonyx.Inventory;

namespace Baryonyx.Combat
{
    /// <summary>
    /// A card skill grown by one card's multiplier (doc/features/inventory.md, quality): every
    /// number grows the way that helps its owner. The parts of a stat, the hits, the turns and the
    /// amounts (energy, cards drawn, cost lowered, stacks, a buff's percent) are multiplied and
    /// rounded, a chance stops at 100%, and the cost is lowered by 1 for each 1.0 the multiplier
    /// grows (staying at 1 or more). A
    /// condition's threshold ("HPが半分以下") stays. The description is rewritten to match: the
    /// hits after "{0}ダメージ" ("×3"), the turns before "ターン", and each amount where its unit
    /// follows it. A number the description does not write stays unwritten.
    /// </summary>
    public static class CardSkillQuality
    {
        /// <summary>
        /// The upper limit of <paramref name="skill"/>'s multiplier, each kind's own
        /// (doc/features/inventory.md): ×1.00 for a card without one, which then has no range.
        /// </summary>
        public static float LimitOf(CardSkill skill)
        {
            if (skill == null)
                return Quality.MinMultiplier;
            if (skill.MaxMultiplier > 0f)
                return skill.MaxMultiplier;
            var card = skill.Base ?? skill;
            return card.MaxMultiplier > 0f ? card.MaxMultiplier : Quality.MinMultiplier;
        }

        /// <summary>
        /// One card of <paramref name="skill"/>'s kind at <paramref name="multiplier"/> (rounded to
        /// a hundredth and kept within ×1.00 and the limit). At ×1.00 it is the definition itself.
        /// </summary>
        public static CardSkill WithMultiplier(this CardSkill skill, float multiplier) =>
            WithMultiplier(skill, multiplier, 0f);

        /// <summary>
        /// The same, with the kind's upper limit set to <paramref name="limit"/> for this one card
        /// (0 keeps the kind's own), as the quality lab tries other limits.
        /// </summary>
        public static CardSkill WithMultiplier(this CardSkill skill, float multiplier, float limit)
        {
            var card = skill.Base ?? skill;
            float max = limit > 0f ? limit : LimitOf(card);
            max = Math.Max(Quality.MinMultiplier, max);
            float m = Math.Clamp(Quality.Percent(multiplier) / 100f, Quality.MinMultiplier, max);
            if (Quality.Percent(m) == 100 && limit <= 0f)
                return card;
            var actions = new CardAction[card.Actions.Count];
            for (int i = 0; i < actions.Length; i++)
                actions[i] = Scale(card.Actions[i], m);
            return new CardSkill
            {
                Id = card.Id,
                Name = card.Name,
                User = card.User,
                Element = card.Element,
                Kind = card.Kind,
                Target = card.Target,
                Cost = Quality.Cost(card.Cost, m),
                Text = Rewrite(card, actions),
                Actions = Array.AsReadOnly(actions),
                BorrowedArt = card.BorrowedArt,
                MaxMultiplier = limit > 0f ? limit : card.MaxMultiplier,
                FoilStyle = card.FoilStyle,
                Multiplier = m,
                Base = card,
            };
        }

        /// <summary>One action with its numbers grown by <paramref name="m"/>.</summary>
        public static CardAction Scale(CardAction from, float m)
        {
            var to = new CardAction
            {
                Kind = from.Kind,
                Target = from.Target,
                Stat = from.Stat,
                Percent = from.HasPower ? Quality.Amount(from.Percent, m) : from.Percent,
                Hits =
                    from.Kind == CardActionKind.Damage
                        ? Math.Max(1, Quality.Count(from.Hits, m))
                        : from.Hits,
                Status = from.Status,
                Turns = from.Turns > 0 ? Quality.Count(from.Turns, m) : from.Turns,
                Amount =
                    from.Amount <= 0 ? from.Amount
                    : from.Kind == CardActionKind.Revive ? Quality.Chance(from.Amount, m)
                    : Quality.Amount(from.Amount, m),
                Elements = from.Elements,
                When = from.When,
                BonusWhen = from.BonusWhen,
                BonusPercent = from.BonusPercent,
                ActStepPercent = from.ActStepPercent,
                ActCapPercent = from.ActCapPercent,
            };
            return to;
        }

        // 説明文の数字を、行動の順に書き換える。同じ数字が2つあれば、前の行動が前の数字を取る。
        private static string Rewrite(CardSkill card, IReadOnlyList<CardAction> actions)
        {
            string text = card.Text ?? "";
            var edits = new List<(int Start, int Length, string Text)>();
            int cursor = 0;
            int power = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                var from = card.Actions[i];
                var to = actions[i];
                if (from.HasPower)
                {
                    int index = power++;
                    if (from.Kind == CardActionKind.Damage && to.Hits != from.Hits)
                        RewriteHits(text, index, to.Hits, edits);
                }
                if (from.Turns > 0 && to.Turns != from.Turns)
                    Replace(text, $"(?<![0-9]){from.Turns}(?=ターン)", to.Turns, edits, ref cursor);
                if (from.Amount > 0 && to.Amount != from.Amount)
                {
                    if (
                        from.Kind == CardActionKind.Revive
                        && from.Amount == 50
                        && !Contains(text, "50%")
                    )
                        Replace(text, "半分", $"{to.Amount}%", edits, ref cursor);
                    else if (AmountPattern(from) is { } pattern)
                        Replace(text, pattern, to.Amount, edits, ref cursor);
                }
            }
            edits.Sort((a, b) => b.Start.CompareTo(a.Start));
            foreach (var (start, length, value) in edits)
                text = text.Substring(0, start) + value + text.Substring(start + length);
            return text;
        }

        private static bool Contains(string text, string part) =>
            text.IndexOf(part, StringComparison.Ordinal) >= 0;

        // 単位が後ろに付く数字の形。行動の種類ごとに、説明文での書き方が違う。
        private static string AmountPattern(CardAction action)
        {
            string n = $"(?<![0-9.]){action.Amount}";
            return action.Kind switch
            {
                CardActionKind.Energy => $"(?<=エネルギー\\+|エネルギーを){action.Amount}(?![0-9])",
                CardActionKind.Draw => n + "(?=枚)",
                CardActionKind.CostDown => n + "(?=下げる)",
                CardActionKind.Status => n + "(?=%|つ|回)",
                CardActionKind.Delay => n + "(?=つ)",
                CardActionKind.Dispel => n + "(?=つ)",
                CardActionKind.Revive => n + "(?=%)",
                _ => null,
            };
        }

        // 「{0}ダメージ×3」の回数を書き換える。1回だった攻撃が2回以上になれば「×2」を足す。
        private static void RewriteHits(
            string text,
            int index,
            int hits,
            List<(int Start, int Length, string Text)> edits
        )
        {
            string mark = "{" + index + "}ダメージ";
            int at = text.IndexOf(mark, StringComparison.Ordinal);
            if (at < 0)
                return;
            int after = at + mark.Length;
            var written = Regex.Match(text.Substring(after), "^×([0-9]+)");
            if (written.Success)
                edits.Add((after + 1, written.Groups[1].Length, hits.ToString()));
            else if (hits > 1)
                edits.Add((after, 0, "×" + hits));
        }

        private static void Replace(
            string text,
            string pattern,
            int value,
            List<(int Start, int Length, string Text)> edits,
            ref int cursor
        ) => Replace(text, pattern, value.ToString(), edits, ref cursor);

        // 前の行動より後ろの、まだ書き換えていない最初の一致を書き換える。後ろになければ前から探す。
        private static void Replace(
            string text,
            string pattern,
            string value,
            List<(int Start, int Length, string Text)> edits,
            ref int cursor
        )
        {
            Match found = null;
            foreach (Match match in Regex.Matches(text, pattern))
            {
                if (Overlaps(edits, match.Index, match.Length))
                    continue;
                if (match.Index >= cursor)
                {
                    found = match;
                    break;
                }
                found ??= match;
            }
            if (found == null)
                return;
            edits.Add((found.Index, found.Length, value));
            cursor = found.Index + found.Length;
        }

        private static bool Overlaps(
            List<(int Start, int Length, string Text)> edits,
            int start,
            int length
        )
        {
            foreach (var edit in edits)
                if (start < edit.Start + Math.Max(1, edit.Length) && edit.Start < start + length)
                    return true;
            return false;
        }
    }
}
