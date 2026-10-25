using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private FixedRandom random;

        [SetUp]
        public void ForgetThePickedSkill()
        {
            random = FixedRandom.Use();
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
            Time.timeScale = 1f;
            random?.Dispose();
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

        // 開発用の画面なので、読み込みと、一覧から選んだスキルが再生されて残ることだけを確かめる。
        // どのスキルが何に覚醒するか、どのタブに並ぶかは CardSkillLabTests（EditMode）が確かめる。
        [UnityTest]
        public IEnumerator ASkillPickedFromTheListCutsShortTheOnePlayingAndIsRemembered()
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
            Assert.That(
                lab.WindUpButtons,
                Has.Count.EqualTo(CardSkillLab.WindUps.Count),
                "Every wind-up is listed."
            );
            Assert.That(lab.Index, Is.Zero, "With nothing remembered, the first skill plays.");
            Assert.That(lab.Playing, Is.True);
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
                    lab.EvolveButton,
                }
            )
            {
                AssertReachable(control);
                SceneTests.AssertTouchSize(control.transform);
            }

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
            // Meteor opens with its cut-in, which draws no shape, so a shape left now would be
            // the cut-short skill's.
            Assert.That(lab.Vfx.LiveCount, Is.Zero, "What the cut-short skill drew is gone.");
            Assert.That(lab.Vfx.TimeBent, Is.False, "What the cut-short skill did is undone.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PlayerPrefs.GetString(CardSkillLab.SkillKey), Is.EqualTo("Meteor"));
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
