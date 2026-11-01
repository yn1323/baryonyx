using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.App;
using Baryonyx.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // Homeのデバッグルームの一覧から持ち物のデバッグルームへ移り、選んだ品質と数で物を足し、「ホームへ」で戻る。
    public sealed class HomeInventoryLabTests
    {
        private static readonly string[] Keys =
        {
            InventoryLab.TabKey,
            InventoryLab.QualityKey,
            InventoryLab.AmountKey,
        };

        private TestGameServices services;

        // ルームは分類・品質・数を保存するため、開発者の値をテストのあとに戻す。
        private readonly Dictionary<string, int> saved = new();

        private sealed class FakeGrants : IInventoryGrants
        {
            public readonly List<(
                string Category,
                string Id,
                ItemQuality Quality,
                int Count
            )> Calls = new();

            public Task<InventoryGranted> GrantAsync(
                string category,
                string id,
                ItemQuality quality,
                int count,
                CancellationToken token
            )
            {
                Calls.Add((category, id, quality, count));
                bool held = category != "item" && category != "material";
                return Task.FromResult(
                    new InventoryGranted
                    {
                        Count = count,
                        Held = held
                            ? Enumerable
                                .Range(0, count)
                                .Select(i => (1.2f + i * 0.01f, quality))
                                .ToArray()
                            : new (float, ItemQuality)[0],
                    }
                );
            }
        }

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            saved.Clear();
            foreach (var key in Keys)
                if (PlayerPrefs.HasKey(key))
                    saved[key] = PlayerPrefs.GetInt(key);
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            InventoryLab.Grants = null;
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(HomeInventoryLabTests));
            foreach (var key in Keys)
                PlayerPrefs.DeleteKey(key);
            foreach (var (key, value) in saved)
                PlayerPrefs.SetInt(key, value);
        }

        [UnityTest]
        public IEnumerator TheLabAddsThePickedKindAtTheQualityAndCountChosen()
        {
            PlayerPrefs.DeleteKey(InventoryLab.TabKey);
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            home.View.DebugRoomsButton.onClick.Invoke();
            Assert.That(home.View.Dialog.Choose("持ち物のデバッグルーム"), Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.InventoryLab,
                10f,
                "The inventory lab did not open."
            );
            var lab = Object.FindAnyObjectByType<InventoryLab>();
            Assert.That(lab, Is.Not.Null);
            yield return SceneTests.WaitUntil(
                () => lab.Rows.Count > 0 && !lab.Transition.IsPlaying,
                10f,
                "The inventory lab did not start."
            );
            Assert.That(lab.Tab, Is.EqualTo(InventoryLabTab.Weapons));
            SceneTests.AssertTouchSize(lab.Rows[0].transform);
            var fake = new FakeGrants();
            InventoryLab.Grants = fake;

            // スキルを品質B・10個で足す。
            lab.Tabs[(int)InventoryLabTab.Skills].Button.onClick.Invoke();
            lab.QualityTabs[System.Array.IndexOf(InventoryLab.Qualities, ItemQuality.B)]
                .Button.onClick.Invoke();
            lab.AmountTabs[1].Button.onClick.Invoke();
            Assert.That(lab.QualityRow.activeSelf, Is.True);
            Assert.That(lab.Rows.Count, Is.EqualTo(lab.Entries.Count));
            lab.Rows[0].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => lab.LastGrant.IsCompleted);
            var skill = lab.Entries[0];
            Assert.That(fake.Calls.Single(), Is.EqualTo(("skill", skill.Id, ItemQuality.B, 10)));
            StringAssert.StartsWith($"{skill.Name} ×10 を足しました", lab.Status.text);
            StringAssert.Contains("×1.20〜×1.29", lab.Status.text);

            // アイテムには品質がないため、品質のタブを隠す。
            lab.Tabs[(int)InventoryLabTab.Items].Button.onClick.Invoke();
            Assert.That(lab.QualityRow.activeSelf, Is.False);
            lab.Rows[1].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => lab.LastGrant.IsCompleted);
            Assert.That(fake.Calls[1].Category, Is.EqualTo("item"));
            Assert.That(lab.Status.text, Is.EqualTo($"{lab.Entries[1].Name} ×10 を足しました"));

            // サーバーがなければ足せないと知らせる。
            InventoryLab.Grants = null;
            lab.Rows[0].onClick.Invoke();
            yield return SceneTests.WaitUntil(() => lab.LastGrant.IsCompleted);
            Assert.That(lab.Status.text, Is.EqualTo(InventoryLab.NoServerMessage));
            Assert.That(
                PlayerPrefs.GetInt(InventoryLab.TabKey),
                Is.EqualTo((int)InventoryLabTab.Items)
            );

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
