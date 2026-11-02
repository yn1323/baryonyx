using System.Collections;
using System.Linq;
using Baryonyx.Adventure;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // ドロップエディター。ダンジョンを選び、一覧の物を押して最奥だけにする・外す、「＋」からほかの一覧の物を移す。
    // クリア回数の報酬を押して数・物・品質を変え、変えたダンジョンをJSONにしてコピーする。
    public sealed class DropLabSceneTests
    {
        private const string ScenePath = SceneTests.ScenesFolder + "/Debug/DropLab.unity";
        private const string Forest = AdventureCatalog.ForestRuins;

        private string clipboard;

        [SetUp]
        public void Prepare()
        {
            DropLab.Forget();
            // コピーはクリップボードへ書くため、テストの前の中身を戻す。
            clipboard = GUIUtility.systemCopyBuffer;
        }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            yield return SceneTests.UnloadAll(nameof(DropLabSceneTests));
            DropLab.Forget();
            GUIUtility.systemCopyBuffer = clipboard;
        }

        private static DropLab.Tile TileOf(DropLab lab, string id) =>
            lab.Tiles.Single(tile => tile.Id == id);

        private static Transform Part(Component card, string name) =>
            card.GetComponentsInChildren<Transform>(true).First(part => part.name == name);

        [UnityTest]
        public IEnumerator DropsAndRewardsAreChangedAndTheChangesCopied()
        {
            DropLab lab = null;
            yield return SceneTests.Load<DropLab>(
                ScenePath,
                found => found.Tiles.Count > 0,
                found => lab = found
            );
            foreach (var button in new[] { lab.HomeButton, lab.DungeonButton, lab.CopyButton })
                SceneTests.AssertTouchSize(button.transform);
            SceneTests.AssertTouchSize(lab.Tiles[0].Button.transform);
            SceneTests.AssertTouchSize(lab.Rewards[0].Button.transform);
            Assert.That(lab.Dungeon, Is.EqualTo(Forest));
            Assert.That(
                lab.Tiles.Where(tile => tile.Id != "").Select(tile => tile.Id),
                Is.EquivalentTo(AdventureDrops.Pools[Forest].Select(entry => entry.Id))
            );
            Assert.That(
                Part(TileOf(lab, "guardian-seed").Button, DropLab.BossName).gameObject.activeSelf,
                Is.True
            );

            lab.CopyButton.onClick.Invoke();
            Assert.That(lab.Status.text, Is.EqualTo("変えたダンジョンはまだありません"));

            // 札を押して選び、最奥だけで落ちるようにする。☆はスキルだけが変えられる。
            TileOf(lab, "spore-powder").Button.onClick.Invoke();
            Assert.That(lab.ChosenDrop, Is.EqualTo("spore-powder"));
            Assert.That(lab.DropActions.blocksRaycasts, Is.True);
            Assert.That(lab.RewardActions.blocksRaycasts, Is.False);
            StringAssert.Contains("毒胞子の粉", lab.Info.text);
            StringAssert.StartsWith("落ちる物1つごとの割合", lab.Detail.text);
            Assert.That(lab.StarsButton.gameObject.activeSelf, Is.False);
            SceneTests.AssertTouchSize(lab.BossButton.transform);
            lab.BossButton.onClick.Invoke();
            Assert.That(lab.BossLabel.text, Is.EqualTo(DropLab.BossText(true)));
            Assert.That(
                Part(TileOf(lab, "spore-powder").Button, DropLab.BossName).gameObject.activeSelf,
                Is.True
            );
            StringAssert.Contains("魔物 —", lab.Detail.text);

            // 外す。
            TileOf(lab, "herb").Button.onClick.Invoke();
            lab.RemoveButton.onClick.Invoke();
            Assert.That(lab.Tiles.Any(tile => tile.Id == "herb"), Is.False);
            Assert.That(lab.Status.text, Is.EqualTo("薬草を一覧から外しました"));
            Assert.That(lab.DropActions.blocksRaycasts, Is.False);

            // スキルの段の「＋」から、ほかのダンジョンにあるスキルを足すと、そこから移る。強化素材は選べない。
            lab.Tiles.First(tile => tile.Id == "" && tile.Category == AdventureDropCategory.Skill)
                .Button.onClick.Invoke();
            Assert.That(lab.Picking, Is.True);
            Assert.That(lab.PickerGroup.blocksRaycasts, Is.True);
            Assert.That(lab.PickerCategory, Is.EqualTo(AdventureDropCategory.Skill));
            Assert.That(lab.PickerCategories.Last().gameObject.activeSelf, Is.False);
            SceneTests.AssertTouchSize(lab.Picks[0].Button.transform);
            var mana = lab.Picks.Single(pick => pick.Id == "ManaPrayer");
            Assert.That(
                Part(mana.Button, DropLab.DetailName).GetComponent<TMP_Text>().text,
                Is.EqualTo("港町ポルトリア")
            );
            mana.Button.onClick.Invoke();
            Assert.That(lab.Picking, Is.False);
            Assert.That(lab.Status.text, Is.EqualTo("港町ポルトリアの一覧から移しました"));
            Assert.That(lab.ChosenDrop, Is.EqualTo("ManaPrayer"));
            Assert.That(lab.StarsButton.gameObject.activeSelf, Is.True);
            lab.StarsButton.onClick.Invoke();
            Assert.That(lab.StarsLabel.text, Is.EqualTo("★5"));

            // 2回目の報酬（磨き石）の数を変える。書けない数は理由を出して戻す。
            lab.Rewards[1].Button.onClick.Invoke();
            Assert.That(lab.ChosenReward, Is.EqualTo(2));
            Assert.That(lab.RewardActions.blocksRaycasts, Is.True);
            Assert.That(lab.DropActions.blocksRaycasts, Is.False);
            Assert.That(lab.QualityButton.gameObject.activeSelf, Is.False);
            lab.CountInput.onEndEdit.Invoke("15");
            Assert.That(lab.Rewards[1].Name.text, Is.EqualTo("磨き石 ×15"));
            lab.CountInput.onEndEdit.Invoke("0");
            Assert.That(lab.Status.text, Is.EqualTo("数は1〜99にします"));
            Assert.That(lab.CountInput.text, Is.EqualTo("15"));

            // 物を替える：選ぶ一覧は今の物の分類から開き、強化素材も選べる。装備にすると品質Aになる。
            lab.ChangeButton.onClick.Invoke();
            Assert.That(lab.PickerCategory, Is.EqualTo(AdventureDropCategory.Material));
            Assert.That(lab.PickerCategories.Last().gameObject.activeSelf, Is.True);
            lab.PickerCategories[1].onClick.Invoke();
            Assert.That(lab.PickerCategory, Is.EqualTo(AdventureDropCategory.Accessory));
            lab.Picks.Single(pick => pick.Id == "moss-charm").Button.onClick.Invoke();
            Assert.That(lab.ChosenReward, Is.EqualTo(2));
            Assert.That(lab.Rewards[1].Name.text, Is.EqualTo("苔玉のお守り"));
            Assert.That(lab.QualityButton.gameObject.activeSelf, Is.True);
            Assert.That(lab.QualityLabel.text, Is.EqualTo("品質：A"));
            lab.QualityButton.onClick.Invoke();
            Assert.That(lab.QualityLabel.text, Is.EqualTo("品質：S"));

            // ダンジョンの一覧では、変えたダンジョンに印が付く。選ぶと一覧を閉じてそのダンジョンを出す。
            lab.DungeonButton.onClick.Invoke();
            Assert.That(lab.ListingDungeons, Is.True);
            SceneTests.AssertTouchSize(lab.DungeonRows[0].transform);
            StringAssert.Contains("●", lab.DungeonRows[1].GetComponentInChildren<TMP_Text>().text);
            StringAssert.DoesNotContain(
                "●",
                lab.DungeonRows[2].GetComponentInChildren<TMP_Text>().text
            );
            lab.DungeonRows[2].onClick.Invoke();
            Assert.That(lab.ListingDungeons, Is.False);
            Assert.That(lab.Dungeon, Is.EqualTo("glaciera-cave"));
            Assert.That(lab.DungeonLabel.text, Is.EqualTo("グラシエラ氷窟　Lv18"));

            lab.CopyButton.onClick.Invoke();
            Assert.That(lab.Status.text, Is.EqualTo("変えた2つのダンジョンをコピーしました"));
            string json = GUIUtility.systemCopyBuffer;
            Assert.That(json, Is.EqualTo(DropLab.Drafts.ChangesJson()));
            StringAssert.Contains("\"format\": \"baryonyx-drop-lab\"", json);
            StringAssert.Contains("\"id\": \"port-portoria\"", json);
            StringAssert.Contains("\"id\": \"moss-charm\"", json);
        }
    }
}
