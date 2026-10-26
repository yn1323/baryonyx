using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Items;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.UI.Cards;
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

        // カードを作り直したら、ライブラリと商会の品質強化の一覧も作り直す。古いコストと威力のまま残らないこと。
        [Test]
        public void TheCardEntriesMatchTheCards()
        {
            var data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            Assert.That(data, Is.Not.Null);
            foreach (var entry in data.Entries.Where(entry => entry.Kind == LibraryKind.Skill))
            {
                var card = CardSkills.Find(entry.Id);
                Assert.That(card, Is.Not.Null, entry.Id);
                Assert.That(
                    entry.KindLine,
                    Is.EqualTo(LibraryScreenAssets.KindLineOf(card, card.Cost)),
                    entry.Id
                );
                Assert.That(entry.Description, Is.EqualTo(CardText.Description(card)), entry.Id);
            }
            foreach (var entry in data.Entries.Where(entry => entry.Kind == LibraryKind.Item))
            {
                var item = ItemCards.Find(entry.Id);
                Assert.That(item, Is.Not.Null, entry.Id);
                Assert.That(
                    entry.KindLine,
                    Is.EqualTo(LibraryScreenAssets.KindLineOf(item.Card, item.Cost)),
                    entry.Id
                );
                Assert.That(
                    entry.Description,
                    Is.EqualTo(CardText.Description(item.Card)),
                    entry.Id
                );
            }
        }
    }
}
