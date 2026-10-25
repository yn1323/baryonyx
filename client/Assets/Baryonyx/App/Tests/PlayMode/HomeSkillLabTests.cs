using System.Collections;
using System.Collections.Generic;
using Baryonyx.App;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // Homeの右上のデバッグルームの一覧からスキルのデバッグルームへ移り、ルームの「ホームへ」でHomeへ戻る。
    public sealed class HomeSkillLabTests
    {
        private static readonly string[] Keys =
        {
            CardSkillLab.SkillKey,
            CardSkillLab.WeightKey,
            CardSkillLab.EnemyKey,
            CardSkillLab.RepeatKey,
        };

        private TestGameServices services;

        // ルームは選んだスキルと再生のしかたを保存するため、開発者の値をテストのあとに戻す。
        private readonly Dictionary<string, string> savedStrings = new();
        private readonly Dictionary<string, int> savedInts = new();

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            savedStrings.Clear();
            savedInts.Clear();
            if (PlayerPrefs.HasKey(CardSkillLab.SkillKey))
                savedStrings[CardSkillLab.SkillKey] = PlayerPrefs.GetString(CardSkillLab.SkillKey);
            for (int i = 1; i < Keys.Length; i++)
                if (PlayerPrefs.HasKey(Keys[i]))
                    savedInts[Keys[i]] = PlayerPrefs.GetInt(Keys[i]);
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            // スキルの演出が止めた時間を、途中で落ちても次のテストへ残さない。
            Time.timeScale = 1f;
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(HomeSkillLabTests));
            foreach (var key in Keys)
                PlayerPrefs.DeleteKey(key);
            foreach (var (key, value) in savedStrings)
                PlayerPrefs.SetString(key, value);
            foreach (var (key, value) in savedInts)
                PlayerPrefs.SetInt(key, value);
        }

        [UnityTest]
        public IEnumerator TheListOpensTheSkillLabAndItsHomeButtonReturns()
        {
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            home.View.DebugRoomsButton.onClick.Invoke();
            Assert.That(home.View.Dialog.IsShown, Is.True);

            // 続けて押しても、画面は1回だけ開く。
            Assert.That(home.View.Dialog.Choose("スキルのデバッグルーム"), Is.True);
            Assert.That(home.View.Dialog.Choose("スキルのデバッグルーム"), Is.False);
            Assert.That(home.Presenter.ScreenOpened, Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.CardSkillLab,
                10f,
                "The skill lab did not open."
            );
            var lab = Object.FindAnyObjectByType<CardSkillLab>();
            Assert.That(lab, Is.Not.Null);
            yield return SceneTests.WaitUntil(
                () => lab.Playing && !lab.Transition.IsPlaying,
                10f,
                "The skill lab did not start."
            );

            // 「ホームへ」は一覧を開いているあいだ隠れ、押せる大きさがある。
            Assert.That(lab.HomeButton.gameObject.activeInHierarchy, Is.True);
            SceneTests.AssertTouchSize(lab.HomeButton.transform);
            lab.OpenPicker();
            Assert.That(lab.HomeButton.gameObject.activeInHierarchy, Is.False);
            lab.ClosePicker();
            Assert.That(lab.HomeButton.gameObject.activeInHierarchy, Is.True);

            // 続けて押しても1回だけ戻り、再生中のスキルは止まる。
            lab.HomeButton.onClick.Invoke();
            lab.HomeButton.onClick.Invoke();
            Assert.That(lab.Leaving, Is.True);
            Assert.That(lab.Playing, Is.False);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                10f,
                "Home did not open again."
            );
            home = Object.FindAnyObjectByType<HomeBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.HomeReady(home));
            // スキルの演出が止めた時間を、ホームへ持ち込まない。
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
