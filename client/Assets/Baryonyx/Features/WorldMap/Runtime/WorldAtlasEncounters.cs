using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Baryonyx.Adventure;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;

namespace Baryonyx.WorldMap
{
    /// <summary>How strong a square's battle is: as the dungeons' packs, strong enemies or masters.</summary>
    public enum WorldBattleStrength
    {
        Pack,
        Elite,
        Boss,
    }

    /// <summary>
    /// The enemies a square's words call for (doc/features/world-atlas/square-events.md#戦闘の設定), read before
    /// they are drawn: the enemies named, how many are drawn at random, and what could not be read.
    /// </summary>
    public sealed class WorldBattleWords
    {
        public List<string> Named { get; } = new();

        /// <summary>Enemies drawn at random; -1 lets the strength's formations decide how many.</summary>
        public int Random { get; set; } = -1;

        public List<string> Errors { get; } = new();

        public int Count => Named.Count + Math.Max(0, Random);
    }

    /// <summary>
    /// The battles of the nested world map's squares (doc/features/world-atlas/square-events.md#戦闘の設定). A
    /// square's battle is set in words, a level and a strength. The words name enemies by their
    /// names ("苔スライム、苔むした狼", "苔スライム×2") and call for random ones ("ランダム",
    /// "ランダム2体"), up to three in all; empty words are all random. Random enemies come from the
    /// dungeon whose level is nearest the battle's, in the formations and with the strength of its
    /// packs, strong enemies or masters (AdventureCatalog), and the level is the square's own (the
    /// map's recommended level when not set).
    /// </summary>
    public static class WorldAtlasEncounters
    {
        public const int MaxEnemies = 3;
        public const string RandomWord = "ランダム";

        private static readonly char[] Separators = { '、', ',', '，', '・', ' ', '　', '\n' };
        private static readonly Regex RandomToken = new(@"^ランダム(?:([0-9０-９])体?)?$");
        private static readonly Regex CountedToken = new(@"^(.+?)\s*[×xX＊*]\s*([0-9０-９])$");

        public static string NameOf(WorldBattleStrength strength) =>
            strength switch
            {
                WorldBattleStrength.Elite => "強い",
                WorldBattleStrength.Boss => "主",
                _ => "ふつう",
            };

        /// <summary>The strength a layout's name gives (empty or unknown is a pack).</summary>
        public static WorldBattleStrength StrengthOf(string name) =>
            name switch
            {
                "elite" => WorldBattleStrength.Elite,
                "boss" => WorldBattleStrength.Boss,
                _ => WorldBattleStrength.Pack,
            };

        /// <summary>The name a layout gives <paramref name="strength"/> (null for a pack).</summary>
        public static string LayoutNameOf(WorldBattleStrength strength) =>
            strength switch
            {
                WorldBattleStrength.Elite => "elite",
                WorldBattleStrength.Boss => "boss",
                _ => null,
            };

        /// <summary>Reads the words of a square's battle.</summary>
        public static WorldBattleWords Read(string words)
        {
            var read = new WorldBattleWords();
            var tokens = (words ?? "")
                .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => token.Length > 0)
                .ToList();
            bool bareRandom = false;
            foreach (string token in tokens)
            {
                var random = RandomToken.Match(token);
                if (random.Success)
                {
                    if (random.Groups[1].Success)
                        read.Random = Math.Max(0, read.Random) + Digit(random.Groups[1].Value);
                    else
                        bareRandom = true;
                    continue;
                }
                string name = token;
                int count = 1;
                var counted = CountedToken.Match(token);
                if (counted.Success)
                {
                    name = counted.Groups[1].Value.Trim();
                    count = Digit(counted.Groups[2].Value);
                }
                var enemy = Find(name);
                if (enemy == null)
                {
                    read.Errors.Add($"「{name}」という敵はいません");
                    continue;
                }
                for (int i = 0; i < count; i++)
                    read.Named.Add(enemy.Id);
            }
            // 名前と並べた「ランダム」は1体。「ランダム」だけなら、数も編成に任せる。
            if (bareRandom && read.Named.Count > 0)
                read.Random = Math.Max(0, read.Random) + 1;
            if (read.Count > MaxEnemies)
                read.Errors.Add($"敵は{MaxEnemies}体までです");
            if (read.Random == 0 && read.Named.Count == 0)
                read.Errors.Add("敵が1体もいません");
            return read;
        }

        /// <summary>
        /// The battle of a square, or null when its words cannot be read. <paramref name="seed"/>
        /// draws the random enemies; the same seed draws the same ones.
        /// </summary>
        public static AdventureEncounter Draw(
            string id,
            string words,
            int level,
            WorldBattleStrength strength,
            int seed
        )
        {
            var read = Read(words);
            if (read.Errors.Count > 0)
                return null;
            var area = AreaNear(level);
            var random = new RouteRandom(seed);
            var formation = Formation(read, strength, random);
            var kinds = new string[MaxEnemies];
            var taken = new List<string>();
            var named = read.Named.ToList();
            if (formation != null)
            {
                // 名前の敵を、同じ大きさの立ち位置から入れる。
                for (int place = 0; place < MaxEnemies; place++)
                {
                    var body = formation.Places[place];
                    int match = named.FindIndex(kind => body != null && BodyOf(kind) == body);
                    if (match < 0)
                        continue;
                    kinds[place] = named[match];
                    taken.Add(named[match]);
                    named.RemoveAt(match);
                }
                for (int place = 0; place < MaxEnemies; place++)
                {
                    var body = formation.Places[place];
                    if (body == null || kinds[place] != null)
                        continue;
                    kinds[place] =
                        named.Count > 0
                            ? Pop(named)
                            : Take(PoolOf(area, body.Value), taken, random);
                    if (kinds[place] != null)
                        taken.Add(kinds[place]);
                }
            }
            else
            {
                // 合う編成がないときは、小さい順に前から並べる。
                var all = named
                    .Concat(
                        Enumerable
                            .Range(0, Math.Max(0, read.Random))
                            .Select(_ =>
                                Take(area.Small.Concat(area.Medium).ToArray(), taken, random)
                            )
                    )
                    .Where(kind => kind != null)
                    .OrderBy(kind => BodyOf(kind) ?? EnemyBody.Medium)
                    .ToList();
                int first = all.Count == 1 ? 1 : 0;
                for (int i = 0; i < all.Count && first + i < MaxEnemies; i++)
                    kinds[first + i] = all[i];
            }
            var (hp, power) = Scales(strength, formation);
            return new AdventureEncounter(id, kinds, level, hp, power);
        }

