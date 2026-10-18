using System;
using System.Collections.Generic;

namespace Baryonyx.Library
{
    /// <summary>The kinds the library lists (doc/features/library.md), in the menu's order.</summary>
    public enum LibraryKind
    {
        Character,
        Weapon,
        Armor,
        Accessory,
        Skill,
        Item,
        Enemy,
        Steps,
    }

    public static class LibraryKinds
    {
        /// <summary>The kinds whose entries are found (all but the daily steps).</summary>
        public static readonly IReadOnlyList<LibraryKind> Entries = new[]
        {
            LibraryKind.Character,
            LibraryKind.Weapon,
            LibraryKind.Armor,
            LibraryKind.Accessory,
            LibraryKind.Skill,
            LibraryKind.Item,
            LibraryKind.Enemy,
        };

        // サーバーの記録での分類の名前（server/src/features/library/catalog.ts）。
        public static string Key(LibraryKind kind) =>
            kind switch
            {
                LibraryKind.Character => "character",
                LibraryKind.Weapon => "weapon",
                LibraryKind.Armor => "armor",
                LibraryKind.Accessory => "accessory",
                LibraryKind.Skill => "skill",
                LibraryKind.Item => "item",
                LibraryKind.Enemy => "enemy",
                _ => null,
            };

        public static bool TryParse(string key, out LibraryKind kind)
        {
            foreach (var entry in Entries)
                if (Key(entry) == key)
                {
                    kind = entry;
                    return true;
                }
            kind = LibraryKind.Steps;
            return false;
        }

        // メニューと見出しに出す名前。
        public static string Label(LibraryKind kind) =>
            kind switch
            {
                LibraryKind.Character => "キャラクター",
                LibraryKind.Weapon => "武器",
                LibraryKind.Armor => "防具",
                LibraryKind.Accessory => "アクセサリー",
                LibraryKind.Skill => "スキル",
                LibraryKind.Item => "アイテム",
                LibraryKind.Enemy => "敵",
                LibraryKind.Steps => "日々の歩数",
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

        /// <summary>The key of the guide screen's item that opens the kind's panel.</summary>
        public static string GuideItemKey(LibraryKind kind) =>
            "library-" + kind.ToString().ToLowerInvariant();
    }
}
