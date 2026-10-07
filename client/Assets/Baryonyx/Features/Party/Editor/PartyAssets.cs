using System;
using System.IO;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using UnityEditor;
using UnityEngine;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;

namespace Baryonyx.Party.Editor
{
    /// <summary>
    /// The party mock data: the characters with their battle sprite, illustration, level and
    /// card skills, the four party slots, and the art the formation's screens draw the cards and
    /// elements with. The characters, levels and cards are mock data until characters have data
    /// (doc/features/party.md).
    /// </summary>
    public static class PartyAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Party";
        public const string DataPath = Folder + "/Data/PartyMockData.asset";
        public const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";
        public const string IllustrationFolder = CharacterArtFolder + "/Illustrations";
        public const string AttributeArtFolder = "Assets/Baryonyx/Shared/Art/Attributes";
        public const string SkillArtFolder = "Assets/Baryonyx/Features/Combat/UI/Art";

        [MenuItem("Baryonyx/Party/Create Mock Data")]
        public static PartyMockData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.Refresh();
            var data = AssetFolders.LoadOrCreate<PartyMockData>(DataPath);
            // カスタムスキルの2枚。戦闘画面のモックと同じスキルから、属性の違う2枚を選ぶ（サーバーの初期データと同じ）。
            data.Members = new[]
            {
                Member("toma", "トーマ", 12, "Toma", "Fire", "Ice"),
                Member("luka", "ルカ", 11, "Luka", "VitalThrust", "Thunder"),
                Member("aria", "アリア", 10, "Aria", "Slash", "ShieldBash"),
                Member("mina", "ミナ", 10, "Mina", "Heal", "HolyHammer"),
                // 絵のないキャラは、4人の絵に色を掛けて左右を反転した仮の絵で見分ける。
                StandIn(
                    Member("anselm", "アンセルム", 8, "Aria", "EarthSplitter", "Fire"),
                    new Color(0.8f, 0.9f, 1f),
                    flip: true
                ),
                StandIn(
                    Member("greta", "グレタ", 7, "Luka", "VitalThrust", "Ice"),
                    new Color(1f, 0.82f, 0.9f),
                    flip: false
                ),
                StandIn(
                    Member("lutz", "ルッツ", 5, "Toma", "Thunder", "ShieldBash"),
                    new Color(0.86f, 1f, 0.8f),
                    flip: true
                ),
                StandIn(
                    Member("rita", "リタ", 3, "Mina", "Slash", "VitalThrust"),
                    new Color(1f, 0.88f, 0.76f),
                    flip: true
                ),
                StandIn(
                    Member("ritsu", "リツ", 1, "Luka", "Ice", "Thunder"),
                    new Color(0.86f, 0.84f, 1f),
                    flip: true
                ),
            };
            // 縦長のイラスト（Codex CLIで描いた下書き、doc/art/direction.md）。ない人は空のまま。
            foreach (var member in data.Members)
                member.Illustration = Illustration(member.Id);
            data.Formation = new[] { "toma", "luka", "aria", "mina" };
            data.ElementIcons = ((CardElement[])Enum.GetValues(typeof(CardElement)))
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : Guide.Icon($"{AttributeArtFolder}/Element{element}.aseprite")
                )
                .ToArray();
            data.SkillArts = CardSkills
                .All.Select(card => new PartySkillArt
                {
                    Skill = card.Id,
                    Art = ArtAssets.LoadTexture($"{SkillArtFolder}/Card{card.Id}.aseprite"),
                })
                .ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            return data;
        }

        /// <summary>
        /// The character's illustration, imported smooth and compressed: it is a painted picture
        /// shown smaller than it is, not pixel art. Null when the character has none yet.
        /// </summary>
        public static Texture2D Illustration(string id)
        {
            string path = $"{IllustrationFolder}/{id}.png";
            if (!File.Exists(path))
                return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static PartyMember Member(
            string id,
            string name,
            int level,
            string art,
            params string[] cards
        ) =>
            new()
            {
                Id = id,
                Name = name,
                Level = level,
                Art = ArtAssets.ImportTexture(
                    $"{CharacterArtFolder}/Battle{art}.aseprite",
                    FilterMode.Point
                ),
                Cards = cards.Select(Card).ToArray(),
            };

        private static PartyMember StandIn(PartyMember member, Color tint, bool flip)
        {
            member.Tint = tint;
            member.Flip = flip;
            return member;
        }

        private static PartyCard Card(string skill)
        {
            if (CardSkills.Find(skill) == null)
                throw new InvalidOperationException("Unknown card skill: " + skill);
            return new PartyCard { Skill = skill };
        }
    }
}
