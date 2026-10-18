using System;
using UnityEngine;

namespace Baryonyx.Library
{
    /// <summary>
    /// The library's entries as the screen shows them, written by the generator from each
    /// feature's own definitions (the party's characters, the card skills, the equipment, the
    /// enemies), in their order, so the panels read the art without loading by path. Generate it
    /// again when a definition changes; it holds nothing the player found.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Library Data")]
    public sealed class LibraryData : ScriptableObject
    {
        public LibraryEntryData[] Entries = Array.Empty<LibraryEntryData>();

        // 属性ごとのアイコン（CardElementの値の順。Noneはnull）。敵の開示した弱点に出す。
        public Sprite[] ElementIcons = Array.Empty<Sprite>();
    }

    [Serializable]
    public sealed class LibraryEntryData
    {
        public LibraryKind Kind;
        public string Id = "";

        // 分類ごとに定義の並び順で1から振る番号（doc/features/library.md）。
        [Min(1)]
        public int Number = 1;
        public string Name = "";

        // ☆の数。ないものは0。
        [Min(0)]
        public int Rarity;

        // 種類の行（「武器」「攻撃・敵単体｜コスト2」など、リッチテキスト）と説明。
        public string KindLine = "";
        public string Description = "";

        public Texture2D Art;
        public Rect ArtUv = new(0f, 0f, 1f, 1f);
        public Color ArtTint = Color.white;

        /// <summary>The size of the art's shown part in dots (the texture cut to <see cref="ArtUv"/>).</summary>
        public Vector2 ArtDots =>
            Art != null
                ? new Vector2(
                    Mathf.Abs(ArtUv.width) * Art.width,
                    Mathf.Abs(ArtUv.height) * Art.height
                )
                : Vector2.zero;
    }
}
