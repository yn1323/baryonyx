using System;
using Baryonyx.Combat;
using UnityEngine;

namespace Baryonyx.Training
{
    /// <summary>
    /// The mock growth of each character until levels have data (doc/features/progression.md):
    /// the stats at Lv 100, the two passive and two unique skills with the levels that unlock
    /// them, and the runes a level costs. The characters themselves (name, level, art, cards)
    /// are the party's mock data.
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

        // Lv 100 のステータス。各Lvの値は、成長率を掛けて出す（doc/features/progression.md）。
        public CharacterStats Level100;

        public TrainingSkill[] Passives = Array.Empty<TrainingSkill>();
        public TrainingSkill[] Uniques = Array.Empty<TrainingSkill>();

        public CharacterStats StatsAt(int level) => Level100.At(level);
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
}
