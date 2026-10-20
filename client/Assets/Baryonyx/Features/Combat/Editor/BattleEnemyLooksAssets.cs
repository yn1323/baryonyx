using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor.Art;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// Makes the table of every enemy's picture for the battle screen (<see cref="BattleEnemyLooks"/>):
    /// the picture, its drawn part and its face. The mock's three enemies keep the faces chosen for
    /// them; the others take a square at the top of their drawn part, as wide as the turn order's
    /// icon, centred on the dots of their head.
    /// </summary>
    public static class BattleEnemyLooksAssets
    {
        public const string AssetPath = "Assets/Baryonyx/Features/Combat/UI/BattleEnemyLooks.asset";
        private const string EnemyArtFolder = "Assets/Baryonyx/Features/Combat/UI/Art/Enemies";

        [MenuItem("Baryonyx/Combat/Create Enemy Looks")]
        public static void CreateMenu() => Create(24);

        /// <summary>Makes (or remakes) the table, with faces <paramref name="faceDots"/> dots square.</summary>
        public static BattleEnemyLooks Create(int faceDots)
        {
            var looks = AssetDatabase.LoadAssetAtPath<BattleEnemyLooks>(AssetPath);
            if (looks == null)
            {
                looks = ScriptableObject.CreateInstance<BattleEnemyLooks>();
                AssetDatabase.CreateAsset(looks, AssetPath);
            }
            var faces = BattleInspectAssets.Enemies.ToDictionary(
                spec => spec.Definition.Id,
                spec => spec.Face
            );
            var list = new List<BattleEnemyLook>();
            foreach (var enemy in CombatEnemies.All)
            {
                var path = $"{EnemyArtFolder}/{ArtName(enemy.Id)}.aseprite";
                ArtAssets.ImportTexture(path, FilterMode.Point);
                var texture = ArtAssets.LoadTexture(path);
                if (texture == null)
                    throw new InvalidOperationException($"Missing enemy art: {path}");
                var bounds = BattleInspectAssets.OpaqueBoundsOf(texture);
                var face = faces.TryGetValue(enemy.Id, out var chosen)
                    ? chosen
                    : FaceOf(texture, bounds, faceDots);
                list.Add(
                    new BattleEnemyLook
                    {
                        Id = enemy.Id,
                        Texture = texture,
                        TargetCenter = new Vector2(
                            bounds.center.x - texture.width * 0.5f,
                            bounds.center.y
                        ),
                        TargetSize = bounds.size,
                        IconUv = new Rect(
                            face.x / (float)texture.width,
                            (texture.height - face.y - faceDots) / (float)texture.height,
                            faceDots / (float)texture.width,
                            faceDots / (float)texture.height
                        ),
                    }
                );
            }
            looks.Looks = list.ToArray();
            EditorUtility.SetDirty(looks);
            AssetDatabase.SaveAssetIfDirty(looks);
            return looks;
        }

        /// <summary>"moss-slime" → "MossSlime", the picture's file name.</summary>
        public static string ArtName(string id) =>
            string.Concat(
                id.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1))
            );

        // 描かれた部分の上端から、顔の大きさの正方形を取る。横は、その範囲の描かれたドットの真ん中にそろえる。
        // 返す位置は、画像の上から数えた行の左上。
        private static Vector2Int FaceOf(Texture2D texture, Rect bounds, int size)
        {
            int top = texture.height - (int)bounds.yMax;
            int left = Mathf.RoundToInt(bounds.center.x - size * 0.5f);
            var pixels = BattleInspectAssets.PixelsOf(texture);
            if (pixels != null)
            {
                long sum = 0;
                int count = 0;
                int rowFrom = Math.Max(0, (int)bounds.yMax - size);
                for (int y = rowFrom; y < (int)bounds.yMax; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 128)
                        continue;
                    sum += x;
                    count++;
                }
                if (count > 0)
                    left = Mathf.RoundToInt(sum / (float)count - size * 0.5f);
            }
            left = Mathf.Clamp(left, 0, Math.Max(0, texture.width - size));
            top = Mathf.Clamp(top, 0, Math.Max(0, texture.height - size));
            return new Vector2Int(left, top);
        }
    }
}
