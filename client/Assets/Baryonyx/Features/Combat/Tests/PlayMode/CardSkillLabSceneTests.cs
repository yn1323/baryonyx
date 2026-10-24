using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    // スキルのデバッグルーム。一覧から選んだスキルは、再生中のスキルを打ち切ってすぐに再生され、
    // 選んだスキルと再生のしかたは次のPlayのために残る。
    public sealed class CardSkillLabSceneTests
    {
        private const string ScenePath = SceneTests.ScenesFolder + "/Debug/CardSkillLab.unity";

        private static readonly string[] IntKeys =
        {
            CardSkillLab.WeightKey,
            CardSkillLab.EnemyKey,
            CardSkillLab.RepeatKey,
            CardSkillLab.EvolveKey,
        };

        // 開発者が最後に選んだスキルを、テストで上書きしたまま残さない。
        private string savedSkill;
        private readonly Dictionary<string, int> savedInts = new();

        [SetUp]
        public void ForgetThePickedSkill()
        {
            savedSkill = PlayerPrefs.HasKey(CardSkillLab.SkillKey)
                ? PlayerPrefs.GetString(CardSkillLab.SkillKey)
                : null;
            savedInts.Clear();
            foreach (var key in IntKeys)
                if (PlayerPrefs.HasKey(key))
                    savedInts[key] = PlayerPrefs.GetInt(key);
            PlayerPrefs.DeleteKey(CardSkillLab.SkillKey);
            foreach (var key in IntKeys)
                PlayerPrefs.DeleteKey(key);
        }

        [UnityTearDown]
        public IEnumerator RestoreThePickedSkill()
        {
            yield return SceneTests.UnloadAll(nameof(CardSkillLabSceneTests));
            PlayerPrefs.DeleteKey(CardSkillLab.SkillKey);
            if (savedSkill != null)
                PlayerPrefs.SetString(CardSkillLab.SkillKey, savedSkill);
            foreach (var key in IntKeys)
            {
                PlayerPrefs.DeleteKey(key);
                if (savedInts.TryGetValue(key, out int value))
                    PlayerPrefs.SetInt(key, value);
            }
        }

        [UnityTest]
        public IEnumerator AnySkillPickedFromTheListCutsShortTheOnePlayingAndIsRemembered()
        {
            CardSkillLab lab = null;
            // ホームから開くときのシャッターが開ききってから押す。
            yield return SceneTests.Load<CardSkillLab>(
                ScenePath,
                found =>
                    found.SkillButtons.Count > 0
                    && (found.Transition == null || !found.Transition.IsPlaying),
                found => lab = found
            );
            var all = CardSkillLab.Skills;
            Assert.That(lab.SkillButtons, Has.Count.EqualTo(all.Count), "Every skill is listed.");
            for (int i = 0; i < all.Count; i++)
                Assert.That(
                    lab.SkillButtons[i].transform.parent,
                    Is.SameAs(lab.TabContents[CardSkillLab.TabOf(i)]),
                    all[i].Name + " is on its tab."
                );
            Assert.That(lab.Index, Is.Zero, "With nothing remembered, the first skill plays.");
            Assert.That(lab.Playing, Is.True);
            Assert.That(lab.PickerOpen, Is.False);
            yield return SceneTests.WaitUntil(
                () => lab.Vfx.LiveCount > 0,
                message: "The first skill's effect shows."
            );
            foreach (
                var control in new[]
                {
                    lab.PreviousButton,
                    lab.ListButton,
                    lab.NextButton,
                    lab.ReplayButton,
                    lab.WeightButton,
                    lab.TargetButton,
                    lab.RepeatButton,
                }
            )
            {
                AssertReachable(control);
                SceneTests.AssertTouchSize(control.transform);
            }

            // The list opens over the stage on the tab of the skill playing; the other tab shows
            // the unique skills' stages, and the list scrolls with a swipe.
            lab.ListButton.onClick.Invoke();
            Assert.That(lab.PickerOpen, Is.True);
            Assert.That(lab.Tab, Is.Zero, "It opens on the card skills.");
            Assert.That(
                lab.ListButton.gameObject.activeInHierarchy,
                Is.False,
                "The row of controls hides under the list."
            );
            Assert.That(lab.TabContents[0].gameObject.activeSelf, Is.True);
            Assert.That(lab.TabContents[1].gameObject.activeSelf, Is.False);
            Assert.That(lab.Scroll.vertical, Is.True);
            lab.Tabs[1].Button.onClick.Invoke();
            Assert.That(lab.Tab, Is.EqualTo(1));
            Assert.That(lab.Scroll.content, Is.SameAs(lab.TabContents[1]));
            Assert.That(lab.TabContents[0].gameObject.activeSelf, Is.False);
            lab.Tabs[0].Button.onClick.Invoke();
            // A graphic just shown takes taps once it has been drawn.
            yield return null;
            int meteor = Enumerable.Range(0, all.Count).Single(i => all[i].Id == "Meteor");
            AssertReachable(lab.SkillButtons[meteor]);
            lab.SkillButtons[meteor].onClick.Invoke();
            Assert.That(lab.PickerOpen, Is.False, "The list closes on a pick.");
            Assert.That(lab.ListButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(lab.Current.Id, Is.EqualTo("Meteor"));
            Assert.That(lab.Playing, Is.True);
            Assert.That(lab.Caption.text, Does.Contain("メテオ"));
            Assert.That(lab.Detail.text, Does.Contain("トーマ"));
            // Meteor opens with its cut-in, which draws no shape, so a shape left now would be
            // the cut-short skill's.
            Assert.That(lab.Vfx.LiveCount, Is.Zero, "What the cut-short skill drew is gone.");
            Assert.That(lab.Vfx.TimeBent, Is.False, "What the cut-short skill did is undone.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PlayerPrefs.GetString(CardSkillLab.SkillKey), Is.EqualTo("Meteor"));

            // How it plays is changed from the row, each change playing it again.
            lab.WeightButton.onClick.Invoke();
            Assert.That(lab.Weight, Is.EqualTo(BattleHitWeight.Weak));
            Assert.That(lab.WeightLabel.text, Is.EqualTo("当たり：弱点"));
            lab.TargetButton.onClick.Invoke();
            Assert.That(lab.Enemy, Is.EqualTo(1));
            Assert.That(lab.TargetLabel.text, Is.EqualTo("ねらう敵：" + lab.EnemyNames[1]));
            lab.NextButton.onClick.Invoke();
            Assert.That(lab.Index, Is.EqualTo(meteor + 1));
            lab.PreviousButton.onClick.Invoke();
            Assert.That(lab.Index, Is.EqualTo(meteor));
            Assert.That(PlayerPrefs.GetInt(CardSkillLab.WeightKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(CardSkillLab.EnemyKey), Is.EqualTo(1));

            // Played once instead of over and over, a short skill ends and stays ended.
            lab.RepeatButton.onClick.Invoke();
            Assert.That(lab.Repeat, Is.False);
            Assert.That(lab.RepeatLabel.text, Is.EqualTo("くり返し：切"));
            lab.ListButton.onClick.Invoke();
            lab.SkillButtons[0].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => !lab.Playing,
                seconds: 6f,
                message: all[0].Name + " ends."
            );
            for (int i = 0; i < 30; i++)
                yield return null;
            Assert.That(lab.Playing, Is.False, "It does not start again.");
            lab.ReplayButton.onClick.Invoke();
            Assert.That(lab.Playing, Is.True, "Played again on demand.");
        }

        [UnityTest]
        public IEnumerator AUniqueSkillEvolvesIntoTheStagePickedAndThatStagePlays()
        {
            CardSkillLab lab = null;
            yield return SceneTests.Load<CardSkillLab>(
                ScenePath,
                found =>
                    found.SkillButtons.Count > 0
                    && (found.Transition == null || !found.Transition.IsPlaying),
                found => lab = found
            );
            var all = CardSkillLab.Skills;
            int Find(string id) => Enumerable.Range(0, all.Count).Single(i => all[i].Id == id);
            string[] Ids(IEnumerable<CardSkill> skills) =>
                skills.Select(skill => skill.Id).ToArray();
            Assert.That(lab.Evolve, Is.False, "Evolution starts turned off.");
            Assert.That(lab.EvolveLabel.text, Is.EqualTo("覚醒：切"));
            AssertReachable(lab.EvolveButton);
            SceneTests.AssertTouchSize(lab.EvolveButton.transform);

            // A first stage evolves into its branches' first steps, a first step into its
            // branch's last; a last step and a card skill do not evolve.
            Assert.That(
                Ids(CardSkillLab.EvolvesInto(all[Find("ManaBurst")])),
                Is.EqualTo(new[] { "ManaBurst.1", "ManaBurst.3", "ManaBurst.5" })
            );
            Assert.That(
                Ids(CardSkillLab.EvolvesInto(all[Find("ManaBurst.1")])),
                Is.EqualTo(new[] { "ManaBurst.2" })
            );
            Assert.That(CardSkillLab.EvolvesInto(all[Find("ManaBurst.2")]), Is.Empty);
            Assert.That(CardSkillLab.EvolvesInto(all[0]), Is.Empty);

            // Turned off, a unique skill plays at once.
            int first = Find("ManaBurst");
            lab.Play(first);
            yield return null;
            Assert.That(lab.Vfx.Evolving, Is.False);
            Assert.That(lab.Detail.text, Does.Not.Contain("覚醒して使う"));

            // Turned on, the card played rises and cracks, and the stages it can become are
            // offered under it, with nothing picked yet.
            lab.EvolveButton.onClick.Invoke();
            Assert.That(lab.Evolve, Is.True);
            Assert.That(lab.EvolveLabel.text, Is.EqualTo("覚醒：入"));
            Assert.That(PlayerPrefs.GetInt(CardSkillLab.EvolveKey), Is.EqualTo(1));
            Assert.That(lab.Detail.text, Does.Contain("覚醒して使う"));
            yield return SceneTests.WaitUntil(
                () => lab.Vfx.Evolving,
                message: "The card played rises to evolve."
            );
            var card = lab.Vfx.EvolutionLayer.GetComponentInChildren<BattleInspectCardView>();
            Assert.That(card.Name.text, Is.EqualTo(all[first].Name), "It shows the stage played.");
            Assert.That(lab.Vfx.DimAlpha, Is.GreaterThan(0f), "The stage darkens.");
            var choice = lab.Choice;
            yield return SceneTests.WaitUntil(
                () => choice.IsOpen && choice.Panel.blocksRaycasts,
                seconds: 4f,
                message: "The stages to evolve into are offered."
            );
            Assert.That(lab.Choosing, Is.True);
            Assert.That(choice.Count, Is.EqualTo(3));
            Assert.That(choice.Selected, Is.EqualTo(-1));
            Assert.That(choice.ConfirmButton.interactable, Is.False, "Nothing to confirm yet.");
            Assert.That(
                lab.ListButton.gameObject.activeInHierarchy,
                Is.False,
                "The lab's controls step aside."
            );
            for (int i = 0; i < 3; i++)
            {
                AssertReachable(choice.Slots[i]);
                Assert.That(
                    choice.Cards[i].Name.text,
                    Is.EqualTo(all[Find("ManaBurst." + (1 + i * 2))].Name)
                );
                Assert.That(choice.AcquiredTags[i].activeSelf, Is.False);
            }
            AssertReachable(choice.ConfirmButton);
            SceneTests.AssertTouchSize(choice.ConfirmButton.transform);

            // The stage picked is confirmed; the card shatters into it, goes into its user, and
            // that stage plays.
            choice.Slots[1].onClick.Invoke();
            Assert.That(choice.Selected, Is.EqualTo(1));
            Assert.That(choice.ConfirmButton.interactable, Is.True);
            choice.ConfirmButton.onClick.Invoke();
            Assert.That(choice.IsOpen, Is.False);
            Assert.That(lab.Choosing, Is.False);
            Assert.That(lab.ListButton.gameObject.activeInHierarchy, Is.True);
            string picked = all[Find("ManaBurst.3")].Name;
            yield return SceneTests.WaitUntil(
                () => card.Name.text == picked,
                message: "It shatters into the stage picked."
            );
            yield return SceneTests.WaitUntil(
                () => !lab.Vfx.Evolving,
                message: "The evolved card goes into its user."
            );
            Assert.That(card.gameObject.activeSelf, Is.False);
            Assert.That(lab.Playing, Is.True, "Then the stage picked plays.");

            // Offered again, the stage evolved into is marked acquired.
            lab.Replay();
            yield return SceneTests.WaitUntil(() => choice.IsOpen, seconds: 4f);
            Assert.That(choice.AcquiredTags[1].activeSelf, Is.True);
            Assert.That(choice.AcquiredTags[0].activeSelf, Is.False);

            // Cut short while choosing, nothing of it is left.
            lab.Play(0);
            Assert.That(choice.IsOpen, Is.False);
            Assert.That(lab.Choosing, Is.False);
            Assert.That(lab.Vfx.Evolving, Is.False);
            Assert.That(card.gameObject.activeSelf, Is.False, "The cut-short card is hidden.");
            Assert.That(lab.ListButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(lab.Vfx.TimeBent, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // From a first step, its one stage is shown picked for a moment with no button, and
            // the card shatters into it by itself.
            lab.Play(Find("ManaBurst.1"));
            yield return SceneTests.WaitUntil(() => choice.IsOpen, seconds: 4f);
            Assert.That(choice.Count, Is.EqualTo(1));
            Assert.That(choice.Selected, Is.Zero);
            Assert.That(choice.ConfirmButton.gameObject.activeSelf, Is.False);
            yield return SceneTests.WaitUntil(
                () => !choice.IsOpen,
                seconds: choice.SingleShow + 1f,
                message: "It goes on without a tap."
            );
            Assert.That(lab.Choosing, Is.False);
            string last = all[Find("ManaBurst.2")].Name;
            yield return SceneTests.WaitUntil(
                () => card.Name.text == last,
                message: "It shatters into its one stage."
            );
        }

        // 溜めのタブには、スキルが最初に出す溜めが全種類並び、押した溜めだけを当てる相手なしで再生する。
        // 覚醒版の演出（doc/features/skill-awakening.md#演出）。先に状態を付ける段階、当たりの回数が
        // 増える段階、当たりのあとに働きを足す段階、敵と味方のどちらにも届く段階を、しるしの種類が
        // 一通りそろうように選んで、最後まで流れることを確かめる。
        [UnityTest]
        public IEnumerator AwakenedStagesAreListedOnTheirTabAndPlayToTheirEnd()
        {
            CardSkillLab lab = null;
            yield return SceneTests.Load<CardSkillLab>(
                ScenePath,
                found =>
                    found.SkillButtons.Count > 0
                    && (found.Transition == null || !found.Transition.IsPlaying),
                found => lab = found
            );
            var all = CardSkillLab.Skills;
            var stages = CardSkillAwakenings.All.SelectMany(awakening => awakening.Branches);
            foreach (var stage in stages)
            {
                int at = Enumerable.Range(0, all.Count).Single(i => all[i].Id == stage.Id);
                Assert.That(CardSkillLab.TabOf(at), Is.EqualTo(2), stage.Name);
            }
            if (lab.Repeat)
                lab.RepeatButton.onClick.Invoke();
            foreach (
                var id in new[]
                {
                    "Fire.2",
                    "EarthSplitter.1",
                    "Thundercloud.1",
                    "Scout.2",
                    "ShieldBash.1",
                    "Heal.2",
                    "Guard.2",
                    "GuardianOath.2",
                    "ArrowRain.2",
                    "AbsoluteZero.1",
                    "PoisonNeedle.2",
                    "Revelation.2",
                    "Regen.1",
                    "Whirlwind.2",
                    "ChainLightning.2",
                    "Embers.1",
                }
            )
            {
                lab.Play(Enumerable.Range(0, all.Count).Single(i => all[i].Id == id));
                Assert.That(lab.Current.Id, Is.EqualTo(id));
                yield return SceneTests.WaitUntil(
                    () => lab.Vfx.LiveCount > 0,
                    seconds: 3f,
                    message: id + " draws."
                );
                yield return SceneTests.WaitUntil(
                    () => !lab.Playing,
                    seconds: 15f,
                    message: id + " plays to its end."
                );
            }
        }

        [UnityTest]
        public IEnumerator EveryWindUpIsListedOnItsTabAndPlaysOnItsOwn()
        {
            CardSkillLab lab = null;
            yield return SceneTests.Load<CardSkillLab>(
                ScenePath,
                found =>
                    found.SkillButtons.Count > 0
                    && (found.Transition == null || !found.Transition.IsPlaying),
                found => lab = found
            );
            var windUps = CardSkillLab.WindUps;
            Assert.That(
                lab.WindUpButtons,
                Has.Count.EqualTo(windUps.Count),
                "Every wind-up is listed."
            );
            Assert.That(lab.TabContents, Has.Length.EqualTo(4));
            const int tab = CardSkillLab.WindUpTab;
            foreach (var button in lab.WindUpButtons)
                Assert.That(button.transform.parent, Is.SameAs(lab.TabContents[tab]));

            lab.ListButton.onClick.Invoke();
            lab.Tabs[tab].Button.onClick.Invoke();
            Assert.That(lab.Tab, Is.EqualTo(tab));
            Assert.That(lab.Scroll.content, Is.SameAs(lab.TabContents[tab]));
            yield return null;
            int fire = windUps.ToList().IndexOf(BattleWindUp.FireSpell);
            var picked = lab.WindUpButtons[fire];
            AssertReachable(picked);
            SceneTests.AssertTouchSize(picked.transform);
            Assert.That(
                picked.GetComponentInChildren<TMPro.TMP_Text>().text,
                Is.EqualTo(CardSkillLab.WindUpName(BattleWindUp.FireSpell))
            );
            picked.onClick.Invoke();
            Assert.That(lab.PickerOpen, Is.False, "The list closes on a pick.");
            Assert.That(lab.IsWindUp, Is.True);
            Assert.That(lab.CurrentWindUp, Is.EqualTo(BattleWindUp.FireSpell));
            Assert.That(lab.Current, Is.Null);
            Assert.That(lab.Index, Is.EqualTo(CardSkillLab.Skills.Count + fire));
            Assert.That(lab.Caption.text, Does.Contain("炎の溜め"));
            Assert.That(lab.Detail.text, Does.Contain("トーマ"));
            Assert.That(
                PlayerPrefs.GetString(CardSkillLab.SkillKey),
                Is.EqualTo("WindUp.FireSpell")
            );
            yield return SceneTests.WaitUntil(
                () => lab.Vfx.LiveCount > 0,
                message: "The wind-up's circle shows."
            );

            // Every wind-up draws something of its own as soon as it is played.
            for (int i = 0; i < windUps.Count; i++)
            {
                lab.Play(CardSkillLab.Skills.Count + i);
                Assert.That(lab.CurrentWindUp, Is.EqualTo(windUps[i]));
                yield return SceneTests.WaitUntil(
                    () => lab.Vfx.LiveCount > 0,
                    seconds: 2f,
                    message: CardSkillLab.WindUpName(windUps[i]) + " draws."
                );
            }

            // Played once, it ends; the next one after the last wind-up wraps round to the first skill.
            lab.RepeatButton.onClick.Invoke();
            lab.Play(CardSkillLab.Count - 1);
            Assert.That(lab.CurrentWindUp, Is.EqualTo(windUps[windUps.Count - 1]));
            yield return SceneTests.WaitUntil(
                () => !lab.Playing,
                seconds: 6f,
                message: "The wind-up ends."
            );
            lab.NextButton.onClick.Invoke();
            Assert.That(lab.Index, Is.Zero);
            Assert.That(lab.IsWindUp, Is.False);
        }

        // Nothing over the button takes the tap: the first thing under its middle is the button.
        private static void AssertReachable(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center)
            );
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current) { position = point },
                hits
            );
            Assert.That(hits, Is.Not.Empty, button.name);
            Assert.That(
                hits[0].gameObject.GetComponentInParent<Button>(),
                Is.SameAs(button),
                button.name
            );
        }
    }
}
