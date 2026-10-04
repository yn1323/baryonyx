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
            yield return SceneTests.Load<CardSkillLab>(
                ScenePath,
                found => found.SkillButtons.Count > 0,
                found => lab = found
            );
            var all = CardSkills.All;
            Assert.That(lab.SkillButtons, Has.Count.EqualTo(all.Count), "Every skill is listed.");
            for (int i = 0; i < all.Count; i++)
                Assert.That(
                    lab.SkillButtons[i].transform.parent,
                    Is.SameAs(lab.Columns[(int)all[i].User]),
                    all[i].Name + " is under its user."
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

            // The list opens over the stage, and a skill picked there plays at once.
            lab.ListButton.onClick.Invoke();
            Assert.That(lab.PickerOpen, Is.True);
            // A graphic just shown takes taps once it has been drawn.
            yield return null;
            int meteor = Enumerable.Range(0, all.Count).Single(i => all[i].Id == "Meteor");
            AssertReachable(lab.SkillButtons[meteor]);
            lab.SkillButtons[meteor].onClick.Invoke();
            Assert.That(lab.PickerOpen, Is.False, "The list closes on a pick.");
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
