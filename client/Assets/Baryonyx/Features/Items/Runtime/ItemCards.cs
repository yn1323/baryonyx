using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using static Baryonyx.Combat.CardAction;

namespace Baryonyx.Items
{
    /// <summary>
    /// One kind of item (a tool or a material: doc/features/items.md). An item is played as a card
    /// from a custom skill slot, like a skill, but has no quality: every one of a kind is the same.
    /// Its rarity (stars) tells only how hard it is to find.
    /// </summary>
    public sealed class ItemCard
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }

        // レア度（☆の数）。サーバーのドロップの一覧（server/src/features/adventure/drops.ts）と同じ値にする。
        public int Rarity { get; internal set; }
        public CardElement Element { get; internal set; }
        public CardKind Kind { get; internal set; }
        public CardTarget Target { get; internal set; }
        public int Cost { get; internal set; }

        // 効果の説明。{0} は1つ目の威力。
        public string Text { get; internal set; }
        public IReadOnlyList<CardAction> Actions { get; internal set; } = Array.Empty<CardAction>();

        private CardSkill card;

        /// <summary>
        /// The item as a card, for the screens that show cards: no element restriction and no
        /// quality, so it never sparkles (×1.00 with a top of ×1.00).
        /// </summary>
        public CardSkill Card =>
            card ??= new CardSkill
            {
                Id = Id,
                Name = Name,
                Element = Element,
                Kind = Kind,
                Target = Target,
                Cost = Cost,
                Text = Text,
                Actions = Actions,
                MaxMultiplier = Inventory.Quality.MinMultiplier,
            };
    }

    /// <summary>
    /// The items (provisional). Each is found in one destination only (server/src/features/items
    /// and the drop lists in server/src/features/adventure/drops.ts); the ids match the server.
    /// The deeper the destination, the stronger its items. Using them in battle comes with the
    /// custom skill slots holding items.
    /// </summary>
    public static class ItemCards
    {
        private const CardTarget Enemy = CardTarget.OneEnemy;
        private const CardTarget Enemies = CardTarget.AllEnemies;
        private const CardTarget Ally = CardTarget.OneAlly;
        private const CardTarget Allies = CardTarget.AllAllies;
        private const CardTarget Nobody = CardTarget.None;
        private const CardStat Phy = CardStat.PhysicalAttack;
        private const CardStat Mag = CardStat.MagicAttack;
        private const CardStat Def = CardStat.PhysicalDefense;

        public static readonly IReadOnlyList<ItemCard> All = Array.AsReadOnly(
            new[]
            {
                // --- ミストラ遺跡（Lv 10） ---------------------------------------------------
                Item(
                    "herb",
                    "薬草",
                    1,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体のHPを{0}回復。",
                    Heal(Ally, Mag, 50)
                ),
                Item(
                    "acorn",
                    "どんぐり",
                    1,
                    CardElement.Blunt,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "敵1体に投げつけ、{0}ダメージ。",
                    Damage(Enemy, Phy, 40)
                ),
                Item(
                    "moss-wad",
                    "苔の綿",
                    2,
                    CardElement.None,
                    CardKind.Guard,
                    Ally,
                    1,
                    "味方1体に{0}ブロック。",
                    Block(Ally, Def, 45)
                ),
                Item(
                    "dew-vial",
                    "朝露の小瓶",
                    2,
                    CardElement.None,
                    CardKind.Support,
                    Nobody,
                    0,
                    "カードを1枚引く。",
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                Item(
                    "spore-powder",
                    "毒胞子の粉",
                    3,
                    CardElement.None,
                    CardKind.Debuff,
                    Enemies,
                    1,
                    "敵全体に[毒]を2つ重ねる。",
                    Apply(Enemies, CardStatus.Poison, 0, 2)
                ),
                Item(
                    "guardian-seed",
                    "守り手の種",
                    5,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    2,
                    "味方全体のHPを{0}回復し、3ターンの間、毎ターン少し回復する。",
                    Heal(Allies, Mag, 30),
                    Apply(Allies, CardStatus.Regen, 3, Mag, 10)
                ),
                // --- 港町ポルトリア（Lv 14） -------------------------------------------------
                Item(
                    "dried-fish",
                    "干し魚",
                    1,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体のHPを{0}回復。",
                    Heal(Ally, Mag, 60)
                ),
                Item(
                    "shell-shard",
                    "貝殻のつぶて",
                    1,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "敵1体に投げつけ、{0}ダメージ。",
                    Damage(Enemy, Phy, 50)
                ),
                Item(
                    "sailor-rope",
                    "船乗りのロープ",
                    2,
                    CardElement.None,
                    CardKind.Debuff,
                    Enemy,
                    1,
                    "敵1体を縛り、行動順を1つ遅らせる。",
                    Simple(CardActionKind.Delay, Enemy, 1)
                ),
                Item(
                    "sea-salt",
                    "清めの海塩",
                    2,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体の[状態異常]を消し、HPを{0}回復。",
                    Simple(CardActionKind.Cleanse, Ally),
                    Heal(Ally, Mag, 25)
                ),
                Item(
                    "ink-pot",
                    "墨つぼ",
                    3,
                    CardElement.None,
                    CardKind.Debuff,
                    Enemy,
                    1,
                    "敵1体に墨を浴びせ、2ターンの間[防御ダウン]にする。",
                    Apply(Enemy, CardStatus.Vulnerable, 2)
                ),
                Item(
                    "pirate-rum",
                    "海賊のラム酒",
                    4,
                    CardElement.None,
                    CardKind.Buff,
                    Allies,
                    1,
                    "味方全体の攻撃力を2ターン20%上げる。",
                    Apply(Allies, CardStatus.AttackUp, 2, 20)
                ),
            }
        );

        public static ItemCard Find(string id)
        {
            foreach (var item in All)
                if (item.Id == id)
                    return item;
            return null;
        }

        private static ItemCard Item(
            string id,
            string name,
            int rarity,
            CardElement element,
            CardKind kind,
            CardTarget target,
            int cost,
            string text,
            params CardAction[] actions
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Rarity = rarity,
                Element = element,
                Kind = kind,
                Target = target,
                Cost = cost,
                Text = text,
                Actions = Array.AsReadOnly(actions),
            };
    }
}
