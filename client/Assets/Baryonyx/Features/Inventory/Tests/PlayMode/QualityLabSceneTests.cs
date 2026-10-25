using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 品質のデバッグルーム。タブと一覧で選んだ物の倍率を動かすと、品質の文字・キラキラ・効果の数字が変わり、
    // 選んだ物と倍率は次のPlayのために残る。
    public sealed class QualityLabSceneTests
    {
        private const string ScenePath = SceneTests.ScenesFolder + "/Debug/QualityLab.unity";

        private static readonly string[] Keys =
        {
            QualityLab.TabKey,
            QualityLab.EntryKey,
            QualityLab.MultiplierKey,
        };

        // 開発者が最後に選んだ物を、テストで上書きしたまま残さない。
        private readonly Dictionary<string, string> savedStrings = new();
        private readonly Dictionary<string, int> savedInts = new();
        private readonly Dictionary<string, float> savedFloats = new();

        [SetUp]
        public void ForgetThePick()
        {
            savedStrings.Clear();
            savedInts.Clear();
            savedFloats.Clear();
            if (PlayerPrefs.HasKey(QualityLab.TabKey))
                savedInts[QualityLab.TabKey] = PlayerPrefs.GetInt(QualityLab.TabKey);
            if (PlayerPrefs.HasKey(QualityLab.EntryKey))
                savedStrings[QualityLab.EntryKey] = PlayerPrefs.GetString(QualityLab.EntryKey);
            if (PlayerPrefs.HasKey(QualityLab.MultiplierKey))
                savedFloats[QualityLab.MultiplierKey] = PlayerPrefs.GetFloat(
                    QualityLab.MultiplierKey
                );
            foreach (var key in Keys)
                PlayerPrefs.DeleteKey(key);
        }

        [UnityTearDown]
        public IEnumerator RestoreThePick()
        {
            yield return SceneTests.UnloadAll(nameof(QualityLabSceneTests));
            foreach (var key in Keys)
                PlayerPrefs.DeleteKey(key);
            foreach (var (key, value) in savedInts)
                PlayerPrefs.SetInt(key, value);
            foreach (var (key, value) in savedStrings)
                PlayerPrefs.SetString(key, value);
            foreach (var (key, value) in savedFloats)
                PlayerPrefs.SetFloat(key, value);
        }

        private static IEnumerator Open(System.Action<QualityLab> found) =>
            SceneTests.Load<QualityLab>(
                ScenePath,
                lab => lab.Rows.Count > 0 && (lab.Transition == null || !lab.Transition.IsPlaying),
                found
            );

        [UnityTest]
        public IEnumerator MovingTheMultiplierChangesTheQualityTheSparkleAndTheNumbers()
        {
            QualityLab lab = null;
            yield return Open(found => lab = found);

            // 初めは、カードスキルの1枚目を品質Sの中ほどで出す。
            Assert.That(lab.Tab, Is.EqualTo(QualityLabTab.Skills));
            Assert.That(lab.Rows, Has.Count.EqualTo(CardSkills.All.Count));
            Assert.That(lab.Quality, Is.EqualTo(ItemQuality.S));
            Assert.That(lab.Main.Card.gameObject.activeSelf, Is.True);
            Assert.That(lab.Main.Item.Root.activeSelf, Is.False);

            // ファイアを選び、上限（×2.4）で：品質S、威力240%、やけど5ターン、コスト1、キラキラが出る。
            lab.Rows[CardSkills.All.ToList().FindIndex(skill => skill.Id == "Fire")]
                .onClick.Invoke();
            lab.SetMultiplier(2.4f);
            Assert.That(lab.Current.Id, Is.EqualTo("Fire"));
            Assert.That(lab.Limit, Is.EqualTo(2.4f));
            Assert.That(lab.QualityLetter.text, Is.EqualTo("S"));
            Assert.That(lab.EffectText.text, Does.Contain("属攻の240%"));
            Assert.That(lab.EffectText.text, Does.Contain("5ターン"));
            Assert.That(lab.NumbersText.text, Does.Contain("コスト　2 → 1"));
            var foil = lab.Main.Card.Foil;
            Assert.That(foil.Quality, Is.EqualTo(ItemQuality.S));
            Assert.That(foil.Image.enabled, Is.True);

            // 下げると品質も下がり、B・Cではキラキラが消える。
            lab.QualityButtons[(int)ItemQuality.B].onClick.Invoke();
            Assert.That(lab.Quality, Is.EqualTo(ItemQuality.B));
            Assert.That(lab.QualityLetter.text, Is.EqualTo("B"));
            Assert.That(foil.Image.enabled, Is.False);
            lab.StepDownButton.onClick.Invoke();
            Assert.That(
                lab.Multiplier,
                Is.EqualTo(Quality.Middle(ItemQuality.B, 2.4f) - 0.01f).Within(0.001f)
            );

            // ×1.00より下と上限より上には動かない。
            lab.SetMultiplier(0.5f);
            Assert.That(lab.Multiplier, Is.EqualTo(1f));
            lab.SetMultiplier(9f);
            Assert.That(lab.Multiplier, Is.EqualTo(lab.Limit));

            // 見比べは、C・B・A・Sの中ほどで並び、A・Sだけが光る。
            Assert.That(lab.Compare, Has.Length.EqualTo(4));
            Assert.That(
                lab.Compare.Select(preview => preview.Card.Foil.Image.enabled),
                Is.EqualTo(new[] { false, false, true, true })
            );

            // 上限を試しに下げると、同じ倍率でも品質が上がる。戻すと種類の上限になる。
            lab.SetMultiplier(1.8f);
            Assert.That(lab.Quality, Is.EqualTo(ItemQuality.B));
            lab.SetLimit(2f);
            Assert.That(lab.Quality, Is.EqualTo(ItemQuality.A));
            lab.LimitResetButton.onClick.Invoke();
            Assert.That(lab.Limit, Is.EqualTo(2.4f));
        }

        [UnityTest]
        public IEnumerator EveryKindHasItsTabAndThePickIsRemembered()
        {
            QualityLab lab = null;
            yield return Open(found => lab = found);

            lab.Tabs[(int)QualityLabTab.Weapons].Button.onClick.Invoke();
            Assert.That(
                lab.Rows,
                Has.Count.EqualTo(
                    EquipmentCatalog.All.Count(gear => gear.Slot == EquipmentSlot.Weapon)
                )
            );
            // 装備は絵の板で出し、2回発動の効果を持つ炎のダガーは上限が×1.8。
            Assert.That(lab.Main.Item.Root.activeSelf, Is.True);
            Assert.That(lab.Main.Card.gameObject.activeSelf, Is.False);
            Assert.That(lab.Current.Id, Is.EqualTo("flame-dagger"));
            Assert.That(lab.Limit, Is.EqualTo(1.8f));
            lab.SetMultiplier(1.8f);
            Assert.That(lab.EffectText.text, Is.EqualTo("速度 +23%｜攻撃スキルが18%で2回発動"));
            Assert.That(lab.Main.Item.Quality.text, Is.EqualTo("S"));
            Assert.That(lab.Main.Item.Foil.Image.enabled, Is.True);

            lab.Tabs[(int)QualityLabTab.UniqueSkills].Button.onClick.Invoke();
            Assert.That(lab.Rows.Count, Is.GreaterThan(0));
            lab.Tabs[(int)QualityLabTab.Armor].Button.onClick.Invoke();
            Assert.That(lab.Rows.Count, Is.GreaterThan(0));
            // 見本のアクセサリーは、効果の数字に倍率を掛ける。
            lab.Tabs[(int)QualityLabTab.Accessories].Button.onClick.Invoke();
            Assert.That(lab.Rows, Has.Count.EqualTo(QualityLabEntry.SampleAccessories.Length));
            int sample = lab.Entries.ToList().FindIndex(entry => entry.Id == "sample-accessory-2");
            lab.Rows[sample].onClick.Invoke();
            lab.SetMultiplier(2f);
            Assert.That(lab.EffectText.text, Is.EqualTo("属攻 +12%｜会心 +6%"));
            Assert.That(lab.Quality, Is.EqualTo(ItemQuality.A));

            // 次のPlayでは、同じ物を同じ倍率で出す。
            yield return Open(found => lab = found);
            Assert.That(lab.Tab, Is.EqualTo(QualityLabTab.Accessories));
            Assert.That(lab.Current.Id, Is.EqualTo("sample-accessory-2"));
            Assert.That(lab.Multiplier, Is.EqualTo(2f).Within(0.001f));
        }
    }
}
