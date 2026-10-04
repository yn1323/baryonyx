using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Party;
using Baryonyx.Training;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TavernTrainingTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
            TrainingSession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            TrainingSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernTrainingTests));
        }

        // 酒場の「育成」は一覧の代わりに1人の詳細を開く。◀▶で人を替え、「レベルアップ」で重ねて開いた
        // 画面で上げる数を選んでから上げる。戻る操作は重ねた画面を先に閉じる。
        [UnityTest]
        public IEnumerator TrainingRaisesTheLevelThroughTheDialog()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = ItemOf(view, TrainingSession.GuideItemKey);
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            Assert.That(view.MenuItems[item].transform.Find("Icon"), Is.Not.Null);
            view.MenuItems[item].onClick.Invoke();
            var training = view.PanelFor(item).GetComponent<TrainingView>();
            Assert.That(training.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            Assert.That(training.Dialog.activeSelf, Is.False);

            // 左のキャラの列は「もどる」より下、右の枠は画面の題名より下に置き、どれも重ならない。
            var detail = training.transform.Find("Layout/Detail");
            AssertBelow(training.Prev.transform, view.Back.transform);
            AssertBelow(training.transform.Find("Layout/Backdrop"), view.Back.transform);
            AssertBelow(detail, view.transform.Find("SafeArea/Title"));
            Assert.That(ScreenRect(detail).Overlaps(ScreenRect(view.Back.transform)), Is.False);
            Assert.That(
                ScreenRect(training.LevelUp.transform).xMax,
                Is.LessThanOrEqualTo(ScreenRect(detail).xMin)
            );
            AssertBelow(training.Figure.transform, training.Level.transform);
            AssertBelow(training.Runes.transform, training.Figure.transform);
            // ◀▶は128四方。横に長いボタンは、メニューの行と同じく高さを7割まで許す。
            SceneTests.AssertTouchSize(training.Prev.transform);
            SceneTests.AssertTouchSize(training.Next.transform);
            SceneTests.AssertTouchSize(training.LevelUp.transform, 0.7f);
            SceneTests.AssertTouchSize(training.Cards.transform, 0.7f);
            Assert.That(
                ScreenRect(training.Prev.transform).Overlaps(ScreenRect(training.Figure.transform)),
                Is.False
            );

            // 最初はパーティの1人目。ステータス・スキル・カードは今のレベルの値。
            var party = PartySession.Formation(training.Party);
            var first = party.Find(party.Member(0));
            var growth = training.Data.Find(first.Id);
            Assert.That(training.Name.text, Is.EqualTo(first.Name));
            Assert.That(training.Level.text, Is.EqualTo(first.Level.ToString()));
            Assert.That(training.Runes.text, Is.EqualTo("8,450"));
            Assert.That(
                training.Stats.Select(label => label.text),
                Is.EqualTo(Values(growth.StatsAt(first.Level)))
            );
            Assert.That(training.Skills[0].Name.text, Is.EqualTo(growth.Passives[0].Name));
            Assert.That(training.Skills[1].Lock.activeSelf, Is.True);
            Assert.That(
                training.Skills[1].When.text,
                Is.EqualTo($"Lv {growth.Passives[1].UnlockLevel}")
            );
            Assert.That(training.CardSlots.All(card => card.Name.text != ""), Is.True);

            // ◀▶で人を替える。
            training.Next.onClick.Invoke();
            Assert.That(training.Name.text, Is.EqualTo(party.Name(party.Member(1))));
            training.Prev.onClick.Invoke();
            Assert.That(training.Name.text, Is.EqualTo(first.Name));

            // レベルアップを重ねて開く。暗幕が背面の「もどる」を押せなくする。
            training.LevelUp.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.True);
            // 開いたばかりの暗幕は、1度描かれるまでタップの判定に入らない。
            yield return null;
            foreach (
                var button in new Component[]
                {
                    training.Less,
                    training.More,
                    training.Max,
                    training.Cancel,
                    training.Confirm,
                }
            )
                SceneTests.AssertTouchSize(button.transform, 0.7f);
            Assert.That(TopHit(view.Back.transform).name, Is.EqualTo("Dim"));
            // 端末の戻るキーと同じ経路（「もどる」）は、重ねた画面だけを閉じる。
            view.Back.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(training.gameObject.activeSelf, Is.True);

            training.LevelUp.onClick.Invoke();
            training.More.onClick.Invoke();
            training.More.onClick.Invoke();
            int target = first.Level + 3;
            Assert.That(training.To.text, Is.EqualTo(target.ToString()));
            Assert.That(
                training.Diffs[0].After.text,
                Is.EqualTo(growth.StatsAt(target).Hp.ToString())
            );
            long cost = TrainingRules.CostBetween(first.Level, target, training.Data.CostPerLevel);
            training.Confirm.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(PartySession.LevelOf(training.Party, first.Id), Is.EqualTo(target));
            Assert.That(training.Level.text, Is.EqualTo(target.ToString()));
            Assert.That(
                training.Runes.text,
                Is.EqualTo((8450 - cost).ToString("#,0", CultureInfo.InvariantCulture))
            );
            Assert.That(view.ToastMessage, Does.StartWith($"{first.Name}が Lv {target} になり"));

            // カードの付け替え画面は、カードスキルの画面ができるまで「準備中」と知らせる。
            training.Cards.onClick.Invoke();
            Assert.That(view.ToastMessage, Is.EqualTo(TrainingPresenter.CardsComingSoon));

            // 「もどる」でメニューへ。開き直しても、アプリを動かしている間はレベルが残る。
            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(training.gameObject.activeSelf, Is.False);
            view.MenuItems[item].onClick.Invoke();
            Assert.That(training.Name.text, Is.EqualTo(first.Name));
            Assert.That(training.Level.text, Is.EqualTo(target.ToString()));
            yield return null;
        }

        private static int ItemOf(Baryonyx.UI.GuideMenu.GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        private static IEnumerable<string> Values(TrainingStats stats) =>
            Enumerable.Range(0, TrainingStats.Count).Select(i => stats[i].ToString());

        // 画面のその位置を押したとき、いちばん手前で受け取るもの。
        private static GameObject TopHit(Transform target)
        {
            var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = ScreenRect(target).center,
            };
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            return hits[0].gameObject;
        }

        private static Rect ScreenRect(Transform target)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertBelow(Transform lower, Transform upper) =>
            Assert.That(
                ScreenRect(lower).yMax,
                Is.LessThanOrEqualTo(ScreenRect(upper).yMin + 0.5f),
                lower.name
            );
    }
}
