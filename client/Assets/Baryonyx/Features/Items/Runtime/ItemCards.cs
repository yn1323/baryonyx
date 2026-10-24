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
                // --- グラシエラ氷窟（Lv 18） -------------------------------------------------
                Item(
                    "icicle-shard",
                    "氷柱のかけら",
                    1,
                    CardElement.Ice,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "敵1体に投げつけ、{0}ダメージ。",
                    Damage(Enemy, Phy, 60)
                ),
                Item(
                    "cavern-water",
                    "地底湖の清水",
                    1,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体のHPを{0}回復。",
                    Heal(Ally, Mag, 70)
                ),
                Item(
                    "glowworm-lamp",
                    "ツチボタルの灯",
                    2,
                    CardElement.None,
                    CardKind.Support,
                    Enemy,
                    0,
                    "敵1体の[弱点]をすべて見抜き、カードを1枚引く。",
                    Simple(CardActionKind.RevealWeakness, Enemy),
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                Item(
                    "frost-crystal",
                    "霜の結晶",
                    3,
                    CardElement.Ice,
                    CardKind.Attack,
                    Enemies,
                    1,
                    "敵全体に{0}ダメージ。[凍え]2ターン。",
                    Damage(Enemies, Mag, 40),
                    Apply(Enemies, CardStatus.Chill, 2)
                ),
                Item(
                    "troll-stoneskin",
                    "トロルの石皮",
                    4,
                    CardElement.None,
                    CardKind.Guard,
                    Allies,
                    2,
                    "味方全体に[ブロック]{0}。",
                    Block(Allies, Def, 60)
                ),
                // --- しじまの沼（Lv 22） -----------------------------------------------------
                Item(
                    "mud-ball",
                    "泥だんご",
                    1,
                    CardElement.Blunt,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "敵1体に投げつけ、{0}ダメージ。",
                    Damage(Enemy, Phy, 70)
                ),
                Item(
                    "lotus-root",
                    "蓮の根",
                    1,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体のHPを{0}回復。",
                    Heal(Ally, Mag, 80)
                ),
                Item(
                    "wisp-wick",
                    "鬼火の灯心",
                    2,
                    CardElement.Fire,
                    CardKind.Attack,
                    Enemy,
                    1,
                    "敵1体に{0}ダメージ。[やけど]2ターン。",
                    Damage(Enemy, Mag, 50),
                    Apply(Enemy, CardStatus.Burn, 2)
                ),
                Item(
                    "bog-venom",
                    "沼の毒水",
                    3,
                    CardElement.None,
                    CardKind.Debuff,
                    Enemies,
                    1,
                    "敵全体に[毒]を3つ重ねる。",
                    Apply(Enemies, CardStatus.Poison, 0, 3)
                ),
                Item(
                    "croc-tear",
                    "大ワニの涙",
                    4,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    2,
                    "味方全体の[状態異常]を消し、HPを{0}回復。",
                    Simple(CardActionKind.Cleanse, Allies),
                    Heal(Allies, Mag, 45)
                ),
                // --- アルマジャ王墓（Lv 26） -------------------------------------------------
                Item(
                    "cactus-fruit",
                    "サボテンの実",
                    1,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体のHPを{0}回復。",
                    Heal(Ally, Mag, 90)
                ),
                Item(
                    "scorpion-stinger",
                    "砂サソリの毒針",
                    1,
                    CardElement.Pierce,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "敵1体に{0}ダメージ。[毒]を1つ重ねる。",
                    Damage(Enemy, Phy, 70),
                    Apply(Enemy, CardStatus.Poison, 0, 1)
                ),
                Item(
                    "hourglass-sand",
                    "砂時計の砂",
                    2,
                    CardElement.None,
                    CardKind.Debuff,
                    Enemy,
                    1,
                    "敵1体の行動順を1つ遅らせ、カードを1枚引く。",
                    Simple(CardActionKind.Delay, Enemy, 1),
                    Simple(CardActionKind.Draw, Nobody, 1)
                ),
                Item(
                    "grimoire-page",
                    "魔導書の切れ端",
                    3,
                    CardElement.Thunder,
                    CardKind.Attack,
                    Enemies,
                    1,
                    "敵全体に{0}ダメージ。",
                    Damage(Enemies, Mag, 60)
                ),
                Item(
                    "brass-spring",
                    "真鍮のぜんまい",
                    5,
                    CardElement.None,
                    CardKind.Support,
                    Nobody,
                    1,
                    "カードを2枚引き、次に使うカードのコストを0にする。",
                    Simple(CardActionKind.Draw, Nobody, 2),
                    Simple(CardActionKind.NextCardFree)
                ),
                // --- 月影の墓所（Lv 30） -----------------------------------------------------
                Item(
                    "grave-candle",
                    "墓前のろうそく",
                    1,
                    CardElement.Fire,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "敵1体に火を移し、{0}ダメージ。",
                    Damage(Enemy, Mag, 90)
                ),
                Item(
                    "moonflower-honey",
                    "月見草の蜜",
                    1,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体のHPを{0}回復。",
                    Heal(Ally, Mag, 100)
                ),
                Item(
                    "raven-feather",
                    "大鴉の羽根",
                    2,
                    CardElement.None,
                    CardKind.Buff,
                    Allies,
                    1,
                    "味方全体の速度を2ターン上げる。",
                    Apply(Allies, CardStatus.SpeedUp, 2, 20)
                ),
                Item(
                    "holy-water",
                    "聖水",
                    3,
                    CardElement.None,
                    CardKind.Guard,
                    Allies,
                    1,
                    "味方全体の[状態異常]を消し、[ブロック]{0}。",
                    Simple(CardActionKind.Cleanse, Allies),
                    Block(Allies, Def, 50)
                ),
                Item(
                    "soul-phial",
                    "魂の小瓶",
                    5,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    2,
                    "味方1体のHPを最大の半分回復。倒れていれば復活。",
                    Simple(CardActionKind.Revive, Ally, 50)
                ),
                // --- ヴォルガン火山（Lv 35） -------------------------------------------------
                Item(
                    "lava-rock",
                    "溶岩のつぶて",
                    1,
                    CardElement.Fire,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "敵1体に投げつけ、{0}ダメージ。",
                    Damage(Enemy, Phy, 100)
                ),
                Item(
                    "lizard-tail",
                    "火とかげのしっぽ",
                    1,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体のHPを{0}回復。",
                    Heal(Ally, Mag, 110)
                ),
                Item(
                    "sulfur-powder",
                    "硫黄の粉",
                    2,
                    CardElement.Fire,
                    CardKind.Attack,
                    Enemies,
                    1,
                    "敵全体に{0}ダメージ。[やけど]2ターン。",
                    Damage(Enemies, Mag, 45),
                    Apply(Enemies, CardStatus.Burn, 2)
                ),
                Item(
                    "flametail-plume",
                    "炎尾の尾羽",
                    3,
                    CardElement.None,
                    CardKind.Buff,
                    Ally,
                    1,
                    "味方1体の攻撃に3ターン炎属性を加え、攻撃力を2ターン25%上げる。",
                    Apply(Ally, CardStatus.FireBlade, 3),
                    Apply(Ally, CardStatus.AttackUp, 2, 25)
                ),
                Item(
                    "obsidian-core",
                    "黒曜石の核",
                    5,
                    CardElement.Blunt,
                    CardKind.Attack,
                    Enemies,
                    2,
                    "敵全体に{0}ダメージ。[防御ダウン]2ターン。",
                    Damage(Enemies, Phy, 110),
                    Apply(Enemies, CardStatus.Vulnerable, 2)
                ),
                // --- 白嶺ネーヴェ（Lv 40） ---------------------------------------------------
                Item(
                    "hard-snowball",
                    "固い雪玉",
                    1,
                    CardElement.Ice,
                    CardKind.Attack,
                    Enemy,
                    0,
                    "敵1体に投げつけ、{0}ダメージ。",
                    Damage(Enemy, Phy, 115)
                ),
                Item(
                    "snowdrop",
                    "雪割り草",
                    1,
                    CardElement.None,
                    CardKind.Heal,
                    Ally,
                    1,
                    "味方1体のHPを{0}回復。",
                    Heal(Ally, Mag, 125)
                ),
                Item(
                    "boar-jerky",
                    "猪の干し肉",
                    2,
                    CardElement.None,
                    CardKind.Heal,
                    Allies,
                    1,
                    "味方全体のHPを{0}回復。",
                    Heal(Allies, Mag, 55)
                ),
                Item(
                    "crystal-horn",
                    "水晶角のかけら",
                    3,
                    CardElement.Ice,
                    CardKind.Attack,
                    Enemy,
                    2,
                    "敵1体に{0}ダメージ。[凍結]させる。",
                    Damage(Enemy, Mag, 120),
                    Apply(Enemy, CardStatus.Freeze, 1)
                ),
                Item(
                    "mistdragon-scale",
                    "霧竜の鱗",
                    5,
                    CardElement.None,
                    CardKind.Guard,
                    Allies,
                    1,
                    "味方全体の受けるダメージを2ターン30%減らし、エネルギーを1回復する。",
                    Apply(Allies, CardStatus.Protect, 2, 30),
                    Simple(CardActionKind.Energy, Nobody, 1)
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
