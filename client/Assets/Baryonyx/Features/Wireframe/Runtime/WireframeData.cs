using System;
using UnityEngine;

namespace Baryonyx.Wireframe
{
    // These are display samples, not game balance or a production item catalog.
    public sealed class WireframeData : ScriptableObject
    {
        public WireCharacter[] Characters =
        {
            new("アリア", "守り手", "かばう", "盾の一撃", "鉄壁の構え"),
            new("トーマ", "魔法使い", "火の矢", "大きな火球", "星降る夜の魔法陣"),
            new("ルカ", "斥候", "速突き", "足払い", "追い風の連撃"),
            new("ミナ", "癒し手", "手当て", "守りの光", "みんなを包む祈り"),
            new("ノエル", "旅の戦士", "横なぎ", "砕き打ち", "渾身の一振り"),
        };
        public WireEquipment[] Equipment =
        {
            new("旅立ちの武器", 12, 8),
            new("古い護身の武器", 9, 16),
            new("宝箱の武器", 18, 14),
        };
        public string[] Places = { "木漏れ日の森", "古い坑道" };
    }

    [Serializable]
    public sealed class WireCharacter
    {
        public string Name;
        public string Role;
        public string[] Skills;

        public WireCharacter(string name, string role, params string[] skills)
        {
            Name = name;
            Role = role;
            Skills = skills;
        }
    }

    [Serializable]
    public sealed class WireEquipment
    {
        public string Name;
        public int Power;
        public int Down;

        public WireEquipment(string name, int power, int down)
        {
            Name = name;
            Power = power;
            Down = down;
        }
    }
}
