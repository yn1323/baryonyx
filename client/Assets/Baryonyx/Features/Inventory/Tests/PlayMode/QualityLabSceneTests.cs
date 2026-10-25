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

        // 開発用の画面なので、読み込み、倍率を動かしたときのつなぎ込み、選んだ物が残ることだけを確かめる。
        // 倍率と品質・効果の数字の計算は QualityTests（EditMode）が確かめる。
        [UnityTest]
        public IEnumerator MovingTheMultiplierShowsTheQualityAndThePickIsRemembered()
        {
            QualityLab lab = null;
            yield return Open(found => lab = found);

            // 初めは、カードスキルの1枚目を品質Sの中ほどで出す。
            Assert.That(lab.Tab, Is.EqualTo(QualityLabTab.Skills));
            Assert.That(lab.Rows, Has.Count.EqualTo(CardSkills.All.Count));
            Assert.That(lab.Quality, Is.EqualTo(ItemQuality.S));
            Assert.That(lab.Main.Card.gameObject.activeSelf, Is.True);

            // ファイアを選び、上限（×2.4）で：品質S、威力360%（属攻の150%の2.4倍）。
            lab.Rows[CardSkills.All.ToList().FindIndex(skill => skill.Id == "Fire")]
                .onClick.Invoke();
            lab.SetMultiplier(2.4f);
            Assert.That(lab.Current.Id, Is.EqualTo("Fire"));
            Assert.That(lab.QualityLetter.text, Is.EqualTo("S"));
            Assert.That(lab.EffectText.text, Does.Contain("属攻の360%"));
            lab.QualityButtons[(int)ItemQuality.B].onClick.Invoke();
            Assert.That(lab.Quality, Is.EqualTo(ItemQuality.B));
            Assert.That(lab.QualityLetter.text, Is.EqualTo("B"));

            // 装備のタブは装備を絵の板で出す。
            lab.Tabs[(int)QualityLabTab.Weapons].Button.onClick.Invoke();
            Assert.That(
                lab.Rows,
                Has.Count.EqualTo(
                    EquipmentCatalog.All.Count(gear => gear.Slot == EquipmentSlot.Weapon)
                )
            );
            Assert.That(lab.Main.Item.Root.activeSelf, Is.True);
            Assert.That(lab.Main.Card.gameObject.activeSelf, Is.False);
            string picked = lab.Current.Id;
            lab.SetMultiplier(1.5f);

            // 次のPlayでは、同じ物を同じ倍率で出す。
            yield return Open(found => lab = found);
            Assert.That(lab.Tab, Is.EqualTo(QualityLabTab.Weapons));
            Assert.That(lab.Current.Id, Is.EqualTo(picked));
            Assert.That(lab.Multiplier, Is.EqualTo(1.5f).Within(0.001f));
        }
    }
}
