using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// One kind of enemy: the id it is recorded under (its revealed weaknesses on the server,
    /// server/src/features/combat/catalog.ts), its name and its weaknesses in icon order.
    /// </summary>
    public sealed class EnemyDefinition
    {
        public EnemyDefinition(string id, string name, params CardElement[] weaknesses)
        {
            Id = id;
            Name = name;
            Weaknesses = weaknesses ?? Array.Empty<CardElement>();
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<CardElement> Weaknesses { get; }
    }

    /// <summary>
    /// The kinds of enemy (doc/catalog/enemies, the ids without their "enemy-" prefix). Add a
    /// new kind here and to the server's ENEMY_IDS, so its weaknesses can be recorded.
    /// </summary>
    public static class CombatEnemies
    {
        public static readonly EnemyDefinition MossSlime = new(
            "moss-slime",
            "苔スライム",
            CardElement.Thunder,
            CardElement.Fire
        );

        public static readonly EnemyDefinition MossWolf = new(
            "moss-wolf",
            "苔むした狼",
            CardElement.Ice,
            CardElement.Fire
        );

        public static readonly EnemyDefinition ForestGuardian = new(
            "forest-guardian",
            "森の守り手",
            CardElement.Fire,
            CardElement.Slash
        );

        public static readonly IReadOnlyList<EnemyDefinition> All = new[]
        {
            MossSlime,
            MossWolf,
            ForestGuardian,
        };

        public static EnemyDefinition Find(string id)
        {
            foreach (var enemy in All)
                if (enemy.Id == id)
                    return enemy;
            return null;
        }
    }

    /// <summary>The names the server keeps the elements under ("slash", "fire" and so on).</summary>
    public static class CardElementKeys
    {
        public static string Key(CardElement element) =>
            element switch
            {
                CardElement.Slash => "slash",
                CardElement.Fire => "fire",
                CardElement.Ice => "ice",
                CardElement.Thunder => "thunder",
                CardElement.Blunt => "blunt",
                CardElement.Pierce => "pierce",
                _ => null,
            };

        public static bool TryParse(string key, out CardElement element)
        {
            element = key switch
            {
                "slash" => CardElement.Slash,
                "fire" => CardElement.Fire,
                "ice" => CardElement.Ice,
                "thunder" => CardElement.Thunder,
                "blunt" => CardElement.Blunt,
                "pierce" => CardElement.Pierce,
                _ => CardElement.None,
            };
            return element != CardElement.None;
        }
    }
}
