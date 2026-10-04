using System;
using UnityEngine;

namespace Baryonyx.Training
{
    /// <summary>
    /// The mock growth of each character until levels have data (doc/features/progression.md):
    /// the stats at Lv 1 and their rise per level, the two passive and two unique skills with
    /// the levels that unlock them, and the runes a level costs. The characters themselves
    /// (name, level, art, cards) are the party's mock data.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Training Mock Data")]
    public sealed class TrainingMockData : ScriptableObject
    {
        public TrainingCharacter[] Characters = Array.Empty<TrainingCharacter>();

        // Lv n から n+1 へ上げるのに要るルーンは n × CostPerLevel。
        [Min(1)]
        public int CostPerLevel = 100;

        [Min(2)]
        public int MaxLevel = 30;

        // ホームで所持ルーンを取得していないとき（展示室、シーンを単体で開いたとき）の所持ルーン。
        [Min(0)]
        public int MockRunes = 8450;

        public TrainingCharacter Find(string id) =>
            string.IsNullOrEmpty(id) ? null : Array.Find(Characters, entry => entry.Id == id);
    }

    [Serializable]
    public sealed class TrainingCharacter
    {
        // PartyMockData.Members の Id。
        public string Id = "";

        // Lv 1 のステータスと、1レベル上がるごとの伸び。
        public TrainingStats Base;
        public TrainingStats Growth;

        public TrainingSkill[] Passives = Array.Empty<TrainingSkill>();
        public TrainingSkill[] Uniques = Array.Empty<TrainingSkill>();

        public TrainingStats StatsAt(int level) => Base + Growth * Mathf.Max(0, level - 1);
    }

    [Serializable]
    public sealed class TrainingSkill
    {
        public string Name = "";
        public string Description = "";

        [Min(1)]
        public int UnlockLevel = 1;

        // 固有スキルを使うときに払うエネルギー。パッシブは0。
        [Min(0)]
        public int Energy;

        // 仮のアイコン。カードの挿絵（64×58）の中央24×24ドットを4倍で出す。
        public Texture2D Icon;
    }

    /// <summary>The five stats of a character (provisional items, doc/features/party.md).</summary>
    [Serializable]
    public struct TrainingStats
    {
        public const int Count = 5;

        // 表示の順。HP・ちから・まりょく・まもり・すばやさ。
        public static readonly string[] Labels =
        {
            "HP",
            "ちから",
            "まりょく",
            "まもり",
            "すばやさ",
        };

        public int Hp;
        public int Strength;
        public int Magic;
        public int Defense;
        public int Speed;

        public TrainingStats(int hp, int strength, int magic, int defense, int speed)
        {
            Hp = hp;
            Strength = strength;
            Magic = magic;
            Defense = defense;
            Speed = speed;
        }

        public int this[int index] =>
            index switch
            {
                0 => Hp,
                1 => Strength,
                2 => Magic,
                3 => Defense,
                4 => Speed,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        public static TrainingStats operator +(TrainingStats a, TrainingStats b) =>
            new(
                a.Hp + b.Hp,
                a.Strength + b.Strength,
                a.Magic + b.Magic,
                a.Defense + b.Defense,
                a.Speed + b.Speed
            );

        public static TrainingStats operator -(TrainingStats a, TrainingStats b) =>
            new(
                a.Hp - b.Hp,
                a.Strength - b.Strength,
                a.Magic - b.Magic,
                a.Defense - b.Defense,
                a.Speed - b.Speed
            );

        public static TrainingStats operator *(TrainingStats a, int times) =>
            new(
                a.Hp * times,
                a.Strength * times,
                a.Magic * times,
                a.Defense * times,
                a.Speed * times
            );
    }
}