        /// <summary>The battle background of a battle at <paramref name="level"/>.</summary>
        public static BattleStage StageNear(int level) => AreaNear(level).Stage;

        /// <summary>
        /// What a square's battle comes to, in words for the map editor: the enemies named and
        /// where the random ones come from, the level and the strength, or why it cannot be read.
        /// </summary>
        public static string Describe(string words, int level, WorldBattleStrength strength)
        {
            var read = Read(words);
            if (read.Errors.Count > 0)
                return string.Join("\n", read.Errors);
            var parts = read.Named.Select(kind => CombatEnemies.Find(kind)?.Name ?? kind).ToList();
            var area = AreaNear(level);
            if (read.Random > 0)
                parts.Add($"{RandomWord}{read.Random}体");
            else if (read.Random < 0)
                parts.Add(RandomWord);
            string where =
                read.Random != 0 ? $"\nランダムは{area.Name}（Lv{area.Level}）の魔物から" : "";
            return $"{string.Join("・", parts)}　Lv{level}・{NameOf(strength)}{where}";
        }

        private static int Digit(string text) => (int)char.GetNumericValue(text[0]);

        private static EnemyDefinition Find(string name) =>
            CombatEnemies.All.FirstOrDefault(enemy => enemy.Name == name || enemy.Id == name);

        private static EnemyBody? BodyOf(string kind) => CombatEnemyStats.Find(kind)?.Body;

        // 戦闘のLvにいちばん近い行き先。同じだけ近いときは手前の行き先。
        private static AdventureArea AreaNear(int level) =>
            AdventureCatalog.Areas.OrderBy(area => Math.Abs(area.Level - level)).First();

        private static string[] PoolOf(AdventureArea area, EnemyBody body) =>
            body switch
            {
                EnemyBody.Small => area.Small,
                EnemyBody.Medium => area.Medium,
                _ => area.Bosses,
            };

        // 強さの編成のうち、敵の数と名前の敵の大きさに合うものを1つ選ぶ。数を任されたときは、どれでもよい。
        private static AdventureFormation Formation(
            WorldBattleWords read,
            WorldBattleStrength strength,
            RouteRandom random
        )
        {
            var fits = FormationsOf(strength)
                .Where(formation =>
                    (read.Random < 0 && read.Named.Count == 0)
                    || formation.Places.Count(body => body != null) == read.Count
                )
                .Where(formation => Hosts(formation, read.Named))
                .ToList();
            return fits.Count == 0 ? null : fits[random.Range(0, fits.Count)];
        }

        private static IReadOnlyList<AdventureFormation> FormationsOf(
            WorldBattleStrength strength
        ) =>
            strength switch
            {
                WorldBattleStrength.Elite => AdventureCatalog.EliteFormations,
                WorldBattleStrength.Boss => new[]
                {
                    new AdventureFormation(
                        new EnemyBody?[] { EnemyBody.Small, EnemyBody.Medium, EnemyBody.Large },
                        1f,
                        1f,
                        1,
                        1
                    ),
                },
                _ => AdventureCatalog.PackFormations,
            };

        // 名前の敵を、それぞれ同じ大きさの立ち位置に入れられるか。
        private static bool Hosts(AdventureFormation formation, IReadOnlyList<string> named)
        {
            var free = formation.Places.Where(body => body != null).ToList();
            foreach (string kind in named)
            {
                int at = free.IndexOf(BodyOf(kind));
                if (at < 0)
                    return false;
                free.RemoveAt(at);
            }
            return true;
        }

        private static (float Hp, float Power) Scales(
            WorldBattleStrength strength,
            AdventureFormation formation
        ) =>
            strength switch
            {
                WorldBattleStrength.Elite => (
                    AdventureCatalog.EliteHpScale * (formation?.HpScale ?? 1f),
                    AdventureCatalog.ElitePowerScale * (formation?.PowerScale ?? 1f)
                ),
                WorldBattleStrength.Boss => (
                    AdventureCatalog.BossHpScale,
                    AdventureCatalog.BossPowerScale
                ),
                _ => (
                    AdventureCatalog.PackHpScale * (formation?.HpScale ?? 1f),
                    AdventureCatalog.PackPowerScale * (formation?.PowerScale ?? 1f)
                ),
            };

        private static string Pop(List<string> named)
        {
            string kind = named[0];
            named.RemoveAt(0);
            return kind;
        }

        // 一覧から、同じ戦闘にまだいない種類を1体選ぶ。みな出ていれば、一覧のどれでもよい。
        private static string Take(string[] kinds, List<string> taken, RouteRandom random)
        {
            if (kinds == null || kinds.Length == 0)
                return null;
            var left = kinds.Where(kind => !taken.Contains(kind)).ToArray();
            var from = left.Length > 0 ? left : kinds;
            return from[random.Range(0, from.Length)];
        }
    }
}
