using System.Collections;
using System.Collections.Generic;
using Baryonyx.App;
using Baryonyx.Home;
using Baryonyx.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // Homeの右上のスキルのデバッグルームの下のボタンから品質のデバッグルームへ移り、ルームの「ホームへ」でHomeへ戻る。
    public sealed class HomeQualityLabTests
    {
        private static readonly string[] Keys =
        {
            QualityLab.TabKey,
            QualityLab.EntryKey,
            QualityLab.MultiplierKey,
        };

        private TestGameServices services;

        // ルームは選んだ物と倍率を保存するため、開発者の値をテストのあとに戻す。
        private readonly Dictionary<string, string> saved = new();

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            saved.Clear();
            if (PlayerPrefs.HasKey(QualityLab.TabKey))
                saved[QualityLab.TabKey] = PlayerPrefs.GetInt(QualityLab.TabKey).ToString();
            if (PlayerPrefs.HasKey(QualityLab.EntryKey))
                saved[QualityLab.EntryKey] = PlayerPrefs.GetString(QualityLab.EntryKey);
            if (PlayerPrefs.HasKey(QualityLab.MultiplierKey))
                saved[QualityLab.MultiplierKey] = PlayerPrefs
                    .GetFloat(QualityLab.MultiplierKey)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(HomeQualityLabTests));
            foreach (var key in Keys)
                PlayerPrefs.DeleteKey(key);
            if (saved.TryGetValue(QualityLab.TabKey, out var tab))
                PlayerPrefs.SetInt(QualityLab.TabKey, int.Parse(tab));
            if (saved.TryGetValue(QualityLab.EntryKey, out var entry))
                PlayerPrefs.SetString(QualityLab.EntryKey, entry);
            if (saved.TryGetValue(QualityLab.MultiplierKey, out var multiplier))
                PlayerPrefs.SetFloat(
                    QualityLab.MultiplierKey,
                    float.Parse(multiplier, System.Globalization.CultureInfo.InvariantCulture)
                );
        }

        [UnityTest]
        public IEnumerator QualityLabButtonOpensTheLabAndItsHomeButtonReturns()
        {
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            Assert.That(
                SceneNames.ScreenFor(HomeAction.QualityLab),
                Is.EqualTo(SceneNames.QualityLab)
            );
            Assert.That(home.View.QualityLabButton, Is.Not.Null);
            // ボタンは88の丸で、押せる範囲（HitArea）を広げて指の大きさにしている。
            SceneTests.AssertTouchSize(home.View.QualityLabButton.transform.Find("HitArea"));

            // 続けて押しても、画面は1回だけ開く。
            home.View.QualityLabButton.onClick.Invoke();
            home.View.QualityLabButton.onClick.Invoke();
            Assert.That(home.Presenter.ScreenOpened, Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.QualityLab,
                10f,
                "The quality lab did not open."
            );
            var lab = Object.FindAnyObjectByType<QualityLab>();
            Assert.That(lab, Is.Not.Null);
            yield return SceneTests.WaitUntil(
                () => lab.Rows.Count > 0 && !lab.Transition.IsPlaying,
                10f,
                "The quality lab did not start."
            );
            Assert.That(lab.HomeButton.gameObject.activeInHierarchy, Is.True);
            SceneTests.AssertTouchSize(lab.HomeButton.transform);

            // 続けて押しても1回だけ戻る。
            lab.HomeButton.onClick.Invoke();
            lab.HomeButton.onClick.Invoke();
            Assert.That(lab.Leaving, Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                10f,
                "Home did not open again."
            );
            home = Object.FindAnyObjectByType<HomeBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.HomeReady(home));
        }
    }
}
