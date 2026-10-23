using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.Items;
using UnityEditor;

namespace Baryonyx.Inventory.Editor
{
    /// <summary>
    /// Bakes <see cref="ItemArt"/> from the 32×32 art of the items, enhancement materials,
    /// accessories, weapons and armour in Shared/Art (doc/rules/frontend-design.md, the art of
    /// the things in the game).
    /// The screens' generators call <see cref="Load"/> to hand it to their views.
    /// </summary>
    public static class ItemArtAssets
    {
        public const string AssetPath = "Assets/Baryonyx/Features/Inventory/Data/ItemArt.asset";
        public const string ArtRoot = "Assets/Baryonyx/Shared/Art";

        [MenuItem("Baryonyx/Inventory/Create Item Art")]
        public static void CreateAssets()
        {
            Load();
            AssetDatabase.SaveAssets();
        }

        /// <summary>The art file of a kind, by its folder: Items/Herb.aseprite and so on.</summary>
        public static string PathOf(string folder, string id) =>
            $"{ArtRoot}/{folder}/{ItemArt.FileName(id)}.aseprite";

        public static string FolderOf(EquipmentSlot slot) =>
            slot switch
            {
                EquipmentSlot.Weapon => "Weapons",
                EquipmentSlot.Armor => "Armor",
                _ => "Accessories",
            };

        /// <summary>The baked art, made again from the files every call so new art is never missed.</summary>
        public static ItemArt Load()
        {
            AssetFolders.Ensure(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
            var art = AssetFolders.LoadOrCreate<ItemArt>(AssetPath);
            var entries = new List<ItemArt.Entry>();
            void Add(string id, string path)
            {
                if (File.Exists(path))
                    entries.Add(
                        new ItemArt.Entry
                        {
                            Id = id,
                            Sprite = AsepriteCanvasImport.LoadSprite(path),
                        }
                    );
            }
            foreach (var item in ItemCards.All)
                Add(item.Id, PathOf("Items", item.Id));
            foreach (var gear in EquipmentCatalog.All)
                Add(gear.Id, PathOf(FolderOf(gear.Slot), gear.Id));
            foreach (var material in EnhancementMaterials.All)
                Add(material.Id, PathOf("Materials", material.Id));
            art.Entries = entries.ToArray();
            EditorUtility.SetDirty(art);
            return art;
        }
    }
}
