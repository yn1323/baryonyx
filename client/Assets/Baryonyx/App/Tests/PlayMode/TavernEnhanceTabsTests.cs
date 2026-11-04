using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Enhancement;
using Baryonyx.Equipment;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Tavern;
using Baryonyx.Training;
using Baryonyx.UI.GuideMenu;
using Baryonyx.Workshop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 冒険者のタブの右下の「強化」「覚醒」（doc/features/screens/loadout.md#強化、#覚醒）。
    public sealed class TavernEnhanceTabsTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
            TrainingSession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
            EnhancementSession.Reset();
            EnhancementSession.Source = new EnhancementMemorySource(
                new Dictionary<string, int>
                {
                    [EnhancementMaterials.White] = 20,
                    [EnhancementMaterials.Shard] = 2,
                },
                5000,
                shownBalance: false
            );
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            TrainingSession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
            EnhancementSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernEnhanceTabsTests));
        }

        // 装備のタブで防具を選んでから「強化」へ移ると、その人の装備・固有スキル・カスタムスキルを見出しごとに並べ、
        // 防具を選んでおく。右は商会の品質強化と同じで、磨き石で品質を上げる。フリックで人を替え、戻ると一覧へ戻る。
        [UnityTest]
        public IEnumerator TheEnhanceTabListsThePersonsThingsAndPolishesThem()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var page = default(TrainingView);
            yield return OpenPage(view, value => page = value);
            yield return SceneTests.WaitForTask(page.LoadTask);
            Assert.That(
                page.Sheet.Tabs.Select(tab => tab.Label.text),
                Is.EqualTo(new[] { "ステータス", "装備", "スキル", "突破", "強化", "覚醒" })
            );
            foreach (var tab in page.Sheet.Tabs)
            {
                SceneTests.AssertTouchSize(tab.transform);
                SceneTests.AssertBelow(tab.transform, view.Back.transform);
            }
            Assert.That(
                view.Definition.Items[ItemOf(view, EnhancementSession.SheetItemKey)].Hidden,
                Is.True
            );
            Assert.That(
                view.Definition.Items[ItemOf(view, CardLoadoutSession.AwakenItemKey)].Hidden,
                Is.True
            );

            page.Sheet.Tabs[1].Button.onClick.Invoke();
            var equipment = Panel<EquipmentView>(view, EquipmentSession.GuideItemKey);
            yield return SceneTests.WaitForTask(equipment.LoadTask);
            equipment.Presenter.SelectSlot((int)EquipmentSlot.Armor);
            string person = PartySession.Selected;
            equipment.Sheet.Tabs[4].Button.onClick.Invoke();
            var enhance = Panel<WorkshopEnhanceView>(view, EnhancementSession.SheetItemKey);
            Assert.That(enhance.gameObject.activeInHierarchy, Is.True);
            Assert.That(equipment.gameObject.activeInHierarchy, Is.False);
            yield return SceneTests.WaitForTask(enhance.LoadTask);
            Assert.That(enhance.ForPerson, Is.True);
            Assert.That(enhance.Sheet.Tabs[4].Chosen, Is.True);
            Assert.That(enhance.Person, Is.EqualTo(person));
            var member = enhance.Party.Find(person);
            Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "パーティー", member.Name)));

            // 装備のタブで選んでいた防具を選んでおく。
            Assert.That(enhance.Personal[enhance.Selected].Place, Is.EqualTo("armor"));
            Assert.That(enhance.Rows[enhance.Selected].Card.Selected, Is.True);

            // 見出しは装備・固有スキル・カスタムスキルの順で、それぞれ最初の行のすぐ上に置く。行に使っている人は書かない。
            var groups = enhance.Personal.Select(entry => entry.Group).Distinct().ToArray();
            Assert.That(
                groups,
                Is.EqualTo(
                    new[]
                    {
                        EnhancementTargets.GearGroup,
                        EnhancementTargets.UniqueGroup,
                        EnhancementTargets.SkillGroup,
                    }
                )
            );
            var headings = enhance
                .Headings.Where(heading => heading.gameObject.activeSelf)
                .ToArray();
            Assert.That(
                headings.Select(heading => heading.GetComponentInChildren<TMP_Text>().text),
                Is.EqualTo(groups)
            );
            for (int i = 0; i < groups.Length; i++)
            {
                int row = enhance.Personal.ToList().FindIndex(entry => entry.Group == groups[i]);
                Assert.That(
                    enhance.Rows[row].transform.GetSiblingIndex(),
                    Is.EqualTo(headings[i].GetSiblingIndex() + 1),
                    groups[i]
                );
            }
            // 読み込む間に伏せた行のうち、物の来ない行はめくってから消える。
            yield return SceneTests.WaitUntil(
                () =>
                    enhance.Rows.Count(row => row.gameObject.activeSelf) == enhance.Personal.Count,
                message: "The rows did not match what the person holds."
            );
            Assert.That(
                enhance
                    .Rows.Where(row => row.gameObject.activeSelf)
                    .All(row => row.Price == null || row.Price.text == ""),
                Is.True
            );

            // カスタムスキルを選び、磨き石の札を押して「強化」で品質を上げる。
            int skill = enhance
                .Personal.ToList()
                .FindIndex(entry => entry.Group == EnhancementTargets.SkillGroup);
            enhance.Select(skill);
            float before = enhance.Targets[skill].Multiplier;
            enhance.AddStep();
            Assert.That(enhance.Steps, Is.EqualTo(1));
            Assert.That(enhance.Confirm.interactable, Is.True);
            enhance.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(enhance.Work);
            Assert.That(enhance.Targets[skill].Multiplier, Is.GreaterThan(before));
            Assert.That(enhance.Personal[skill].Target, Is.SameAs(enhance.Targets[skill]));

            // 左へのフリックで次の人。選んでいる所（1つ目のカスタムスキル）はそのまま。
            var people = PartySession.Formation(enhance.Party).TabOrder().People;
            int at = people.ToList().FindIndex(entry => entry.Id == person);
            var next = people[(at + 1) % people.Count];
            enhance.Swipes[0].Flick(1);
            Assert.That(enhance.Person, Is.EqualTo(next.Id));
            Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "パーティー", next.Name)));
            if (enhance.Personal.Any(entry => entry.Place == EnhancementTargets.SlotPlace(0)))
                Assert.That(
                    enhance.Personal[enhance.Selected].Place,
                    Is.EqualTo(EnhancementTargets.SlotPlace(0))
                );

            // 戻ると、タブを開いた冒険者の一覧へ戻る。
            view.Back.onClick.Invoke();
            Assert.That(enhance.gameObject.activeInHierarchy, Is.False);
            Assert.That(
                view.PanelFor(ItemOf(view, PartySession.GuideItemKey)).activeInHierarchy,
                Is.True
            );
        }

        // 「覚醒」のタブは、スキルのタブと同じ6行とツリーで、覚醒できる段階を押して選び、覚醒の欠片とルーンで覚醒させる。
        // 固有スキルはツリー、スキルは覚醒する前の1枚と2つの枝で出し、持っている段階だけ中身を見せる。
        [UnityTest]
        public IEnumerator TheAwakenTabAwakensAUniqueSkillAndASkillCard()
        {
            var server = new FakePartySource(5000, "toma")
                .With("toma", 12, PartyCardKey.Card("c1"), PartyCardKey.Card("c2"))
                .WithOwned(
                    new PartySkillCard("c1", "Fire", 1f),
                    new PartySkillCard("c2", "Ice", 1f),
                    new PartySkillCard("c3", "Fire", 1f, 2)
                );
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var page = default(TrainingView);
            yield return OpenPage(view, value => page = value);
            yield return SceneTests.WaitForTask(page.LoadTask);
            page.Sheet.Tabs[5].Button.onClick.Invoke();
            var awaken = Panel<CardLoadoutView>(view, CardLoadoutSession.AwakenItemKey);
            Assert.That(awaken.gameObject.activeInHierarchy, Is.True);
            yield return SceneTests.WaitForTask(awaken.LoadTask);
            var awakening = awaken.Awakening;
            Assert.That(awakening, Is.Not.Null);
            Assert.That(awaken.Sheet.Tabs[5].Chosen, Is.True);

            // 開くと1つ目の固有スキルのツリー。覚醒できる段階は封じたまま押せ、先頭の1つを金の枠で囲んでおく。
            Assert.That(awaken.Presenter.State.Unique, Is.EqualTo(0));
            Assert.That(awaken.Tree.Root.activeSelf, Is.True);
            Assert.That(awakening.Branches.Root.activeSelf, Is.False);
            var option = awakening.Picked;
            Assert.That(option, Is.Not.Null);
            Assert.That(option.IsSkillCard, Is.False);
            var stage = awaken.Tree.Stages[option.Stage];
            Assert.That(stage.Shadow.activeSelf, Is.True);
            Assert.That(stage.Card.Selected.activeSelf, Is.True);
            Assert.That(stage.Card.Button.interactable, Is.True);
            Assert.That(awakening.CostCard.activeSelf, Is.True);
            yield return SceneTests.WaitUntil(
                () => awakening.Confirm.interactable,
                message: "The shards were not read."
            );
            Assert.That(awakening.ShardCount.text, Is.EqualTo("1/2"));

            awakening.Confirm.onClick.Invoke();
            Assert.That(awakening.Busy, Is.True);
            Assert.That(awakening.Scene.activeSelf, Is.True);
            yield return SceneTests.WaitForTask(awakening.Work, AwakeningSeconds);
            var progress = UniqueSkillSession.ProgressOf("toma", 0, null);
            Assert.That(progress.Has(option.Stage), Is.True);
            Assert.That(progress.Set, Is.EqualTo(option.Stage));
            Assert.That(awakening.Busy, Is.False);
            Assert.That(awakening.Scene.activeSelf, Is.False);
            Assert.That(awaken.Tree.Stages[option.Stage].Shadow.activeSelf, Is.False);

            // スキル：1つ目のカスタムスキルの枠（ファイア）。別の1枚が取った2つ目の枝だけ中身を見せる。
            awaken.SlotRows[0].Card.Button.onClick.Invoke();
            Assert.That(awaken.Tree.Root.activeSelf, Is.False);
            Assert.That(awakening.Branches.Root.activeSelf, Is.True);
            Assert.That(awakening.Branches.Stages[0].Shadow.activeSelf, Is.False);
            Assert.That(awakening.Branches.Stages[1].Shadow.activeSelf, Is.True);
            Assert.That(awakening.Branches.Stages[2].Shadow.activeSelf, Is.False);
            Assert.That(awakening.Picked.Stage, Is.EqualTo(1));
            awakening.Pick(2, true);
            Assert.That(awakening.Picked.Stage, Is.EqualTo(2));
            Assert.That(awakening.Branches.Stages[2].Card.Selected.activeSelf, Is.True);
            Assert.That(awakening.Branches.Stages[1].Card.Selected.activeSelf, Is.False);
            Assert.That(awakening.Confirm.interactable, Is.True);
            awakening.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(awakening.Work, AwakeningSeconds);
            Assert.That(
                PartySession.State.SkillCardOf(PartyCardKey.Card("c1")).AwakenedBranch,
                Is.EqualTo(2)
            );
            Assert.That(
                awaken.SlotRows[0].Card.Name.text,
                Is.EqualTo(CardSkills.Find("Fire.2").Name)
            );
            // 覚醒した1枚は、もう覚醒できない。
            Assert.That(awakening.Picked, Is.Null);
            Assert.That(awakening.CostCard.activeSelf, Is.False);
            Assert.That(awakening.Confirm.interactable, Is.False);
        }

        // 覚醒の演出（浮き上がってひびが入り、割れて現れてから行へ戻る。商会のスキル覚醒と同じ）を待つ秒数。
        private const float AwakeningSeconds = 15f;

        private static int ItemOf(GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        private static T Panel<T>(GuideMenuView view, string key)
            where T : Component => view.PanelFor(ItemOf(view, key)).GetComponent<T>();

        private static string Trail(params string[] parts) =>
            string.Join(GuideMenuView.TrailSeparator, parts);

        // メニューの「冒険者」から一覧を開き、読み終えたらパーティの先頭の人の冒険者のタブ（ステータス）を開く。
        private static IEnumerator OpenPage(GuideMenuView view, System.Action<TrainingView> opened)
        {
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            roster.Slots[0].Card.Click();
            roster.Chosen.Detail.onClick.Invoke();
            opened(Panel<TrainingView>(view, TrainingSession.GuideItemKey));
        }
    }
}
