using System.Linq;
using Baryonyx.Equipment;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Baryonyx.Tests.EditMode
{
    // 生成したライブラリのデータ（LibraryScreenAssets.CreateData）が、元の定義と食い違っていないこと。
    public sealed class LibraryDataTests
    {
        // 商会の「買う」「合成」とライブラリは、装備の効果を焼き込んだ説明文で出す。編成の装備の画面と同じ書き方にする。
        [Test]
        public void TheGearDescriptionsMatchTheCatalog()
        {
            var data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            Assert.That(data, Is.Not.Null);
            var gear = data
                .Entries.Where(entry =>
                    entry.Kind is LibraryKind.Weapon or LibraryKind.Armor or LibraryKind.Accessory
                )
                .ToArray();
            Assert.That(gear, Is.Not.Empty);
            foreach (var entry in gear)
                Assert.That(
                    entry.Description,
                    Is.EqualTo(EquipmentCatalog.Find(entry.Id)?.Detail),
                    entry.Id
                );
        }
    }
}
