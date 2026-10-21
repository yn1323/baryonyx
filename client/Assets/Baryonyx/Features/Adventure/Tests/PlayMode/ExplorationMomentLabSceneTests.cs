using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Inventory;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class ExplorationMomentLabSceneTests
    {
        private const string ScenePath =
            "Assets/Baryonyx/App/Scenes/Debug/ExplorationMomentLab.unity";
        private static readonly string[] Keys =
        {
            ExplorationMomentLab.MomentKey,
            ExplorationMomentLab.QualityKey,
            ExplorationMomentLab.RepeatKey,
        };

        private Scene loadedScene;

        // The lab remembers its choices on this machine; the test starts from none and gives
        // the developer's back after.
        private readonly Dictionary<string, int> saved = new();

        [SetUp]
        public void ForgetChoices()
        {
            saved.Clear();
            foreach (var key in Keys)
            {
                if (PlayerPrefs.HasKey(key))
                    saved[key] = PlayerPrefs.GetInt(key);
                PlayerPrefs.DeleteKey(key);
            }
        }

        [UnityTest]
        public IEnumerator EachMomentOfTheExplorationPlaysOnItsButton()
        {
            var lab = default(ExplorationMomentLab);
            yield return Load(found => lab = found);
            var view = lab.View;
            var moments = lab.Moments;

            Assert.That(view.ShownRun, Is.Not.Null, "The sample route is drawn.");
            Assert.That(
                lab.MomentButtons,
                Has.Length.EqualTo(Enum.GetValues(typeof(ExplorationMoment)).Length)
            );
            Assert.That(lab.Samples, Is.Not.Empty, "The chests hold the ACT bonuses.");

            // The first Play opens on the monsters of a monster room.
            Assert.That(lab.Moment, Is.EqualTo(ExplorationMoment.Encounter));
            yield return SceneTests.WaitUntil(() => lab.Busy, 5f, "The opening moment starts.");
            yield return SceneTests.WaitUntil(
                () => moments.Mark.gameObject.activeSelf,
                3f,
                "A \"！\" shows over the party."
            );
            yield return SceneTests.WaitUntil(
                () => moments.Band.gameObject.activeSelf,
                3f,
                "The band shows."
            );
            Assert.That(moments.Band.Label.text, Is.EqualTo(ExplorationRoom.BattlePrompt));
            yield return SceneTests.WaitUntil(
                () => moments.WipeBands.All(band => band.gameObject.activeSelf),
                3f,
                "The wipe closes in."
            );
            yield return SceneTests.WaitUntil(() => !lab.Busy, 5f, "The opening moment ends.");
            yield return null;
            Assert.That(moments.WipeBands.Any(band => band.gameObject.activeSelf), Is.False);
            Assert.That(moments.Mark.gameObject.activeSelf, Is.False);

            var controls = lab.MomentButtons.Concat(
                new[] { lab.QualityButton, lab.RepeatButton, lab.ResetButton, lab.OtherLabButton }
            );
            foreach (var button in controls)
            {
                SceneTests.AssertTappable(loadedScene, button);
                SceneTests.AssertTouchSize(button.transform);
            }

            lab.MomentButtons[(int)ExplorationMoment.MasterEncounter].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => moments.Band.gameObject.activeSelf,
                4f,
                "The master's band shows."
            );
            Assert.That(moments.Band.Label.text, Is.EqualTo(ExplorationMoments.MasterPrompt));
            yield return SceneTests.WaitUntil(() => !lab.Busy, 6f, "The master's moment ends.");
            yield return null;

            // The chest is S at first.
            Assert.That(lab.Quality, Is.EqualTo(ItemQuality.S));
            yield return Press(lab, ExplorationMoment.Chest);
            Assert.That(view.ChestSprite.texture, Is.SameAs(view.ChestOpen), "The chest is open.");
            Assert.That(moments.Reveal.Shown, Is.True, "What it held shows on its plate.");
            Assert.That(moments.Reveal.Quality.text, Is.EqualTo("S"));
            Assert.That(moments.Reveal.Kind.text, Is.EqualTo("ACTボーナス"));
            Assert.That(moments.Reveal.Name.text, Is.EqualTo(lab.Samples[0].Name));

            lab.QualityButton.onClick.Invoke();
            Assert.That(lab.Quality, Is.Null);
            Assert.That(lab.QualityLabel.text, Does.Contain("なし"));
            yield return Press(lab, ExplorationMoment.Chest);
            Assert.That(moments.Reveal.Nothing.gameObject.activeSelf, Is.True);

            string entrance = view.ShownRun.RoomId;
            yield return Press(lab, ExplorationMoment.Walk, seconds: 8f);
            Assert.That(view.ShownRun.RoomId, Is.Not.EqualTo(entrance), "The party walked on.");
            Assert.That(moments.Reveal.Shown, Is.False, "A new moment takes the plate away.");

            lab.ResetButton.onClick.Invoke();
            Assert.That(view.ShownRun.RoomId, Is.EqualTo(entrance), "Back at the entrance.");
            Assert.That(
                PlayerPrefs.GetInt(ExplorationMomentLab.MomentKey),
                Is.EqualTo((int)ExplorationMoment.Walk),
                "The moment last pressed is remembered."
            );
        }

        private static IEnumerator Press(
            ExplorationMomentLab lab,
            ExplorationMoment moment,
            float seconds = 6f
        )
        {
            var button = lab.MomentButtons[(int)moment];
            Assert.That(button.interactable, Is.True, moment + " can be pressed.");
            button.onClick.Invoke();
            Assert.That(lab.Busy, Is.True, moment + " starts at once.");
            yield return SceneTests.WaitUntil(() => !lab.Busy, seconds, moment + " ends.");
            yield return null;
        }

        private IEnumerator Load(Action<ExplorationMomentLab> found)
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;
            var lab = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ExplorationMomentLab>(true))
                .Single();
            Assert.That(lab.View, Is.Not.Null, "The lab plays over the exploration screen.");
            Assert.That(lab.Moments, Is.Not.Null);
            Assert.That(lab.Moments.View, Is.SameAs(lab.View));
            found(lab);
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            foreach (var key in Keys)
            {
                if (saved.TryGetValue(key, out int value))
                    PlayerPrefs.SetInt(key, value);
                else
                    PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
